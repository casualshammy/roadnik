using Ax.Fw.DependencyInjection;
using Ax.Fw.SharedTypes.Interfaces;
using Roadnik.MAUI.Interfaces;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace Roadnik.MAUI.Modules.CompassProvider;

internal class CompassProviderImpl : ICompassProvider, IAppModule<ICompassProvider>
{
  public static ICompassProvider ExportInstance(IAppDependencyCtx _ctx)
  {
    return _ctx.CreateInstance((ILog _log) => new CompassProviderImpl(_log["compass-provider"]));
  }

  private CompassProviderImpl(
    ILog _log)
  {
    var observable = Observable
      .Create<float?>(_observer =>
      {
        _observer.OnNext(null);

        try
        {
          if (!Compass.Default.IsSupported)
            throw new NotSupportedException($"Compass is not supported in this device");

          Compass.Default.Start(SensorSpeed.UI, true);
        }
        catch (Exception ex)
        {
          _log.Error($"Can't start compass provider: {ex}");
          return Disposable.Empty;
        }

        var subs = Observable
          .FromEventPattern<CompassChangedEventArgs>(_ => Compass.Default.ReadingChanged += _, _ => Compass.Default.ReadingChanged -= _)
          .Select(_ => (float?)_.EventArgs.Reading.HeadingMagneticNorth)
          .StartWith((float?)null)
          .Subscribe(_ => _observer.OnNext(_));

        _log.Info($"Compass provider is started");

        return Disposable.Create(() =>
        {
          subs.Dispose();
          Compass.Default.Stop();
          _log.Info($"Compass provider is stopped");
        });
      })
      .Publish()
      .RefCount();

    Values = observable;
  }

  public IObservable<float?> Values { get; }

}
