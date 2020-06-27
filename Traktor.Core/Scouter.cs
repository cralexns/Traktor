using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
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
        public class ScouterSettings
        {
            public RequirementConfig[] Requirements { get; set; }

            public RedirectConfig[] Redirects { get; set; }
        }

        public class RequirementConfig
        {
            public string MediaType { get; set; }

            public class Parameter
            {
                public enum ParameterCategory
                {
                    Resolution,
                    Audio,
                    Source,
                    Tag,
                    Group,
                    SizeMb,
                    FreeText
                }

                public enum ParameterComparison
                {
                    Equal,
                    NotEqual,
                    Minimum,
                    Maximum
                }

                public ParameterCategory Category { get; set; }

                public ParameterComparison Comparison { get; set; } = ParameterComparison.Equal;
                public string[] Definition { get; set; }  
                public TimeSpan? Patience { get; set; }
                public int Weight { get; set; } = 1;

                public IEnumerable<T> GetEnum<T>() where T : struct, IComparable
                {
                    foreach (var def in this.Definition)
                    {
                        yield return GetEnum<T>(def as string);
                    }
                }

                private T GetEnum<T>(string definition) where T : struct, IComparable
                {
                    if (Enum.TryParse<T>(definition, true, out T resolution))
                    {
                        return resolution;
                    }
                    throw new NotSupportedException($"Def: {Definition} is not valid in {Category}");
                }

                private bool Compare(IComparable value1, IComparable value2)
                {
                    switch (this.Comparison)
                    {
                        case ParameterComparison.Equal:
                            return value1.Equals(value2);
                        case ParameterComparison.NotEqual:
                            return !value1.Equals(value2);
                        case ParameterComparison.Minimum:
                            return value1.CompareTo(value2) >= 0;
                        case ParameterComparison.Maximum:
                            return value1.CompareTo(value2) <= 0;
                    }
                    return false;
                }

                private bool IsMatch(string def, IndexerResult result)
                {
                    switch (this.Category)
                    {
                        case ParameterCategory.Resolution:
                            return Compare(result.VideoQuality, GetEnum<IndexerResult.VideoQualityLevel>(def));
                        case ParameterCategory.Audio:
                        case ParameterCategory.Source:
                        case ParameterCategory.Tag:
                            var req = GetEnum<IndexerResult.QualityTrait>(def);
                            foreach (var trait in result.Traits)
                            {
                                if (Compare(trait, req))
                                    return true;
                            }
                            return false;
                        case ParameterCategory.Group:
                            return Compare(result.Group, def as string);
                        case ParameterCategory.SizeMb:
                            return Compare(result.SizeBytes, ((def.ToLong() ?? 0) * 1024 * 1024));
                        case ParameterCategory.FreeText:
                            return result.Title.Contains(def as string);
                    }
                    return false;
                }

                public int CalculateScore(IndexerResult result)
                {
                    foreach (var def in this.Definition)
                    {
                        if (IsMatch(def, result))
                        {
                            return this.Weight;
                        }
                    }

                    return 0;
                }

                private string GetEnumDescription(Enum value)
                {
                    // Get the Description attribute value for the enum value
                    FieldInfo fi = value.GetType().GetField(value.ToString());
                    DescriptionAttribute[] attributes = (DescriptionAttribute[])fi.GetCustomAttributes(typeof(DescriptionAttribute), false);

                    if (attributes.Length > 0)
                        return attributes[0].Description;
                    else
                        return value.ToString();
                }
            }

            public List<Parameter> Parameters { get; set; }

            /// <summary>
            /// On release day, never wait longer than this time, overrules patience setting but only on release date.
            /// </summary>
            public TimeSpan? ReleaseDateDeadlineTime { get; set; }

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

        public class RedirectConfig
        {
            public class RedirectRule
            {
                public int? SeasonFromStart { get; set; }
                public int? SeasonFromEnd { get; set; }

                public int? EpisodeFromStart { get; set; }
                public int? EpisodeFromEnd { get; set; }

                public string SeasonCalculation { get; set; }
                public string EpisodeCalculation { get; set; }

                public bool AppliesTo(Episode episode)
                {
                    if (episode.Season < (SeasonFromStart ?? episode.Season) || episode.Season > (SeasonFromEnd ?? episode.Season))
                        return false;

                    if (episode.Number < (EpisodeFromStart ?? episode.Number) || episode.Number > (EpisodeFromEnd ?? episode.Number))
                        return false;

                    return true;
                }
            }

            public string TraktShowSlug { get; set; }
            public RedirectRule[] Rules { get; set; }
        }

        public ScouterSettings Settings { get; private set; }

        public List<IIndexer> Indexers { get; private set; }

        public event Action<ScoutResult, Media> OnScouted;
        public Scouter(ScouterSettings settings)
        {
            this.Settings = settings;

            this.Indexers = Assembly.GetExecutingAssembly().GetTypes().Where(x => typeof(IIndexer).IsAssignableFrom(x) && !x.IsInterface && !x.IsAbstract)
                .Select(x => Activator.CreateInstance(x) as IIndexer).ToList();
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
                public int Score { get; set; }

                public Magnet(IndexerResult result, int score)
                {
                    this.Title = result.Title;
                    this.IsFullSeason = result.IsFullSeason;
                    this.Link = new Uri(result.Magnet);
                    this.Score = score;
                }
            }

            public State Status { get; set; }
            public IReadOnlyList<Magnet> Results { get; set; } = new List<Magnet>();
            public DateTime Date { get; set; } = DateTime.Now;

            public ScoutResult(IEnumerable<(IndexerResult, int)> results = null)
            {
                this.Date = DateTime.Now;
                if (results != null)
                {
                    this.Status = State.Found;
                    this.Results = results.Select(x => new Magnet(x.Item1, x.Item2)).ToList();
                }
            }

            public void RemoveBannedLinks(params Uri[] links)
            {
                if (links.Any())
                    this.Results = this.Results.Where(x => !links.Contains(x.Link)).ToList();
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
            return this.Settings.Requirements.FirstOrDefault(x => x.MediaType == media.GetType().Name);
        }

        private ScoutResult GetScoutResult(Media media, bool force = false)
        {
            var requirements = GetRequirementsForMedia(media);

            if (!force && media.LastScoutedAt.HasValue && requirements.Timeout.HasValue && new DateTime(Math.Max(media.Release?.Ticks ?? media.StateDate.Ticks, media.StateDate.Ticks)).Add(requirements.Timeout.Value) < DateTime.Now)
                return new ScoutResult { Status = ScoutResult.State.Timeout };

            if (!force && requirements.Delay.HasValue && media.Release?.Add(requirements.Delay.Value) > DateTime.Now)
                return new ScoutResult { Status = ScoutResult.State.Delayed };

            if (!force && requirements.NoResultThrottle.HasValue && media.LastScoutedAt.HasValue && !media.FirstSpottedAt.HasValue && media.LastScoutedAt.Value.Add(requirements.NoResultThrottle.Value) > DateTime.Now)
                return new ScoutResult { Status = ScoutResult.State.Throttle };

            var indexers = GetIndexersForMedia(media);
            //var minScore = GetRequiredScore(media, requirements);
            //var maximumScore = GetRequiredScore(media, requirements, true);

            var results = new List<(IndexerResult Result, (bool Passed, int Score, int MaximumScore) Evaluation)>();
            for (var i = 0; i < indexers.Count; i++)
            {
                //results = results.Concat(indexers[i].FindResultsFor(media).Select((x) => (Result: x, Score: MediaRequirementScore(media, x, requirements))))
                //    .OrderByDescending(x => x.Score).ThenByDescending(x => indexers[i].Priority).ThenByDescending(x => x.Result.Seeds + x.Result.Peers).ToList();

                var mediaToScout = GetRedirectedMedia(media) ?? media;

                var resultsFromIndexer = (indexers[i].FindResultsFor(mediaToScout) ?? new List<IndexerResult>()).Select((x) => (Result: x, Evaluation: EvaluateResult(x, media, requirements)))
                    .OrderByDescending(x => x.Evaluation.Score).ThenByDescending(x => indexers[i].Priority).ThenByDescending(x => (x.Result.Seeds * 10) + x.Result.Peers).ToList();

                results = results.Concat(resultsFromIndexer).ToList();  
            }

            if (results.Any() && results.Any(x => x.Evaluation.Passed && x.Evaluation.Score == x.Evaluation.MaximumScore))
            {
                return new ScoutResult(results.Select(x => (x.Result, x.Evaluation.Score))) { Status = ScoutResult.State.Found };
            }

            if (results.Any())
                return new ScoutResult(results.Select(x => (x.Result, x.Evaluation.Score))) { Status = results.Any(x=>x.Evaluation.Passed) ? ScoutResult.State.Found : ScoutResult.State.BelowReqs };

            return new ScoutResult { Status = ScoutResult.State.NotFound };
        }

        public Media GetRedirectedMedia(Media media)
        {
            if (media is Episode episode)
            {
                var redirect = this.Settings.Redirects?.FirstOrDefault(x => x.TraktShowSlug == episode.ShowId.Slug);
                if (redirect != null)
                {
                    var dt = new System.Data.DataTable();
                    foreach (var rule in redirect.Rules.Where(x=>x.AppliesTo(episode)))
                    {
                        int? seasonRedirect = null;
                        int? episodeRedirect = null;
                        if (!string.IsNullOrEmpty(rule.SeasonCalculation))
                        {
                            seasonRedirect = dt.Compute(string.Format(rule.SeasonCalculation, episode.Season), "") as int?;
                        }
                        if (!string.IsNullOrEmpty(rule.EpisodeCalculation))
                        {
                            episodeRedirect = dt.Compute(string.Format(rule.EpisodeCalculation, episode.Number), "") as int?;
                        }

                        if (seasonRedirect.HasValue || episodeRedirect.HasValue)
                        {
                            return episode.Clone(seasonRedirect ?? episode.Season, episodeRedirect ?? episode.Number);
                        }
                    }
                }
            }
            return null;
        }

        private (bool Passed, int Score, int MaximumScore) EvaluateResult(IndexerResult result, Media media, RequirementConfig requirement)
        {
            bool isDeadline = requirement.ReleaseDateDeadlineTime.HasValue && DateTime.Now.Date == media.Release?.Date && DateTime.Now.TimeOfDay >= requirement.ReleaseDateDeadlineTime;
            var patienceDate = GetPatienceCalculationDate(media);

            var total = 0;
            var passed = true;
            foreach (var parameter in requirement.Parameters)
            {
                bool canDisqualify = !parameter.Patience.HasValue || (!isDeadline && patienceDate.Add(parameter.Patience.Value) >= DateTime.Now);
                int score = parameter.CalculateScore(result);

                total += score;
                if (canDisqualify && score <= 0)
                    passed = false;
            }

            return (passed, total, requirement.Parameters.Sum(x=>x.Weight));
        }

        private DateTime GetPatienceCalculationDate(Media media)
        {
            if (media is Movie movie)
            {
                // TODO: Think about making this a config or removing it entirely. (The idea is that if the movie was released a long time ago then regardless of first spotted date we're done waiting)
                if (movie.Release.HasValue && movie.Release.Value.AddMonths(6) < DateTime.Now)
                    return movie.Release.Value;

                return movie.FirstSpottedAt ?? DateTime.Now;
            }

            return media.Release ?? media.StateDate;
        }

        public List<IIndexer> GetIndexersForMedia(Media media)
        {
            var indexers = this.Indexers.Where(x => x.SupportedMediaTypes.Contains(media.GetType()));

            return indexers.Where(x => !(x.SpecializedGenres?.Any() ?? false) || x.SpecializedGenres.Any(y => media.Genres?.Contains(y) ?? false))
                .OrderByDescending(x => media.Genres?.Count(y => x.SpecializedGenres?.Contains(y) ?? false) ?? 0).ToList();
        }
    }
}
