using Ax.Fw;
using Ax.Fw.DependencyInjection;
using Ax.Fw.Log;
using Ax.Fw.SharedTypes.Interfaces;
using Roadnik.Interfaces;
using Roadnik.Server.Data;
using Roadnik.Server.Data.Settings;
using Roadnik.Server.Interfaces;
using Roadnik.Server.Modules.DbProvider;
using Roadnik.Server.Modules.FCMProvider;
using Roadnik.Server.Modules.HttpClientProvider;
using Roadnik.Server.Modules.ReqRateLimiter;
using Roadnik.Server.Modules.RoomsController;
using Roadnik.Server.Modules.StravaCredentialsController;
using Roadnik.Server.Modules.TilesCache;
using Roadnik.Server.Modules.WebServer;
using Roadnik.Server.Modules.WebSocketController;
using System.Reactive.Linq;
using System.Text.RegularExpressions;

namespace Roadnik.Server;

public partial class Program
{
  public static async Task Main(string[] _args)
  {
    if (!AppConfig.TryCreateAppConfig(out var appConfig))
    {
      Console.WriteLine($"Can't create app config!");
      return;
    }

    var lifetime = new Lifetime();
    using var log = new GenericLog();
    log.AttachConsoleLog();

    if (!Directory.Exists(appConfig.LogDirPath))
      Directory.CreateDirectory(appConfig.LogDirPath);
    if (!Directory.Exists(appConfig.DataDirPath))
      Directory.CreateDirectory(appConfig.DataDirPath);

    log.AttachFileLog(() => Path.Combine(appConfig.LogDirPath, $"{DateTimeOffset.UtcNow:yyyy-MM-dd}.log"), TimeSpan.FromSeconds(5));
    lifetime.ToDisposeOnEnding(FileLoggerCleaner.Create(new DirectoryInfo(appConfig.LogDirPath), false, GetLogFilesCleanerRegex(), TimeSpan.FromDays(30), true, TimeSpan.FromHours(1)));

    var version = Consts.AppVersion;
    log.Info($"\n" +
      $"-------------------------------------------\n" +
      $"**Roadnik Server Started**\n" +
      $"Version: __{version}__\n" +
      $"Address: __{appConfig.BindIp}:{appConfig.BindPort}__\n" +
      $"OS: {Environment.OSVersion} {(Environment.Is64BitOperatingSystem ? "x64" : "x86")}\n" +
      $"-------------------------------------------");

    var depMgr = AppDependencyManager
      .Create()
      .AddSingleton<ILog>(log)
      .AddSingleton<IDbProvider>(new DbProviderImpl(lifetime, log["db-provider"], appConfig))
      .AddSingleton<ILifetime>(lifetime)
      .AddSingleton<IReadOnlyLifetime>(lifetime)
      .AddSingleton<IAppConfig>(appConfig)
      .AddSingleton<IReqRateLimiter>(new ReqRateLimiterImpl())
      .AddModule<FCMPublisherImpl, IFCMPublisher>()
      .AddModule<RoomsControllerImpl, IRoomsController>()
      .AddModule<TilesCacheImpl, ITilesCache>()
      .AddModule<WebSocketCtrlImpl, IWebSocketCtrl>()
      .AddModule<WebServerImpl, IWebServer>()
      .AddModule<HttpClientProviderImpl, IHttpClientProvider>()
      .AddModule<StravaCredentialsControllerImpl, IStravaCredentialsController>()
      .ActivateOnStart<IWebServer>()
      .ActivateOnStart<IStravaCredentialsController>()
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
