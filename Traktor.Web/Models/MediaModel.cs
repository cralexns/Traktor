using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Traktor.Core.Domain;

namespace Traktor.Web.Models
{
    public class MediaModel
    {
        public MediaInfo MediaInfo { get; set; }
        public Core.Scouter.ScoutResult ScoutResult { get; set; }
    }
}
