using Ax.Fw;
using Ax.Fw.SharedTypes.Interfaces;
using Grace.DependencyInjection;
using JustLogger;
using JustLogger.Interfaces;
using Roadnik.MAUI.Interfaces;
using System.ComponentModel;
using System.Text.RegularExpressions;

namespace Roadnik.MAUI.Toolkit;

public abstract class ContainerizedMauiApplication : Application, IMauiApp
{
  protected ContainerizedMauiApplication() : base()
  {
    var lifetime = new Lifetime();
    var depMgr = new ExportClassMgr(lifetime, GetServices(lifetime));
    Container = depMgr.ServiceProvider;
  }

  public IInjectionScope Container { get; }

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

public abstract class ContainerizedContentPage : ContentPage
{
  protected ContainerizedContentPage()
  {
    if (Application.Current is not IMauiApp app)
      throw new ApplicationException($"Application is not {nameof(ContainerizedMauiApplication)}");

    Container = app.Container;
  }

  public IInjectionScope Container { get; }

}