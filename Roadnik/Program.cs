using Ax.Fw;
using Ax.Fw.App;
using Ax.Fw.DependencyInjection;
using Ax.Fw.Extensions;
using Ax.Fw.Log;
using Ax.Fw.SharedTypes.Interfaces;
using Ax.Fw.Storage;
using Ax.Fw.Storage.Data;
using Ax.Fw.Storage.Interfaces;
using FluentArgs;
using Roadnik.Interfaces;
using Roadnik.Modules.ReqRateLimiter;
using Roadnik.Modules.RoomsController;
using Roadnik.Modules.Settings;
using Roadnik.Modules.TilesCache;
using Roadnik.Modules.WebSocketController;
using Roadnik.Server.Data.Settings;
using Roadnik.Server.Interfaces;
using Roadnik.Server.JsonCtx;
using Roadnik.Server.Modules.DbProvider;
using Roadnik.Server.Modules.FCMProvider;
using Roadnik.Server.Modules.HttpClientProvider;
using Roadnik.Server.Modules.UdpServer;
using Roadnik.Server.Modules.WebServer;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using ILog = Ax.Fw.SharedTypes.Interfaces.ILog;

namespace Roadnik;

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

    //var fallbackLogFilePath = Path.Combine(Path.GetTempPath(), $"roadnik-{DateTimeOffset.UtcNow:yyyy-MM-dd}.log");

    //var app = AppBase.Create()
    //  .UseConsoleLog()
    //  .UseConfigFile<RawAppSettings>(configFilePath, SettingsJsonCtx.Default)
    //  .UseFileLogFromConf<RawAppSettings>(_conf =>
    //  {
    //    if (_conf == null || _conf.LogDirPath.IsNullOrWhiteSpace())
    //      return fallbackLogFilePath;

    //    return Path.Combine(_conf.LogDirPath, $"{DateTimeOffset.UtcNow:yyyy-MM-dd}.log");
    //  })
    //  .UseFileLogRotateFromConf< RawAppSettings >(_conf =>
    //  {
    //    if (_conf == null || _conf.LogDirPath.IsNullOrWhiteSpace())
    //      return fallbackLogFilePath;
    //  }, false, )

    var lifetime = new Lifetime();
    using var log = new GenericLog(null);
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

    var dbProvider = new DbProviderImpl(lifetime, log["db-provider"], settings);

    var depMgr = AppDependencyManager
      .Create()
      .AddSingleton<ILog>(log)
      .AddSingleton<IDbProvider>(dbProvider)
      .AddSingleton<ILifetime>(lifetime)
      .AddSingleton<IReadOnlyLifetime>(lifetime)
      .AddSingleton<ISettingsController>(settingsController)
      .AddSingleton<IReqRateLimiter>(new ReqRateLimiterImpl())
      .AddModule<FCMPublisherImpl, IFCMPublisher>()
      .AddModule<RoomsControllerImpl, IRoomsController>()
      .AddModule<TilesCacheImpl, ITilesCache>()
      .AddModule<WebSocketCtrlImpl, IWebSocketCtrl>()
      .AddModule<WebServerImpl, IWebServer>()
      .AddModule<UdpServerImpl, IUdpServer>()
      .AddModule<HttpClientProviderImpl, IHttpClientProvider>()
      .ActivateOnStart<IWebServer>()
      .ActivateOnStart<IUdpServer>()
      .Build();

    var version = new SerializableVersion(Assembly.GetExecutingAssembly()?.GetName()?.Version ?? new Version(0, 0, 0, 0));
    log.Info($"\n" +
      $"-------------------------------------------\n" +
      $"**Roadnik Server Started**\n" +
      $"Version: __{version}__\n" +
      $"Address: __{settings.IpBind}:{settings.PortBind}__\n" +
      $"OS: {Environment.OSVersion} {(Environment.Is64BitOperatingSystem ? "x64" : "x86")}\n" +
      $"Config file: __{configFilePath}__\n" +
      $"-------------------------------------------");

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

  [GeneratedRegex(@"roadnik\-.+\.log")]
  private static partial Regex GetLogFilesFallbackCleanerRegex();

}
