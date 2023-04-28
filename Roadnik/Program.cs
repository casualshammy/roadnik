using Ax.Fw;
using Ax.Fw.Extensions;
using Ax.Fw.SharedTypes.Interfaces;
using Ax.Fw.Storage;
using Ax.Fw.Storage.Extensions;
using Ax.Fw.Storage.Interfaces;
using CommandLine;
using Grace.DependencyInjection;
using JustLogger;
using JustLogger.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Roadnik.Interfaces;
using Roadnik.Modules.Settings;
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
    var assembly = Assembly.GetEntryAssembly() ?? throw new Exception("Can't get assembly!");
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

    var classAggregator = new ExportClassMgr(lifetime, GetServices(lifetime, configFilePath));
    var host = CreateHostBuilder(_args, classAggregator).Build();

    using var log = classAggregator.Locate<ILoggerDisposable>();
    var settings = classAggregator.Locate<ISettings>();
    log.Info($"-------------------------------------------");
    log.Info($"Roadnik Server Started");
    log.Info($"Address: {settings.IpBind}:{settings.PortBind}");
    log.Info($"OS: {Environment.OSVersion} {(Environment.Is64BitOperatingSystem ? "x64" : "x86")}");
    log.Info($"Config file: '{configFilePath}'");
    log.Info($"-------------------------------------------");

    host.Run();
    lifetime.Complete();

    log.Info($"-------------------------------------------");
    log.Info($"Server stopped");
    log.Info($"-------------------------------------------");
  }

  public static IHostBuilder CreateHostBuilder(string[] _args, ExportClassMgr _classAggregator)
  {
    var settings = _classAggregator.Locate<ISettings>();
    return Host
        .CreateDefaultBuilder(_args)
        .ConfigureLogging((_ctx, _logBuilder) => _logBuilder.ClearProviders())
        .UseGraceContainer(_classAggregator.ServiceProvider)
        .ConfigureWebHostDefaults(_webBuilder =>
        {
          _webBuilder
            .UseKestrel(_serverOptions =>
            {
              _serverOptions.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
              _serverOptions.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(1);
              _serverOptions.Listen(IPAddress.Parse(settings.IpBind), settings.PortBind);
            })
            .UseStartup<Startup>();
        });
  }

  public static IReadOnlyDictionary<Type, Func<IExportLocatorScope, object>> GetServices(ILifetime _lifetime, string _configFilePath)
  {
    var settings = JsonConvert.DeserializeObject<SettingsImpl>(File.ReadAllText(_configFilePath, Encoding.UTF8));
    if (settings == null)
      throw new FormatException($"Settings file is corrupted!");

    if (!Directory.Exists(settings.LogDirPath))
      Directory.CreateDirectory(settings.LogDirPath);

    var logger = new CompositeLogger(new FileLogger(() => Path.Combine(settings.LogDirPath, $"{DateTimeOffset.UtcNow:yyyy-MM-dd}.log"), 5000), new ConsoleLogger());
    _lifetime.DisposeOnCompleted(FileLoggerCleaner.Create(new DirectoryInfo(settings.LogDirPath), false, new Regex(@".+\.log"), TimeSpan.FromDays(30)));

    var docStorage = new SqliteDocumentStorage(Path.Combine(settings.DataDirPath, "data.v0.db"))
      .WithCache(1000, TimeSpan.FromHours(1));

    _lifetime.DisposeOnCompleted(docStorage);

    Observable
      .Interval(TimeSpan.FromDays(1))
      .StartWithDefault()
      .SelectAsync(async (_, _ct) => await docStorage.FlushAsync(true, _ct))
      .Subscribe(_lifetime);

    return new Dictionary<Type, Func<IExportLocatorScope, object>>()
    {
      { typeof(JustLogger.Interfaces.ILogger), _scope => logger },
      { typeof(ILoggerDisposable), _scope => logger },
      { typeof(IDocumentStorage), _scope => docStorage },
      { typeof(ILifetime), _scope => _lifetime },
      { typeof(IReadOnlyLifetime), _scope => _lifetime },
      { typeof(ISettings), _scope => settings}
    };
  }
}

public class Options
{
  [Option('c', "config", Required = false, HelpText = "Set config file path")]
  public string? ConfigFilePath { get; set; }
}