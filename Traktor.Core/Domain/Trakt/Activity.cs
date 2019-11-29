using System;
using System.Collections.Generic;
using System.Text;
using RestSharp;

namespace Traktor.Core.Domain.Trakt
{
    public class LastActivity : TraktAPIObjectBase
    {
        public LastActivity() : base("sync/last_activities") {}

        public DateTime all { get; set; }
        public Movies movies { get; set; }
        public Episodes episodes { get; set; }
        public Shows shows { get; set; }
        public Seasons seasons { get; set; }
        public Comments comments { get; set; }
        public Lists lists { get; set; }

        public class Movies
        {
            public DateTime watched_at { get; set; }
            public DateTime collected_at { get; set; }
            public DateTime rated_at { get; set; }
            public DateTime watchlisted_at { get; set; }
            public DateTime commented_at { get; set; }
            public DateTime paused_at { get; set; }
            public DateTime hidden_at { get; set; }
        }

        public class Episodes
        {
            public DateTime watched_at { get; set; }
            public DateTime collected_at { get; set; }
            public DateTime rated_at { get; set; }
            public DateTime watchlisted_at { get; set; }
            public DateTime commented_at { get; set; }
            public DateTime paused_at { get; set; }
        }

        public class Shows
        {
            public DateTime rated_at { get; set; }
            public DateTime watchlisted_at { get; set; }
            public DateTime commented_at { get; set; }
            public DateTime hidden_at { get; set; }
        }

        public class Seasons
        {
            public DateTime rated_at { get; set; }
            public DateTime watchlisted_at { get; set; }
            public DateTime commented_at { get; set; }
            public DateTime hidden_at { get; set; }
        }

        public class Comments
        {
            public DateTime liked_at { get; set; }
        }

        public class Lists
        {
            public DateTime liked_at { get; set; }
            public DateTime updated_at { get; set; }
            public DateTime commented_at { get; set; }
        }
    }
}
