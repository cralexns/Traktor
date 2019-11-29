using System;
using System.Collections.Generic;
using System.Text;

namespace Traktor.Core.Domain.Trakt
{
    public class WatchedMovie : TraktAPIObjectBase
    {
        public WatchedMovie() : base("sync/watched/movies") { }

        public int plays { get; set; }
        public DateTime last_watched_at { get; set; }
        public DateTime last_updated_at { get; set; }
        public Movie movie { get; set; }

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
    }
}
