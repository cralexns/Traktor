using System;
using System.Collections.Generic;
using System.Text;

namespace Traktor.Core.Domain.Trakt
{
    public class Season : TraktAPIObjectBase
    {
        public Season() : base("/shows/{id}/seasons?extended=episodes") { }

        public int number { get; set; }
        public Ids ids { get; set; }
        public List<Episode> episodes { get; set; }

        public class Ids : ITraktId, ITVDB, ITMDB
        {
            public int trakt { get; set; }
            public int tvdb { get; set; }
            public int tmdb { get; set; }
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
    }
}
