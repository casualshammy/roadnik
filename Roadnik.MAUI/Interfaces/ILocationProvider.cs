using Roadnik.MAUI.Data;
using Roadnik.MAUI.Data.LocationProvider;

namespace Roadnik.MAUI.Interfaces;

internal interface ILocationProvider
{
  IObservable<LocationData> Location { get; }
  IObservable<string> ProviderDisabled { get; }
  IObservable<string> ProviderEnabled { get; }

  void StopLocationWatcher();
  void StartLocationWatcher(IReadOnlyList<string> _providers, TimeSpan _frequency);
  static abstract Task<LocationData?> GetCurrentBestLocationAsync(TimeSpan _timeout, CancellationToken _ct);
  void StartLocationWatcher(LocationProviders _providers, TimeSpan _frequency);
}