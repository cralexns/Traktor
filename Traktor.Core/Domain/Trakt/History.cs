using System;
using System.Collections.Generic;
using System.Text;

namespace Traktor.Core.Domain.Trakt
{
    public class History : TraktAPIObjectBase
    {
        public History() : base(new TraktAPIRequest("/sync/history/{type}/{id}", "sync/history?start_at={start_at}")) { }

        public long id { get; set; }
        public DateTime watched_at { get; set; }
        public string action { get; set; }
        public string type { get; set; }
        public Movie movie { get; set; }
        public Episode episode { get; set; }
        public Show show { get; set; }
        

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
    }
}
