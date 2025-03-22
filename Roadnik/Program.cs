using Ax.Fw;
using Ax.Fw.DependencyInjection;
using Ax.Fw.Extensions;
using Ax.Fw.Log;
using Ax.Fw.SharedTypes.Interfaces;
using FluentArgs;
using Roadnik.Interfaces;
using Roadnik.Modules.WebSocketController;
using Roadnik.Server.Data;
using Roadnik.Server.Interfaces;
using Roadnik.Server.Modules.DbProvider;
using Roadnik.Server.Modules.FCMProvider;
using Roadnik.Server.Modules.HttpClientProvider;
using Roadnik.Server.Modules.ReqRateLimiter;
using Roadnik.Server.Modules.RoomsController;
using Roadnik.Server.Modules.Settings;
using Roadnik.Server.Modules.TilesCache;
using Roadnik.Server.Modules.WebServer;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Text.RegularExpressions;

namespace Roadnik.Server;

public partial class Program
{
  public static async Task Main(string[] _args)
  {
    //var assembly = Assembly.GetEntryAssembly() ?? throw new Exception("Can't get assembly!");
    var workingDir = AppContext.BaseDirectory;

    var configFilePath = (string?)null;
    FluentArgsBuilder
      .New()
      .Parameter("-c", "--config").WithDescription("Path to config file").IsOptional()
      .Call(_configPath =>
      {
        configFilePath = _configPath;
      })
      .Parse(_args);

    configFilePath ??= Environment.GetEnvironmentVariable("ROADNIK_CONFIG");
    configFilePath ??= Path.Combine(workingDir, "../_config.json");

    if (!File.Exists(configFilePath))
    {
      Console.WriteLine($"Config file is not exist! Press any key to close the app...");
      Console.ReadLine();
      return;
    }

    var lifetime = new Lifetime();
    using var log = new GenericLog();
    log.AttachConsoleLog();

    var settingsController = new SettingsController(configFilePath, log["settings-ctrl"], lifetime);

    var settings = await settingsController.Settings
      .TakeUntil(DateTimeOffset.UtcNow + TimeSpan.FromSeconds(5), TaskPoolScheduler.Default)
      .WhereNotNull()
      .FirstOrDefaultAsync();

    if (settings == null)
      throw new FormatException($"Settings file is corrupted!");

    if (!Directory.Exists(settings.LogDirPath))
      Directory.CreateDirectory(settings.LogDirPath);

    log.AttachFileLog(() => Path.Combine(settings.LogDirPath, $"{DateTimeOffset.UtcNow:yyyy-MM-dd}.log"), TimeSpan.FromSeconds(5));

    lifetime.ToDisposeOnEnding(FileLoggerCleaner.Create(new DirectoryInfo(settings.LogDirPath), false, GetLogFilesCleanerRegex(), TimeSpan.FromDays(30), true, TimeSpan.FromHours(1)));

    if (!Directory.Exists(settings.DataDirPath))
      Directory.CreateDirectory(settings.DataDirPath);

    var version = Consts.AppVersion;
    log.Info($"\n" +
      $"-------------------------------------------\n" +
      $"**Roadnik Server Started**\n" +
      $"Version: __{version}__\n" +
      $"Address: __{settings.IpBind}:{settings.PortBind}__\n" +
      $"OS: {Environment.OSVersion} {(Environment.Is64BitOperatingSystem ? "x64" : "x86")}\n" +
      $"Config file: __{configFilePath}__\n" +
      $"-------------------------------------------");

    var depMgr = AppDependencyManager
      .Create()
      .AddSingleton<ILog>(log)
      .AddSingleton<IDbProvider>(new DbProviderImpl(lifetime, log["db-provider"], settings))
      .AddSingleton<ILifetime>(lifetime)
      .AddSingleton<IReadOnlyLifetime>(lifetime)
      .AddSingleton<ISettingsController>(settingsController)
      .AddSingleton<IReqRateLimiter>(new ReqRateLimiterImpl())
      .AddModule<FCMPublisherImpl, IFCMPublisher>()
      .AddModule<RoomsControllerImpl, IRoomsController>()
      .AddModule<TilesCacheImpl, ITilesCache>()
      .AddModule<WebSocketCtrlImpl, IWebSocketCtrl>()
      .AddModule<WebServerImpl, IWebServer>()
      .AddModule<HttpClientProviderImpl, IHttpClientProvider>()
      .ActivateOnStart<IWebServer>()
      .Build();

    lifetime.InstallConsoleCtrlCHook();

    try
    {
      await Task.Delay(-1, lifetime.Token);
    }
    catch (OperationCanceledException) { }

    var lifetimeEndDeadline = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(1);
    await lifetime.OnEnd
      .TakeUntil(lifetimeEndDeadline)
      .LastOrDefaultAsync();

    log.Info($"\n" +
      $"-------------------------------------------\n" +
      $"Server stopped\n" +
      $"-------------------------------------------");
  }

  [GeneratedRegex(@".+\.log")]
  private static partial Regex GetLogFilesCleanerRegex();

}
