using Traktor.Core.Domain;
using Traktor.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Traktor.Core.Data;
using Traktor.Core.Services.Downloader;
using Traktor.Core.Services.Indexer;
using Microsoft.Extensions.Configuration;
using Traktor.Core.Extensions;
using System.Collections.Concurrent;

namespace Traktor.Core
{
    public class Curator : IDisposable
    {
        public class CuratorConfiguration
        {
            public Scouter.ScouterSettings Scout { get; set; }

            public MediaDownloader.DownloaderSettings Download { get; set; }

            public FileService.FileConfiguration File { get; set; }

            public bool SynchronizeCollection { get; set; }
            public TimeSpan ScoutFrequency { get; set; }
            public int MaximumCalendarLookbackDays { get; set; }
            public bool IgnoreSpecialSeasons { get; set; }
            public bool ExcludeUnwatchedShowsFromCalendar { get; set; }
            public bool EnsureDownloadIntegrity { get; set; }

            public TimeSpan? RemoveWatchedMoviesAfter { get; set; }
            public TimeSpan? RemoveWatchedEpisodesAfter { get; set; }
            public TimeSpan? RemoveWatchedSeasonsAfter { get; set; }

            public Dictionary<string, string> RenameFilePattern { get; set; }

            public Library.ImageFetchingBehavior FetchImages { get; set; }

            public static CuratorConfiguration Default => new CuratorConfiguration
            {
                Download = new MediaDownloader.DownloaderSettings
                {
                    Path = "Downloads",
                    Port = 3333,
                    MaximumUploadSpeedKb = 100,
                    MaximumDownloadSpeedKb = 1024 * 5,
                    MaxConcurrent = 3
                },
                Scout = new Scouter.ScouterSettings
                {
                    Requirements = new[] 
                    { 
                        new Scouter.RequirementConfig
                        {
                            MediaType = nameof(Episode),
                            Delay = null,
                            Timeout = TimeSpan.FromDays(7),
                            Parameters = new List<Scouter.RequirementConfig.Parameter>
                            {
                                new Scouter.RequirementConfig.Parameter { Category = Scouter.RequirementConfig.Parameter.ParameterCategory.Resolution, Definition = new [] { nameof(IndexerResult.VideoQualityLevel.HD_720p) }, Comparison = Scouter.RequirementConfig.Parameter.ParameterComparison.Minimum },
                                new Scouter.RequirementConfig.Parameter { Category = Scouter.RequirementConfig.Parameter.ParameterCategory.Resolution, Definition = new[] { nameof(IndexerResult.VideoQualityLevel.FHD_1080p) }, Patience = TimeSpan.FromHours(7) },
                                new Scouter.RequirementConfig.Parameter { Category = Scouter.RequirementConfig.Parameter.ParameterCategory.Tag, Definition = new[] { nameof(IndexerResult.QualityTrait.PROPER), nameof(IndexerResult.QualityTrait.REPACK) }, Patience = TimeSpan.FromHours(3)},
                                new Scouter.RequirementConfig.Parameter { Category = Scouter.RequirementConfig.Parameter.ParameterCategory.Peers, Definition = new[] { "1S" }, Comparison = Scouter.RequirementConfig.Parameter.ParameterComparison.Minimum }
                            }
                        },
                        new Scouter.RequirementConfig
                        {
                            MediaType = nameof(Movie),
                            Delay = TimeSpan.FromDays(1),
                            Timeout = null,
                            NoResultThrottle = TimeSpan.FromDays(1),
                            Parameters = new List<Scouter.RequirementConfig.Parameter>
                            {
                                new Scouter.RequirementConfig.Parameter { Category = Scouter.RequirementConfig.Parameter.ParameterCategory.Resolution, Definition = new[] { nameof(IndexerResult.VideoQualityLevel.FHD_1080p) }, Comparison = Scouter.RequirementConfig.Parameter.ParameterComparison.Minimum },
                                new Scouter.RequirementConfig.Parameter { Category = Scouter.RequirementConfig.Parameter.ParameterCategory.Resolution, Definition = new[] { nameof(IndexerResult.VideoQualityLevel.FHD_1080p) }, Patience = TimeSpan.FromDays(30) },
                                new Scouter.RequirementConfig.Parameter { Category = Scouter.RequirementConfig.Parameter.ParameterCategory.Audio, Definition = new [] { nameof(IndexerResult.QualityTrait.AC5_1), nameof(IndexerResult.QualityTrait.DTS), nameof(IndexerResult.QualityTrait.Atmos) }, Patience = TimeSpan.FromDays(30) },
                                new Scouter.RequirementConfig.Parameter { Category = Scouter.RequirementConfig.Parameter.ParameterCategory.Source, Definition = new [] { nameof(IndexerResult.QualityTrait.BluRay) }, Patience = TimeSpan.Zero },
                                new Scouter.RequirementConfig.Parameter { Category = Scouter.RequirementConfig.Parameter.ParameterCategory.Audio, Definition = new [] { nameof(IndexerResult.QualityTrait.AAC)}, Comparison = Scouter.RequirementConfig.Parameter.ParameterComparison.NotEqual },
                                new Scouter.RequirementConfig.Parameter { Category = Scouter.RequirementConfig.Parameter.ParameterCategory.Group, Definition = new [] { "SPARKS" }, Patience = TimeSpan.Zero },
                                new Scouter.RequirementConfig.Parameter { Category = Scouter.RequirementConfig.Parameter.ParameterCategory.SizeMb, Definition = new [] { "10000" }, Comparison = Scouter.RequirementConfig.Parameter.ParameterComparison.Minimum },
                                new Scouter.RequirementConfig.Parameter { Category = Scouter.RequirementConfig.Parameter.ParameterCategory.Peers, Definition = new[] { "1S" }, Comparison = Scouter.RequirementConfig.Parameter.ParameterComparison.Minimum }
                            }
                        }
                    },
                    Redirects = new []
                    {
                        new Scouter.RedirectConfig
                        {
                            TraktShowSlug = "chilling-adventures-of-sabrina",
                            Rules = new[]
                            {
                                new Scouter.RedirectConfig.RedirectRule
                                {
                                    SeasonFromStart = 1,
                                    SeasonFromEnd = 1,
                                    EpisodeFromStart = 11,
                                    SeasonCalculation = "2",
                                    EpisodeCalculation = "{0} - 11"
                                },
                                new Scouter.RedirectConfig.RedirectRule
                                {
                                    SeasonFromStart = 2,
                                    SeasonCalculation = "{0} + 1"
                                }
                            }
                        }
                    }
                },
                File = new FileService.FileConfiguration
                {
                    MediaDestinations = new Dictionary<string, string>
                    {
                        { nameof(Episode), "Episodes" },
                        { nameof(Movie), "Movies" }
                    },
                    MediaTypes = new string[]
                    {
                    "mkv",
                    "mp4",
                    "mpeg",
                    "avi",
                    "wmv",
                    "rm",
                    "divx",
                    "webm"
                    },
                    CleanUpSource = true,
                    IncludeSubs = true
                },
                SynchronizeCollection = true,
                ScoutFrequency = TimeSpan.FromMinutes(30),
                MaximumCalendarLookbackDays = 30,
                IgnoreSpecialSeasons = true,
                ExcludeUnwatchedShowsFromCalendar = true,
                FetchImages = Library.ImageFetchingBehavior.ExcludeCollection,
                RenameFilePattern = new Dictionary<string, string>
                {
                    { nameof(Episode), "{ShowTitle} - {Season}x{Number:00} - {Title}" }
                }
            };
        }

        private TraktService trakt;
        public Curator(TraktService ts)
        {
            trakt = ts;
        }

        public enum CuratorResult
        {
            Error,
            NotInitialized,
            Started,
            Updated,
            TraktAuthenticationRequired,
            Stopped,
            UpdateRunning
        }

        public CuratorConfiguration Config { get; private set; }

        public Library Library { get; private set; }
        public Scouter Scouter { get; set; }
        public IDownloader Downloader { get; set; }
        public FileService File { get; set; }

        public event Action<CuratorEvent> OnCuratorEvent;

        public class CuratorEvent
        {
            public enum EventType
            {
                DownloadIntegrity,
                LibraryCleanup,
            }
            public EventType Type { get; set; }
            public string Message { get; set; }
        }

        public CuratorResult Initialize(CuratorConfiguration config,
            Action<Exception> exceptionCallback,
            Action<Library.LibraryChange> libraryCallback = null,
            Action<Scouter.ScoutResult, Media> scouterCallback = null,
            Action<IDownloadInfo> downloaderCallback = null,
            Action<FileService.FileResult, List<Media>> fileDeliveryCallback = null)
        {
            try
            {
                this.Config = config;

                this.Library = new Library(trakt);
                this.Library.MaximumCalendarLookBackDays = config.MaximumCalendarLookbackDays;
                this.Library.ExcludeUnwatchedShowsFromCalendar = config.ExcludeUnwatchedShowsFromCalendar;
                this.Library.IgnoreSpecialSeasons = config.IgnoreSpecialSeasons;
                this.Library.FetchImages = config.FetchImages;
                this.Library.OnChange += (change) => { Library_OnChange(change, libraryCallback); };

                this.Scouter = new Scouter(config.Scout);
                this.Scouter.OnScouted += Scouter_OnScouted;

                if (scouterCallback != null)
                    this.Scouter.OnScouted += scouterCallback;

                this.Downloader = new MediaDownloader(config.Download ?? new CuratorConfiguration().Download);
                this.Downloader.OnChange += (dli) => { Downloader_OnChange(dli, downloaderCallback); };

                this.File = new FileService(config.File ?? new CuratorConfiguration().File);
                this.File.OnChange += (delivery, medias) => { File_OnChange(delivery, medias, fileDeliveryCallback); };
            }
            catch (TraktAPIException tex) when (tex.Status == TraktAPIException.APIStatus.AuthenticatedRequired)
            {
                return CuratorResult.TraktAuthenticationRequired;
            }
            catch (Exception ex)
            {
                if (System.Diagnostics.Debugger.IsAttached)
                    throw;

                exceptionCallback.Invoke(ex);
                return CuratorResult.Error;
            }

            this.Library.Save();
            return CuratorResult.Started;
        }

        public void UpdateConfiguration(CuratorConfiguration config)
        {
            this.Config = config;
            this.Scouter.UpdateSettings(config.Scout);
            this.File.UpdateConfiguration(config.File);
        }

        private void Scouter_OnScouted(Scouter.ScoutResult arg1, Media arg2)
        {
            //throw new NotImplementedException();
        }

        private void File_OnChange(FileService.FileResult fileResult, List<Media> medias, Action<FileService.FileResult, List<Media>> fileDeliveryCallback = null)
        {
            switch (fileResult.Action)
            {
                case FileService.FileResult.FileAction.Deliver:
                    if (fileResult.Status == FileService.FileResult.ActionStatus.OK && (fileResult.Files?.Any() ?? false))
                    {
                        File_OnDelivery(fileResult, medias);
                    }
                    break;
                case FileService.FileResult.FileAction.Rename:
                    if (fileResult.Status == FileService.FileResult.ActionStatus.OK && (fileResult.Files?.Any() ?? false))
                    {
                        medias.ForEach(x => x.RelativePath = fileResult.Files);
                        this.Library.Save();
                    }
                    break;
                case FileService.FileResult.FileAction.Delete:
                    if (fileResult.Status == FileService.FileResult.ActionStatus.OK || fileResult.Status == FileService.FileResult.ActionStatus.MediaNotFound)
                    {
                        medias.ForEach(x => x.RelativePath = new string[0]);
                        this.Library.Save();
                    }
                    break;
            }

            fileDeliveryCallback?.Invoke(fileResult, medias);

            
        }

        private void File_OnDelivery(FileService.FileResult fileResult, List<Media> medias)
        {
            if (medias.Count == 1)
            {
                medias.First().RelativePath = fileResult.Files;
            }
            else if (medias.All(x => x is Episode))
            {
                var episodes = medias.OfType<Episode>().OrderBy(x => x.Number).ToList();
                try
                {
                    var indexer = GetBestNumberingIndexer(episodes.First(), fileResult);

                    var episodeFiles = fileResult.Files.Select(x=>new { Numbering = indexer.GetNumbering(x), File = x }).GroupBy(x=>x.Numbering).ToDictionary(x => x.Key, x => x.Select(y=>y.File).ToArray());
                    foreach (var media in episodes)
                    {
                        var redirectedMedia = this.Scouter.GetRedirectedMedia(media) as Episode ?? media;
                        media.RelativePath = episodeFiles.GetValueByKey((redirectedMedia.Season, redirectedMedia.Number, null)) ?? episodeFiles.FirstOrDefault(x => x.Key.Season == redirectedMedia.Season && x.Key.Episode <= redirectedMedia.Number && x.Key.Range >= redirectedMedia.Number).Value;
                    }
                }
                catch
                {
                    var alphabeticallyOrderedFiles = new Queue<string>(fileResult.Files.Where(x => this.File.Config.MediaTypes.Contains(System.IO.Path.GetExtension(x).Substring(1))).OrderByDescending(x => x));
                    if (alphabeticallyOrderedFiles.Count() >= medias.Count)
                    {
                        foreach (var media in episodes.OrderByDescending(x=>x.Number))
                        {
                            media.RelativePath = new[] { alphabeticallyOrderedFiles.Dequeue() };
                        }

                        if (alphabeticallyOrderedFiles.Count > 0)
                        {
                            var lastEpisode = episodes.Last();
                            lastEpisode.RelativePath = lastEpisode.RelativePath.Concat(alphabeticallyOrderedFiles).ToArray();
                        }
                    }
                }
            }

            // TODO: Some more error handling here? If we fail to correctly apply paths to media then it breaks the functionality that cleans up the library, we need to at the very least log the results of this operation.

            this.Library.Save();
        }

        private IIndexer GetBestNumberingIndexer(Media media, FileService.FileResult delivery)
        {
            return Scouter.GetIndexersForMedia(media).OrderByDescending(idxr => delivery.Files.Count(x => idxr.GetNumbering(x).Episode.HasValue)).FirstOrDefault();
        }

        private void Downloader_OnChange(IDownloadInfo downloadInfo, Action<IDownloadInfo> downloaderCallback = null)
        {
            downloaderCallback?.Invoke(downloadInfo);

            switch (downloadInfo.State)
            {
                case IDownloadInfo.DownloadState.Initializing:
                    break;
                case IDownloadInfo.DownloadState.Downloading:
                    // Check average speed and potentially do something if it's taking too long to download this magnet.
                    break;
                case IDownloadInfo.DownloadState.Waiting:
                    // This is not started yet, probably because we've reached max concurrent and there are other higher priority downloads.
                    break;
                case IDownloadInfo.DownloadState.Completed:
                    // Lets pass this information to IFileHandler which will move files into the proper location (Series or Movie folder)
                    DeliverContent(downloadInfo);
                    break;
                case IDownloadInfo.DownloadState.Failed:
                    // If it's failed, retry same magnet or maybe try selecting another one if possible.
                    break;
            }
        }

        private void DeliverContent(IDownloadInfo downloadInfo, int retry = 0)
        {
            if (downloadInfo.State != IDownloadInfo.DownloadState.Completed)
                return;

            var relatedMedia = this.Library.GetMediaWithMagnet(downloadInfo.MagnetUri);
            lock (GetLockForUri(downloadInfo.MagnetUri))
            {
                if (relatedMedia?.Any(x => x.State != Media.MediaState.Collected) ?? false)
                {
                    var deliveryResult = this.File.DeliverFiles(downloadInfo, relatedMedia);
                    if (deliveryResult.Status == FileService.FileResult.ActionStatus.OK)
                    {
                        try
                        {
                            var fileRenamePattern = this.Config.RenameFilePattern?.GetValueByKey(relatedMedia.FirstOrDefault().GetType().Name);
                            if (!string.IsNullOrEmpty(fileRenamePattern))
                            {
                                foreach (var media in relatedMedia)
                                {
                                    this.File.RenameMediaFileTo(media, SmartFormat.Smart.Format(fileRenamePattern, media));
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            // ?
                        }

                        var indexers = this.Scouter.GetIndexersForMedia(relatedMedia.First());

                        var quality = indexers.Select(x => x.GetQualityLevel(downloadInfo.Name)).OrderByDescending(x => x).FirstOrDefault();
                        var traits = indexers.Select(x => x.GetTraits(downloadInfo.Name)).OrderByDescending(x => x.Length).FirstOrDefault();

                        var changes = this.Library.SetMediaAsCollected(relatedMedia.ToArray(), IndexerQualityLevelToTraktResolution(quality), IndexerQualityTraitsToTraktAudio(traits));
                        if (changes.Any())
                            this.Library.Save();

                        this.Downloader.Stop(downloadInfo.MagnetUri, this.File.Config.CleanUpSource, true);
                    }
                    else if (deliveryResult.Status == FileService.FileResult.ActionStatus.TransientError && retry < 3)
                    {
                        retry++;
                        Task.Delay(TimeSpan.FromMinutes(retry * 5)).ContinueWith((t) => { DeliverContent(downloadInfo, retry); });
                    }
                }
                else this.Downloader.Stop(downloadInfo.MagnetUri, true, true);
            }
        }

        private static ConcurrentDictionary<Uri, object> uriLockDictionary = new ConcurrentDictionary<Uri, object>();
        private object GetLockForUri(Uri uri)
        {
            return uriLockDictionary.GetOrAdd(uri, (u) => { return new object(); });
        }

        private string IndexerQualityLevelToTraktResolution(IndexerResult.VideoQualityLevel qualityLevel)
        {
            switch (qualityLevel)
            {
                default:
                case IndexerResult.VideoQualityLevel.Unknown:
                    return null;
                case IndexerResult.VideoQualityLevel.HD_720p:
                    return "hd_720p";
                case IndexerResult.VideoQualityLevel.FHD_1080p:
                    return "hd_1080p";
                case IndexerResult.VideoQualityLevel.UHD_2160p:
                    return "uhd_4k";
            }
        }

        private string IndexerQualityTraitsToTraktAudio(IndexerResult.QualityTrait[] qualityTraits)
        {
            if (qualityTraits == null)
                return null;

            if (qualityTraits.Contains(IndexerResult.QualityTrait.DTS_HD_MA))
                return "dts_ma";

            if (qualityTraits.Contains(IndexerResult.QualityTrait.DTS_HD))
                return "dolby_truehd";

            if (qualityTraits.Contains(IndexerResult.QualityTrait.Atmos))
                return "dolby_atmos";

            if (qualityTraits.Contains(IndexerResult.QualityTrait.DTS) || qualityTraits.Contains(IndexerResult.QualityTrait.AC5_1) || qualityTraits.Contains(IndexerResult.QualityTrait.AC7_1))
                return "dts";

            if (qualityTraits.Contains(IndexerResult.QualityTrait.AAC))
                return "aac";

            return null;
        }

        private void Library_OnChange(Library.LibraryChange change, Action<Library.LibraryChange> libraryCallback = null)
        {
            if (change.Status == Library.LibraryChange.Change.Removed && change.Media.State.Is(Media.MediaState.Collected) && this.Config.SynchronizeCollection && this.File.DeleteMediaFiles(change.Media).Status == FileService.FileResult.ActionStatus.OK)
            {
                // Deleted local media.
            }

            libraryCallback?.Invoke(change);

            if (!change.InternalSource)
            {
                if (change.Status == Library.LibraryChange.Change.Removed && change.Media.State.Is(Media.MediaState.Available) && change.Media.Magnet != null && this.Downloader.Stop(change.Media.Magnet, true, true) != null)
                {
                    // Removed media from downloader.
                }

                if (change.Status == Library.LibraryChange.Change.State && change.Media.State == Media.MediaState.Collected && change.Media.Magnet != null && this.Downloader.Stop(change.Media.Magnet, true, true) != null)
                {
                    // Removed collected media from downloader.
                }
            }

            if (change.Media.State == Media.MediaState.Available && (change.Status == Library.LibraryChange.Change.Added || change.Status == Library.LibraryChange.Change.State))
            {
                lastScoutDate = DateTime.MinValue; // We have newly available media, scout on next update.
            }
        }

        private List<Library.LibraryChange> UpdateLibrary()
        {
            try
            {
                return this.Library.Update();
            }
            catch (TraktAPIException tex) when (tex.Status == TraktAPIException.APIStatus.AuthenticatedRequired)
            {
                return null;
            }
        }

        private void ScoutAndStartDownloads(List<Media> mediaToScout = null, bool force = false, params Uri[] bannedUris)
        {
            this.lastScoutDate = DateTime.Now;

            mediaToScout = mediaToScout ?? this.Library.ToList();

            // Special scouting for episodes to determine if we should download a full season or single episodes.
            foreach (var season in mediaToScout.OfType<Episode>().State(Media.MediaState.Available).HasMagnet(false).GroupBy(x => new { x.ShowId.Trakt, x.Season }).ToList())
            {
                foreach (var episode in season.HasMagnet(false))
                {
                    var scoutResult = this.Scouter.Scout(episode, force);
                    scoutResult.RemoveBannedLinks(bannedUris);
                    switch (scoutResult.Status)
                    {
                        case Scouter.ScoutResult.State.NotFound:
                        case Scouter.ScoutResult.State.Found:
                        case Scouter.ScoutResult.State.BelowReqs when ignoreRequirements.Contains(episode):
                            if (scoutResult.Status != Scouter.ScoutResult.State.NotFound)
                            {
                                episode.AddMagnets(scoutResult.Results);
                            }

                            if (scoutResult.Status == Scouter.ScoutResult.State.NotFound || season.Count() == episode.TotalEpisodesInSeason || (episode.Magnets.FirstOrDefault(x => x.Link == episode.Magnet)?.IsFullSeason ?? false))
                            {
                                var fullSeasonMagnet = season.FirstOrDefault(x => x.Magnets?.Any(y => y.IsFullSeason) ?? false)?.Magnets.FirstOrDefault(x => x.IsFullSeason);
                                if (fullSeasonMagnet != null)
                                {
                                    foreach (var episodeInSeason in season)
                                    {
                                        episodeInSeason.SetMagnet(fullSeasonMagnet.Link, true);
                                    }
                                }
                            }
                            break;
                        case Scouter.ScoutResult.State.Timeout:
                            this.Library.SetMediaAsAbandoned(episode);
                            break;
                    }
                }

                // Start downloads.
                foreach (var episodeToDownload in season.HasMagnet().OrderBy(x => x.Season).ThenBy(x => x.Number))
                {
                    this.Downloader.Download(episodeToDownload.Magnet, episodeToDownload.GetPriority());
                }
            }

            foreach (var media in mediaToScout.OfType<Movie>().State(Media.MediaState.Available).HasMagnet(false))
            {
                var scoutResult = this.Scouter.Scout(media, true);
                scoutResult.RemoveBannedLinks(bannedUris);

                switch (scoutResult.Status)
                {
                    case Scouter.ScoutResult.State.Found:
                    case Scouter.ScoutResult.State.BelowReqs when ignoreRequirements.Contains(media):
                        media.AddMagnets(scoutResult.Results);
                        this.Downloader.Download(media.Magnet, media.GetPriority());
                        break;
                    case Scouter.ScoutResult.State.Timeout:
                        this.Library.SetMediaAsAbandoned(media);
                        break;
                }
            }
        }

        private DateTime lastScoutDate = DateTime.MinValue;
        private static object updateLock = new object();
        public CuratorResult Update()
        {
            if (!System.Threading.Monitor.TryEnter(updateLock, 0))
                return CuratorResult.UpdateRunning;

            try
            {
                if (this.Library == null)
                    return CuratorResult.NotInitialized;

                var libraryChanges = UpdateLibrary();
                if (libraryChanges == null)
                    return CuratorResult.TraktAuthenticationRequired;

                // Start available downloads..
                foreach (var media in this.Library.State(Media.MediaState.Available).HasMagnet().Where(x => this.Downloader.GetStatus(x.Magnet) == null))
                {
                    this.Downloader.Download(media.Magnet, media.GetPriority());
                }

                if (lastScoutDate.Add(this.Config.ScoutFrequency) < DateTime.Now)
                {
                    ScoutAndStartDownloads();
                }
                else
                {
                    var mediaDeadlines = this.Library.State(Media.MediaState.Available).Where(x => x.Magnet == null && this.Scouter.ScoutDeadlineReached(x)).ToList();
                    if (mediaDeadlines.Any())
                        ScoutAndStartDownloads(mediaDeadlines);
                }

                if (this.Config.EnsureDownloadIntegrity)
                    EnsureDownloadIntegrify();

                HandleLibraryCleanup();

                /*
                 IDEA
                    1. Look into keeping the main executable running when Traktor crashes and add functionality to web interface to restart Traktor - this way you can always see the log!
                    4. Add support to Nyaa Indexer for seasonal anime numbering system. (SAO uses [Show Name - Season Name - Episode XX])
                        - Support multiple season torrents, fx.  "[anime4life.] Sword Art Online S1,S2+Extra Edition (BDRip 1080p AC3) Dual Audio"
                    ?. Implement auto update - could use https://github.com/Tyrrrz/Onova
                 */

                this.Library.Save();

                //MonitorTorrentEngine();

                return CuratorResult.Updated;
            }
            finally
            {
                System.Threading.Monitor.Exit(updateLock);
            }
        }

        private long? totalBytesDownloaded;
            
        //private void MonitorTorrentEngine()
        //{
        //    if (this.Downloader.All().Any(x => x.State == IDownloadInfo.DownloadState.Downloading))
        //    {
        //        var currentTotalBytesDownloaded = this.Downloader.All().Where(x => x.State == IDownloadInfo.DownloadState.Downloading).Sum(x => x.DownloadedBytes);
        //        if (currentTotalBytesDownloaded != totalBytesDownloaded)
        //            totalBytesDownloaded = currentTotalBytesDownloaded;
        //        else
        //        {
        //            TriggerCuratorEvent(CuratorEvent.EventType.DownloadIntegrity, "TorrentEngine went a full update cycle with active downloads without receiving any data, restarting engine..");
        //            RestartMonoTorrent();
        //        }
        //    }
        //    else
        //    {
        //        totalBytesDownloaded = null;
        //    }
            
        //}

        private void HandleLibraryCleanup()
        {
            if (this.Config.RemoveWatchedMoviesAfter.HasValue)
            {
                var moviesToRemove = this.Library.OfType<Movie>().HasPath().Where(x => x.WatchedAt < DateTime.Now.Add(-this.Config.RemoveWatchedMoviesAfter.Value)).ToList();
                if (moviesToRemove.Any())
                {
                    TriggerCuratorEvent(CuratorEvent.EventType.LibraryCleanup, $"Found {moviesToRemove} movies qualifying for cleanup..");
                    moviesToRemove.ForEach(x => this.File.DeleteMediaFiles(x));
                }
                
            }

            if (this.Config.RemoveWatchedEpisodesAfter.HasValue)
            {
                var episodesToRemove = this.Library.OfType<Episode>().HasPath().Where(x => x.WatchedAt < DateTime.Now.Add(-this.Config.RemoveWatchedEpisodesAfter.Value)).ToList();
                if (episodesToRemove.Any())
                {
                    TriggerCuratorEvent(CuratorEvent.EventType.LibraryCleanup, $"Found {episodesToRemove} episodes qualifying for cleanup..");
                    episodesToRemove.ForEach(x => this.File.DeleteMediaFiles(x));
                }
                
            }

            if (this.Config.RemoveWatchedSeasonsAfter.HasValue)
            {
                var episodesToRemove = this.Library.OfType<Episode>().HasPath().Where(x => x.WatchedAt < DateTime.Now.Add(-this.Config.RemoveWatchedSeasonsAfter.Value)).GroupBy(x => new { x.ShowId.Trakt, x.Season });
                foreach (var groupedEpisodes in episodesToRemove)
                {
                    var totalEpisodesInSeason = groupedEpisodes.Max(x => x.TotalEpisodesInSeason);
                    if (totalEpisodesInSeason == 0)
                    {
                        totalEpisodesInSeason = this.Library.GetTotalEpisodesInSeason(groupedEpisodes.FirstOrDefault().ShowId, groupedEpisodes.Key.Season);
                        foreach (var episode in groupedEpisodes)
                            episode.TotalEpisodesInSeason = totalEpisodesInSeason;
                    }

                    if (totalEpisodesInSeason == groupedEpisodes.Count())
                    {
                        TriggerCuratorEvent(CuratorEvent.EventType.LibraryCleanup, $"Found a full season {groupedEpisodes.Key.Season} of {groupedEpisodes.FirstOrDefault().ShowTitle} ({groupedEpisodes.Count()} episodes) qualifying for cleanup..");
                        foreach (var episode in groupedEpisodes)
                        {
                            this.File.DeleteMediaFiles(episode);
                        }
                    }
                }
            }
        }

        private class DownloadHistory
        {
            public long Size { get; set; }
            public IDownloadInfo.DownloadState State { get; set; }
            public DateTime Updated { get; set; }
            public bool RestartedOnce { get; set; }
            public bool Abandoned { get; set; }
        }

        private ConcurrentDictionary<Uri, DownloadHistory> dlhistory = new ConcurrentDictionary<Uri, DownloadHistory>();
        private void EnsureDownloadIntegrify()
        {
            var allDownloads = this.Downloader.All();
            var activeDownloads = allDownloads.Count(x => x.State == IDownloadInfo.DownloadState.Initializing || x.State == IDownloadInfo.DownloadState.Downloading);
            foreach (var dli in allDownloads)
            {
                var relatedMedia = this.Library.GetMediaWithMagnet(dli.MagnetUri);
                var patience = (relatedMedia.Max(x => x.Release) < DateTime.Now.AddMonths(-6) && dli.State == IDownloadInfo.DownloadState.Stalled) ? TimeSpan.FromHours(5) : TimeSpan.FromMinutes(30);

                patience *= Math.Max(1, ((dli.Progress/100) * 10));

                var isBroken = false;
                var history = dlhistory.GetOrAdd(dli.MagnetUri, new DownloadHistory { Size = dli.Size, Updated = DateTime.Now, State = dli.State });

                if (dli.DownloadedBytes != history.Size || dli.State != history.State)
                {
                    history.Size = dli.DownloadedBytes;
                    history.State = dli.State;
                    history.Updated = DateTime.Now;
                }
                else if (history.Updated.Add(patience) < DateTime.Now)
                {
                    switch (dli.State)
                    {
                        case IDownloadInfo.DownloadState.Stalled:
                            isBroken = (history.Updated.AddHours(1) < DateTime.Now && dli.Peers == 0 && dli.Seeds == 0);
                            break;
                        case IDownloadInfo.DownloadState.Waiting:
                            isBroken = activeDownloads < this.Config.Download.MaxConcurrent || this.Config.Download.MaxConcurrent == 0;
                            break;
                        case IDownloadInfo.DownloadState.Failed:
                        case IDownloadInfo.DownloadState.Downloading:
                        case IDownloadInfo.DownloadState.Initializing:
                            isBroken = true;
                            break;
                    }
                }

                if (isBroken)
                {
                    if (!history.RestartedOnce)
                    {
                        TriggerCuratorEvent(CuratorEvent.EventType.DownloadIntegrity, $"{dli.Name} appears stalled, restarting download..");
                        this.Downloader.Restart(dli.MagnetUri, true);
                        history.RestartedOnce = true;
                        history.Updated = DateTime.Now;
                    }
                    else
                    {
                        TriggerCuratorEvent(CuratorEvent.EventType.DownloadIntegrity, $"{dli.Name} appears stalled even after restarting, looking for substitute and abandoning magnet..");
                        history.Abandoned = true;

                        this.Downloader.Stop(dli.MagnetUri, true, true);

                        var affectedMedia = this.Library.GetMediaWithMagnet(dli.MagnetUri);
                        if (affectedMedia.Any())
                        {
                            ScoutAndStartDownloads(affectedMedia, true, dlhistory.Where(x=>x.Value.Abandoned).Select(x=>x.Key).ToArray());
                        }
                    }
                }
            }
        }

        public void RestartDownload(Media media, bool deleteTorrent = false)
        {
            this.Downloader.Restart(media.Magnet, deleteTorrent);
        }

        //public void RestartMonoTorrent()
        //{
            
        //    (this.Downloader as MediaDownloader).Restart();
        //}

        public void HashCheck(Media media)
        {
            this.Downloader.HashCheck(media.Magnet);
        }

        private List<Media> ignoreRequirements = new List<Media>();
        public void IgnoreRequirement(Media media)
        {
            if (!ignoreRequirements.Contains(media))
                ignoreRequirements.Add(media);

            ForceScout(media);
        }

        public void CancelDownload(Media media)
        {
            var magnet = media.Magnet;
            media.SetMagnet(null, true);
            this.Downloader.Stop(magnet, true, true);
        }

        public void ForceScout(Media media)
        {
            this.ScoutAndStartDownloads(new List<Media>() { media }, true);
        }

        public void TryAnotherMagnet(Media media)
        {
            if (media.Magnet != null && this.Downloader.All().Any(x=>x.MagnetUri == media.Magnet))
            {
                this.Downloader.Stop(media.Magnet, true, true);
                media.SetMagnet(null, true);

                this.ScoutAndStartDownloads(new List<Media> { media }, true, media.Magnet);
            }
        }

        public void Restart(Media media)
        {
            if (media.State.Is(Media.MediaState.Collected, Media.MediaState.Abandoned))
            {
                media.ChangeStateTo(Media.MediaState.Registered);
                if (media.Id == null)
                {
                    // WUT? Well.. EF throws invalid operation exception if we try to add Id to existing media (and Library will if the media doesn't have an Id) - something to do with owned types..
                    this.Library.Remove(media);
                    this.Library.Add(media);
                }
            }
        }

        public void Remove(Media media)
        {
            if (media.Magnet != null && this.Downloader.GetStatus(media.Magnet) != null)
            {
                this.Downloader.Stop(media.Magnet, true, true);
            }

            this.Library.Remove(media);
        }

        private void TriggerCuratorEvent(CuratorEvent.EventType type, string message)
        {
            this.OnCuratorEvent?.Invoke(new CuratorEvent { Type = type, Message = message });
        }

        public void Dispose()
        {
            this.Library.Dispose();
        }
    }
}
