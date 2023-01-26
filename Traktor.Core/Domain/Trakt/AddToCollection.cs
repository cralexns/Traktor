using System;
using System.Collections.Generic;
using System.Text;

namespace Traktor.Core.Domain.Trakt
{
    public class AddToCollection : TraktAPIObjectBase
    {
        public AddToCollection() : base("https://api.trakt.tv/sync/collection", RestSharp.Method.Post) { }

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

        public class NotFound
        {
            public class Item
            {
                public class Ids
                {
                    public string imdb { get; set; }
                }

                public Ids ids { get; set; }
            }

            public List<Item> movies { get; set; }
            public List<Item> shows { get; set; }
            public List<Item> seasons { get; set; }
            public List<Item> episodes { get; set; }
        }

        public Added added { get; set; }
        public Updated updated { get; set; }
        public Existing existing { get; set; }
        public NotFound not_found { get; set; }
    }
}
