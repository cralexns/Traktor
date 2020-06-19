using Microsoft.Extensions.Configuration;
using Serilog;
using System;
using System.Linq;
using System.Threading;
using Traktor.Core;
using Traktor.Core.Services;
using Traktor.Core.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Traktor.Core.Tools;

namespace ConsoleApp2
{
    class Program
    {
        public static TimeSpan Interval { get; set; }
        public static bool KeepAlive { get; set; }
        public static string ConnectivityScriptPath { get; set; }

        static void Main(string[] args)
        {
            var appName = nameof(Traktor);
            using (var mutex = new Mutex(true, appName))
            {
                if (!mutex.WaitOne(TimeSpan.Zero))
                {
                    Console.WriteLine("Traktor is already running. Exiting..");
                    return;
                }

                RunTraktor(args);

                mutex.ReleaseMutex();
            }
        }

        private static void RunTraktor(string[] args)
        {
            var logLevelSwitch = new Serilog.Core.LoggingLevelSwitch(Serilog.Events.LogEventLevel.Information);
            Log.Logger = new LoggerConfiguration().MinimumLevel.ControlledBy(logLevelSwitch).WriteTo.Console(outputTemplate: "{Message}{NewLine}").WriteTo.File("Logs\\.log", outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}", rollingInterval: RollingInterval.Day).CreateLogger();

            try
            {
                var config = new ConfigurationBuilder()
                            .SetBasePath(Environment.CurrentDirectory)
                            .AddCommandLine(args)
                            .AddJsonFile("appsettings.json", true, true)
                            .Build();

                Interval = config.GetValue<TimeSpan?>("interval") ?? TimeSpan.FromMinutes(5);
                KeepAlive = config.GetValue<bool>("keepalive");
                ConnectivityScriptPath = config.GetValue<string>("connectscript");

                var logLevel = config.GetValue<string>("loglevel");
                if (!string.IsNullOrEmpty(logLevel))
                    logLevelSwitch.MinimumLevel = Enum.Parse<Serilog.Events.LogEventLevel>(logLevel);
                else if (System.Diagnostics.Debugger.IsAttached)
                    logLevelSwitch.MinimumLevel = Serilog.Events.LogEventLevel.Debug;

                Log.Information($"Minimum log level = {logLevelSwitch.MinimumLevel}");
                AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
                {
                    LogException((Exception)e.ExceptionObject);
                };

                AppDomain.CurrentDomain.ProcessExit += (sender, e) =>
                {
                    Log.Information("Shutting down ..");
                };

                if (logLevelSwitch.MinimumLevel == Serilog.Events.LogEventLevel.Verbose)
                {
                    Log.Information("Logging all exceptions raised (First Chance), this also includes handled exceptions.");
                    AppDomain.CurrentDomain.FirstChanceException += (sender, e) =>
                    {
                        Log.Verbose($"Exception (FC): {e.Exception}");
                    };
                }

                DisplayBindingIp();

                //Console.ReadLine();
                //return;

                Curator.CuratorConfiguration traktorConfig = LoadCuratorConfiguration(config, args);

                TraktService ts = new TraktService();
                Curator curator = new Curator(ts);

                if (StartCurator(curator, ts, traktorConfig))
                {
                    if (!string.IsNullOrEmpty(config.GetValue<string>("urls")))
                    {
                        var startup = new Traktor.Web.Startup(config, curator);
                        var host = Host.CreateDefaultBuilder(args).ConfigureWebHostDefaults(x =>
                        {
                            x.ConfigureServices(startup.ConfigureServices).Configure(startup.Configure);
                        }).UseConsoleLifetime().Build().RunAsync();

                        Log.Information($"Running Traktor.Web @ {string.Join(", ", startup.Addresses)}");
                    }

                    ScheduleCuratorUpdates(curator, ts);
                }
            }
            catch (Exception ex)
            {
                LogException(ex);
                throw;
            }
        }

        private static void ScheduleCuratorUpdates(Curator curator, TraktService ts)
        {
            Log.Information($"Scheduling update every {Interval} ..");
            // Schedule update.
            Timer timer = null;
            using (timer = new Timer((t) =>
            {
                timer.Change(Timeout.Infinite, Timeout.Infinite);
                try
                {
                    var result = UpdateCurator(curator);
                    if (result.Is(Curator.CuratorResult.Error, Curator.CuratorResult.NotInitialized, Curator.CuratorResult.Stopped))
                    {
                        Environment.Exit(1);
                    }

                    if (result.Is(Curator.CuratorResult.TraktAuthenticationRequired))
                    {
                        
                        if (!AuthenticateTrakt(ts))
                        {
                            Environment.Exit(1);
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (!string.IsNullOrEmpty(ConnectivityScriptPath) && (ex.GetBaseException() is System.Net.Sockets.SocketException sEx && sEx.ErrorCode == 10013) || (ex is Traktor.Core.Services.Indexer.RarbgIndexer.RarBgTokenException rEx))
                    {
                        Log.Error($"Caught exception: {ex.Message} - potential connectivity issue, launch connectivity script: {ConnectivityScriptPath}");
                        var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(ConnectivityScriptPath) { CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardError = true });
                        process.OutputDataReceived += (object sender, System.Diagnostics.DataReceivedEventArgs e) => Log.Debug($"[ConnectivityScript:Output] {e.Data}");
                        process.ErrorDataReceived += (object sender, System.Diagnostics.DataReceivedEventArgs e) => Log.Debug($"[ConnectivityScript:Error] {e.Data}");
                        process.WaitForExit();
                    }
                    else
                    {
                        Log.Error($"Caught exception: {ex.Message}");

                        if (!KeepAlive)
                            throw;
                    }
                }

                timer.Change((int)Interval.TotalMilliseconds, Timeout.Infinite);

                //if (logLevelSwitch.MinimumLevel == Serilog.Events.LogEventLevel.Debug)
                //{
                //    PrintDownloads(curator);
                //}
            }, null, 1, Timeout.Infinite))
            {
                while (HandleInput(curator))
                {
                    // Keep alive waiting for input.
                }
            }
        }

        private static void LogException(Exception ex)
        {
            Log.Fatal(ex, $"Unhandled Exception: {ex.Message}");
        }

        private static Curator.CuratorResult UpdateCurator(Curator curator)
        {
            var update = curator.Update();
            if (update == Curator.CuratorResult.Updated)
                Log.Debug($"Curator => {update}");
            else Log.Information($"Curator => {update}");

            return update;
        }

        private static void DisplayBindingIp()
        {
            using (var s = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.InterNetwork, System.Net.Sockets.SocketType.Dgram, System.Net.Sockets.ProtocolType.Udp))
            {
                s.Bind(new System.Net.IPEndPoint(System.Net.IPAddress.Any, 0));
                s.Connect("google.com", 0);

                var ipaddr = s.LocalEndPoint as System.Net.IPEndPoint;
                var addr = ipaddr.Address.ToString();
                Log.Write(Serilog.Events.LogEventLevel.Debug, $"New connections seem to be binding to local IP: {addr}");
            }
        }

        private static bool HandleInput(Curator curator)
        {
            var input = Console.ReadLine();
            switch (input)
            {
                case "downloads":
                    PrintDownloads(curator);
                    return true;
                case "update":
                    UpdateCurator(curator);
                    return true;
                case "restartmt":
                    (curator.Downloader as Traktor.Core.Services.Downloader.MediaDownloader).Restart();
                    return true;
                case "terminate":
                case "quit":
                case "exit":
                case "abort":
                    Console.WriteLine("Exiting..");
                    return false;
                case string value when value.StartsWith("forcedl"):
                    var dlIndex = value.Split(" ").Skip(1).FirstOrDefault().ToInt();
                    if (dlIndex.HasValue)
                    {
                        var dli = curator.Downloader.All()[dlIndex.Value];
                        if (dli != null)
                        {
                            if ((curator.Downloader as Traktor.Core.Services.Downloader.MediaDownloader).Force(dli.MagnetUri))
                            {
                                Console.WriteLine($"Forced DL on {dli.Name}!");
                            }
                        }
                    }
                    return true;
                default:
                    return true;
            }
        }

        private static void PrintDownloads(Curator curator)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            var downloadInfos = curator.Downloader.All();
            Console.WriteLine($"--- Downloads ({downloadInfos.Count}) ---");
            foreach (var downloadInfo in downloadInfos)
            {
                var percentage = Math.Round(downloadInfo.Progress, 2);
                var downSpeed = Utility.SizeSuffix(downloadInfo.DownloadSpeed);
                var upSpeed = Utility.SizeSuffix(downloadInfo.UploadSpeed);
                Console.WriteLine(@$"[{downloadInfos.IndexOf(downloadInfo) + 1}] {downloadInfo.State}: {downloadInfo.Name} - {percentage}%, (▼ {downSpeed}/s) (▲ {upSpeed}/s) [{downloadInfo.Leechs}L|{downloadInfo.Seeds}S|{downloadInfo.Peers}P]");
            }
            Console.WriteLine("---");
        }

        private static Curator.CuratorConfiguration LoadCuratorConfiguration(IConfiguration config, string[] args)
        {
            var traktorConfig = config.GetSection("traktor").Get<Curator.CuratorConfiguration>();

            if (traktorConfig == null)
            {
                traktorConfig = Curator.CuratorConfiguration.Default;
                var configJson = Newtonsoft.Json.JsonConvert.SerializeObject(new { traktor = traktorConfig }, Newtonsoft.Json.Formatting.Indented, new Newtonsoft.Json.Converters.StringEnumConverter());

                System.IO.File.WriteAllText(System.IO.Path.Combine(Environment.CurrentDirectory, "appsettings.json"), configJson);
            }
            return traktorConfig;
        }

        private static bool StartCurator(Curator curator, TraktService ts, Curator.CuratorConfiguration config)
        {
            Log.Debug("Traktor starting ...");
            switch (curator.Initialize(config, e => LogException(e),
                change =>
                {
                    Log.Information($"[Library] {change} - {change.Media}");
                },
                (scoutResult, media) =>
                {
                    if (scoutResult.Status == Scouter.ScoutResult.State.Throttle && media.LastScoutedAt.Value.Add(Interval) < DateTime.Now)
                        return;

                    Log.Information($"[Scouter] {media} = {scoutResult.Status}");
                    foreach (var magnet in scoutResult.Results)
                    {
                        Log.Debug($" - {magnet.Title}");
                    }
                },
                downloadInfo =>
                {
                    Log.Information($"[Downloader] {downloadInfo.State}: {downloadInfo.Name} ({Math.Round(downloadInfo.Progress, 2)}%) [L={downloadInfo.Leechs}, S={downloadInfo.Seeds} ({downloadInfo.Peers})]");
                },
                (fileResult, medias) =>
                {
                    switch (fileResult.Action)
                    {
                        case FileService.FileResult.FileAction.Deliver:
                            Log.Information($"[Delivery] {fileResult.Files?.Count() ?? 0} files moved to folder: '{fileResult.FolderName}' = {fileResult.Status}");
                            if (fileResult.Status == FileService.FileResult.ActionStatus.Error)
                                Log.Error($" .. {fileResult.Error}");
                            break;
                        case FileService.FileResult.FileAction.Rename:
                            Log.Information($"[Rename] {fileResult.Files?.Count() ?? 0} files renamed in folder '{fileResult.FolderName}' = {fileResult.Status}");
                            if (fileResult.Status == FileService.FileResult.ActionStatus.Error)
                                Log.Error($" .. {fileResult.Error}");
                            break;
                        case FileService.FileResult.FileAction.Delete:
                            Log.Information($"[Delete] {fileResult.Files?.Count() ?? 0} files deleted in folder '{fileResult.FolderName}' = {fileResult.Status}");
                            if (fileResult.Status == FileService.FileResult.ActionStatus.Error)
                                Log.Error($" .. {fileResult.Error}");
                            break;
                        default:
                            break;
                    }
                }))
            {
                case Curator.CuratorResult.Started:
                    Log.Debug("Traktor started.");
                    curator.OnCuratorEvent += (x) => Log.Information($"[{x.Type}] {x.Message}");
                    return true;
                case Curator.CuratorResult.TraktAuthenticationRequired:
                    if (AuthenticateTrakt(ts))
                        return StartCurator(curator, ts, config);
                    return false;
                default:
                case Curator.CuratorResult.Error:
                    Log.Error("Traktor crashed!");
                    return false;
                case Curator.CuratorResult.Stopped:
                    Log.Information("Traktor stopped!");
                    return false;
            }
        }

        private static bool AuthenticateTrakt(TraktService ts)
        {
            var dAuth = ts.AuthenticateDevice();

            Log.Warning($"Input [{dAuth.user_code}] @ {dAuth.verification_url} ..");
            if (ts.AuthenticateDeviceWaitForActivation(dAuth))
            {
                Console.WriteLine("Authenticated!");
                return true;
            }
            else
            {
                Console.WriteLine("Failed to authenticate...");
                return false;
            }
        }
    }
}
