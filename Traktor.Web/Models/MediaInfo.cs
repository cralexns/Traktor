using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Traktor.Core.Domain;

namespace Traktor.Web.Models
{
    public class MediaInfo
    {
        public Media Media { get; set; }
        public Core.Services.Downloader.IDownloadInfo Download { get; set; }
        public int Priority { get; set; }
    }
}
