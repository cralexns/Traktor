using System;
using System.Collections.Generic;
using System.Text;

namespace Traktor.Core.Domain.Trakt
{
    public class CalendarMovie :TraktAPIObjectBase
    {
        public CalendarMovie() : base(new TraktAPIRequest("calendars/my/movies", "calendars/my/movies/{start_date}/{days}")) { }

        public DateTime released { get; set; }
        public Movie movie { get; set; }

        public class Movie
        {
            public string title { get; set; }
            public int year { get; set; }
            public Ids ids { get; set; }

            public class Ids : ITraktId, IIMDB, ITMDB, ISlug
            {
                public int trakt { get; set; }
                public string slug { get; set; }
                public string imdb { get; set; }
                public int tmdb { get; set; }
            }
        }
    }
}
