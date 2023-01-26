using System;
using System.Collections.Generic;
using System.Text;

namespace Traktor.Core.Domain.Trakt
{
    public class Episode : TraktAPIObjectBase
    {
        public Episode() : base(new TraktAPIRequest("/shows/{id}/seasons/{season}", "/shows/{id}/seasons/{season}/episodes/{episode}?extended=full")) { }

        public int season { get; set; }
        public int number { get; set; }
        public string title { get; set; }
        public Ids ids { get; set; }
        public object number_abs { get; set; }
        public string overview { get; set; }
        public DateTime? first_aired { get; set; }
        public DateTime updated_at { get; set; }
        public decimal rating { get; set; }
        public int votes { get; set; }
        public int comment_count { get; set; }
        public List<string> available_translations { get; set; }
        public int runtime { get; set; }

        public class Ids : ITraktId, ITVDB, IIMDB, ITMDB
        {
            public int trakt { get; set; }
            public int tvdb { get; set; }
            public string imdb { get; set; }
            public int tmdb { get; set; }
        }
    }
}
