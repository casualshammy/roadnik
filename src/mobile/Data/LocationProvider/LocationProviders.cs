namespace Roadnik.MAUI.Data.LocationProvider;

[Flags]
internal enum LocationProviders
{
  Gps = 1,
  Network = 2,
  Passive = 4,
  All = Gps | Network | Passive,
}
