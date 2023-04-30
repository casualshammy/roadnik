namespace Roadnik.Data;

internal record WebAppState(LatLon Location, bool AutoPan, string? MapLayer, double Zoom);
