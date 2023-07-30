using MonoTorrent;
using MonoTorrent.BEncoding;
using MonoTorrent.Client;
using MonoTorrent.Client.Listeners;
using MonoTorrent.Dht;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Traktor.Core.Extensions;

namespace Traktor.Core.Services.Downloader
{
    public class MediaDownloader : IDisposable, IDownloader
    {
        //public string[] Paths { get; set; } // 0=base, 1=torrents, 2=downloads, 3=fastresume.data, 4=dhtnodes
        private ClientEngine Engine { get; set; }
        private IDictionary<Uri, PrioritizedTorrentManager> Torrents { get; set; } = new ConcurrentDictionary<Uri, PrioritizedTorrentManager>();

        private class PrioritizedTorrentManager : TorrentManager
        {
            public PrioritizedTorrentManager(int priority, MagnetLink magnetLink, string savePath, TorrentSettings settings, string torrentSave) : base(magnetLink, savePath, settings, torrentSave)
            {
                this.Priority = priority;
            }

            public PrioritizedTorrentManager(int priority, Torrent torrent, string savePath, TorrentSettings settings) : base(torrent, savePath, settings)
            {
                this.Priority = priority;
            }
            public int Priority { get; set; }
        }

        public class DownloaderSettings
        {
            [System.Runtime.Serialization.IgnoreDataMember]
            public IPAddress IP { get; set; } = IPAddress.Any;
            public int Port { get; set; }

            public int MaximumDownloadSpeedKb { get; set; }
            public int MaximumUploadSpeedKb { get; set; }
            public int MaximumConnections { get; set; } = 200;
            public string Path { get; set; }
            public int MaxConcurrent { get; set; }
            public bool DisableSparseFiles { get; set; }
        }

        public DownloaderSettings Settings { get; private set; }

        public string BasePath { get; private set; }
        public string DownloadPath { get; private set; }
        public string CachePath { get; set; }
        private BEncodedDictionary FastResume { get; set; }

        public event Action<IDownloadInfo> OnChange;

        public MediaDownloader(DownloaderSettings settings)
        {
            this.Settings = settings;
            this.BasePath = Environment.CurrentDirectory;

            this.CachePath = Path.Combine(this.BasePath, ".cache");
            this.DownloadPath = Path.IsPathRooted(settings.Path) ? settings.Path : Path.Combine(this.BasePath, settings.Path);

            //this.Port = port;
            //this.Paths = new[]
            //{
            //    basePath,
            //    Path.Combine(basePath, "tcache"),
            //    Path.Combine(basePath, "Downloads"),
            //    Path.Combine(basePath, "tcache", "fastresume.data"),
            //    Path.Combine(basePath, "DhtNodes")
            //};

            //Console.CancelKeyPress += delegate { Shutdown().Wait(); };
            AppDomain.CurrentDomain.ProcessExit += delegate { Shutdown().Wait(); };
            AppDomain.CurrentDomain.UnhandledException += delegate (object sender, UnhandledExceptionEventArgs e) { Shutdown().Wait(); };
            Thread.GetDomain().UnhandledException += delegate (object sender, UnhandledExceptionEventArgs e) { Shutdown().Wait(); };

            try
            {
                var fastResumePath = Path.Combine(this.CachePath, "fastresume.data");
                FastResume = BEncodedValue.Decode<BEncodedDictionary>(File.ReadAllBytes(fastResumePath));
                File.Delete(fastResumePath);
            }
            catch
            {
                FastResume = new BEncodedDictionary();
            }

            InitializeClientEngine().Wait();
        }

        
        public void Download(Uri downloadUrl, int priority = 0)
        {
            PrioritizedTorrentManager torrentManager = null;

            lock (listLock)
            {
                if (this.Torrents.ContainsKey(downloadUrl))
                    return;

                Torrent torrent = null;
                MagnetLink magnetLink = null;
                if (downloadUrl.Scheme.StartsWith("http"))
                {
                    byte[] torrentData = new WebClient().DownloadData(downloadUrl.ToString());
                    if (Encoding.UTF8.GetString(torrentData,0,7) != "magnet:")
                    {
                        using (var stream = new MemoryStream(torrentData))
                        {
                            torrent = Torrent.Load(stream);
                        }

                        string torrentFileName = Path.Combine(this.CachePath, $"{torrent.Name}.torrent");
                        File.WriteAllBytes(torrentFileName, torrentData);
                    }
                    else
                    {
                        downloadUrl = new Uri(Encoding.UTF8.GetString(torrentData));
                    }
                }

                if (downloadUrl.Scheme == "magnet")
                {
                    magnetLink = MagnetLink.FromUri(downloadUrl);
                    torrent = GetTorrentFile(magnetLink);
                }

                if (torrent != null)
                {
                    torrentManager = new PrioritizedTorrentManager(priority, torrent, Path.Combine(this.DownloadPath, torrent.Name), new TorrentSettings());
                }
                else
                {
                    torrentManager = new PrioritizedTorrentManager(priority, magnetLink, Path.Combine(this.DownloadPath, magnetLink.Name), new TorrentSettings(), Path.Combine(this.CachePath, $"{magnetLink.InfoHash.ToHex()}.torrent"));
                }

                //torrentManager = new PrioritizedTorrentManager(priority, magnetLink, Path.Combine(this.DownloadPath, magnetLink.Name), new TorrentSettings(), Path.Combine(this.CachePath, $"{magnetLink.InfoHash.ToHex()}.torrent"));

                this.Torrents.Add(downloadUrl, torrentManager);
            }

            StartTorrentManager(torrentManager);
        }

        private string GetTorrentPath(InfoHash hash) => Path.Combine(this.CachePath, $"{hash.ToHex()}.torrent");

        private Torrent GetTorrentFile(MagnetLink magnetLink)
        {
            string torrentPath = GetTorrentPath(magnetLink.InfoHash);
            if (Torrent.TryLoad(torrentPath, out var torrent))
            {
                return torrent;
            }

            try
            {
                var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                return Engine.DownloadMetadataAsync(magnetLink, cancel.Token).ContinueWith(x =>
                {
                    var torrentData = x.Result;
                    if (Torrent.TryLoad(torrentData, out var torrent))
                    {
                        File.WriteAllBytes(torrentPath, torrentData);
                        return torrent;
                    }
                    return null;
                }).Result;
            }
            catch (AggregateException)
            {
                return null;
            }
        }

        private void StartTorrentManager(PrioritizedTorrentManager torrentManager)
        {
            var frKey = torrentManager.Torrent?.InfoHash.ToHex();
            try
            {
                if (!string.IsNullOrEmpty(frKey) && FastResume.ContainsKey(frKey))
                    torrentManager.LoadFastResume(new FastResume((BEncodedDictionary)FastResume[frKey]));
            }
            catch (InvalidOperationException ex)
            {
                FastResume.Remove(frKey);
            }

            Engine.Register(torrentManager).Wait();

            torrentManager.TorrentStateChanged += TorrentManager_TorrentStateChanged;
            //torrentManager.PeersFound += TorrentManager_PeersFound;

            if (torrentManager.Complete || Settings.MaxConcurrent == 0)
                torrentManager.StartAsync().Wait();
            else if (manageDownloadsTimer == null)
                manageDownloadsTimer = new Timer((x) => { ManageActiveDownloads(); }, null, TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(-1));
            else manageDownloadsTimer.Change(TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(-1));
        }

        //private void TorrentManager_PeersFound(object sender, PeersAddedEventArgs e)
        //{
        //    if (e.ExistingPeers != e.NewPeers && e.NewPeers != 0 && e.NewPeers == 50)
        //    {
        //        e.TorrentManager.TrackerManager.Announce().Wait();
        //    }
        //}

        public bool Force(Uri magnetUri, bool setMaxPriority = true)
        {
            var torrentManager = this.Torrents.GetValueByKey(magnetUri);
            if (torrentManager != null)
            {
                if (setMaxPriority)
                    torrentManager.Priority = this.Torrents.Max(x => x.Value.Priority) + 1;

                if (torrentManager.State == TorrentState.Stopped)
                    torrentManager.StartAsync().Wait();
                return true;
            }
            return false;
        }

        private Timer manageDownloadsTimer;
        private void TorrentManager_TorrentStateChanged(object sender, TorrentStateChangedEventArgs e)
        {
            //if (e.OldState == TorrentState.Metadata && e.TorrentManager.Torrent != null)
            //{
            //    e.TorrentManager.MoveFilesAsync(Path.Combine(this.Settings.DownloadPath, e.TorrentManager.Torrent.Name), true);
            //}

            if (e.NewState == TorrentState.Seeding)
            {
                e.TorrentManager.StopAsync().Wait();
            }

            if (e.NewState == TorrentState.Stopped) // e.NewState == TorrentState.Downloading || 
            {
                ManageActiveDownloads();
            }

            //Console.WriteLine($"TSC: OldState={e.OldState}, NewState={e.NewState}, Torrent={e.TorrentManager}");
            if (e.NewState.Is(TorrentState.Downloading, TorrentState.Error, TorrentState.Stopped, TorrentState.Paused, TorrentState.Metadata))
            {
                var keyValuePair = this.Torrents.FirstOrDefault(x => x.Value == e.TorrentManager);
                this.OnChange?.Invoke(new DownloadInfo(keyValuePair.Key, keyValuePair.Value, keyValuePair.Value.Priority, e.NewState));
            }
        }

        //private void StartDownload()
        //{
        //    if (this.Settings.MaxConcurrent == 0 || this.Active.Count < this.Settings.MaxConcurrent)
        //    {
        //        var waiting = this.Torrents.Values.OrderByDescending(x=>x.Priority).FirstOrDefault(x => !x.Complete && !x.State.Is(TorrentState.Downloading, TorrentState.Hashing, TorrentState.Metadata, TorrentState.Starting));
        //        if (waiting != null)
        //            waiting.StartAsync().Wait();
        //    } 
        //}

        private static object manageLock = new object();
        private int ManageActiveDownloads()
        {
            //if (!Engine?.IsRunning ?? true)
            //    return -1;

            lock (manageLock)
            {
                var orderedIncompleteManagers = this.Torrents.Values.Where(x=>!x.Complete).OrderByDescending(x => x.Priority).ThenByDescending(x => x.PartialProgress).ToList(); //  && !x.State.Is(TorrentState.Stopping, TorrentState.Metadata, TorrentState.Hashing, TorrentState.Stopped)

                var maxActive = this.Settings.MaxConcurrent;
                if (maxActive == 0)
                    maxActive = orderedIncompleteManagers.Count;

                var changes = 0;
                for (var i = 0; i < orderedIncompleteManagers.Count; i++)
                {
                    var tm = orderedIncompleteManagers[i];
                    if (tm.State == TorrentState.Downloading && tm.StartTime.AddMinutes(10) < DateTime.Now && i < maxActive && tm.Peers.Available == 0 && tm.OpenConnections == 0)
                    {
                        maxActive++;
                        continue;
                    }

                    if (i < maxActive)
                    {
                        if (tm.State == TorrentState.Stopped && !tm.Complete)
                        {
                            tm.Settings.MaximumConnections = Math.Max(1, this.Settings.MaximumConnections / Math.Min(orderedIncompleteManagers.Count, maxActive));
                            tm.StartAsync().Wait();
                            changes++;
                        }
                    }
                    else if (tm.State != TorrentState.Stopped && tm.State != TorrentState.Stopping)
                    {
                        tm.StopAsync().Wait();
                        changes++;
                    }
                }
                return changes;
            }
        }

        public class DownloadInfo : IDownloadInfo
        {
            public Uri MagnetUri { get; set; }
            public string Name { get; set; }
            public DateTime Started { get; set; }
            public double Progress { get; set; }
            public string[] Files { get; set; }
            public int Peers { get; set; }
            public int Leechs { get; set; }
            public int Seeds { get; set; }
            public int Priority { get; set; }

            public long Size { get; set; }
            public long DownloadedBytes { get; set; }
            public long UploadedBytes { get; set; }

            public long DownloadSpeed { get; set; }
            public long UploadSpeed { get; set; }

            public int MaxConnections { get; set; }

            public IDownloadInfo.DownloadState State { get; set; }

            public DateTime? Completed { get; set; }

            internal DownloadInfo(Uri magnetUri, TorrentManager torrentManager, int priority, TorrentState? state = null)
            {
                if (MagnetLink.FromUri(magnetUri).InfoHash != torrentManager.InfoHash)
                    throw new InvalidOperationException("TorrentManager doesn't match Magnet");

                MagnetUri = magnetUri;
                Name = torrentManager.Torrent?.Name ?? torrentManager.InfoHash.ToHex();
                Started = torrentManager.StartTime;
                Progress = torrentManager.PartialProgress;
                Peers = torrentManager.Peers.Available;
                Seeds = torrentManager.Peers.Seeds;
                Leechs = torrentManager.Peers.Leechs;
                Files = torrentManager.Torrent?.Files.Select(x => x.FullPath).ToArray() ?? new string[0];
                State = GetState(torrentManager, state);
                Size = torrentManager.Torrent?.Size ?? 0;
                Priority = priority;

                DownloadedBytes = torrentManager.Monitor?.DataBytesDownloaded ?? 0;
                UploadedBytes = torrentManager.Monitor?.DataBytesUploaded ?? 0;

                DownloadSpeed = torrentManager.Monitor.DownloadSpeed;
                UploadSpeed = torrentManager.Monitor.UploadSpeed;

                MaxConnections = torrentManager.Settings.MaximumConnections;
            }

            private IDownloadInfo.DownloadState GetState(TorrentManager manager, TorrentState? state = null)
            {
                switch (state ?? manager.State)
                {
                    case TorrentState.Stopped:
                    case TorrentState.Seeding:
                    case TorrentState.Stopping:
                        return manager.PartialProgress == 100 ? IDownloadInfo.DownloadState.Completed : IDownloadInfo.DownloadState.Waiting;
                    case TorrentState.Paused:
                    case TorrentState.HashingPaused:
                        return IDownloadInfo.DownloadState.Waiting;
                    case TorrentState.Starting:
                    case TorrentState.Hashing:
                    case TorrentState.Metadata:
                        return IDownloadInfo.DownloadState.Initializing;
                    case TorrentState.Downloading:
                        return manager.Peers.Available == 0 && manager.OpenConnections == 0 && manager.StartTime.AddMinutes(5) < DateTime.Now ? IDownloadInfo.DownloadState.Stalled : IDownloadInfo.DownloadState.Downloading;
                    default:
                    case TorrentState.Error:
                        return IDownloadInfo.DownloadState.Failed;
                }
            }
        }

        public IDownloadInfo GetStatus(Uri magnetUri)
        {
            var torrentManager = this.Torrents.GetValueByKey(magnetUri);
            if (torrentManager != null)
            {
                return new DownloadInfo(magnetUri, torrentManager, torrentManager.Priority);
            }
            return null;
        }

        public List<IDownloadInfo> All()
        {
            return this.Torrents.Select(x => new DownloadInfo(x.Key, x.Value, x.Value.Priority)).Cast<IDownloadInfo>().OrderByDescending(x=>x.Priority).ThenByDescending(x => x.Progress).ToList();
        }

        public void Restart(Uri magnetUri, bool deleteTorrentFile = false)
        {
            if (magnetUri == null)
                return;

            var tm = this.Torrents.GetValueByKey(magnetUri);
            if (tm != null)
            {
                tm.TorrentStateChanged -= TorrentManager_TorrentStateChanged;
                tm.StopAsync().Wait();

                if (deleteTorrentFile && tm.Torrent != null)
                {
                    var torrentPath = (!string.IsNullOrEmpty(tm.Torrent.TorrentPath)) ? tm.Torrent.TorrentPath : GetTorrentPath(tm.Torrent.InfoHash);
                    File.Delete(torrentPath);
                }

                tm.TorrentStateChanged += TorrentManager_TorrentStateChanged;
                tm.StartAsync().Wait();
            }
        }

        public void HashCheck(Uri magnetUri)
        {
            if (magnetUri == null)
                return;

            var tm = this.Torrents.GetValueByKey(magnetUri);
            if (tm != null && tm.HasMetadata)
            {
                if (tm.State != TorrentState.Stopped)
                tm.StopAsync().Wait();
                tm.HashCheckAsync(true);
            }
        }

        private static object listLock = new object();

        public IDownloadInfo Stop(Uri magnetUri, bool deleteFiles = false, bool remove = false)
        {
            if (magnetUri == null)
                return null;

            PrioritizedTorrentManager torrentManager = null;
            lock (listLock)
            {
                torrentManager = this.Torrents.GetValueByKey(magnetUri);
                if (torrentManager == null)
                    return null;

                if (remove)
                {
                    torrentManager.TorrentStateChanged -= TorrentManager_TorrentStateChanged;
                    this.Torrents.Remove(magnetUri);

                    try
                    {
                        var torrentPath = (!string.IsNullOrEmpty(torrentManager.Torrent.TorrentPath)) ? torrentManager.Torrent.TorrentPath : GetTorrentPath(torrentManager.Torrent.InfoHash);
                        File.Delete(torrentPath);
                    }
                    catch
                    {
                        // If we fail to delete the torrent file, it's no big deal - don't want to crash because of it.
                    }
                }
            }

            if (!torrentManager.State.Is(TorrentState.Stopped, TorrentState.Stopping))
            {
                torrentManager.StopAsync().Wait();
                Engine.Unregister(torrentManager).Wait();
                ManageActiveDownloads();
            }

            if (deleteFiles)
            {
                for (var i=1; i<=3; i++)
                {
                    try
                    {
                        if (torrentManager.SavePath != this.DownloadPath && Directory.Exists(torrentManager.SavePath))
                            Directory.Delete(torrentManager.SavePath, true);
                        break;
                    }
                    catch (IOException e) when ((e.HResult & 0x0000FFFF) == 32) // ERROR_SHARING_VIOLATION
                    {
                        if (i == 1)
                        {
                            // Maybe this helps with sharing violation, we shouldn't be causing any locks to the files in the SavePath ourselves but locks are happening and MonoTorrent supposedly closes all streams when you stop a torrent so..
                            GC.Collect();
                            GC.WaitForPendingFinalizers();
                        }

                        Task.Delay(2500*i).Wait();
                    }
                }

                
            }

            return new DownloadInfo(magnetUri, torrentManager, torrentManager.Priority);
        }

        private async Task InitializeClientEngine()
        {
            EngineSettings engineSettings = new EngineSettings
            {
                ListenPort = this.Settings.Port,
                PreferEncryption = true,
                MaximumDownloadSpeed = this.Settings.MaximumDownloadSpeedKb * 1024,
                MaximumUploadSpeed = this.Settings.MaximumUploadSpeedKb * 1024,
                MaximumConnections = this.Settings.MaximumConnections,
                AllowedEncryption = EncryptionTypes.All
            };

            // Create the default settings which a torrent will have.
            //TorrentSettings torrentDefaults = new TorrentSettings();

            // Create an instance of the engine.
            Engine = new ClientEngine(engineSettings); //new ClientEngine(engineSettings, PeerListenerFactory.CreateTcp(new IPEndPoint(this.Settings.IP, this.Settings.Port)));
            //Engine.StatsUpdate += Engine_StatsUpdate;

            byte[] nodes = Array.Empty<byte>();
            try
            {
                nodes = File.ReadAllBytes(Path.Combine(this.CachePath, "DhtNodes"));
            }
            catch
            {
                Console.WriteLine("No existing dht nodes could be loaded");
            }

            DhtEngine dht = new DhtEngine(new IPEndPoint(this.Settings.IP, this.Settings.Port));
            await Engine.RegisterDhtAsync(dht);

            // This starts the Dht engine but does not wait for the full initialization to
            // complete. This is because it can take up to 2 minutes to bootstrap, depending
            // on how many nodes time out when they are contacted.
            await Engine.DhtEngine.StartAsync(nodes);

            // If the SavePath does not exist, we want to create it.
            if (!Directory.Exists(this.DownloadPath))
                Directory.CreateDirectory(this.DownloadPath);

            if (!Directory.Exists(this.CachePath))
            {
                var di = Directory.CreateDirectory(this.CachePath);
                di.Attributes = FileAttributes.Directory | FileAttributes.Hidden;
            }
        }

        //private DateTime lastDownloadActivity = DateTime.Now;
        //private long totalBytesDownloaded;
        //private void Engine_StatsUpdate(object sender, StatsUpdateEventArgs e)
        //{
        //    var totalDownloaded = Engine.Torrents.Sum(x => x.Monitor.DataBytesDownloaded);
        //    if (totalBytesDownloaded != totalDownloaded || !Engine.Torrents.Any(x => x.State == TorrentState.Downloading))
        //    {
        //        lastDownloadActivity = DateTime.Now;
        //        totalBytesDownloaded = totalDownloaded;
        //    }
        //    else if ((DateTime.Now - lastDownloadActivity).TotalMinutes > 10)
        //    {
        //        totalBytesDownloaded = 0;
        //        lastDownloadActivity = DateTime.Now;
        //        Restart();
        //    }
        //}

        private async Task Shutdown()
        {
            var tmArray = this.Torrents.Values.ToArray();
            for (int i = 0; i < tmArray.Count(); i++)
            {
                tmArray[i].TorrentStateChanged -= TorrentManager_TorrentStateChanged;

                if (tmArray[i].State != TorrentState.Stopping)
                    tmArray[i].StopAsync().Wait();

                if (tmArray[i].HashChecked)
                {
                    var hex = tmArray[i].Torrent.InfoHash.ToHex();
                    var data = tmArray[i].SaveFastResume().Encode();

                    if (FastResume.ContainsKey(hex))
                        FastResume[hex] = data;
                    else FastResume.Add(hex, data);
                }
            }

            var nodes = await Engine.DhtEngine.SaveNodesAsync();
            File.WriteAllBytes(Path.Combine(this.CachePath, "DhtNodes"), nodes);
            File.WriteAllBytes(Path.Combine(this.CachePath, "fastresume.data"), FastResume.Encode());
            Engine.Dispose();
        }

        //public void Restart()
        //{
        //    Console.WriteLine("Restart MonoTorrent...");
        //    Shutdown().Wait();
        //    InitializeClientEngine().Wait();

        //    Console.WriteLine("Restarted MonoTorrent, re-registering torrents.");

        //    foreach (var ptm in this.Torrents)
        //        StartTorrentManager(ptm.Value);
        //}

        public void Dispose()
        {
            //if (manageDownloadsTimer != null)
            //    manageDownloadsTimer.Dispose();

            Shutdown().Wait();
        }
    }
}
