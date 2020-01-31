using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Traktor.Core.Data;
using Traktor.Core.Extensions;
using Traktor.Core.Services;

namespace Traktor.Core.Domain
{
    public class Library : IEnumerable<Media>, IDisposable
    {
        //private IList<Media> col;
        private Dictionary<string, Media> indexCol;

        private TraktService trakt;
        private LibraryDbContext db;
        private AssetService assets;

        public DateTime LastCalendarUpdate { get; set; }
        public DateTime LastActivityUpdate { get; set; }
        public DateTime LastPhysicalReleaseUpdate { get; set; }

        public int MaximumCalendarLookBackDays { get; set; } = 60;
        public bool ExcludeUnwatchedShowsFromCalendar { get; set; } = true;
        public bool IgnoreSpecialSeasons { get; set; } = true;

        public enum ImageFetchingBehavior
        {
            Never,
            ExcludeCollection,
            All
        }
        public ImageFetchingBehavior FetchImages { get; set; }

        public event Action<LibraryChange> OnChange;

        private void TriggerOnChange(List<LibraryChange> changes)
        {
            if (changes?.Any() ?? false && this.OnChange != null)
            {
                foreach (var change in changes)
                    TriggerOnChange(change);
            }
        }
        private void TriggerOnChange(LibraryChange change)
        {
            this.OnChange?.Invoke(change);
        }

        public Library(TraktService trakt)
        {
            this.trakt = trakt;

            this.indexCol = new Dictionary<string, Media>();
            
            this.db = new LibraryDbContext();
            this.db.Database.EnsureCreated();

            this.assets = new AssetService();

            var mediaItems = this.db.Media.ToList();
            if (mediaItems.Any())
            {
                foreach (var item in mediaItems.OfType<Episode>())
                {
                    var entry = this.db.Entry(item);
                    item.ShowId = new Media.MediaId(
                        entry.Property("ShowTrakt").CurrentValue as int?,
                        entry.Property("ShowSlug").CurrentValue as string,
                        entry.Property("ShowIMDB").CurrentValue as string,
                        entry.Property("ShowTVDB").CurrentValue as int?,
                        entry.Property("ShowTMDB").CurrentValue as int?);
                }

                this.indexCol = mediaItems.ToDictionary(k => GetKeyForMedia(k), v => v);
            }
            else
            {
                this.indexCol = new Dictionary<string, Media>();
                var initial = GetMoviesFromTraktCollection().Cast<Media>().Concat(GetEpisodesFromTraktCollection()).ToList();

                this.AddRange(initial);
                this.AddRange(GetMediaFromTraktWatchlist().ToList());
            }

            this.LastActivityUpdate = this.Max(x => x.CollectedAt ?? x.WatchlistedAt ?? DateTime.MinValue);
        }

        public List<LibraryChange> Update()
        {
            var changes = new List<LibraryChange>();
            // Update library from Trakt lists.
            var activity = trakt.One<Trakt.LastActivity>();
            if (activity.all > LastActivityUpdate)
            {
                if (activity.episodes.collected_at > LastActivityUpdate)
                {
                    // Fetch collection and synchronize with local collection, check for added items and removed items (by comparing every item in trakt collection with local collection)
                    var collectedEpisodes = GetMediaFromTraktCollection<Episode>().ToList();
                    changes.AddRange(SynchronizeLibrary(collectedEpisodes, x => x.State == Media.MediaState.Collected, x => x.State != Media.MediaState.Collected && !x.CollectedAt.HasValue, UpdateCollected));
                }
                if (activity.movies.collected_at > LastActivityUpdate)
                {
                    var collectedMovies = GetMediaFromTraktCollection<Movie>().ToList();
                    changes.AddRange(SynchronizeLibrary(collectedMovies, x => x.State == Media.MediaState.Collected, x => x.State != Media.MediaState.Collected && !x.CollectedAt.HasValue, UpdateCollected));
                }
                if (activity.movies.watchlisted_at > LastActivityUpdate || activity.shows.watchlisted_at > LastActivityUpdate || activity.seasons.watchlisted_at > LastActivityUpdate || activity.episodes.watchlisted_at > LastActivityUpdate)
                {
                    // Fetch entries from watchlist.
                    var watchlistedMedia = GetMediaFromTraktWatchlist().ToList();

                    changes.AddRange(SynchronizeLibrary(watchlistedMedia.OfType<Movie>().ToList(), x => x.State != Media.MediaState.Collected && x.WatchlistedAt.HasValue));
                    changes.AddRange(SynchronizeLibrary(watchlistedMedia.OfType<Episode>().ToList(), x => x.State != Media.MediaState.Collected && x.WatchlistedAt.HasValue));
                }

                this.LastActivityUpdate = activity.all;
            }

            if (LastCalendarUpdate.Date < DateTime.UtcNow.Date)
            {
                var startDate = LastCalendarUpdate;
                this.LastCalendarUpdate = DateTime.UtcNow;

                var episodes = GetMediaFromCalendar<Episode>(startDate).Where(x=>!this.IgnoreSpecialSeasons || x.Season != 0).ToList();

                var hiddenMediaIds = GetHiddenMediaIds();
                foreach (var hidden in hiddenMediaIds)
                {
                    Func<Episode, bool> removeCondition = (x) => x.State != Media.MediaState.Collected && x.ShowId.Equals(hidden.ShowId) && (!hidden.Season.HasValue || x.Season == hidden.Season);
                    episodes.RemoveAll(new Predicate<Episode>(removeCondition));
                    changes.AddRange(this.RemoveAll(removeCondition));
                }

                if (this.ExcludeUnwatchedShowsFromCalendar)
                {
                    var watchedShowIds = GetWatchedShowIds();
                    // If we never watched this show, don't any episodes of it from the calendar. (Trakt adds all episodes of a show to the calendar if you've collected/watchlisted 1 episode)
                    episodes.RemoveAll(x => !watchedShowIds.Contains(x.ShowId));
                }

                changes.AddRange(SynchronizeLibrary(episodes, null, x => x.State == Media.MediaState.Awaiting, UpdateFromCalendar));

                var movies = GetMediaFromCalendar<Movie>(startDate).ToList();
                changes.AddRange(SynchronizeLibrary(movies, null, x => x.State == Media.MediaState.Awaiting, UpdateFromCalendar));
            }

            // Update media states.
            foreach (var media in this)
            {
                var originalState = media.State;
                if (media.State == Media.MediaState.Registered)
                {
                    var traktMedia = GetSingleMedia(media);
                    media.Release = traktMedia.Release;
                    media.Genres = traktMedia.Genres;

                    if (media is Episode)
                    {
                        media.Id = media.Id ?? traktMedia.Id;
                        media.Title = traktMedia.Title;
                    }

                    //if (media is Movie movie)
                    //{
                    //    movie.PhysicalRelease = GetPhysicalReleaseDate(movie);
                    //}

                    media.ChangeStateTo(Media.MediaState.Awaiting);
                }

                if (media.State == Media.MediaState.Awaiting && media.Release <= DateTime.Now)
                {
                    media.ChangeStateTo(Media.MediaState.Available);
                }

                if (originalState != media.State)
                {
                    var change = new LibraryChange(media, LibraryChange.Change.State, originalState);
                    changes.Add(change);
                    //TriggerOnChange(new List<LibraryChange> { change });
                }

                if (string.IsNullOrEmpty(media.ImageUrl) && this.FetchImages != ImageFetchingBehavior.Never && (this.FetchImages != ImageFetchingBehavior.ExcludeCollection || media.State != Media.MediaState.Collected))
                {
                    var image = assets.GetAsset(media);
                    if (!string.IsNullOrEmpty(image))
                    {
                        media.ImageUrl = image;
                        if (media is Episode episode)
                        {
                            foreach (var episodeInShow in this.OfType<Episode>().Where(x => x.ShowId.Equals(episode.ShowId)))
                                episodeInShow.ImageUrl = image;
                        }
                    }
                }
            }

            //if (LastPhysicalReleaseUpdate.AddDays(1) < DateTime.Now)
            //{
            //    foreach (var movie in this.OfType<Movie>().State(Media.MediaState.Available).Where(x=>!x.PhysicalRelease.HasValue))
            //    {
            //        movie.PhysicalRelease = GetPhysicalReleaseDate(movie);
            //        if (movie.PhysicalRelease.HasValue && movie.PhysicalRelease > DateTime.UtcNow)
            //        {
            //            movie.SetState(Media.MediaState.Awaiting);
            //            changes.Add(new LibraryChange(movie, LibraryChange.Change.State, Media.MediaState.Available));
            //        }
            //    }
            //    LastPhysicalReleaseUpdate = DateTime.Now;
            //}

            // Populate awaiting/available shows with genres. (We have to do this on a show basis since that info isn't on the individual episode.
            foreach (var groupedEpisodes in this.OfType<Episode>().State(Media.MediaState.Awaiting, Media.MediaState.Available).Where(x => x.Genres == null).GroupBy(x => x.ShowId))
            {
                var genres = GetGenresForShow(groupedEpisodes.Key);
                foreach (var episode in groupedEpisodes)
                    episode.Genres = genres?.ToArray() ?? new string[0];
            }

            TriggerOnChange(changes);
            return changes;
        }

        private LibraryChange? UpdateCollected<T>(T existing, T update) where T : Media
        {
            var previousState = existing.State;
            existing.CollectedAt = update.CollectedAt;
            existing.ChangeStateTo(Media.MediaState.Collected);

            return new LibraryChange(existing, LibraryChange.Change.State, previousState);
        }

        private LibraryChange? UpdateFromCalendar<T>(T existing, T update) where T : Media
        {
            existing.Release = update.Release;
            existing.WatchlistedAt = null;
            if (existing is Episode)
            {
                if (update.Id != null && existing.Id == null)
                    existing.Id = update.Id;

                if (!string.IsNullOrEmpty(update.Title))
                    existing.Title = update.Title;
            }
            return null;
        }

        public struct LibraryChange
        {
            public enum Change
            {
                Added,
                State,
                Removed,
                None
            }
            public Change Status { get; private set; }
            public Media Media { get; private set; }

            public Media.MediaState OldState { get; private set; }

            public bool InternalSource { get; private set; }

            public LibraryChange(Media media, Change status, Media.MediaState? oldState = null, bool @internal = false)
            {
                this.Status = status;
                this.Media = media;

                this.OldState = oldState ?? media.State;
                this.InternalSource = @internal;
            }

            public override string ToString()
            {
                switch (this.Status)
                {
                    default:
                    case Change.Added:
                    case Change.Removed:
                        return $"{this.Status} ({this.OldState})";
                    case Change.State:
                        return $"{this.Status} ({this.OldState} -> {this.Media.State})";
                }
            }
        }

        private List<LibraryChange> SynchronizeLibrary<T>(List<T> incoming, Func<T, bool> removeCondition = null, Func<T, bool> updateCondition = null, Func<T, T, LibraryChange?> updateFunc = null) where T : Media
        {
            var mediaInLibrary = this.OfType<T>().ToList();

            var mediaToAdd = incoming.Except(mediaInLibrary, new Media.EqualityComparer<T>()).ToList();
            var mediaToRemove = (removeCondition != null) ? mediaInLibrary.Where(removeCondition).Except(incoming, new Media.EqualityComparer<T>()).ToList() : null;
            var mediaToUpdate = (updateCondition != null) ? incoming.Intersect(mediaInLibrary.Where(updateCondition), new Media.EqualityComparer<T>()).ToList() : null;

            var changes = new List<LibraryChange>();
            if (mediaToAdd.Any())
            {
                changes.AddRange(this.AddRange(mediaToAdd));
            }

            if (mediaToUpdate?.Any() ?? false)
            {
                foreach (var media in mediaToUpdate)
                {
                    var existingMedia = this[media] as T;
                    if (existingMedia != null)
                    {
                        var change = updateFunc(existingMedia, media);
                        if (change != null)
                            changes.Add(change.Value);

                        //var previousState = existingMedia.State;

                        //if (media.Release.HasValue)
                        //    existingMedia.Release = media.Release;

                        //if (media.CollectedAt.HasValue)
                        //    existingMedia.CollectedAt = media.CollectedAt;

                        //existingMedia.WatchlistedAt = media.WatchlistedAt;

                        //if (existingMedia is Episode)
                        //{
                        //    if (media.Id != null)
                        //        existingMedia.Id = media.Id;

                        //    if (!string.IsNullOrEmpty(media.Title))
                        //        existingMedia.Title = media.Title;
                        //}

                        //existingMedia.ChangeStateTo(media.State);

                        //changes.Add(new LibraryChange(existingMedia, LibraryChange.Change.State, previousState));
                    }
                    else throw new LibraryMediaException(media, "Attempt to update media failed, looked up unexisting or wrong type item.");
                }
            }

            if (mediaToRemove?.Any() ?? false)
            {
                changes.AddRange(this.RemoveRange(mediaToRemove));
            }

            //TriggerOnChange(changes);
            return changes;
        }

        private static object saveLock = new object();
        public void Save()
        {
            lock(saveLock)
            {
                this.db.SaveChanges();
            }
        }

        public Media this[Media media]
        {
            get
            {
                return this.indexCol.GetValueByKey(GetKeyForMedia(media));
            }
        }

        public LibraryChange Add(Media media)
        {
            var key = GetKeyForMedia(media);
            if (this.indexCol.ContainsKey(key))
                return new LibraryChange(media, LibraryChange.Change.None);

            //switch (media)
            //{
            //    case Episode ep:
            //        if (this.col.OfType<Episode>().Contains(ep)) return;
            //        break;
            //    default:
            //        if (this.col.Contains(media)) return;
            //        break;
            //}

            this.indexCol.Add(key, media);

            var entry = db.Entry(media);
            if (entry.State == Microsoft.EntityFrameworkCore.EntityState.Detached)
            {
                this.db.Media.Add(media);

                var episode = media as Episode;
                if (episode != null)
                {
                    entry.Property("ShowTrakt").CurrentValue = episode.ShowId.Trakt;
                    entry.Property("ShowSlug").CurrentValue = episode.ShowId.Slug;
                    entry.Property("ShowIMDB").CurrentValue = episode.ShowId.IMDB;
                    entry.Property("ShowTVDB").CurrentValue = episode.ShowId.TVDB;
                    entry.Property("ShowTMDB").CurrentValue = episode.ShowId.TMDB;
                }
            }

            return new LibraryChange(media, LibraryChange.Change.Added);
        }

        public List<LibraryChange> AddRange(IEnumerable<Media> medias)
        {
            return medias.Select(x => this.Add(x)).ToList();
        }

        public LibraryChange Remove(Media media)
        {
            if (this.indexCol.Remove(GetKeyForMedia(media)))
            {
                this.db.Media.Remove(media);
                return new LibraryChange(media, LibraryChange.Change.Removed);
            }
            return new LibraryChange(media, LibraryChange.Change.None);
        }

        public List<LibraryChange> RemoveRange(IEnumerable<Media> medias)
        {
            return medias.Select(x => this.Remove(x)).ToList();
        }

        public List<LibraryChange> RemoveAll<T>(Func<T, bool> condition) where T : Media
        {
            return this.RemoveRange(this.OfType<T>().Where(condition));
        }

        public List<Media> GetMediaWithMagnet(Uri magnetUri)
        {
            return this.Where(x => x.Magnet == magnetUri).ToList();
        }

        private string GetKeyForMedia(Media media)
        {
            switch (media)
            {
                case Episode ep:
                    return GetKeyForEpisode(ep.ShowId, ep.Season, ep.Number);
                default:
                    return GetKeyForMovie(media.Id);
            }
        }
        private string GetKeyForMovie(Media.MediaId id) => id.GetKey();
        private string GetKeyForEpisode(Media.MediaId id, int season, int episode) => $"{id.GetKey()}.{season}.{episode}";

        public IEnumerator<Media> GetEnumerator()
        {
            return this.indexCol.Values.GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.indexCol.Values.GetEnumerator();
        }

        public List<LibraryChange> SetMediaAsCollected(Media[] medias, string resolution = null, string audio = null)
        {
            var response = this.trakt.One<Trakt.AddToCollection>(null, new 
            { 
                movies = medias.OfType<Movie>().Select(x=> new {
                    ids = new { trakt = x.Id.Trakt },
                    resolution,
                    audio
                }).ToList(),
                episodes = medias.OfType<Episode>().Select(x=> new
                {
                    ids = new { trakt = x.Id.Trakt },
                    resolution,
                    audio
                })
            });

            var changes = new List<LibraryChange>();
            foreach (var media in medias)
            {
                var oldState = media.State;
                if (media is Movie movie && !response.not_found.movies.Any(x=>x.ids.imdb == movie.Id.IMDB))
                {
                    media.SetState(Media.MediaState.Collected);
                    changes.Add(new LibraryChange(media, LibraryChange.Change.State, oldState, true));
                }
                else if (media is Episode episode && !response.not_found.episodes.Any(x=>x.ids.imdb == episode.Id.IMDB))
                {
                    media.SetState(Media.MediaState.Collected);
                    changes.Add(new LibraryChange(media, LibraryChange.Change.State, oldState, true));
                }
            }

            TriggerOnChange(changes);
            return changes;
        }

        public void SetMediaAsAbandoned(Media media)
        {
            var oldState = media.State;
            media.SetState(Media.MediaState.Abandoned);
            
            TriggerOnChange(new LibraryChange(media, LibraryChange.Change.State, oldState, true));
        }

        private IEnumerable<Media.MediaId> GetWatchedShowIds()
        {
            return trakt.Many<Trakt.WatchedShow>().Select(x => x.show.ids.ToMediaId());
        }

        //private IEnumerable<Movie> GetWatchedMovies()
        //{
        //    return trakt.Many<Trakt.WatchedMovie>().Select(x => new Movie(x.movie.ids.ToMediaId()) { WatchedAt = x.last_watched_at, Title = x.movie.title, Year = x.movie.year });
        //}

        //private IEnumerable<Episode> GetWatchedEpisodes()
        //{
        //    foreach (var watchedShow in trakt.Many<Trakt.WatchedShow>())
        //    {
        //        foreach (var watchedEpisode in watchedShow.seasons.SelectMany(x=>x.episodes, (x, y) => new { season = x.number, episode = y.number, watchedAt = y.last_watched_at }))
        //        {
        //            yield return new Episode(watchedShow.show.ids.ToMediaId(), watchedEpisode.season, watchedEpisode.episode)
        //            {
        //                WatchedAt = watchedEpisode.watchedAt
        //            };
        //        }
        //    }
        //}

        private IEnumerable<Movie> GetMoviesFromTraktCollection()
        {
            var collectedMovies = trakt.Many<Domain.Trakt.CollectedMovie>();

            return collectedMovies.Select(x => new Movie(x.movie.ids.ToMediaId())
            {
                Title = x.movie.title,
                Year = x.movie.year,
                CollectedAt = x.collected_at
            }.SetState(Media.MediaState.Collected));
        }
        private IEnumerable<Episode> GetEpisodesFromTraktCollection()
        {
            var collectedShows = trakt.Many<Domain.Trakt.CollectedShow>();

            return collectedShows.SelectMany(s => s.seasons, (x, e) => new { x.show, season = e }).SelectMany(x => x.season.episodes, (x, e) => new Episode(x.show.ids.ToMediaId(), x.season.number, e.number)
            {
                ShowTitle = x.show.title,
                Year = x.show.year,
                CollectedAt = e.collected_at
            }.SetState(Media.MediaState.Collected));
        }
        private IEnumerable<Media> GetMediaFromTraktWatchlist()
        {
            var watchlist = trakt.Many<Domain.Trakt.Watchlist>();

            foreach (var item in watchlist)
            {
                switch (item.type)
                {
                    default:
                    case "movie":
                        yield return new Movie(item.movie?.ids.ToMediaId())
                        {
                            Title = item.movie.title,
                            Year = item.movie.year,
                            WatchlistedAt = item.listed_at
                        };
                        break;
                    case "episode":
                        yield return new Episode(item.show?.ids.ToMediaId(), item.episode.season, item.episode.number)
                        {
                            Id = item.episode?.ids.ToMediaId(),
                            ShowTitle = item.show.title,
                            Year = item.show.year,
                            Title = item.episode.title,
                            WatchlistedAt = item.listed_at
                        };
                        break;
                    case "show":
                        var showId = item.show.ids.ToMediaId();
                        var seasons = trakt.Many<Domain.Trakt.Season>(new { id = showId.Trakt });
                        foreach (var season in seasons)
                        {
                            if (this.IgnoreSpecialSeasons && season.number == 0)
                                continue;

                            var totalEpisodes = season.episodes.Count;
                            foreach (var episode in season.episodes)
                                yield return new Episode(showId, episode.season, episode.number)
                                {
                                    Id = episode.ids.ToMediaId(),
                                    Title = episode.title,
                                    ShowTitle = item.show.title,
                                    Year = item.show.year,
                                    WatchlistedAt = item.listed_at,
                                    TotalEpisodesInSeason = totalEpisodes
                                };
                        }
                        break;
                    case "season":
                        var seasonShowId = item.show.ids.ToMediaId();
                        var episodes = trakt.Many<Domain.Trakt.Episode>(new { id = seasonShowId.Trakt, season = item.season.number }).ToList();
                        foreach (var episode in episodes)
                            yield return new Episode(seasonShowId, episode.season, episode.number)
                            {
                                Id = episode.ids.ToMediaId(),
                                Title = episode.title,
                                ShowTitle = item.show.title,
                                Year = item.show.year,
                                WatchlistedAt = item.listed_at,
                                TotalEpisodesInSeason = episodes.Count
                            };
                        break;

                }
            }

            yield break;
        }
        private IEnumerable<T> GetMediaFromTraktCollection<T>() where T : Media
        {
            if (typeof(T) == typeof(Media))
                throw new InvalidOperationException();

            if (typeof(T) == typeof(Episode))
                return GetEpisodesFromTraktCollection().Cast<T>();
            return GetMoviesFromTraktCollection().Cast<T>();
        }
        private IEnumerable<T> GetMediaFromCalendar<T>(DateTime date, int maximumDays = 30) where T : Media
        {
            var daysToLookBack = Math.Min(this.MaximumCalendarLookBackDays, (int)Math.Ceiling((DateTime.UtcNow.Date - date.Date).TotalDays));
            while (daysToLookBack >= 0)
            {
                var startDate = DateTime.UtcNow.AddDays(-daysToLookBack);
                var days = Math.Max(1, Math.Min(maximumDays, daysToLookBack));

                if (startDate.Date != DateTime.Now.Date)
                    days++;

                if (typeof(T) == typeof(Movie))
                    foreach (var movie in GetMoviesFromCalendar(startDate, days))
                        yield return movie as T;
                else if (typeof(T) == typeof(Episode))
                    foreach (var episode in GetEpisodesFromCalendar(startDate, days))
                        yield return episode as T;

                daysToLookBack -= maximumDays;
            }
        }

        private IEnumerable<Episode> GetEpisodesFromCalendar(DateTime startDate, int days)
        {
            var calendarShows = trakt.Many<Domain.Trakt.CalendarShow>(new { start_date = startDate.ToString("yyyy-MM-dd"), days = days });
            foreach (var show in calendarShows)
            {
                yield return new Episode(show.show.ids.ToMediaId(), show.episode.season, show.episode.number)
                {
                    ShowTitle = show.show.title,
                    Title = show.episode.title,
                    Year = show.show.year,
                    Release = show.first_aired.ToLocalTime(),
                    Id = show.episode.ids.ToMediaId()
                }.SetState(show.first_aired <= DateTime.UtcNow ? Media.MediaState.Available : Media.MediaState.Awaiting);
            }
        }

        private IEnumerable<Movie> GetMoviesFromCalendar(DateTime startDate, int days)
        {
            var calendarMovies = trakt.Many<Domain.Trakt.CalendarMovie>(new { start_date = startDate.ToString("yyyy-MM-dd"), days = days });
            foreach (var movie in calendarMovies)
            {
                yield return new Movie(movie.movie.ids.ToMediaId())
                {
                    Title = movie.movie.title,
                    Year = movie.movie.year,
                    Release = movie.released.ToLocalTime()
                }.SetState(movie.released <= DateTime.UtcNow ? Media.MediaState.Available : Media.MediaState.Awaiting);
            }
        }

        private DateTime? GetPhysicalReleaseDate(Movie movie)
        {
            var releases = trakt.Many<Trakt.MovieRelease>(new { id = movie.Id.IMDB });
            return releases.Where(x => x.release_type == "physical" || x.release_type == "digital" || x.release_type == "tv").OrderBy(x => x.release_date).FirstOrDefault()?.release_date;
        }

        private List<string> GetGenresForShow(Media.MediaId showId)
        {
            return trakt.One<Trakt.Show>(new { id = showId.Trakt }).genres;
        }

        private T GetSingleMedia<T>(T media) where T : Media
        {
            if (media is Movie movie)
            {
                var traktMovie = trakt.One<Trakt.Movie>(new { id = movie.Id.Trakt });
                return new Movie(traktMovie.ids.ToMediaId())
                {
                    Title = traktMovie.title,
                    Year = traktMovie.year,
                    Release = traktMovie.released?.ToLocalTime(),
                    Genres = traktMovie.genres?.ToArray() ?? new string[0]
                } as T;
            }

            if (media is Episode episode)
            {
                var traktEpisode = trakt.One<Trakt.Episode>(new { id = episode.ShowId.Trakt, season = episode.Season, episode = episode.Number });
                return new Episode(episode.ShowId, episode.Season, episode.Number)
                {
                    Title = traktEpisode.title,
                    Year = episode.Year,
                    Release = traktEpisode.first_aired?.ToLocalTime(),
                    Id = traktEpisode.ids.ToMediaId()
                } as T;
            }

            return null;
        }

        private IEnumerable<(Media.MediaId ShowId, int? Season)> GetHiddenMediaIds()
        {
            return trakt.Many<Trakt.HiddenWatched>().Select(x =>
            {
                switch (x.type)
                {
                    default:
                    case "show":
                        return (x.show.ids.ToMediaId(), (int?)null);
                    case "season":
                        return (x.show.ids.ToMediaId(), x.season.number);
                }
            });
        }

        public void Dispose()
        {
            this.db.Dispose();
        }
    }

    public class LibraryMediaException : Exception
    {
        public Media Media { get; set; }
        public LibraryMediaException(Media media, string message) : base(message)
        {
            this.Media = media;
        }
    }

    public static class LibraryExtension
    {
        public static IEnumerable<T> State<T>(this IEnumerable<T> media, params Media.MediaState[] state) where T : Media
        {
            return media.Where(x => state.Contains(x.State));
        }

        public static IEnumerable<T> HasMagnet<T>(this IEnumerable<T> media, bool hasMagnet = true) where T : Media
        {
            return media.Where(x => (x.Magnet != null) == hasMagnet);
        }
    }
}
