using Ax.Fw.DependencyInjection;
using Ax.Fw.Extensions;
using Ax.Fw.SharedTypes.Interfaces;
using Ax.Fw.Storage.Interfaces;
using AxToolsServerNet.Data.Serializers;
using Roadnik.Common.ReqRes;
using Roadnik.Interfaces;
using Roadnik.Modules.Controllers;
using Roadnik.Server.Interfaces;
using Roadnik.Server.Modules.WebServer.Middlewares;
using System.Net;
using System.Reactive.Linq;
using ILogger = Ax.Fw.SharedTypes.Interfaces.ILogger;

namespace Roadnik.Server.Modules.WebServer;

public class WebServerImpl : IWebServer, IAppModule<IWebServer>
{
  private readonly ISettingsController p_settingsController;
  private readonly IDocumentStorageAot p_documentStorage;
  private readonly ILogger p_logger;
  private readonly IWebSocketCtrl p_webSocketCtrl;
  private readonly IRoomsController p_roomsController;
  private readonly ITilesCache p_tilesCache;
  private readonly IReqRateLimiter p_reqRateLimiter;
  private readonly IFCMPublisher p_fCMPublisher;

  public static IWebServer ExportInstance(IAppDependencyCtx _ctx)
  {
    return _ctx.CreateInstance((
      ISettingsController _settingsController,
      IDocumentStorageAot _documentStorage,
      ILogger _logger,
      IWebSocketCtrl _webSocketCtrl,
      IRoomsController _roomsController,
      ITilesCache _tilesCache,
      IReqRateLimiter _reqRateLimiter,
      IFCMPublisher _fCMPublisher,
      IReadOnlyLifetime _lifetime) => new WebServerImpl(_settingsController, _documentStorage, _logger, _webSocketCtrl, _roomsController, _tilesCache, _reqRateLimiter, _fCMPublisher, _lifetime));
  }

  private WebServerImpl(
    ISettingsController _settingsController,
    IDocumentStorageAot _documentStorage,
    ILogger _logger,
    IWebSocketCtrl _webSocketCtrl,
    IRoomsController _roomsController,
    ITilesCache _tilesCache,
    IReqRateLimiter _reqRateLimiter,
    IFCMPublisher _fCMPublisher,
    IReadOnlyLifetime _lifetime)
  {
    p_settingsController = _settingsController;
    p_documentStorage = _documentStorage;
    p_logger = _logger;
    p_webSocketCtrl = _webSocketCtrl;
    p_roomsController = _roomsController;
    p_tilesCache = _tilesCache;
    p_reqRateLimiter = _reqRateLimiter;
    p_fCMPublisher = _fCMPublisher;

    _settingsController.Settings
      .DistinctUntilChanged(_ => HashCode.Combine(_?.IpBind, _?.PortBind))
      .HotAlive(_lifetime, (_conf, _life) =>
      {
        if (_conf == null)
          return;

        var thread = new Thread(async () =>
        {
          try
          {
            _logger.Info($"Starting kestrel on {_conf.IpBind}:{_conf.PortBind}...");

            using (var host = CreateWebHost(_conf.IpBind, _conf.PortBind))
              await host.RunAsync(_life.Token);

            _logger.Info($"Kestrel on {_conf.IpBind}:{_conf.PortBind} is stopped");
          }
          catch (Exception ex)
          {
            _logger.Error($"Error in kestrel thread: {ex}");
          }
        });

        thread.IsBackground = true;
        thread.Start();
      });
  }

  private IHost CreateWebHost(string _host, int _port)
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
      _opt.Listen(IPAddress.Parse(_host), _port);
    });

    var app = builder.Build();

    var requestsRequreAuth = new HashSet<string>()
    {
      ReqPaths.REGISTER_ROOM,
      ReqPaths.UNREGISTER_ROOM,
      ReqPaths.LIST_REGISTERED_ROOMS
    };

    app
      .UseMiddleware<ForwardProxyMiddleware>()
      .UseResponseCompression()
      .UseMiddleware<AdminAccessMiddleware>(requestsRequreAuth, p_settingsController)
      .UseWebSockets(new WebSocketOptions()
      {
        KeepAliveInterval = TimeSpan.FromSeconds(30)
      });

    var controller = new ApiControllerV0(
      p_settingsController,
      p_documentStorage,
      p_logger,
      p_webSocketCtrl,
      p_roomsController,
      p_tilesCache,
      p_reqRateLimiter,
      p_fCMPublisher);

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
    app.MapPost(ReqPaths.REGISTER_ROOM, controller.RegisterRoomAsync);
    app.MapPost(ReqPaths.UNREGISTER_ROOM, controller.DeleteRoomRegistrationAsync);
    app.MapGet(ReqPaths.LIST_REGISTERED_ROOMS, controller.ListUsersAsync);

    return app;
  }

}

