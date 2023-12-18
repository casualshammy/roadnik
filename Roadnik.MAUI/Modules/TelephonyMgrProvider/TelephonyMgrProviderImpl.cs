#if ANDROID
using Android.Telephony;
using Ax.Fw;
using Ax.Fw.Attributes;
using Ax.Fw.Extensions;
using Ax.Fw.Pools;
using Ax.Fw.SharedTypes.Interfaces;
using Roadnik.MAUI.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Roadnik.MAUI.Modules.TelephonyMgrProvider;

[ExportClass(typeof(ITelephonyMgrProvider), Singleton: true)]
internal class TelephonyMgrProviderImpl : ITelephonyMgrProvider
{
  private readonly TelephonyManager? p_telephonyManager;

  public TelephonyMgrProviderImpl(IReadOnlyLifetime _lifetime)
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
          var signalStrengh = GetSignalStrength();
          _observer.OnNext(signalStrengh);
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
#endif
