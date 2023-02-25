using RateLimiter;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Traktor.Core.Domain;
using Traktor.Core.Extensions;
using ComposableAsync;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using RestSharp.Serializers.NewtonsoftJson;

namespace Traktor.Core.Services.Indexer
{
    public class RarbgIndexer : IndexerBase, IIndexer
    {
        public class RarBgTokenException : Exception
        {
            public RarBgTokenException() : base("Failed to acquire token.")
            {

            }
        }

        public string Name => "Rarbg";
        private enum QueryType
        {
            IMDB,
            TVDB,
            TMDB,
            Text
        }

        public string ApiUrl { get; set; } = "https://torrentapi.org/pubapi_v2.php";
        public string AppId { get; set; } = "Traktor";
        public static string Token { get; private set; }

        public int Priority { get; set; } = 100;

        public Type[] SupportedMediaTypes => new[] { typeof(Movie), typeof(Episode) };

        public string[] SpecializedGenres => null;

        private TimeLimiter limiter = TimeLimiter.GetFromMaxCountByInterval(1, TimeSpan.FromSeconds(2.5));

        private RestClient client;
        public RarbgIndexer()
        {
            this.client = new RestClient(ApiUrl);
            this.client.UseNewtonsoftJson();
            if (string.IsNullOrEmpty(Token))
            {
                GetToken();
            }
        }

        private async Task<bool> GetToken()
        {
            Curator.Debug("GetToken()");

            var requestToken = new RestRequest();
            requestToken.AddParameter("app_id", this.AppId);
            requestToken.AddParameter("get_token", "get_token");

            var response = await MakeRequest<RarBgBaseObject>(requestToken, false);

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                Token = response.Data.token;
                return true;
            }
            throw new RarBgTokenException();
        }

        private async Task<RestResponse<T>> MakeRequest<T>(RestRequest request, bool handleTokenError = true, int? retries = null) where T : RarBgBaseObject, new()
        {
            if (!retries.HasValue)
            {
                Curator.Debug("Await limiter..");
                await limiter;
            }
                

            Curator.Debug($"RestClient Execute()");
            var response = client.Execute<T>(request);
            Curator.Debug("RestClient Execute() Done");

            if (response.StatusCode == System.Net.HttpStatusCode.OK && response.Data != null && response.Data.IsError() && handleTokenError)
            {
                if (response.Data.error_code == 2 || response.Data.error_code == 4)
                {
                    if (await GetToken())
                        return await MakeRequest<T>(request, false);
                }
                else if (response.Data.error_code != 10 && response.Data.error_code != 20)
                    throw new Exception($"API error: {response.Data.error} ({response.Data.error_code})");

                if (response.Data.rate_limit == 1 && response.Data.error_code == 20 && (!retries.HasValue || retries > 0))
                {
                    var remainingRetries = (retries ?? 10)-1;
                    return await MakeRequest<T>(request, true, remainingRetries);
                }
                // rarbg returns error_code=20 and rate_limit=1 if the server is overloaded, could include a few retries here to improve scouting. (supposedly retrying theres a chance to hit another server)
            }
            return response;
        }

        private RestRequest BuildSearchRequest(QueryType type, string query, string category = null)
        {
            /* https://torrentapi.org/pubapi_v2.php?app_id=test&mode=search&search_imdb=tt4939064&token=0ukjglm1r4&format=json_extended&limit=100 */
            var request = new RestRequest();
            request.AddParameter("app_id", this.AppId);
            request.AddParameter("token", Token);
            request.AddParameter("mode", "search");
            request.AddParameter("format", "json_extended");
            request.AddParameter("limit", "100");

            if (!string.IsNullOrEmpty(category))
                request.AddParameter("category", category);

            switch (type)
            {
                case QueryType.IMDB:
                    request.AddParameter("search_imdb", query);
                    break;
                case QueryType.TVDB:
                    request.AddParameter("search_tvdb", query);
                    break;
                case QueryType.TMDB:
                    request.AddParameter("search_themoviedb", query);
                    break;
                case QueryType.Text:
                    request.AddParameter("search_string", query);
                    break;
            }

            return request;
        }

        private IEnumerable<IndexerResult> GetResultsForMovie(Movie movie)
        {
            Curator.Debug("GetResultsForMovie()");

            var category = "movies";
            if (!string.IsNullOrEmpty(movie.Id.IMDB))
                return QueryApi(QueryType.IMDB, movie.Id.IMDB, category);

            if (movie.Id.TMDB.HasValue)
                return QueryApi(QueryType.TMDB, movie.Id.TMDB.ToString(), category);

            return null;
        }

        private IEnumerable<IndexerResult> GetResultsForEpisode(Episode episode)
        {
            Curator.Debug("GetResultsForEpisode()");

            var category = "tv";
            IEnumerable<IndexerResult> results = null;
            if (!string.IsNullOrEmpty(episode.ShowId.IMDB))
                results = QueryApi(QueryType.IMDB, episode.ShowId.IMDB, category);
            else if (episode.ShowId.TVDB.HasValue)
                results = QueryApi(QueryType.TVDB, episode.ShowId.TVDB.ToString(), category);

            return results?.Where(x => x.Season == episode.Season && (x.Episode == episode.Number || x.IsFullSeason));
        }

        private IEnumerable<IndexerResult> QueryApi(QueryType type, string query, string category)
        {
            Curator.Debug("QueryApi()");
            var request = BuildSearchRequest(QueryType.IMDB, query, category);

            Curator.Debug("QueryApi() -> MakeRequest()");
            var response = MakeRequest<RarbgApiResponse>(request).Result;
            if (response.IsSuccessful)
                return ParseResults(response.Data);
            else if (response.ErrorException != null)
            {
                if (response.StatusCode == (System.Net.HttpStatusCode)520)
                {
                    Curator.Debug("QueryApi() -> Cloudflare returned a 520 .. ignore it and return empty result.");
                    return null;
                }    
                throw new Exception($"API error ({response.StatusCode}): {response.ErrorMessage}");
            }
                
            return null;
        }

        private IEnumerable<IndexerResult> ParseResults(RarbgApiResponse response)
        {
            Curator.Debug("ParseResults()");

            if (response.error_code == 20 || response.torrent_results == null)
                yield break;

            foreach (var torrent in response.torrent_results)
            {
                var (parsed, range) = ParseResult(torrent);
                yield return parsed;

                if (parsed.Episode.HasValue && range.HasValue)
                {
                    for (var i = parsed.Episode.Value + 1; i <= range; i++)
                    {
                        yield return parsed.CloneAs(i);
                    }
                }
            }
        }

        private (IndexerResult Result, int? Range) ParseResult(RarbgApiResponse.TorrentResult torrent)
        {
            var result = new IndexerResult(this, torrent.title, torrent.download, torrent.seeders, torrent.leechers)
            {
                Date = torrent.pubdate,
                SizeBytes = torrent.size,
                IMDB = torrent.episode_info.imdb,
                Season = torrent.episode_info.seasonnum.ToInt(),
                Episode = (torrent.episode_info.epnum != "1000000") ? torrent.episode_info.epnum.ToInt() : null
            };

            var numbering = GetNumbering(torrent.title);
            if (!result.Episode.HasValue && !result.Season.HasValue)
            {
                result.Episode = numbering.Episode;
                result.Season = numbering.Season;
            }

            return (result, numbering.Range);
        }

        public List<IndexerResult> FindResultsFor(Media media)
        {
            if (!SupportedMediaTypes.Contains(media.GetType()))
                throw new InvalidOperationException("Unsupported media type");

            switch (media)
            {
                case Movie movie:
                    return GetResultsForMovie(movie)?.ToList();
                case Episode episode:
                    return GetResultsForEpisode(episode)?.ToList();
            }

            return null;
        }
    }

    public class RarbgApiResponse : RarBgBaseObject
    {
        public class TorrentResult
        {
            public class EpisodeInfo
            {
                public string imdb { get; set; }
                public object tvrage { get; set; }
                public string tvdb { get; set; }
                public string themoviedb { get; set; }
                public string airdate { get; set; }
                public string epnum { get; set; }
                public string seasonnum { get; set; }
                public string title { get; set; }
            }

            public string title { get; set; }
            public string category { get; set; }
            public string download { get; set; }
            public int seeders { get; set; }
            public int leechers { get; set; }
            public long size { get; set; }
            public DateTime pubdate { get; set; }
            public EpisodeInfo episode_info { get; set; }
            public int ranked { get; set; }
            public string info_page { get; set; }
        }

        public List<TorrentResult> torrent_results { get; set; }
    }

    public class RarBgBaseObject
    {
        public string error { get; set; }
        public int error_code { get; set; }
        public string token { get; set; }
        public int rate_limit { get; set; }

        public bool IsError() => error_code > 0;
    }
}
