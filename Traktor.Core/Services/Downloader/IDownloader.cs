using System;
using System.Collections.Generic;

namespace Traktor.Core.Services.Downloader
{
    public interface IDownloader
    {
        List<IDownloadInfo> All();
        void Download(Uri magnetUri, int priority = 0);
        IDownloadInfo GetStatus(Uri magnetUri);
        IDownloadInfo Stop(Uri magnetUri, bool deleteFiles = false, bool remove = false);

        void Restart(Uri magnetUri);

        event Action<IDownloadInfo> OnChange;
    }
}