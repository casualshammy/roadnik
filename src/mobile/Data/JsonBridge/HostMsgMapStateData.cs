namespace Roadnik.MAUI.Data.JsonBridge;

internal record HostMsgMapStateData(
  double Lat,
  double Lng,
  double Zoom,
  string Layer,
  IReadOnlyList<string> Overlays,
  string? SelectedAppId,
  double? SelectedPathWindowLeft,
  double? SelectedPathWindowBottom);