namespace Roadnik.MAUI.Data.JsonBridge;

internal record HostMsgMapStateData(
  double Lat, 
  double Lng,
  double Zoom, 
  string Layer, 
  IReadOnlyList<string> Overlays, 
  string? SelectedPath, 
  double? SelectedPathWindowLeft, 
  double? SelectedPathWindowBottom);