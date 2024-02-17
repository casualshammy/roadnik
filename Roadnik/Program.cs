using Ax.Fw;
using Ax.Fw.DependencyInjection;
using Ax.Fw.Extensions;
using Ax.Fw.Log;
using Ax.Fw.SharedTypes.Interfaces;
using Ax.Fw.Storage;
using Ax.Fw.Storage.Interfaces;
using AxToolsServerNet.Data.Serializers;
using FluentArgs;
using Roadnik.Common.ReqRes;
using Roadnik.Interfaces;
using Roadnik.Modules.Controllers;
using Roadnik.Modules.ReqRateLimiter;
using Roadnik.Modules.RoomsController;
using Roadnik.Modules.Settings;
using Roadnik.Modules.TilesCache;
using Roadnik.Modules.WebSocketController;
using Roadnik.Server.Data.Settings;
using Roadnik.Server.Interfaces;
using Roadnik.Server.Modules.FCMProvider;
using Roadnik.Server.Modules.WebServer.Middlewares;
using System.Net;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using ILogger = Ax.Fw.SharedTypes.Interfaces.ILogger;

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

    var lifetime = new Lifetime();

    var settingsController = new SettingsController(configFilePath, lifetime);

    var settings = await settingsController.Settings
      .TakeUntil(DateTimeOffset.UtcNow + TimeSpan.FromSeconds(5), TaskPoolScheduler.Default)
      .WhereNotNull()
      .FirstOrDefaultAsync();

    if (settings == null)
      throw new FormatException($"Settings file is corrupted!");

    if (!Directory.Exists(settings.LogDirPath))
      Directory.CreateDirectory(settings.LogDirPath);

    using var logger = new CompositeLogger(
      new FileLogger(() => Path.Combine(settings.LogDirPath, $"{DateTimeOffset.UtcNow:yyyy-MM-dd}.log"), TimeSpan.FromSeconds(5)),
      new ConsoleLogger());

    lifetime.ToDisposeOnEnding(FileLoggerCleaner.Create(new DirectoryInfo(settings.LogDirPath), false, GetLogFilesCleanerRegex(), TimeSpan.FromDays(30), TimeSpan.FromHours(1)));

    if (!Directory.Exists(settings.DataDirPath))
      Directory.CreateDirectory(settings.DataDirPath);

    var docStorage = lifetime.ToDisposeOnEnding(new SqliteDocumentStorageAot(Path.Combine(settings.DataDirPath, "data.v0.db")));

    Observable
      .Interval(TimeSpan.FromHours(6))
      .StartWithDefault()
      .SelectAsync(async (_, _ct) => await docStorage.FlushAsync(true, _ct))
      .Subscribe(lifetime);

    var depMgr = AppDependencyManager
      .Create()
      .AddSingleton<ILogger>(logger)
      .AddSingleton<IDocumentStorageAot>(docStorage)
      .AddSingleton<ILifetime>(lifetime)
      .AddSingleton<IReadOnlyLifetime>(lifetime)
      .AddSingleton<ISettingsController>(settingsController)
      .AddSingleton<IReqRateLimiter>(new ReqRateLimiterImpl())
      .AddModule<FCMPublisherImpl, IFCMPublisher>()
      .AddModule<RoomsControllerImpl, IRoomsController>()
      .AddModule<TilesCacheImpl, ITilesCache>()
      .AddModule<WebSocketCtrlImpl, IWebSocketCtrl>()
      .Build();

    var webApp = CreateWebHost(depMgr, settings);

    var version = new SerializableVersion(Assembly.GetExecutingAssembly()?.GetName()?.Version ?? new Version(0, 0, 0, 0));
    logger.Info($"-------------------------------------------");
    logger.Info($"Roadnik Server Started");
    logger.Info($"Version: {version}");
    logger.Info($"Address: {settings.IpBind}:{settings.PortBind}");
    logger.Info($"OS: {Environment.OSVersion} {(Environment.Is64BitOperatingSystem ? "x64" : "x86")}");
    logger.Info($"Config file: '{configFilePath}'");
    logger.Info($"-------------------------------------------");

    lifetime.InstallConsoleCtrlCHook();

    webApp.Run();

    logger.Info($"-------------------------------------------");
    logger.Info($"Server stopped");
    logger.Info($"-------------------------------------------");
  }

  private static IHost CreateWebHost(IReadOnlyDependencyContainer _depContainer, AppSettings _appSettings)
  {
    var builder = WebApplication.CreateSlimBuilder();

    builder.Logging.ClearProviders();

    builder.Services.ConfigureHttpJsonOptions(_opt =>
    {
      _opt.SerializerOptions.TypeInfoResolverChain.Insert(0, ControllersJsonCtx.Default);
    });

    builder.Services.AddResponseCompression(_options => _options.EnableForHttps = true);
    builder.WebHost.ConfigureKestrel(_opt =>
    {
      _opt.Limits.KeepAliveTimeout = TimeSpan.FromSeconds(130);
      _opt.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(90);
      _opt.Listen(IPAddress.Parse(_appSettings.IpBind), _appSettings.PortBind);
    });

    var app = builder.Build();

    var reqAllowedWithoutAuth = new HashSet<string>()
    {
      "register-room",
      "unregister-room",
      "list-registered-rooms"
    };

    app
      //.UseHttpsRedirection()
      //.UseRouting()
      .Use(ForwardProxyHeadersMiddlewareAsync)
      .UseResponseCompression()
      .UseMiddleware<AdminAccessMiddleware>(reqAllowedWithoutAuth, _depContainer.Locate<ISettingsController>())
      .UseWebSockets(new WebSocketOptions()
      {
        KeepAliveInterval = TimeSpan.FromSeconds(30)
      });

    var controller = new ApiControllerV0(
      _depContainer.Locate<ISettingsController>(),
      _depContainer.Locate<IDocumentStorageAot>(),
      _depContainer.Locate<ILogger>(),
      _depContainer.Locate<IWebSocketCtrl>(),
      _depContainer.Locate<IRoomsController>(),
      _depContainer.Locate<ITilesCache>(),
      _depContainer.Locate<IReqRateLimiter>(),
      _depContainer.Locate<IFCMPublisher>());

    app.MapMethods("/r/", ["HEAD"], () => Results.Ok());
    app.MapGet("/", controller.GetIndexFile);
    app.MapGet("{**path}", controller.GetStaticFile);
    app.MapGet("/ping", () => Results.Ok());
    app.MapGet("/r/{**path}", controller.GetRoom);
    app.MapGet("/thunderforest", controller.GetThunderforestImageAsync);
    app.MapGet(ReqPaths.STORE_PATH_POINT, controller.StoreRoomPointGetAsync);
    app.MapPost(ReqPaths.STORE_PATH_POINT, controller.StoreRoomPointPostAsync);
    app.MapGet(ReqPaths.GET_ROOM_PATHS, controller.GetRoomPathsAsync);
    app.MapPost(ReqPaths.START_NEW_PATH, controller.StartNewPathAsync);
    app.MapPost(ReqPaths.CREATE_NEW_POINT, controller.CreateNewPointAsync);
    app.MapGet(ReqPaths.LIST_ROOM_POINTS, controller.GetRoomPointsAsync);
    app.MapPost(ReqPaths.DELETE_ROOM_POINT, controller.DeleteRoomPointAsync);
    app.MapGet(ReqPaths.GET_FREE_ROOM_ID, controller.GetFreeRoomIdAsync);
    app.MapPost(ReqPaths.UPLOAD_LOG, controller.UploadLogAsync);
    app.MapGet(ReqPaths.IS_ROOM_ID_VALID, controller.IsRoomIdValid);
    app.MapGet("/ws", controller.StartWebSocketAsync);
    app.MapPost("/register-room", controller.RegisterRoomAsync);
    app.MapPost("/unregister-room", controller.DeleteRoomRegistrationAsync);
    app.MapGet("/list-registered-rooms", controller.ListUsersAsync);

    return app;
  }

  private static Task ForwardProxyHeadersMiddlewareAsync(HttpContext _ctx, RequestDelegate _next)
  {
    if (_ctx.Request.Headers.TryGetValue("CF-Connecting-IP", out var cfConnectingIp))
    {
      var headerValue = cfConnectingIp.ToString();
      if (!headerValue.IsNullOrWhiteSpace() && IPAddress.TryParse(headerValue, out var ip))
      {
        _ctx.Connection.RemoteIpAddress = ip;
        return _next(_ctx);
      }
    }

    if (_ctx.Request.Headers.TryGetValue("X-Forwarded-For", out var xForwardedFor))
    {
      var headerValue = xForwardedFor.ToString();
      if (!headerValue.IsNullOrWhiteSpace())
      {
        var split = headerValue.Split(',', StringSplitOptions.TrimEntries);
        if (split.Length > 0 && IPAddress.TryParse(split[0], out var ip))
        {
          _ctx.Connection.RemoteIpAddress = ip;
          return _next(_ctx);
        }
      }
    }

    return _next(_ctx);
  }

  [GeneratedRegex(@".+\.log")]
  private static partial Regex GetLogFilesCleanerRegex();

}
