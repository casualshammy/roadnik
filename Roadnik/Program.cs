using Ax.Fw.App;
using Ax.Fw.App.Data;
using Ax.Fw.App.Extensions;
using Ax.Fw.SharedTypes.Interfaces;
using Roadnik.Interfaces;
using Roadnik.Server.Data;
using Roadnik.Server.Data.Settings;
using Roadnik.Server.Interfaces;
using Roadnik.Server.Modules.DbProvider;
using Roadnik.Server.Modules.FCMProvider;
using Roadnik.Server.Modules.ReqRateLimiter;
using Roadnik.Server.Modules.RoomsController;
using Roadnik.Server.Modules.StravaTilesProvider;
using Roadnik.Server.Modules.WebServer;
using Roadnik.Server.Modules.WebSocketController;
using Roadnik.Server.Modules.WsMsgController;
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

    if (!Directory.Exists(appConfig.LogDirPath))
      Directory.CreateDirectory(appConfig.LogDirPath);
    if (!Directory.Exists(appConfig.DataDirPath))
      Directory.CreateDirectory(appConfig.DataDirPath);

    var app = AppBase.Create()
      .UseConsoleLog()
      .UseFileLog(() => Path.Combine(appConfig.LogDirPath, $"{DateTimeOffset.UtcNow:yyyy-MM-dd}.log"))
      .UseFileLogRotate(new FileLogRotateDescription(new DirectoryInfo(appConfig.LogDirPath), false, GetLogFilesCleanerRegex(), TimeSpan.FromDays(30), true))
      .UseHttpClient(new Dictionary<string, string> {
        { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/144.0.0.0 Safari/537.36" },
      })
      .AddSingleton<IAppConfig>(appConfig)
      .AddSingleton<IDbProvider>(_ctx => new DbProviderImpl(_ctx.Locate<IReadOnlyLifetime>(), _ctx.Locate<ILog>()["db-provider"], appConfig))
      .AddSingleton<IReqRateLimiter>(new ReqRateLimiterImpl())
      .AddModule<FCMPublisherImpl, IFCMPublisher>()
      .AddModule<RoomsControllerImpl, IRoomsController>()
      .AddModule<WebSocketCtrlImpl, IWebSocketCtrl>()
      .AddModule<WebServerImpl, IWebServer>()
      .AddModule<WsMsgControllerImpl, IWsMsgController>()
      .AddModule<StravaTilesProviderImpl, IStravaTilesProvider>()
      .ActivateOnStart((ILog _log, IReadOnlyLifetime _lifetime) =>
      {
        var version = Consts.AppVersion;
        _log.Info($"\n" +
          $"-------------------------------------------\n" +
          $"**Roadnik Server Started**\n" +
          $"Version: __{version}__\n" +
          $"Address: __{appConfig.BindIp}:{appConfig.BindPort}__\n" +
          $"OS: {Environment.OSVersion} {(Environment.Is64BitOperatingSystem ? "x64" : "x86")}\n" +
          $"-------------------------------------------");

        _lifetime.DoOnEnded(() =>
        {
          _log.Info($"\n" +
            $"-------------------------------------------\n" +
            $"Roadnik Server stopped\n" +
            $"-------------------------------------------");
        });
      })
      .ActivateOnStart<IWebServer>()
      .ActivateOnStart<IWsMsgController>()
      .ActivateOnStart<IStravaTilesProvider>();

    await app.RunWaitAsync();
  }

  [GeneratedRegex(@".+\.log")]
  private static partial Regex GetLogFilesCleanerRegex();

}
