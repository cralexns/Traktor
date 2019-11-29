using System;
using System.Collections.Generic;
using System.Text;

namespace Traktor.Core.Domain.Trakt
{
    public class WatchedShow : TraktAPIObjectBase
    {
        public WatchedShow() : base("sync/watched/shows") { }

        public int plays { get; set; }
        public DateTime last_watched_at { get; set; }
        public DateTime last_updated_at { get; set; }
        public object reset_at { get; set; }
        public Show show { get; set; }
        public List<Season> seasons { get; set; }

        public class Show
        {
            public string title { get; set; }
            public int year { get; set; }
            public Ids ids { get; set; }

            public class Ids : ITraktId, ISlug, ITVDB, IIMDB, ITMDB
            {
                public int trakt { get; set; }
                public string slug { get; set; }
                public int tvdb { get; set; }
                public string imdb { get; set; }
                public int tmdb { get; set; }
            }
        }

        public class Season
        {
            public int number { get; set; }
            public List<Episode> episodes { get; set; }

            public class Episode
            {
                public int number { get; set; }
                public int plays { get; set; }
                public DateTime last_watched_at { get; set; }
            }
        }
    }
}
