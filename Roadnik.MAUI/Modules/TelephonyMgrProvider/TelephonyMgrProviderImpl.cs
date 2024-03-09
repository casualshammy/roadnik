using Android.Media.TV;
using Android.Telephony;
using Ax.Fw.DependencyInjection;
using Ax.Fw.Extensions;
using Ax.Fw.Pools;
using Ax.Fw.SharedTypes.Interfaces;
using Roadnik.MAUI.Interfaces;
using System.Reactive.Concurrency;
using System.Reactive.Linq;

namespace Roadnik.MAUI.Modules.TelephonyMgrProvider;

internal class TelephonyMgrProviderImpl : ITelephonyMgrProvider, IAppModule<ITelephonyMgrProvider>
{
  public static ITelephonyMgrProvider ExportInstance(IAppDependencyCtx _ctx)
  {
    return _ctx.CreateInstance((IReadOnlyLifetime _lifetime, ILog _log) => new TelephonyMgrProviderImpl(_lifetime, _log["telephony-mgr"]));
  }

  private readonly TelephonyManager? p_telephonyManager;

  private TelephonyMgrProviderImpl(
    IReadOnlyLifetime _lifetime,
    ILog _log)
  {
    p_telephonyManager = Android.App.Application.Context.GetSystemService(Android.Content.Context.TelephonyService) as TelephonyManager;

    _lifetime.ToDisposeOnEnded(SharedPool<EventLoopScheduler>.Get(out var scheduler));

    SignalLevel = Observable.Create<double?>(_observer =>
    {
      return Observable
        .Interval(TimeSpan.FromMinutes(1), scheduler)
        .StartWithDefault()
        .Subscribe(_ =>
        {
          try
          {
            var signalStrengh = GetSignalStrength();
            _observer.OnNext(signalStrengh);
          }
          catch (Exception ex)
          {
            _log.Error($"Can't get signal strength", ex);
            _observer.OnNext(null);
          }
        });
    });
  }

  public IObservable<double?> SignalLevel { get; }

  private double? GetSignalStrength()
  {
    if (p_telephonyManager?.AllCellInfo == null)
      return null;

    var signalStrength = 0d;
    foreach (var info in p_telephonyManager.AllCellInfo)
    {
      // we cast to specific type because getting value of abstract field `CellSignalStrength` raises exception
      if (info is CellInfoWcdma wcdma && wcdma.CellSignalStrength != null)
        signalStrength = Math.Max(signalStrength, NormalizeSignalLevel(wcdma.CellSignalStrength.Level));
      else if (info is CellInfoGsm gsm && gsm.CellSignalStrength != null)
        signalStrength = Math.Max(signalStrength, NormalizeSignalLevel(gsm.CellSignalStrength.Level));
      else if (info is CellInfoLte lte && lte.CellSignalStrength != null)
        signalStrength = Math.Max(signalStrength, NormalizeSignalLevel(lte.CellSignalStrength.Level));
      else if (info is CellInfoCdma cdma && cdma.CellSignalStrength != null)
        signalStrength = Math.Max(signalStrength, NormalizeSignalLevel(cdma.CellSignalStrength.Level));
    }
    return signalStrength;
  }

  private static double NormalizeSignalLevel(int _level)
  {
    if (_level == 0)
      return 0.01;

    return (_level + 1) * 20 / 100d;
  }

}
