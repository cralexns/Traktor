﻿using Microsoft.Extensions.Configuration;
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
        private static Mutex mutex = null;
        static void Main(string[] args)
        {
            mutex = new Mutex(true, nameof(Traktor), out bool createdNew);
            if (!createdNew)
            {
                Console.WriteLine("Traktor is already running. Exiting..");
                return;
            }

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

                    Log.Information($"Scheduling update every {Interval} ..");
                    // Schedule update.
                    using (var timer = new Timer((t) =>
                    {
                        if (config.GetValue<bool>("disable-update"))
                            return;

                        UpdateCurator(curator);
                        if (logLevelSwitch.MinimumLevel == Serilog.Events.LogEventLevel.Debug)
                        {
                            PrintDownloads(curator);
                        }
                    }, null, TimeSpan.FromSeconds(10), Interval))
                    {
                        while (HandleInput(curator))
                        {
                            // Keep alive waiting for input.
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogException(ex);
                throw;
            }
        }

        private static void LogException(Exception ex)
        {
            Log.Fatal(ex, $"Unhandled Exception: {ex.Message}");
        }

        private static void UpdateCurator(Curator curator)
        {
            var update = curator.Update();
            if (update == Curator.CuratorResult.Updated)
                Log.Debug($"Curator => {update}");
            else Log.Information($"Curator => {update}");

            if (update == Curator.CuratorResult.Error)
            {
                Environment.Exit(1);
            }
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
                (deliveryResult, medias) =>
                {
                    Log.Information($"[Delivery] {deliveryResult.Files?.Count() ?? 0} files moved to folder: '{deliveryResult.FolderName}' = {deliveryResult.Status}");
                    if (deliveryResult.Status == FileService.DeliveryResult.DeliveryStatus.Error)
                        Log.Error($" .. {deliveryResult.Error}");

                }))
            {
                case Curator.CuratorResult.Started:
                    Log.Debug("Traktor started.");
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