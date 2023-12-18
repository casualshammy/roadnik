using Ax.Fw;
using Ax.Fw.DependencyInjection;
using Ax.Fw.SharedTypes.Interfaces;
using CommunityToolkit.Maui;
using JustLogger;
using JustLogger.Interfaces;
using Roadnik.MAUI.Interfaces;
using Roadnik.MAUI.Modules.DeepLinksController;
using Roadnik.MAUI.Modules.HttpClientProvider;
using Roadnik.MAUI.Modules.LocationProvider;
using Roadnik.MAUI.Modules.LocationReporter;
using Roadnik.MAUI.Modules.PageController;
using Roadnik.MAUI.Modules.PreferencesStorage;
using Roadnik.MAUI.Modules.PushMessagesController;
using Roadnik.MAUI.Modules.TelephonyMgrProvider;
using Roadnik.MAUI.Modules.TilesCache;
using Roadnik.MAUI.Platforms.Android.Services;
using System.Text.RegularExpressions;

namespace Roadnik.MAUI;

public static partial class MauiProgram
{
  public static MauiApp CreateMauiApp()
  {
    var lifetime = new Lifetime();
    lifetime.DoOnEnded(() =>
    {
      Application.Current?.Quit();
    });

    var logsFolder = Path.Combine(FileSystem.Current.AppDataDirectory, "logs");
    if (!Directory.Exists(logsFolder))
      Directory.CreateDirectory(logsFolder);

    var fileLogger = new FileLogger(() => Path.Combine(logsFolder, $"{DateTimeOffset.UtcNow:yyyy-MM-dd}.log"), 1000);
    var androidLogger = new Platforms.Android.Toolkit.AndroidLogger("roadnik");
    var logger = lifetime.ToDisposeOnEnded(new CompositeLogger(androidLogger, fileLogger));

    var appStartedVersionStr = $"============= app is launched ({AppInfo.Current.VersionString}) =============";
    var line = new string(Enumerable.Repeat('=', appStartedVersionStr.Length).ToArray());
    logger.Info(line);
    logger.Info(appStartedVersionStr);
    logger.Info(line);
    lifetime.DoOnEnded(() =>
    {
      logger.Info("=========================================");
      logger.Info("============= app is closed =============");
      logger.Info("=========================================");
    });

    lifetime.ToDisposeOnEnded(FileLoggerCleaner.Create(new DirectoryInfo(logsFolder), false, LogFileCleanerRegex(), TimeSpan.FromDays(30), null, _file =>
    {
      logger.Info($"Old file was removed: '{_file.Name}'");
    }));

    logger.Info($"Installing dependencies...");

    var appDeps = AppDependencyManager
      .Create()
      .AddSingleton<ILifetime>(lifetime)
      .AddSingleton<IReadOnlyLifetime>(lifetime)
      .AddSingleton<ILogger>(logger)
      .AddModule<DeepLinksControllerImpl, IDeepLinksController>()
      .AddModule<HttpClientProviderImpl, IHttpClientProvider>()
      .AddModule<AndroidLocationProviderImpl, ILocationProvider>()
      .AddModule<LocationReporterImpl, ILocationReporter>()
      .AddModule<PagesControllerImpl, IPagesController>()
      .AddModule<PreferencesStorageImpl, IPreferencesStorage>()
      .AddModule<PushMessagesControllerImpl, IPushMessagesController>()
      .AddModule<TelephonyMgrProviderImpl, ITelephonyMgrProvider>()
      .AddModule<TilesCacheImpl, ITilesCache>()
      .ActivateOnStart<IPushMessagesController>();

    Container = appDeps;

    logger.Info($"Dependencies are installed");

    logger.Info($"Building maui app...");

    var app = MauiApp
      .CreateBuilder()
      .UseMauiApp<App>()
      .UseMauiCommunityToolkit()
      .ConfigureFonts(_fonts =>
      {
        _fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
        _fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
      })
      .Build();

    logger.Info($"Maui app is built");

    return app;
  }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
  public static IReadOnlyDependencyContainer Container { get; private set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

  [GeneratedRegex(@"^.+\.log$")]
  private static partial Regex LogFileCleanerRegex();

}