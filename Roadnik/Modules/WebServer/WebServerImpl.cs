using Ax.Fw.App.Interfaces;
using Ax.Fw.DependencyInjection;
using Ax.Fw.SharedTypes.Interfaces;
using Ax.Fw.Web.Extensions;
using Ax.Fw.Web.Middlewares;
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
      IWebSocketCtrl _webSocketCtrl,
      IRoomsController _roomsController,
      IReqRateLimiter _reqRateLimiter,
      IFCMPublisher _fCMPublisher,
      IReadOnlyLifetime _lifetime,
      IHttpClientProvider _httpClientProvider) => new WebServerImpl(
        _appConfig,
        _documentStorage,
        _logger["kestrel"],
        _webSocketCtrl,
        _roomsController,
        _reqRateLimiter,
        _fCMPublisher,
        _lifetime,
        _httpClientProvider));
  }

  private readonly IDbProvider p_documentStorage;
  private readonly ILog p_logger;
  private readonly IWebSocketCtrl p_webSocketCtrl;
  private readonly IRoomsController p_roomsController;
  private readonly IReqRateLimiter p_reqRateLimiter;
  private readonly IFCMPublisher p_fCMPublisher;
  private readonly IHttpClientProvider p_httpClientProvider;

  private WebServerImpl(
    IAppConfig _appConfig,
    IDbProvider _documentStorage,
    ILog _log,
    IWebSocketCtrl _webSocketCtrl,
    IRoomsController _roomsController,
    IReqRateLimiter _reqRateLimiter,
    IFCMPublisher _fCMPublisher,
    IReadOnlyLifetime _lifetime,
    IHttpClientProvider _httpClientProvider)
  {
    p_documentStorage = _documentStorage;
    p_logger = _log;
    p_webSocketCtrl = _webSocketCtrl;
    p_roomsController = _roomsController;
    p_reqRateLimiter = _reqRateLimiter;
    p_fCMPublisher = _fCMPublisher;
    p_httpClientProvider = _httpClientProvider;

    var thread = new Thread(async () =>
    {
      try
      {
        _log.Info($"**Starting** server on __{_appConfig.BindIp}:{_appConfig.BindPort}__...");

        var life = _lifetime.GetChildLifetime();
        if (life == null)
          throw new InvalidOperationException("Failed to create child lifetime");

        using (var host = CreateWebHost(_appConfig, life))
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
    IAppConfig _config,
    IReadOnlyLifetime _life)
  {
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
      _opt.Listen(_config.BindIp, _config.BindPort);
    });

    builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
    builder.Services.AddSingleton(p_logger);
    builder.Services.AddSingleton(p_documentStorage);
    builder.Services.AddSingleton(p_fCMPublisher);
    builder.Services.AddSingleton(_life);
    builder.Services.AddSingleton(_config);
    builder.Services.AddCustomProblemDetails();
    builder.Services.AddCustomRequestId();
    builder.Services.AddCustomRequestLog();
    builder.Services.AddRequestToolkit(RestJsonCtx.Default);
    builder.Services.AddCorsMiddleware(
      new HashSet<string>(["http://localhost:5173", "https://webapp.local", "http://webapp.local:5544"]),
      new HashSet<string>(["GET", "POST", "OPTIONS", "HEAD"]),
      new HashSet<string>(["User-Agent", "X-Requested-With", "If-Modified-Since", "Cache-Control", "Content-Type", "Range"]),
      false);
    builder.Services.AddSingleton<FailToBanMiddleware>();
    builder.Services.AddScoped<LogMiddleware>();
    builder.Services.AddScoped<CommonErrorsHandlerMiddleware>();

    var app = builder.Build();
    app
      .UseMiddleware<LogMiddleware>()
      .UseMiddleware<CorsMiddleware>()
      .UseMiddleware<ForwardProxyMiddleware>()
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
      p_webSocketCtrl,
      p_roomsController,
      p_reqRateLimiter,
      p_httpClientProvider);
    apiCtrlV1.RegisterPaths(app);

    return app;
  }

}

