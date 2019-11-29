using System;

namespace Traktor.Core.Services.Downloader
{
    public interface IDownloadInfo
    {
        string[] Files { get; set; }
        Uri MagnetUri { get; set; }
        string Name { get; set; }
        int Peers { get; set; }
        int Leechs { get; set; }
        double Progress { get; set; }
        int Seeds { get; set; }
        DateTime Started { get; set; }
        DownloadState State { get; set; }
        int Priority { get; set; }
        long Size { get; set; }

        long DownloadSpeed { get; set; }
        long UploadSpeed { get; set; }

        public enum DownloadState
        {
            Initializing,
            Stalled,
            Downloading,
            Waiting,
            Completed,
            Failed
        }
    }
}