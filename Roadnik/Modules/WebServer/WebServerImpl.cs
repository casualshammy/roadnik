﻿using Ax.Fw.DependencyInjection;
using Ax.Fw.Extensions;
using Ax.Fw.SharedTypes.Interfaces;
using Roadnik.Interfaces;
using Roadnik.Server.Interfaces;
using Roadnik.Server.JsonCtx;
using Roadnik.Server.Modules.WebServer.Controllers;
using Roadnik.Server.Modules.WebServer.Middlewares;
using Roadnik.Server.Toolkit;
using System.Net;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using ILog = Ax.Fw.SharedTypes.Interfaces.ILog;

namespace Roadnik.Server.Modules.WebServer;

public class WebServerImpl : IWebServer, IAppModule<IWebServer>
{
  public static IWebServer ExportInstance(IAppDependencyCtx _ctx)
  {
    return _ctx.CreateInstance((
      ISettingsController _settingsController,
      IDbProvider _documentStorage,
      ILog _logger,
      IWebSocketCtrl _webSocketCtrl,
      IRoomsController _roomsController,
      ITilesCache _tilesCache,
      IReqRateLimiter _reqRateLimiter,
      IFCMPublisher _fCMPublisher,
      IReadOnlyLifetime _lifetime,
      IHttpClientProvider _httpClientProvider) => new WebServerImpl(
        _settingsController, 
        _documentStorage, 
        _logger["kestrel"], 
        _webSocketCtrl, 
        _roomsController, 
        _tilesCache, 
        _reqRateLimiter, 
        _fCMPublisher, 
        _lifetime, 
        _httpClientProvider));
  }

  private readonly ISettingsController p_settingsController;
  private readonly IDbProvider p_documentStorage;
  private readonly ILog p_logger;
  private readonly IWebSocketCtrl p_webSocketCtrl;
  private readonly IRoomsController p_roomsController;
  private readonly ITilesCache p_tilesCache;
  private readonly IReqRateLimiter p_reqRateLimiter;
  private readonly IFCMPublisher p_fCMPublisher;
  private readonly IHttpClientProvider p_httpClientProvider;

  private WebServerImpl(
    ISettingsController _settingsController,
    IDbProvider _documentStorage,
    ILog _logger,
    IWebSocketCtrl _webSocketCtrl,
    IRoomsController _roomsController,
    ITilesCache _tilesCache,
    IReqRateLimiter _reqRateLimiter,
    IFCMPublisher _fCMPublisher,
    IReadOnlyLifetime _lifetime,
    IHttpClientProvider _httpClientProvider)
  {
    p_settingsController = _settingsController;
    p_documentStorage = _documentStorage;
    p_logger = _logger;
    p_webSocketCtrl = _webSocketCtrl;
    p_roomsController = _roomsController;
    p_tilesCache = _tilesCache;
    p_reqRateLimiter = _reqRateLimiter;
    p_fCMPublisher = _fCMPublisher;
    p_httpClientProvider = _httpClientProvider;

    var confScheduler = new EventLoopScheduler();

    _settingsController.Settings
      .DistinctUntilChanged(_ => HashCode.Combine(_?.IpBind, _?.PortBind))
      .HotAlive(_lifetime, confScheduler, (_conf, _life) =>
      {
        if (_conf == null)
          return;

        var thread = new Thread(async () =>
        {
          try
          {
            _logger.Info($"Starting server on {_conf.IpBind}:{_conf.PortBind}...");

            using (var host = CreateWebHost(_conf.IpBind, _conf.PortBind))
              await host.RunAsync(_life.Token);

            _logger.Info($"Server on {_conf.IpBind}:{_conf.PortBind} is stopped");
          }
          catch (Exception ex)
          {
            _logger.Error($"Error in thread: {ex}");
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

    builder.Services.AddSingleton<ILog>(p_logger);

    var controller = new ApiControllerV0(
     p_settingsController,
     p_documentStorage,
     p_logger,
     p_webSocketCtrl,
     p_roomsController,
     p_tilesCache,
     p_reqRateLimiter,
     p_fCMPublisher,
     p_httpClientProvider);

    var app = builder.Build();
    app
      .UseMiddleware<LogMiddleware>()
      .UseMiddleware<CorsMiddleware>(p_logger)
      .UseMiddleware<ForwardProxyMiddleware>()
      .UseMiddleware<AuthMiddleware>(p_logger, new GenericController[] { controller })
      .UseResponseCompression()
      .UseWebSockets(new WebSocketOptions()
      {
        KeepAliveInterval = TimeSpan.FromSeconds(30)
      });

    controller.RegisterPaths(app);

    return app;
  }

}

