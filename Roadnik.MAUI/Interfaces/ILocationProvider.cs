﻿using Roadnik.MAUI.Data;

namespace Roadnik.MAUI.Interfaces;

internal interface ILocationProvider
{
  IObservable<LocationData> Location { get; }
  IObservable<string> ProviderDisabled { get; }
  IObservable<string> ProviderEnabled { get; }

  void StopLocationWatcher();
  void StartLocationWatcher(LocationPriority _locationPriority);
  static abstract Task<LocationData?> GetCurrentBestLocationAsync(TimeSpan _timeout, CancellationToken _ct);
}