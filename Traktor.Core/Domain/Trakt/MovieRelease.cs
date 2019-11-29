using System;
using System.Collections.Generic;
using System.Text;

namespace Traktor.Core.Domain.Trakt
{
    public class MovieRelease : TraktAPIObjectBase
    {
        public MovieRelease() : base("movies/{id}/releases") { }
        public string country { get; set; }
        public string certification { get; set; }
        public DateTime release_date { get; set; }
        public string release_type { get; set; }
        public object note { get; set; }
    }
}
