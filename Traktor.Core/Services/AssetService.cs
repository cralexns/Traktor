using OMDbApiNet;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Traktor.Core.Domain;

namespace Traktor.Core.Services
{
    public partial class AssetService
    {
        private AsyncOmdbClient omdb;
        public AssetService()
        {
            omdb = new AsyncOmdbClient(omdbApiKey);
        }

        public async Task<string> GetAsset(Media media)
        {
            var imdb = (media as Episode)?.ShowId.IMDB ?? media.Id?.IMDB;
            if (!string.IsNullOrEmpty(imdb))
            {
                return omdb.GetItemByIdAsync(imdb).Result?.Poster;
            }
            return null;
        }
    }
}
