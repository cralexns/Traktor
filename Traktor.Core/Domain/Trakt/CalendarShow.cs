using System;
using System.Collections.Generic;
using System.Text;

namespace Traktor.Core.Domain.Trakt
{
    public class CalendarShow : TraktAPIObjectBase
    {
        public CalendarShow() : base(new TraktAPIRequest("calendars/my/shows", "calendars/my/shows/{start_date}/{days}")) { }

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

        public DateTime first_aired { get; set; }
        public Episode episode { get; set; }
        public Show show { get; set; }
    }
}
