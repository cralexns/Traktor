using System;
using System.Collections.Generic;
using System.Text;

namespace Traktor.Core.Domain.Trakt
{
    public class CollectedMovie : TraktAPIObjectBase
    {
        public CollectedMovie() : base("sync/collection/movies") { }

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

        public DateTime collected_at { get; set; }
        public DateTime updated_at { get; set; }
        public Movie movie { get; set; }
    }
}
