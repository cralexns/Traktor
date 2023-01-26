using System;
using System.Collections.Generic;
using System.Text;

namespace Traktor.Core.Domain.Trakt
{
    
    public class Movie : TraktAPIObjectBase
    {
        public Movie() : base("movies/{id}?extended=full") { }

        public class Ids : ITraktId, ISlug, IIMDB, ITMDB
        {
            public int trakt { get; set; }
            public string slug { get; set; }
            public string imdb { get; set; }
            public int tmdb { get; set; }
        }

        public string title { get; set; }
        public int year { get; set; }
        public Ids ids { get; set; }
        public string tagline { get; set; }
        public string overview { get; set; }
        public DateTime? released { get; set; }
        public int runtime { get; set; }
        public string country { get; set; }
        public DateTime? updated_at { get; set; }
        public object trailer { get; set; }
        public string homepage { get; set; }
        public decimal rating { get; set; }
        public int votes { get; set; }
        public int comment_count { get; set; }
        public string language { get; set; }
        public List<string> available_translations { get; set; }
        public List<string> genres { get; set; }
        public string certification { get; set; }
    }


}
