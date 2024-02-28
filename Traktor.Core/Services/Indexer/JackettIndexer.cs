using Newtonsoft.Json.Linq;
using RestSharp;
using RestSharp.Serializers.NewtonsoftJson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime;
using System.Text;
using System.Text.RegularExpressions;
using Traktor.Core.Domain;
using Traktor.Core.Extensions;

namespace Traktor.Core.Services.Indexer
{
    internal class JackettIndexer : IndexerBase, IIndexer
    {
        public static IndexerSettings DefaultSettings => new IndexerSettings
        {
            ApiUrl = "http://localhost:9117",
            Enabled = false,
            Priority = 50
        };

        readonly RestClient client;
        readonly string apiKey;
        public JackettIndexer(IndexerSettings settings) : base("Jackett", new[] { typeof(Episode), typeof(Movie) }, null, settings ?? DefaultSettings)
        {
            this.client = new RestClient(this.ApiUrl);
            this.client.UseNewtonsoftJson();
            this.apiKey = settings.ApiKey;
        }


        public class JackettResponse
        {
            public class Result
            {
                public DateTime FirstSeen { get; set; }
                public string Tracker { get; set; }
                public string TrackerId { get; set; }
                public string TrackerType { get; set; }
                public string CategoryDesc { get; set; }
                public object BlackholeLink { get; set; }
                public string Title { get; set; }
                public string Guid { get; set; }
                public string Link { get; set; }
                public string Details { get; set; }
                public DateTime PublishDate { get; set; }
                public int[] Category { get; set; }
                public long Size { get; set; }
                public object Files { get; set; }
                public int? Grabs { get; set; }
                public string Description { get; set; }
                public object RageID { get; set; }
                public object TVDBId { get; set; }
                public int? Imdb { get; set; }
                public object TMDb { get; set; }
                public object TVMazeId { get; set; }
                public object TraktId { get; set; }
                public object DoubanId { get; set; }
                public object[] Genres { get; set; }
                public object[] Languages { get; set; }
                public object[] Subs { get; set; }
                public int? Year { get; set; }
                public object Author { get; set; }
                public object BookTitle { get; set; }
                public object Publisher { get; set; }
                public object Artist { get; set; }
                public object Album { get; set; }
                public object Label { get; set; }
                public object Track { get; set; }
                public int Seeders { get; set; }
                public int Peers { get; set; }
                public string Poster { get; set; }
                public string InfoHash { get; set; }
                public string MagnetUri { get; set; }
                public object MinimumRatio { get; set; }
                public object MinimumSeedTime { get; set; }
                public string DownloadVolumeFactor { get; set; }
                public string UploadVolumeFactor { get; set; }
                public float Gain { get; set; }
            }

            public class Indexer
            {
                public string ID { get; set; }
                public string Name { get; set; }
                public int Status { get; set; }
                public int Results { get; set; }
                public object Error { get; set; }
            }

            public Result[] Results { get; set; }
            public Indexer[] Indexers { get; set; }
        }

        public new List<IndexerResult> FindResultsFor(Media media)
        {
            if (!SupportedMediaTypes.Contains(media.GetType()))
                throw new InvalidOperationException("Unsupported media type");

            switch (media)
            {
                case Movie movie:
                    return GetResultsForMovie(movie)?.ToList();
                case Episode episode:
                    var results = GetResultsForEpisode(episode)?.ToList();
                    return results;
            }

            return null;
        }

        private IEnumerable<IndexerResult> GetResultsForMovie(Movie movie)
        {
            Curator.Debug("JackettIndexer.GetResultsForMovie()");

            if (!string.IsNullOrEmpty(movie.Title))
                return SearchAll(movie.Title).Where(x=>x.Title.StartsWith(movie.Title, StringComparison.OrdinalIgnoreCase));

            return null;
        }

        private string Normalize(string s)
        {
            return Regex.Replace(s.FoldToASCII(), "[^A-Za-z0-9]*", "");
        }

        private IEnumerable<IndexerResult> GetResultsForEpisode(Episode episode)
        {
            Curator.Debug("JackettIndexer.GetResultsForEpisode()");

            if (!string.IsNullOrEmpty(episode.ShowTitle))
            {
                
                var results = SearchAll($"{episode.ShowTitle} S{episode.Season:00}E{episode.Number:00}");
                if (!results.Any() || results.Sum(x=>x.Seeds) < 100 || episode.Release < DateTime.Now.AddMonths(-6))
                {
                    results.ToList().AddRange(SearchAll($"{episode.ShowTitle} S{episode.Season:00}"));
                }
                return results.Where(x => Normalize(x.Name).Equals(Normalize(episode.ShowTitle), StringComparison.OrdinalIgnoreCase) && x.Season == episode.Season && (x.Episode == episode.Number || x.IsFullSeason));
            }

            return null;
        }


        private JackettResponse.Indexer[] GetAvailableJackettIndexers()
        {
            var request = new RestRequest("api/v2.0/indexers", Method.Get);
            request.AddHeader("Accept", "application/json");
            request.AddParameter("apikey", this.apiKey);

            var response = client.Execute<JackettResponse.Indexer[]>(request);
            if (response.IsSuccessful)
            {
                return response.Data;
            }
            return null;
        }

        public IEnumerable<IndexerResult> SearchAll(string query)
        {
            var request = new RestRequest("api/v2.0/indexers/!status:failing,test:passed/results");
            request.AddHeader("Accept", "application/json");
            request.AddParameter("apikey", this.apiKey);
            request.AddParameter("t", "search");
            request.AddParameter("query", query);

            var response = client.Execute<JackettResponse>(request);
            if (response.IsSuccessful)
            {
                foreach (var torrent in response.Data.Results)
                {
                    var (result, range) = ParseJackettTorrent(torrent);
                    yield return result;

                    if (range.HasValue && result.Episode.HasValue)
                    {
                        for (var i = result.Episode.Value + 1; i <= range; i++)
                        {
                            yield return result.CloneAs(i);
                        }
                    }
                }
            }

            if (!response.IsSuccessful)
            {
                throw new IndexerException(this, $"{response.ErrorMessage}");
            }
        }

        private (IndexerResult Result, int? Range) ParseJackettTorrent(JackettResponse.Result torrent)
        {
            var result = new IndexerResult(this, torrent.Title, torrent.Link, torrent.Seeders, torrent.Peers)
            {
                Date = torrent.PublishDate, //DateTimeOffset.FromUnixTimeSeconds(torrent.PublishDate).UtcDateTime,
                SizeBytes = torrent.Size,
                Source = $"{this.Name}:{torrent.TrackerId}"
            };

            var numbering = GetNumbering(torrent.Title);
            if (!result.Episode.HasValue && !result.Season.HasValue)
            {
                result.Episode = numbering.Episode;
                result.Season = numbering.Season;
            }

            return (result, numbering.Range);
        }

        private void SearchOnIndexer(string query, string indexer)
        {
            string jackettUrl = $"api/v2.0/indexers/{indexer}/results?apikey={apiKey}&t=search&q=" + query;
        }
    }
}
