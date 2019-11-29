using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Traktor.Core.Domain;
using Traktor.Core.Services.Downloader;

namespace Traktor.Core.Services
{
    public class FileService
    {
        public class FileConfiguration
        {
            public Dictionary<string, string> MediaDestinations { get; set; }

            public bool CleanUpSource { get; set; }
            public bool IncludeSubs { get; set; }

            public string[] MediaTypes { get; set; }
        }

        public FileConfiguration Config { get; private set; }

        public event Action<DeliveryResult, List<Media>> OnDelivery;

        public FileService(FileConfiguration config)
        {
            this.Config = config;
            if (!this.Config.MediaDestinations?.Any() ?? true)
                throw new System.Configuration.ConfigurationErrorsException("No Media Destinations specified.");

            if (!this.Config.MediaTypes?.Any() ?? true)
                throw new System.Configuration.ConfigurationErrorsException("No media file types specified.");

            if (this.Config.MediaTypes.Any(x => x.StartsWith(".")))
            {
                this.Config.MediaTypes = this.Config.MediaTypes.Select(x => x.StartsWith(".") ? x.Substring(1) : x).ToArray();
            }
        }

        public class DeliveryResult
        {
            public enum DeliveryStatus
            {
                OK,
                Error,
                MediaNotFound,
                MediaAlreadyExists
            }
            public DeliveryStatus Status { get; set; }
            public string[] Files { get; set; }
            public string FolderName { get; set; }
            public string Error { get; set; }
        }

        private string GetPhysicalName(List<Media> relatedMedia)
        {
            try
            {
                return relatedMedia.Select(x => x.GetPhysicalName()).Distinct().Single();
            }
            catch (InvalidOperationException)
            {
                return relatedMedia.OrderByDescending(x => x.StateDate).FirstOrDefault().GetPhysicalName();
            }
        }


        private Type GetMediaType(List<Media> relatedMedia)
        {
            try
            {
                return relatedMedia.Select(x => x.GetType()).Distinct().Single();
            }
            catch (InvalidOperationException)
            {
                return relatedMedia.OrderByDescending(x => x.StateDate).FirstOrDefault().GetType();
            }
        }

        public DeliveryResult DeliverFiles(IDownloadInfo downloadInfo, List<Media> relatedMedia)
        {
            var delivery = HandleFileDelivery(downloadInfo, relatedMedia);
            this.OnDelivery?.Invoke(delivery, relatedMedia);
            return delivery;
        }

        private string BuildMediaPath(string mediaType, string uniqueName)
        {
            return Path.Combine(Path.IsPathRooted(this.Config.MediaDestinations[mediaType]) ? this.Config.MediaDestinations[mediaType] : Path.Combine(Environment.CurrentDirectory, this.Config.MediaDestinations[mediaType]), uniqueName);
        }

        private DeliveryResult HandleFileDelivery(IDownloadInfo downloadInfo, List<Media> relatedMedia)
        {
            var physicalName = GetPhysicalName(relatedMedia);

            char[] invalidCharacters = Path.GetInvalidFileNameChars();
            physicalName = new string(physicalName.Where(c => !invalidCharacters.Contains(c)).ToArray());

            var type = GetMediaType(relatedMedia);
            var mediaPath = BuildMediaPath(type.Name, physicalName);

            var filesMoved = new List<(string OldPath, string NewPath, long Size)>();
            var fileTypesToMove = this.Config.MediaTypes.Concat(this.Config.IncludeSubs ? new List<string> { "sub" } : new List<string>()).ToArray();

            try
            {
                if (!Directory.Exists(mediaPath))
                    Directory.CreateDirectory(mediaPath);

                foreach (var file in downloadInfo.Files.Where(x => fileTypesToMove.Contains(Path.GetExtension(x).Substring(1))))
                {
                    var newPath = Path.Combine(mediaPath, Path.GetFileName(file));
                    var size = new FileInfo(file).Length;
                    for (int i=1; i<=4; i++)
                    {
                        try
                        {
                            File.Move(file, newPath);
                            filesMoved.Add((file, newPath, size));
                            break;
                        }
                        catch (IOException e) when ((e.HResult & 0x0000FFFF) == 32 && i <= 3) // ERROR_SHARING_VIOLATION
                        {
                            System.Threading.Tasks.Task.Delay(5000*i).Wait();
                        }
                        catch (IOException e) when((e.HResult & 0x0000FFFF) == 183 && i <= 3) // ERROR_ALREADY_EXISTS
                        {
                            if (File.Exists(file) && File.Exists(newPath) && new FileInfo(file).Length > new FileInfo(newPath).Length)
                            {
                                File.Delete(newPath);
                                System.Threading.Tasks.Task.Delay(5000).Wait();
                                continue;
                            }
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                foreach (var revert in filesMoved)
                {
                    File.Move(revert.NewPath, revert.OldPath);
                }

                return new DeliveryResult { Status = DeliveryResult.DeliveryStatus.Error, Error = ex.ToString(), FolderName = physicalName };
            }

            var delivery = new DeliveryResult
            {
                Status = filesMoved.Any(x => Path.GetExtension(x.NewPath) != ".sub") ? DeliveryResult.DeliveryStatus.OK : DeliveryResult.DeliveryStatus.MediaNotFound,
                Files = filesMoved.OrderByDescending(x=>x.Size).Select(x=> Path.GetRelativePath(mediaPath, x.NewPath)).ToArray(),
                FolderName = physicalName
            };

            return delivery;
        }

        public bool DeleteMediaFiles(Media media)
        {
            if (media.RelativePath?.Any() ?? false)
            {
                var mediaType = media.GetType();
                var mediaPath = Path.Combine(BuildMediaPath(mediaType.Name, media.GetPhysicalName()));

                try
                {
                    if (media is Movie)
                    {
                        return DeleteFolder(mediaPath);
                    }
                    else
                    {
                        foreach (var mediaFile in media.RelativePath.Select(x => Path.Combine(mediaPath, x)))
                        {
                            if (File.Exists(mediaFile))
                            {
                                var filesInFolder = System.IO.Directory.GetFiles(mediaPath);
                                if (filesInFolder.Count(x => this.Config.MediaTypes.Contains(Path.GetExtension(x).Substring(1))) == 1)
                                {
                                    return DeleteFolder(mediaPath);
                                }
                                else
                                {
                                    File.Delete(mediaFile);
                                    return true;
                                }
                            }
                        }
                    }
                }
                catch (IOException)
                {
                    return false;
                }
            }
            return false;
        }

        private bool DeleteFolder(string mediaPath)
        {
            if (File.Exists(mediaPath))
                mediaPath = Path.GetDirectoryName(mediaPath);

            if (System.IO.Directory.Exists(mediaPath))
            {
                System.IO.Directory.Delete(mediaPath, true);
                return true;
            }

            return false;
        }
    }
}
