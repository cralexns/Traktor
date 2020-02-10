using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Traktor.Core.Domain;
using Traktor.Core.Extensions;
using Traktor.Core.Services.Downloader;
using Traktor.Core.Tools;

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

        public event Action<FileResult, List<Media>> OnChange;

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

        public class FileResult
        {
            public enum FileAction
            {
                Deliver,
                Rename,
                Delete
            }
            public enum ActionStatus
            {
                OK,
                Error,
                TransientError,
                MediaNotFound,
                MediaAlreadyExists
            }
            public FileAction Action { get; set; }
            public ActionStatus Status { get; set; }
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

        public FileResult DeliverFiles(IDownloadInfo downloadInfo, List<Media> relatedMedia)
        {
            var delivery = HandleFileDelivery(downloadInfo, relatedMedia);
            this.OnChange?.Invoke(delivery, relatedMedia);
            return delivery;
        }

        private string BuildMediaPath(string mediaType, string uniqueName)
        {
            return Path.Combine(Path.IsPathRooted(this.Config.MediaDestinations[mediaType]) ? this.Config.MediaDestinations[mediaType] : Path.Combine(Environment.CurrentDirectory, this.Config.MediaDestinations[mediaType]), uniqueName);
        }

        private Media GetMediaForFile(string fileName, List<Media> relatedMedia, IEnumerable<Indexer.IIndexer> indexers)
        {
            if (relatedMedia.Count == 1)
                return relatedMedia.FirstOrDefault();

            if (relatedMedia.Any(x=>x is Episode))
            {
                var numbering = indexers.Select(x => x.GetNumbering(fileName)).OrderByDescending(x => x.Episode.HasValue.ToInt() + x.Season.HasValue.ToInt() + x.Range.HasValue.ToInt()).FirstOrDefault();
                return relatedMedia.Cast<Episode>().FirstOrDefault(x => x.Season == numbering.Season && x.Number == numbering.Episode);
            }
            
            var fileCompareName = Indexer.NyaaIndexer.TransformForComparison(fileName);
            return relatedMedia.OrderBy(x => Utility.GetDamerauLevenshteinDistance(fileCompareName, Indexer.NyaaIndexer.TransformForComparison(x.GetCanonicalName()))).FirstOrDefault();
        }

        public FileResult RenameMediaFileTo(Media media, string newFileName)
        {
            var fileResult = new FileResult
            {
                Action = FileResult.FileAction.Rename,
                Files = media.RelativePath,
                FolderName = media.GetPhysicalName()
            };

            var mediaPath = Path.Combine(BuildMediaPath(media.GetType().Name, fileResult.FolderName));
            try
            {
                List<string> newPaths = new List<string>();
                foreach (var filepath in media.RelativePath)
                {
                    char[] invalidCharacters = Path.GetInvalidFileNameChars();

                    var newPath = Path.Combine(Path.GetDirectoryName(filepath), $"{new string(newFileName.Where(c => !invalidCharacters.Contains(c)).ToArray())}{Path.GetExtension(filepath)}");
                    File.Move(Path.Combine(mediaPath, filepath), Path.Combine(mediaPath, newPath));
                    newPaths.Add(newPath);
                }

                fileResult.Files = newPaths.ToArray();
                fileResult.Status = FileResult.ActionStatus.OK;
            }
            catch (Exception ex)
            {
                fileResult.Status = FileResult.ActionStatus.Error;
            }

            this.OnChange?.Invoke(fileResult, new List<Media> { media });
            return fileResult;
        }

        private FileResult HandleFileDelivery(IDownloadInfo downloadInfo, List<Media> relatedMedia)
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
                    if (!File.Exists(file))
                        continue;

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

                if (ex is IOException ioEx && ((ioEx.HResult & 0x0000FFFF) == 32))
                {
                    return new FileResult { Status = FileResult.ActionStatus.TransientError, Error = ioEx.ToString(), FolderName = physicalName };
                }

                return new FileResult { Status = FileResult.ActionStatus.Error, Error = ex.ToString(), FolderName = physicalName };
            }

            var delivery = new FileResult
            {
                Action = FileResult.FileAction.Deliver,
                Status = filesMoved.Any(x => Path.GetExtension(x.NewPath) != ".sub") ? FileResult.ActionStatus.OK : FileResult.ActionStatus.MediaNotFound,
                Files = filesMoved.OrderByDescending(x=>x.Size).Select(x=> Path.GetRelativePath(mediaPath, x.NewPath)).ToArray(),
                FolderName = physicalName
            };

            return delivery;
        }

        public FileResult DeleteMediaFiles(Media media)
        {
            var fileResult = new FileResult
            {
                Action = FileResult.FileAction.Delete,
                Files = media.RelativePath,
                FolderName = media.GetPhysicalName(),
                Status = FileResult.ActionStatus.MediaNotFound
            };

            if (media.RelativePath?.Any() ?? false)
            {
                bool deleted = false;

                var mediaType = media.GetType();
                var mediaPath = Path.Combine(BuildMediaPath(mediaType.Name, fileResult.FolderName));

                try
                {
                    if (media is Movie)
                    {
                        deleted = DeleteFolder(mediaPath);
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
                                    deleted = DeleteFolder(mediaPath);
                                }
                                else
                                {
                                    File.Delete(mediaFile);
                                    deleted = true;
                                }
                            }
                        }
                    }
                }
                catch (IOException ioEx)
                {
                    deleted = false;
                    fileResult.Error = ioEx.ToString();
                    fileResult.Status = FileResult.ActionStatus.Error;
                    return fileResult;
                }
                
                if (deleted)
                {
                    fileResult.Status = FileResult.ActionStatus.OK;
                }
            }

            this.OnChange?.Invoke(fileResult, new List<Media> { media });
            return fileResult;
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
