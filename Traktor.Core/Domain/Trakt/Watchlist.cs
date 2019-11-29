using System;
using System.Collections.Generic;
using System.Text;

namespace Traktor.Core.Domain.Trakt
{
    public class Watchlist : TraktAPIObjectBase
    {
        public Watchlist() : base(new TraktAPIRequest("sync/watchlist", "sync/watchlist/{type}")) { }

        public int rank { get; set; }
        public DateTime listed_at { get; set; }
        public string type { get; set; }
        public Episode episode { get; set; }
        public Show show { get; set; }
        public Movie movie { get; set; }
        public Season season { get; set; }

        public class Episode
        {
            public int season { get; set; }
            public int number { get; set; }
            public string title { get; set; }
            public Ids ids { get; set; }

            public class Ids : ITraktId, ITVDB, IIMDB, ITMDB
            {
                public int trakt { get; set; }
                public int tvdb { get; set; }
                public string imdb { get; set; }
                public int tmdb { get; set; }
            }
        }

        public class Show
        {
            public string title { get; set; }
            public int year { get; set; }
            public Ids ids { get; set; }

            public class Ids : ITraktId, ITVDB, IIMDB, ITMDB, ISlug
            {
                public int trakt { get; set; }
                public string slug { get; set; }
                public int tvdb { get; set; }
                public string imdb { get; set; }
                public int tmdb { get; set; }
            }
        }

        public class Movie
        {
            public string title { get; set; }
            public int year { get; set; }
            public Ids ids { get; set; }

            public class Ids : ITraktId, ISlug, IIMDB, ITMDB
            {
                public int trakt { get; set; }
                public string slug { get; set; }
                public string imdb { get; set; }
                public int tmdb { get; set; }
            }
        }

        public class Season
        {
            public int number { get; set; }
            public Ids ids { get; set; }

            public class Ids : ITVDB, ITMDB
            {
                public int tvdb { get; set; }
                public int tmdb { get; set; }
            }
        }
    }
}
