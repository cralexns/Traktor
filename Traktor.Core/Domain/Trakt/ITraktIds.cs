using System;
using System.Collections.Generic;
using System.Text;

namespace Traktor.Core.Domain.Trakt
{
    public interface ITraktIds
    {

    }

    public interface ITraktId : ITraktIds
    {
        int trakt { get; set; }
    }

    public interface IIMDB : ITraktIds
    {
        string imdb { get; set; }
    }

    public interface ITMDB : ITraktIds
    {
        int tmdb { get; set; }
    }

    public interface ITVDB : ITraktIds
    {
        int tvdb { get; set; }
    }

    public interface ISlug : ITraktIds
    {
        string slug { get; set; }
    }
}
