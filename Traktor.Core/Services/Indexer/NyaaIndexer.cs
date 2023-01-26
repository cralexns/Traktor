//using RestSharp;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Text.RegularExpressions;
//using Traktor.Core.Domain;

//namespace Traktor.Core.Services.Indexer
//{
//    public class NyaaIndexer : IndexerBase, IIndexer
//    {
//        public string Name => "Nyaa";
//        public override Regex QualityRegex { get; } = new Regex(@"(?:\.|\s|\[)?(?<quality>([0-9]{3,4}p)|(x\d{3,4}))(?:\.|\s|\]|$)", RegexOptions.ExplicitCapture);
//        public override Regex TraitRegex { get; } = new Regex(@"(?:\.|\s)(?<trait>(BluRay)|(Blu-ray)|(DTS-HD\.MA)|(DTS-HD)|(DTS)|(\[?Dual Audio\]?)|((?:[A-Z]*)5\.1)|(7\.1)|(AAC)|(WEB-DL)|(REPACK)|(PROPER))+", RegexOptions.ExplicitCapture);
//        public override Regex NameRegex { get; } = new Regex(@"(?:^\[[\w-\s]+.\]\s|^)(?<name>(?!\d{2})[A-Za-z0-9\s;,\/\!()'-]+?)\s(?:\d{1,2}-\d{1,2}|-\s\d{1,2}|\[|\d{2}|S\d{1}|Complete|ISO|Bluray)", RegexOptions.ExplicitCapture);
//        public override Regex NumberingRegex { get; } = new Regex(@"(?:-?\s)(?<episode>0?[1-9]+|\d{2})(?:\s|v2)|(?<range>(\d{2}\s?(-|~)\s?\d{1,2})|(\d{1,2}-\d{1,2}))|(?:S(?<season>\d+))", RegexOptions.ExplicitCapture); //(2 groups: episode and range)

//        // Complete regex (bool) = (\sCOMPLETE\s)|(?:\[)(Complete)

//        public Type[] SupportedMediaTypes => new Type[] { typeof(Movie), typeof(Episode) };

//        public string[] SpecializedGenres => new[] { "anime" };

//        public int Priority { get; set; } = 1;
//        public string ApiUrl { get; set; } = "https://nyaa.pantsu.cat/api/search/";

//        private RestClient client;
//        public NyaaIndexer()
//        {
//            client = new RestClient(this.ApiUrl);
//        }

//        public List<IndexerResult> FindResultsFor(Media media)
//        {
//            var results = GetResultsForMedia(media).Where(x=> MatchName(x.Name, media.GetCanonicalName()) && (x.Seeds > 0 || x.Peers > 0));
//            if (media is Episode episode)
//            {
//                return results.Where(x=>(x.Season == episode.Season && x.Episode == episode.Number) || x.IsFullSeason || (x.Episode == episode.Number && !x.Season.HasValue)).ToList();
//            }

//            return results.ToList();
//        }

//        public static string TransformForComparison(string text)
//        {
//            return Regex.Replace(text, @"[^A-Za-z0-9\s]", " ");
//        }

//        private bool MatchName(string name1, string name2)
//        {
//            var canonName = TransformForComparison(name1).Split(new char[0], StringSplitOptions.RemoveEmptyEntries);
//            var resultName = TransformForComparison(name2).Split(new char[0], StringSplitOptions.RemoveEmptyEntries);

//            return canonName.Except(resultName, StringComparer.InvariantCultureIgnoreCase).Count() == 0;
//        }

//        private IndexerResult ParseNyaaTorrent(NyaaTorrentResponse.Torrent torrent, Media media)
//        {
//            var idxr = new IndexerResult(this, torrent.name, torrent.magnet, torrent.seeders, torrent.leechers)
//            {
//                Date = torrent.date,
//                SizeBytes = torrent.filesize
//            };

//            var numbering = GetNumbering(torrent.name);
//            idxr.Season = numbering.Season;
//            idxr.Episode = numbering.Episode;

//            if (media is Episode episode)
//            {
//                if (numbering.Range.HasValue && numbering.Range >= episode.Number)
//                {
//                    if (!idxr.Season.HasValue)
//                        idxr.Season = episode.Season;

//                    if (numbering.Episode == 1 && numbering.Range >= episode.TotalEpisodesInSeason)
//                    {
//                        idxr.Episode = null;
//                    }
//                    else
//                    {
//                        idxr.Episode = episode.Number;
//                    }
//                }
//                if (!idxr.Season.HasValue && !idxr.Episode.HasValue && (new Regex(@"(\sCOMPLETE\s)|(?:\[)(Complete)").IsMatch(torrent.name)))
//                {
//                    idxr.Season = episode.Season;
//                }
//            }
            

//            return idxr;
//        }

//        private List<IndexerResult> GetResultsForMedia(Media media)
//        {
//            if (media is Episode episode)
//                return Search(episode.ShowTitle).Select(x=>ParseNyaaTorrent(x, media)).ToList();
//            return Search(media.Title).Select(x => ParseNyaaTorrent(x, media)).ToList();
//        }

//        private IEnumerable<NyaaTorrentResponse.Torrent> Search(string search)
//        {
//            var page = 1;
//            var results = Search(search, 100, page);

//            if (results == null)
//                yield break;
            
//            while (results.torrents.Any())
//            {
//                foreach (var result in results.torrents)
//                    yield return result;

//                page++;
//                results = Search(search, 100, page);
//            }
//        }

//        private NyaaTorrentResponse Search(string search, int limit = 100, int page = 1)
//        {
//            var request = new RestRequest(Method.GET);
//            request.AddParameter("q", search);
//            request.AddParameter("sort", 5);
//            request.AddParameter("c", "3_5");
//            request.AddParameter("limit", limit);
//            request.AddParameter("page", page);

//            var response = client.Execute<NyaaTorrentResponse>(request);
//            if (response.IsSuccessful)
//            {
//                return response.Data;
//            }
//            return null;
//        }

//        private class NyaaTorrentResponse
//        {
//            public List<Torrent> torrents { get; set; }
//            public int queryRecordCount { get; set; }
//            public int totalRecordCount { get; set; }

//            public class Torrent
//            {
//                public int id { get; set; }
//                public string name { get; set; }
//                public int status { get; set; }
//                public string hash { get; set; }
//                public DateTime date { get; set; }
//                public long filesize { get; set; }
//                public string description { get; set; }
//                public int sub_category { get; set; }
//                public int category { get; set; }
//                public int anidbid { get; set; }
//                public int vndbid { get; set; }
//                public int vgmdbid { get; set; }
//                public string videoquality { get; set; }
//                public List<string> languages { get; set; }
//                public string magnet { get; set; }
//                public int seeders { get; set; }
//                public int leechers { get; set; }

//            }
//        }
//    }
//}
