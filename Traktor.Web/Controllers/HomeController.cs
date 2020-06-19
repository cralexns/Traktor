using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Traktor.Core;
using Traktor.Web.Models;

namespace Traktor.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public Curator Curator => HttpContext.Items["curator"] as Curator;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            var items = Curator.Library.ToList();
            var downloads = Curator.Downloader.All();

            return View(items.Select(x=> {
                var download = downloads.FirstOrDefault(y => y.MagnetUri == x.Magnet);
                return new MediaInfo { Media = x, Download = download, Priority = downloads.IndexOf(download)+1 };
            }).ToList());
        }

        public IActionResult Get(int dbId)
        {
            var mediaItem = Curator.Library.FirstOrDefault(x => x.DbId == dbId);
            if (mediaItem != null)
            {
                var download = Curator.Downloader.All().FirstOrDefault(x => x.MagnetUri == mediaItem.Magnet);

                return View(new MediaModel { MediaInfo = new MediaInfo { Media = mediaItem, Download = download } });
            }
            return RedirectToAction("Index");
        }

        public enum MediaAction
        {
            RestartDownload,
            CancelDownload,
            HashCheck,
            Scout,
            Remove,
            Restart,
            TryAnotherMagnet,
            IgnoreRequirements
        }

        [Route("{dbId}/{actionType}")]
        public IActionResult Action(int dbId, MediaAction actionType)
        {
            var mediaItem = Curator.Library.FirstOrDefault(x => x.DbId == dbId);
            if (mediaItem != null)
            {
                switch (actionType)
                {
                    case MediaAction.RestartDownload:
                        Curator.RestartDownload(mediaItem);
                        break;
                    case MediaAction.CancelDownload:
                        Curator.CancelDownload(mediaItem);
                        break;
                    case MediaAction.Scout:
                        Curator.ForceScout(mediaItem);
                        break;
                    case MediaAction.Remove:
                        Curator.Remove(mediaItem);
                        break;
                    case MediaAction.Restart:
                        Curator.Restart(mediaItem);
                        break;
                    case MediaAction.TryAnotherMagnet:
                        Curator.TryAnotherMagnet(mediaItem);
                        break;
                    case MediaAction.HashCheck:
                        Curator.HashCheck(mediaItem);
                        break;
                    case MediaAction.IgnoreRequirements:
                        Curator.IgnoreRequirement(mediaItem);
                        break;
                }
            }

            return RedirectToAction("Index");
        }

        public IActionResult DownloaderRestart()
        {
            Curator.RestartMonoTorrent();

            return RedirectToAction("Index");
        }

        public IActionResult Downloader()
        {
            return View(Curator.Downloader.All());
        }

        public IActionResult Log()
        {
            var logFiles = Directory.GetFiles(Path.Combine(Environment.CurrentDirectory, "Logs"), "*.log").Select(x => new LogModel.LogFile { Path = x, LastWrite = System.IO.File.GetLastWriteTime(x) }).ToList();
            var latest = logFiles.OrderByDescending(x => x.LastWrite).FirstOrDefault();

            using (var fs = new FileStream(latest.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                using (var reader = new StreamReader(fs))
                {
                    return View(new LogModel { Logs = logFiles, SelectedLogIndex = logFiles.IndexOf(latest), LogLines = reader.ReadToEnd().Split(Environment.NewLine) });
                }
            }
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
