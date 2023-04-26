using Ax.Fw;
using Ax.Fw.SharedTypes.Interfaces;
using CommunityToolkit.Maui;
using Grace.DependencyInjection;
using JustLogger;
using JustLogger.Interfaces;
using System.Text.RegularExpressions;

namespace Roadnik.MAUI;

public static class MauiProgram
{
  public static MauiApp CreateMauiApp()
  {
    var lifetime = new Lifetime();
    var depMgr = new ExportClassMgr(lifetime, GetServices(lifetime));
    Container = depMgr.ServiceProvider;

    var builder = MauiApp.CreateBuilder();
    builder
      .UseMauiApp<App>()
      .UseMauiCommunityToolkit()
      .ConfigureFonts(_fonts =>
      {
        _fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
        _fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
      });

    return builder.Build();
  }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
  public static IInjectionScope Container { get; private set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

  private static IReadOnlyDictionary<Type, Func<IExportLocatorScope, object>> GetServices(ILifetime _lifetime)
  {
    var logsFolder = Path.Combine(FileSystem.Current.AppDataDirectory, "logs");
    if (!Directory.Exists(logsFolder))
      Directory.CreateDirectory(logsFolder);

    var logger = _lifetime.DisposeOnCompleted(new FileLogger(() => Path.Combine(logsFolder, $"{DateTimeOffset.UtcNow.Date:d}.log"), 10000));
    _lifetime.DisposeOnCompleted(FileLoggerCleaner.Create(new DirectoryInfo(logsFolder), false, new Regex(@"^.+\.log$"), TimeSpan.FromDays(30)));

    return new Dictionary<Type, Func<IExportLocatorScope, object>>()
    {
      { typeof(ILifetime), _scope => _lifetime },
      { typeof(IReadOnlyLifetime), _scope => _lifetime },
      { typeof(ILogger), _scope => logger },
    };
  }

}