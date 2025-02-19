namespace Roadnik.Server.Data.Settings;

public record RawAppSettings(
  string WebrootDirPath,
  string LogDirPath,
  string DataDirPath,
  string IpBind,
  int PortBind,
  string? ThunderforestApiKey,
  long? MapTilesCacheSize,
  string? AdminApiKey,
  uint? MaxPathPointsPerRoom,
  double? MaxPathPointAgeHours,
  uint? MinPathPointIntervalMs,
  string? FCMServiceAccountJsonPath,
  string? FCMProjectId
);
