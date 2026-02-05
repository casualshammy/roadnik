using Android.Util;
using Ax.Fw;
using Ax.Fw.DependencyInjection;
using Ax.Fw.Log;
using Ax.Fw.SharedTypes.Data.Log;
using Ax.Fw.SharedTypes.Interfaces;
using CommunityToolkit.Maui;
using Maui.Android.InAppUpdates;
using Roadnik.MAUI.Interfaces;
using Roadnik.MAUI.JsonCtx;
using Roadnik.MAUI.Modules.BleDevicesManager;
using Roadnik.MAUI.Modules.CompassProvider;
using Roadnik.MAUI.Modules.DeepLinksController;
using Roadnik.MAUI.Modules.HttpClientProvider;
using Roadnik.MAUI.Modules.LocationReporter;
using Roadnik.MAUI.Modules.MapDataCache;
using Roadnik.MAUI.Modules.PageController;
using Roadnik.MAUI.Modules.PreferencesStorage;
using Roadnik.MAUI.Modules.PushMessagesController;
using Roadnik.MAUI.Modules.TelephonyMgrProvider;
using System.Text.RegularExpressions;
using static Roadnik.MAUI.Data.Consts;
using ILog = Ax.Fw.SharedTypes.Interfaces.ILog;

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

    var log = lifetime.ToDisposeOnEnded(new GenericLog());
    log
      .AttachFileLog(() => Path.Combine(logsFolder, $"{DateTimeOffset.UtcNow:yyyy-MM-dd}.log"), TimeSpan.FromSeconds(1))
      .AttachCustomLog(_entry =>
      {
        if (_entry.Type == LogEntryType.INFO)
          Log.Info(nameof(Roadnik), _entry.Text);
        else if (_entry.Type == LogEntryType.WARN)
          Log.Warn(nameof(Roadnik), _entry.Text);
        else if (_entry.Type == LogEntryType.ERROR)
          Log.Error(nameof(Roadnik), _entry.Text);
      });

    var appStartedVersionStr = $"============= app is launched ({AppInfo.Current.VersionString}) =============";
    var line = new string(Enumerable.Repeat('=', appStartedVersionStr.Length).ToArray());
    log.Info(line);
    log.Info(appStartedVersionStr);
    log.Info(line);
    lifetime.DoOnEnded(() =>
    {
      log.Info("=========================================");
      log.Info("============= app is closed =============");
      log.Info("=========================================");
    });

    lifetime.ToDisposeOnEnded(FileLoggerCleaner.Create(new DirectoryInfo(logsFolder), false, LogFileCleanerRegex(), TimeSpan.FromDays(30), false, null, _file =>
    {
      log.Info($"Old file was removed: '{_file.Name}'");
    }));

    log.Info($"Installing dependencies...");

    var appDeps = AppDependencyManager
      .Create()
      .AddSingleton<ILifetime>(lifetime)
      .AddSingleton<IReadOnlyLifetime>(lifetime)
      .AddSingleton<ILog>(log)
      .AddModule<DeepLinksControllerImpl, IDeepLinksController>()
      .AddModule<HttpClientProviderImpl, IHttpClientProvider>()
      .AddModule<LocationReporterImpl, ILocationReporter>()
      .AddModule<PagesControllerImpl, IPagesController>()
      .AddModule<PreferencesStorageImpl, IPreferencesStorage>()
      .AddModule<PushMessagesControllerImpl, IPushMessagesController>()
      .AddModule<TelephonyMgrProviderImpl, ITelephonyMgrProvider>()
      .AddModule<WebDataCacheImpl, IWebDataCache>()
      .AddModule<CompassProviderImpl, ICompassProvider>()
      .AddModule<BleDevicesManagerImpl, IBleDevicesManager>()
      .ActivateOnStart<IPushMessagesController>();

    Container = appDeps;

    log.Info($"Dependencies are installed");

    var prefsStorage = appDeps.Locate<IPreferencesStorage>();
    var appId = prefsStorage.GetValueOrDefault(PREF_APP_INSTALLATION_ID, PrefsStorageJsonCtx.Default.Guid);
    log.Info($"App installation ID: {appId}");

    log.Info($"Building maui app...");

    var app = MauiApp
      .CreateBuilder()
      .UseMauiApp<App>()
      .UseMauiCommunityToolkit()
      .UseAndroidInAppUpdates()
      .ConfigureFonts(_fonts =>
      {
        _fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
        _fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
      })
      .Build();

    log.Info($"Maui app is built");

    return app;
  }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
  public static IReadOnlyDependencyContainer Container { get; private set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

  [GeneratedRegex(@"^.+\.log$")]
  private static partial Regex LogFileCleanerRegex();

}