using Ax.Fw;
using Ax.Fw.DependencyInjection;
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
    lifetime.DoOnCompleted(() =>
    {
      Application.Current?.Quit();
    });

    var logsFolder = Path.Combine(FileSystem.Current.AppDataDirectory, "logs");
    if (!Directory.Exists(logsFolder))
      Directory.CreateDirectory(logsFolder);

    var logger = lifetime.DisposeOnCompleted(new FileLogger(() => Path.Combine(logsFolder, $"{DateTimeOffset.UtcNow:yyyy-MM-dd}.log"), 1000));
    lifetime.DisposeOnCompleted(FileLoggerCleaner.Create(new DirectoryInfo(logsFolder), false, new Regex(@"^.+\.log$"), TimeSpan.FromDays(30)));

    logger.Info("=======================================");
    logger.Info("============= app started =============");
    logger.Info("=======================================");
    lifetime.DoOnCompleted(() =>
    {
      logger.Info("=======================================");
      logger.Info("============= app stopped =============");
      logger.Info("=======================================");
    });

    var containerBuilder = DependencyManagerBuilder
      .Create(lifetime)
      .AddSingleton<ILifetime>(lifetime)
      .AddSingleton<IReadOnlyLifetime>(lifetime)
      .AddSingleton<ILogger>(logger)
      .Build();

    Container = containerBuilder.ServiceProvider;

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

}