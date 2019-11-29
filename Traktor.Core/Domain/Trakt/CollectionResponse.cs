using System;
using System.Collections.Generic;
using System.Text;

namespace Traktor.Core.Domain.Trakt
{
    public class CollectionResponse
    {
        public class Added
        {
            public int movies { get; set; }
            public int episodes { get; set; }
        }

        public class Updated
        {
            public int movies { get; set; }
            public int episodes { get; set; }
        }

        public class Existing
        {
            public int movies { get; set; }
            public int episodes { get; set; }
        }

        public class Missing
        {
            public class Ids
            {
                public string imdb { get; set; }
            }

            public Ids ids { get; set; }
        }

        public class NotFound
        {
            public List<Missing> movies { get; set; }
            public List<Missing> shows { get; set; }
            public List<Missing> seasons { get; set; }
            public List<Missing> episodes { get; set; }
        }

        public Added added { get; set; }
        public Updated updated { get; set; }
        public Existing existing { get; set; }
        public NotFound not_found { get; set; }
    }
}
