using Traktor.Core.Domain.Trakt;
using System;
using System.Collections.Generic;
using System.Text;

namespace Traktor.Core.Domain
{
    public static class TraktExtensions
    {
        public static Media.MediaId ToMediaId<T>(this T traktIds) where T : ITraktIds
        {
            return new Media.MediaId((traktIds as ITraktId)?.trakt, (traktIds as ISlug)?.slug, (traktIds as IIMDB)?.imdb, (traktIds as ITVDB)?.tvdb, (traktIds as ITMDB)?.tmdb);
        }

        public static T SetState<T>(this T m, Media.MediaState state) where T : Media
        {
            m.ChangeStateTo(state);
            return m;
        }
    }
}
