namespace Roadnik.MAUI.Data;

internal record WebAppState(LatLon Location, bool AutoPan, string? MapLayer, double Zoom);
