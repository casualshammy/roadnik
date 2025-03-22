using Ax.Fw.Extensions;
using Roadnik.Server.Interfaces;
using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace Roadnik.Server.Data.Settings;

internal class AppConfig : IAppConfig
{
  public required string WebrootDirPath { get; init; }
  public required string LogDirPath { get; init; }
  public required string DataDirPath { get; init; }
  public required IPAddress BindIp { get; init; }
  public required int BindPort { get; init; }
  public required uint MaxPathPointsPerRoom { get; init; }
  public required double MaxPathPointAgeHours { get; init; }
  public required uint MinPathPointIntervalMs { get; init; }
  public required string FirebaseServiceAccountJsonPath { get; init; }
  public required string FirebaseProjectId { get; init; }
  public required string? ThunderforestApiKey { get; init; }
  public required long? MapTilesCacheSize { get; init; }
  public required string? AdminApiKey { get; init; }

  public static bool TryCreateAppConfig(
    [NotNullWhen(true)] out AppConfig? _config)
  {
    _config = null;

    var webrootDirPath = Environment.GetEnvironmentVariable("ROADNIK_WEBROOT");
    if (webrootDirPath.IsNullOrWhiteSpace())
      return false;

    var logDirPath = Environment.GetEnvironmentVariable("ROADNIK_LOG_DIR");
    if (logDirPath.IsNullOrWhiteSpace())
      return false;

    var dataDirPath = Environment.GetEnvironmentVariable("ROADNIK_DATA_DIR");
    if (dataDirPath.IsNullOrWhiteSpace())
      return false;

    if (!IPAddress.TryParse(Environment.GetEnvironmentVariable("ROADNIK_BIND_IP"), out var bindIp))
      return false;

    if (!int.TryParse(Environment.GetEnvironmentVariable("ROADNIK_BIND_PORT"), out var bindPort))
      return false;

    if (!uint.TryParse(Environment.GetEnvironmentVariable("ROADNIK_MAX_PATH_POINTS_PER_ROOM"), out var maxPathPointsPerRoom))
      return false;

    if (!double.TryParse(Environment.GetEnvironmentVariable("ROADNIK_MAX_PATH_POINTS_AGE_HOURS"), out var maxPathPointAgeHours))
      return false;

    if (!uint.TryParse(Environment.GetEnvironmentVariable("ROADNIK_MIN_REPORT_INTERVAL"), out var minPathPointIntervalMs))
      return false;

    var firebaseServiceAccountJsonPath = Environment.GetEnvironmentVariable("ROADNIK_FIREBASE_JSON");
    if (firebaseServiceAccountJsonPath.IsNullOrWhiteSpace())
      return false;

    var firebaseProjectId = Environment.GetEnvironmentVariable("ROADNIK_FIREBASE_PROJECT_ID");
    if (firebaseProjectId.IsNullOrWhiteSpace())
      return false;

    var mapTilesCacheSize = (long?)null;
    if (long.TryParse(Environment.GetEnvironmentVariable("ROADNIK_MAP_TILES_CACHE_SIZE"), out var confMapTilesCacheSize))
      mapTilesCacheSize = confMapTilesCacheSize;

    _config = new AppConfig
    {
      WebrootDirPath = webrootDirPath,
      LogDirPath = logDirPath,
      DataDirPath = dataDirPath,
      BindIp = bindIp,
      BindPort = bindPort,
      MaxPathPointsPerRoom = maxPathPointsPerRoom,
      MaxPathPointAgeHours = maxPathPointAgeHours,
      MinPathPointIntervalMs = minPathPointIntervalMs,
      FirebaseServiceAccountJsonPath = firebaseServiceAccountJsonPath,
      FirebaseProjectId = firebaseProjectId,
      ThunderforestApiKey = Environment.GetEnvironmentVariable("ROADNIK_TF_API_KEY"),
      MapTilesCacheSize = mapTilesCacheSize,
      AdminApiKey = Environment.GetEnvironmentVariable("ROADNIK_ADMIN_API_KEY"),
    };
    return true;
  }

}
