using Ax.Fw.App.Interfaces;
using Ax.Fw.DependencyInjection;
using Ax.Fw.SharedTypes.Interfaces;
using Ax.Fw.Web.Extensions;
using Ax.Fw.Web.Middlewares;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Roadnik.Common.JsonCtx;
using Roadnik.Interfaces;
using Roadnik.Server.Interfaces;
using Roadnik.Server.JsonCtx;
using Roadnik.Server.Modules.WebServer.Controllers;
using Roadnik.Server.Modules.WebServer.Middlewares;
using System.Reactive.Linq;

namespace Roadnik.Server.Modules.WebServer;

public class WebServerImpl : IWebServer, IAppModule<IWebServer>
{
  public static IWebServer ExportInstance(IAppDependencyCtx _ctx)
  {
    return _ctx.CreateInstance((
      IAppConfig _appConfig,
      IDbProvider _documentStorage,
      ILog _logger,
      IRoomsController _roomsController,
      IReqRateLimiter _reqRateLimiter,
      IFCMPublisher _fCMPublisher,
      IReadOnlyLifetime _lifetime,
      IHttpClientProvider _httpClientProvider,
      IStravaTilesProvider _stravaTilesProvider) => new WebServerImpl(
        _ctx,
        _appConfig,
        _documentStorage,
        _logger["kestrel"],
        _roomsController,
        _reqRateLimiter,
        _fCMPublisher,
        _lifetime,
        _httpClientProvider,
        _stravaTilesProvider));
  }

  private readonly IDbProvider p_documentStorage;
  private readonly ILog p_logger;
  private readonly IRoomsController p_roomsController;
  private readonly IReqRateLimiter p_reqRateLimiter;
  private readonly IFCMPublisher p_fCMPublisher;
  private readonly IHttpClientProvider p_httpClientProvider;
  private readonly IStravaTilesProvider p_stravaTilesProvider;

  private WebServerImpl(
    IAppDependencyCtx _appCtx,
    IAppConfig _appConfig,
    IDbProvider _documentStorage,
    ILog _log,
    IRoomsController _roomsController,
    IReqRateLimiter _reqRateLimiter,
    IFCMPublisher _fCMPublisher,
    IReadOnlyLifetime _lifetime,
    IHttpClientProvider _httpClientProvider,
    IStravaTilesProvider _stravaTilesProvider)
  {
    p_documentStorage = _documentStorage;
    p_logger = _log;
    p_roomsController = _roomsController;
    p_reqRateLimiter = _reqRateLimiter;
    p_fCMPublisher = _fCMPublisher;
    p_httpClientProvider = _httpClientProvider;
    p_stravaTilesProvider = _stravaTilesProvider;

    var thread = new Thread(async () =>
    {
      try
      {
        _log.Info($"**Starting** server on __{_appConfig.BindIp}:{_appConfig.BindPort}__...");

        var life = _lifetime.GetChildLifetime();
        if (life == null)
          throw new InvalidOperationException("Failed to create child lifetime");

        using (var host = CreateWebHost(_appCtx, _appConfig, life))
        {
          _log.Info($"__Host__ **created**, **starting**...");
          await host.RunAsync(_lifetime.Token);
        }

        _log.Info($"**Server** on __{_appConfig.BindIp}:{_appConfig.BindPort}__ is **stopped**");
      }
      catch (Exception ex)
      {
        _log.Error($"Error in thread: {ex}");
      }
    });

    thread.IsBackground = true;
    thread.Start();
  }

  private IHost CreateWebHost(
    IAppDependencyCtx _appCtx,
    IAppConfig _config,
    IReadOnlyLifetime _life)
  {
    var wsCtrl = _appCtx.Locate<IWebSocketCtrl>();
    var sseCtrl = _appCtx.Locate<ISseServerCtrl>();

    var builder = WebApplication.CreateSlimBuilder();

    builder.Logging.ClearProviders();

    builder.Services.ConfigureHttpJsonOptions(_opt =>
    {
      _opt.SerializerOptions.TypeInfoResolverChain.Insert(0, RestJsonCtx.Default);
      _opt.SerializerOptions.TypeInfoResolverChain.Insert(1, AdditionalRestJsonCtx.Default);
    });

    builder.Services.AddResponseCompression(_options => _options.EnableForHttps = true);
    builder.WebHost.ConfigureKestrel(_opt =>
    {
      _opt.Limits.KeepAliveTimeout = TimeSpan.FromSeconds(130);
      _opt.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(90);
      _opt.ConfigureEndpointDefaults(_listenOptions =>
      {
        _listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
      });
      _opt.Listen(_config.BindIp, _config.BindPort);
    });

    builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
    builder.Services.AddSingleton(p_logger);
    builder.Services.AddSingleton(p_documentStorage);
    builder.Services.AddSingleton(p_fCMPublisher);
    builder.Services.AddSingleton(_life);
    builder.Services.AddSingleton(_config);
    builder.Services.AddSingleton(p_stravaTilesProvider);
    builder.Services.AddSingleton(sseCtrl);
    builder.Services.AddCustomProblemDetails();
    builder.Services.AddCustomRequestId();
    builder.Services.AddCustomRequestLog(true);
    builder.Services.AddRequestToolkit(RestJsonCtx.Default);
    builder.Services.AddCorsMiddleware(
      new HashSet<string>(["http://localhost:5173", "https://webapp.local", "http://webapp.local:5544"]),
      new HashSet<string>(["GET", "POST"]),
      new HashSet<string>([]),
      false);
    builder.Services.AddSingleton<ForwardProxyMiddleware>();
    builder.Services.AddSingleton<FailToBanMiddleware>();
    builder.Services.AddScoped<LogMiddleware>();
    builder.Services.AddScoped<CommonErrorsHandlerMiddleware>();

    var app = builder.Build();
    app
      .UseMiddleware<ForwardProxyMiddleware>()
      .UseMiddleware<LogMiddleware>()
      .UseMiddleware<CorsMiddleware>()
      .UseMiddleware<FailToBanMiddleware>()
      .UseMiddleware<ApiTokenAuthMiddleware>(p_logger)
      .UseMiddleware<CommonErrorsHandlerMiddleware>()
      .UseResponseCompression()
      .UseWebSockets(new WebSocketOptions()
      {
        KeepAliveInterval = TimeSpan.FromSeconds(30)
      });

    var webCtrl = new WebController(_config);
    webCtrl.RegisterPaths(app);

    var apiCtrlV1 = new ApiControllerV1(
      _config,
      wsCtrl,
      p_roomsController,
      p_reqRateLimiter,
      p_httpClientProvider);
    apiCtrlV1.RegisterPaths(app);

    return app;
  }

}

