using System;
using System.Collections.Generic;
using System.Text;

namespace Traktor.Core.Domain.Trakt
{
    public class CollectionRequest
    {
        public class Movie
        {
            public DateTime collected_at { get; set; }
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

        public class Show
        {
            public class Ids : ITraktId, ISlug, IIMDB, ITMDB, ITVDB
            {
                public int trakt { get; set; }
                public string slug { get; set; }
                public int tvdb { get; set; }
                public string imdb { get; set; }
                public int tmdb { get; set; }
            }

            public class Season
            {
                public class Episode
                {
                    public int number { get; set; }
                }

                public int number { get; set; }
                public List<Episode> episodes { get; set; }
            }

            public string title { get; set; }
            public int year { get; set; }
            public Ids ids { get; set; }
            public List<Season> seasons { get; set; }
        }
        public class Season
        {
            public class Ids : ITraktId, ITMDB, ITVDB
            {
                public int trakt { get; set; }
                public int tvdb { get; set; }
                public int tmdb { get; set; }
            }

            public Ids ids { get; set; }
        }

        public class Episode
        {
            public class Ids : ITraktId, IIMDB, ITMDB, ITVDB
            {
                public int trakt { get; set; }
                public int tvdb { get; set; }
                public string imdb { get; set; }
                public int tmdb { get; set; }
            }

            public Ids ids { get; set; }
        }

        public List<Movie> movies { get; set; }
        public List<Show> shows { get; set; }
        public List<Season> seasons { get; set; }
        public List<Episode> episodes { get; set; }
    }
}
