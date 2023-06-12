using RestSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Traktor.Core.Domain;
using Traktor.Core.Extensions;

namespace Traktor.Core.Services.Indexer
{
    public class EzTvIndexer : IndexerBase, IIndexer
    {
        public string Name => "EZTV";
        public Type[] SupportedMediaTypes => new Type[] { typeof(Episode) };

        public string[] SpecializedGenres => null;

        public int Priority => 50;

        public string ApiUrl { get; set; } = "https://eztv.re/api";

        private RestClient client;
        public EzTvIndexer()
        {
            client = new RestClient(this.ApiUrl);
        }

        public List<IndexerResult> FindResultsFor(Media media)
        {
            if (media is Episode episode && !string.IsNullOrEmpty(episode.ShowId?.IMDB))
            {
                var searchFor = episode.ShowId.IMDB.Substring(2);

                var normalizedShowTitle = string.Join("", episode.ShowTitle.ToList().Where(x => Regex.Match(x.ToString(), @"[\w0-9\s]").Success));

                var results = Search(searchFor);
                return results.Where(x => (x.Name == episode.ShowTitle || x.Name == normalizedShowTitle) && x.Season == episode.Season && (x.Episode == episode.Number|| x.IsFullSeason)).ToList();
            }
            return null;
        }

        private IEnumerable<IndexerResult> Search(string numericImdb)
        {
            var request = new RestRequest("get-torrents");
            request.AddParameter("imdb_id", numericImdb);

            var response = client.Execute<EzTvGetTorrentsResponse>(request);
            while (response?.IsSuccessful ?? false)
            {
                var torrents = response.Data.torrents ?? new List<EzTvGetTorrentsResponse.Torrent>();
                foreach (var torrent in torrents)
                {
                    var (result, range) = ParseEzTvTorrent(torrent);
                    yield return result;

                    if (range.HasValue && result.Episode.HasValue)
                    {
                        for (var i= result.Episode.Value+1; i<=range; i++)
                        {
                            yield return result.CloneAs(i);
                        }
                    }
                }

                if (response.Data.limit * response.Data.page < response.Data.torrents_count && torrents.Any())
                {
                    request.AddOrUpdateParameter("page", response.Data.page + 1);
                    response = client.Execute<EzTvGetTorrentsResponse>(request);
                }
                else response = null;
            }
        }

        private (IndexerResult Result, int? Range) ParseEzTvTorrent(EzTvGetTorrentsResponse.Torrent torrent)
        {
            var result = new IndexerResult(this, torrent.title, torrent.magnet_url, torrent.seeds, torrent.peers)
            {
                Date = DateTimeOffset.FromUnixTimeSeconds(torrent.date_released_unix).UtcDateTime,
                SizeBytes = torrent.size_bytes.ToLong() ?? 0,
                Season = torrent.season.ToInt(),
                Episode = torrent.episode != "0" ? torrent.episode.ToInt() : null,
                IMDB = $"tt{torrent.imdb_id}"
            };

            var numbering = GetNumbering(torrent.title);
            if (!result.Episode.HasValue && !result.Season.HasValue)
            {
                result.Episode = numbering.Episode;
                result.Season = numbering.Season;
            }

            return (result, numbering.Range);
        }

        private class EzTvGetTorrentsResponse
        {
            public string imdb_id { get; set; }
            public int torrents_count { get; set; }
            public int limit { get; set; }
            public int page { get; set; }

            public List<Torrent> torrents { get; set; }

            public class Torrent
            {
                public int id { get; set; }
                public string hash { get; set; }
                public string filename { get; set; }
                public string episode_url { get; set; }
                public string torrent_url { get; set; }
                public string magnet_url { get; set; }
                public string title { get; set; }
                public string imdb_id { get; set; }
                public string season { get; set; }
                public string episode { get; set; }
                public string small_screenshot { get; set; }
                public string large_screenshot { get; set; }
                public int seeds { get; set; }
                public int peers { get; set; }
                public long date_released_unix { get; set; }
                public string size_bytes { get; set; }
            }
        }
    }
}
