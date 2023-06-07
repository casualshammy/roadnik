using Ax.Fw;
using Ax.Fw.DependencyInjection;
using Ax.Fw.Extensions;
using Ax.Fw.SharedTypes.Interfaces;
using Ax.Fw.Storage;
using Ax.Fw.Storage.Extensions;
using CommandLine;
using Grace.DependencyInjection;
using JustLogger;
using JustLogger.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Roadnik.Interfaces;
using Roadnik.Modules.Settings;
using Roadnik.Toolkit;
using System.Net;
using System.Reactive.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace Roadnik;

public class Program
{
  public static void Main(string[] _args)
  {
    var assembly = Assembly.GetExecutingAssembly() ?? throw new Exception("Can't get assembly!");
    var workingDir = Path.GetDirectoryName(assembly.Location) ?? throw new Exception("Can't get working dir!");

    var configFilePath = Parser.Default
      .ParseArguments<Options>(_args)
      .MapResult(_x => _x.ConfigFilePath, _ => null) ?? Path.Combine(workingDir, "../_config.json");

    if (!File.Exists(configFilePath))
    {
      Console.WriteLine($"Config file is not exist! Press any key to close the app...");
      Console.ReadLine();
      return;
    }

    var lifetime = new Lifetime();

    var settingsController = new SettingsController(configFilePath, lifetime);

    var settings = JsonConvert.DeserializeObject<SettingsImpl>(File.ReadAllText(configFilePath, Encoding.UTF8))
      ?? throw new FormatException($"Settings file is corrupted!");

    if (!Directory.Exists(settings.LogDirPath))
      Directory.CreateDirectory(settings.LogDirPath);

    using var logger = new CompositeLogger(new FileLogger(() => Path.Combine(settings.LogDirPath, $"{DateTimeOffset.UtcNow:yyyy-MM-dd}.log"), 5000), new ConsoleLogger());

    lifetime.DoOnEnded(() =>
    {
      logger.Info($"-------------------------------------------");
      logger.Info($"Server stopped");
      logger.Info($"-------------------------------------------");
    });

    lifetime.ToDisposeOnEnding(FileLoggerCleaner.Create(new DirectoryInfo(settings.LogDirPath), false, new Regex(@".+\.log"), TimeSpan.FromDays(30), TimeSpan.FromHours(1)));

    if (!Directory.Exists(settings.DataDirPath))
      Directory.CreateDirectory(settings.DataDirPath);

    var docStorage = new SqliteDocumentStorage(Path.Combine(settings.DataDirPath, "data.v0.db"))
      .WithCache(1000, TimeSpan.FromHours(1));

    lifetime.ToDisposeOnEnding(docStorage);

    Observable
      .Interval(TimeSpan.FromHours(6))
      .StartWithDefault()
      .SelectAsync(async (_, _ct) => await docStorage.FlushAsync(true, _ct))
      .Subscribe(lifetime);

    var depMgr = DependencyManagerBuilder
      .Create(lifetime, assembly)
      .AddSingleton<JustLogger.Interfaces.ILogger>(logger)
      .AddSingleton<ILoggerDisposable>(logger)
      .AddSingleton(docStorage)
      .AddSingleton<ILifetime>(lifetime)
      .AddSingleton<IReadOnlyLifetime>(lifetime)
      .AddSingleton<ISettings>(settings)
      .AddSingleton<ISettingsController>(settingsController)
      .Build();

    var webApp = CreateWebApp(_args, depMgr);

    logger.Info($"-------------------------------------------");
    logger.Info($"Roadnik Server Started");
    logger.Info($"Address: {settings.IpBind}:{settings.PortBind}");
    logger.Info($"OS: {Environment.OSVersion} {(Environment.Is64BitOperatingSystem ? "x64" : "x86")}");
    logger.Info($"Config file: '{configFilePath}'");
    logger.Info($"-------------------------------------------");

    webApp.Run();
    lifetime.End();
  }

  public static WebApplication CreateWebApp(string[] _args, DependencyManager _depMgr)
  {
    var settings = _depMgr.Locate<ISettings>();

    var builder = WebApplication.CreateBuilder(_args);
    builder.Host.ConfigureLogging((_ctx, _logBuilder) => _logBuilder.ClearProviders());
    builder.Host.UseGraceContainer(_depMgr.ServiceProvider);
    builder.Host.ConfigureServices(_ => _.AddResponseCompression(_options => _options.EnableForHttps = true));
    builder.WebHost.ConfigureKestrel(_options =>
    {
      _options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
      _options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(1);
      _options.Listen(IPAddress.Parse(settings.IpBind), settings.PortBind);
    });

    builder.Services.AddControllers(_options => _options.Filters.Add<ApiKeyFilter>());
    builder.Services.AddRazorPages();

    var app = builder.Build();
    app
      .UseRouting()
      .UseWebSockets(new WebSocketOptions()
      {
        KeepAliveInterval = TimeSpan.FromSeconds(300)
      })
      .UseForwardedHeaders(new ForwardedHeadersOptions()
      {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
      })
      .UseResponseCompression()
      .UseEndpoints(_endpoints => _endpoints.MapControllers());

    return app;
  }

}

public class Options
{
  [Option('c', "config", Required = false, HelpText = "Set config file path")]
  public string? ConfigFilePath { get; set; }
}