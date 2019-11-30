using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Traktor.Core.Domain;
using Traktor.Core.Extensions;
using Traktor.Core.Services.Indexer;

namespace Traktor.Core
{
    public class Scouter
    {
        public class RequirementConfig
        {
            public string MediaType { get; set; }

            /// <summary>
            /// Absolute minimum quality, will never allow lower qualities.
            /// </summary>
            public IndexerResult.VideoQualityLevel MinQuality { get; set; }

            /// <summary>
            /// Maximum time to wait once available for preferences like PreferredQuality, PreferredTraits (at least one), PreferredGroups and PROPER/REPACKs.
            /// </summary>
            public TimeSpan? Patience { get; set; }

            /// <summary>
            /// On release day, never wait longer than this time, overrules patience setting but only on release date.
            /// </summary>
            public TimeSpan? ReleaseDateDeadlineTime { get; set; }

            public IndexerResult.VideoQualityLevel PreferredQuality { get; set; }
            public IndexerResult.QualityTrait[] PreferredTraits { get; set; } = new IndexerResult.QualityTrait[0];
            public string[] PreferredGroups { get; set; } = new string[0];

            /// <summary>
            /// While patient specify if Scouter should wait for a PROPER / REPACK before picking a result.
            /// </summary>
            public bool WaitForRepackOrProper { get; set; }

            /// <summary>
            /// Minimum time to wait from release before delivering candidates.
            /// </summary>
            public TimeSpan? Delay { get; set; }

            /// <summary>
            /// Maximum time to wait from release for suitable candidates before abandoning item.
            /// </summary>
            public TimeSpan? Timeout { get; set; }

            /// <summary>
            /// Time to wait before scouting media again after finding no results.
            /// </summary>
            public TimeSpan? NoResultThrottle { get; set; }

            
        }

        public RequirementConfig[] Requirements { get; private set; }

        public List<IIndexer> Indexers { get; private set; }

        public event Action<ScoutResult, Media> OnScouted;
        public Scouter(params RequirementConfig[] requirements)
        {
            this.Requirements = requirements;

            this.Indexers = Assembly.GetExecutingAssembly().GetTypes().Where(x => typeof(IIndexer).IsAssignableFrom(x) && !x.IsInterface && !x.IsAbstract)
                .Select(x=>Activator.CreateInstance(x) as IIndexer).ToList();
        }

        public class ScoutResult
        {
            public enum State
            {
                NotFound,
                Found,
                BelowReqs,
                Delayed,
                Timeout,
                Throttle
            }

            public class Magnet
            {
                public string Title { get; set; }
                public bool IsFullSeason { get; set; }
                public Uri Link { get; set; }

                public Magnet(IndexerResult result)
                {
                    this.Title = result.Title;
                    this.IsFullSeason = result.IsFullSeason;
                    this.Link = new Uri(result.Magnet);
                }
            }

            public State Status { get; set; }
            public IReadOnlyList<Magnet> Results { get; set; } = new List<Magnet>();
            public DateTime Date { get; set; } = DateTime.Now;

            public ScoutResult(IEnumerable<IndexerResult> results = null)
            {
                this.Date = DateTime.Now;
                if (results != null)
                {
                    this.Status = State.Found;
                    this.Results = results.Select(x => new Magnet(x)).ToList();
                }
            }
        }

        public ScoutResult Scout(Media media, bool force = false)
        {
            var result = GetScoutResult(media, force);

            if (result.Status.Is(ScoutResult.State.BelowReqs, ScoutResult.State.Found) && !media.FirstSpottedAt.HasValue)
            {
                media.FirstSpottedAt = DateTime.Now;
            }

            if (result.Status != ScoutResult.State.Throttle)
                media.LastScoutedAt = DateTime.Now;

            this.OnScouted?.Invoke(result, media);

            return result;
        }

        public bool ScoutDeadlineReached(Media media)
        {
            var requirements = GetRequirementsForMedia(media);
            if (requirements.ReleaseDateDeadlineTime.HasValue && media.Release?.Date == DateTime.Now.Date)
            {
                return media.LastScoutedAt?.TimeOfDay >= requirements.ReleaseDateDeadlineTime.Value;
            }
            return false;
        }

        public RequirementConfig GetRequirementsForMedia(Media media)
        {
            return this.Requirements.FirstOrDefault(x => x.MediaType == media.GetType().Name);
        }

        private ScoutResult GetScoutResult(Media media, bool force = false)
        {
            var requirements = GetRequirementsForMedia(media);

            if (!force && media.LastScoutedAt.HasValue && requirements.Timeout.HasValue && (media.Release ?? media.StateDate).Add(requirements.Timeout.Value) < DateTime.Now)
                return new ScoutResult { Status = ScoutResult.State.Timeout };

            if (!force && requirements.Delay.HasValue && media.Release?.Add(requirements.Delay.Value) > DateTime.Now)
                return new ScoutResult { Status = ScoutResult.State.Delayed };

            if (!force && requirements.NoResultThrottle.HasValue && media.LastScoutedAt.HasValue && !media.FirstSpottedAt.HasValue && media.LastScoutedAt.Value.Add(requirements.NoResultThrottle.Value) > DateTime.Now)
                return new ScoutResult { Status = ScoutResult.State.Throttle };

            var indexers = GetIndexersForMedia(media);
            var minScore = GetRequiredScore(media, requirements);
            var maximumScore = GetRequiredScore(media, requirements, true);

            var results = new List<(IndexerResult Result, int Score)>();
            for (var i = 0; i < indexers.Count; i++)
            {
                results = results.Concat(indexers[i].FindResultsFor(media).Select((x) => (Result: x, Score: MediaRequirementScore(media, x, requirements))))
                    .OrderByDescending(x => x.Score).ThenByDescending(x => indexers[i].Priority).ThenByDescending(x => x.Result.Seeds + x.Result.Peers).ToList();

                if (results.Any() && results.Max(x => x.Score) >= maximumScore)
                {
                    return new ScoutResult(results.Select(x => x.Result)) { Status = ScoutResult.State.Found };
                }
            }

            if (results.Any())
                return new ScoutResult(results.Select(x => x.Result)) { Status = results.Max(x=>x.Score) >= minScore ? ScoutResult.State.Found : ScoutResult.State.BelowReqs };

            return new ScoutResult { Status = ScoutResult.State.NotFound };
        }

        private int GetRequiredScore(Media media, RequirementConfig requirements, bool forcePatient = false)
        {
            if (forcePatient || HasPatienceForMedia(media, requirements))
            {
                var groupTraitCount = requirements.PreferredGroups.Count() + requirements.PreferredTraits.Count();
                return (2 + groupTraitCount + Math.Min(2, groupTraitCount)) * (requirements.WaitForRepackOrProper ? 2 : 1);
            }  
            return 1;
        }

        private bool HasPatienceForMedia(Media media, RequirementConfig requirements)
        {
            if (requirements.ReleaseDateDeadlineTime.HasValue && DateTime.Now.Date == media.Release?.Date && DateTime.Now.TimeOfDay >= requirements.ReleaseDateDeadlineTime)
                return false;

            if (media is Movie movie)
            {
                // TODO: Think about making this a config or removing it entirely. (The idea is that if the movie was released in teaters a long time ago then regardless of first spotted date we're done waiting)
                if (movie.Release.HasValue && movie.Release.Value.AddMonths(6) < DateTime.Now) 
                    return false;

                if (!movie.FirstSpottedAt.HasValue && requirements.Patience.HasValue)
                    return true;

                return movie.FirstSpottedAt.Value.Add(requirements.Patience ?? TimeSpan.FromSeconds(0)) > DateTime.Now;
            }

            if (media is Episode episode)
            {
                return (episode.Release ?? episode.StateDate).Add(requirements.Patience ?? TimeSpan.FromSeconds(0)) > DateTime.Now;
            }

            return false;
        }

        private int MediaRequirementScore(Media media, IndexerResult result, RequirementConfig requirements)
        {
            var score = 0;
            if (result.VideoQuality < requirements.MinQuality)
                return 0;

            if (result.VideoQuality >= requirements.MinQuality)
                score += 1;

            if (result.VideoQuality == requirements.PreferredQuality)
                score += 1 + requirements.PreferredGroups.Count() + requirements.PreferredTraits.Count();

            if (requirements.PreferredGroups.Any(x => result.Group == x))
                score += 1;

            if (requirements.PreferredTraits.Any(x => result.Traits.Contains(x)))
                score += 1;

            if (requirements.PreferredTraits.Any(x => x == IndexerResult.QualityTrait.PROPER || x == IndexerResult.QualityTrait.REPACK))
                score += score * 2;

            return score;
        }

        public List<IIndexer> GetIndexersForMedia(Media media)
        {
            var indexers = this.Indexers.Where(x => x.SupportedMediaTypes.Contains(media.GetType()));

            return indexers.Where(x => !(x.SpecializedGenres?.Any() ?? false) || x.SpecializedGenres.Any(y => media.Genres?.Contains(y) ?? false))
                .OrderByDescending(x => media.Genres?.Count(y => x.SpecializedGenres?.Contains(y) ?? false) ?? 0).ToList();
        }
    }
}
