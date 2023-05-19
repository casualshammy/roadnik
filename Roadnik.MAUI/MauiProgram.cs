﻿using Ax.Fw;
using Ax.Fw.DependencyInjection;
using Ax.Fw.SharedTypes.Interfaces;
using CommunityToolkit.Maui;
using Grace.DependencyInjection;
using JustLogger;
using JustLogger.Interfaces;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Reflection;
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

    var fileLogger = new FileLogger(() => Path.Combine(logsFolder, $"{DateTimeOffset.UtcNow:yyyy-MM-dd}.log"), 1000);
#if ANDROID
    var androidLogger = new Roadnik.MAUI.Platforms.Android.Toolkit.AndroidLogger("roadnik");
    var logger = lifetime.DisposeOnCompleted(new CompositeLogger(androidLogger, fileLogger));
#else
    var logger = lifetime.DisposeOnCompleted(fileLogger);
#endif

    lifetime.DisposeOnCompleted(FileLoggerCleaner.Create(new DirectoryInfo(logsFolder), false, new Regex(@"^.+\.log$"), TimeSpan.FromDays(30)));

    var appStartedVersionStr = $"============= app is launched ({AppInfo.Current.VersionString}) =============";
    var line = new string(Enumerable.Repeat('=', appStartedVersionStr.Length).ToArray());
    logger.Info(line);
    logger.Info(appStartedVersionStr);
    logger.Info(line);
    lifetime.DoOnCompleted(() =>
    {
      logger.Info("=========================================");
      logger.Info("============= app is closed =============");
      logger.Info("=========================================");
    });

    var assembly = Assembly.GetExecutingAssembly();
    var containerBuilder = DependencyManagerBuilder
      .Create(lifetime, assembly)
      .AddSingleton<ILifetime>(lifetime)
      .AddSingleton<IReadOnlyLifetime>(lifetime)
      .AddSingleton<ILogger>(logger)
      .Build();

    logger.Info($"Dependencies are installed");

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

    logger.Info($"MauiApp is building...");

    return builder.Build();
  }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
  public static IInjectionScope Container { get; private set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

}