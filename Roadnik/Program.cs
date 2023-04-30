using Ax.Fw;
using Ax.Fw.DependencyInjection;
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

    var settings = JsonConvert.DeserializeObject<SettingsImpl>(File.ReadAllText(configFilePath, Encoding.UTF8))
      ?? throw new FormatException($"Settings file is corrupted!");

    if (!Directory.Exists(settings.LogDirPath))
      Directory.CreateDirectory(settings.LogDirPath);

    using var logger = new CompositeLogger(new FileLogger(() => Path.Combine(settings.LogDirPath, $"{DateTimeOffset.UtcNow:yyyy-MM-dd}.log"), 5000), new ConsoleLogger());
    lifetime.DisposeOnCompleted(FileLoggerCleaner.Create(new DirectoryInfo(settings.LogDirPath), false, new Regex(@".+\.log"), TimeSpan.FromDays(30)));

    var docStorage = new SqliteDocumentStorage(Path.Combine(settings.DataDirPath, "data.v0.db"))
      .WithCache(1000, TimeSpan.FromHours(1));

    lifetime.DisposeOnCompleted(docStorage);

    Observable
      .Interval(TimeSpan.FromDays(1))
      .StartWithDefault()
      .SelectAsync(async (_, _ct) => await docStorage.FlushAsync(true, _ct))
      .Subscribe(lifetime);

    var depMgr = DependencyManagerBuilder
      .Create(lifetime)
      .AddSingleton<JustLogger.Interfaces.ILogger>(logger)
      .AddSingleton<ILoggerDisposable>(logger)
      .AddSingleton(docStorage)
      .AddSingleton<ILifetime>(lifetime)
      .AddSingleton<IReadOnlyLifetime>(lifetime)
      .AddSingleton<ISettings>(settings)
      .Build();

    var host = CreateHostBuilder(_args, depMgr).Build();

    logger.Info($"-------------------------------------------");
    logger.Info($"Roadnik Server Started");
    logger.Info($"Address: {settings.IpBind}:{settings.PortBind}");
    logger.Info($"OS: {Environment.OSVersion} {(Environment.Is64BitOperatingSystem ? "x64" : "x86")}");
    logger.Info($"Config file: '{configFilePath}'");
    logger.Info($"-------------------------------------------");

    host.Run();
    lifetime.Complete();

    logger.Info($"-------------------------------------------");
    logger.Info($"Server stopped");
    logger.Info($"-------------------------------------------");
  }

  public static IHostBuilder CreateHostBuilder(string[] _args, DependencyManager _depMgr)
  {
    var settings = _depMgr.Locate<ISettings>();
    return Host
        .CreateDefaultBuilder(_args)
        .ConfigureLogging((_ctx, _logBuilder) => _logBuilder.ClearProviders())
        .UseGraceContainer(_depMgr.ServiceProvider)
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

}

public class Options
{
  [Option('c', "config", Required = false, HelpText = "Set config file path")]
  public string? ConfigFilePath { get; set; }
}