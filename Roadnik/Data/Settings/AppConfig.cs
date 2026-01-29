using Ax.Fw.Extensions;
using Roadnik.Server.Interfaces;
using Roadnik.Server.JsonCtx;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;

namespace Roadnik.Server.Data.Settings;

internal record AppConfig(
  string WebrootDirPath,
  string LogDirPath,
  string DataDirPath,
  IPAddress BindIp,
  int BindPort,
  uint MaxPathPointsPerRoom,
  double MaxPathPointAgeHours,
  uint MinPathPointIntervalMs,
  string FirebaseServiceAccountJsonPath,
  string FirebaseProjectId,
  string? ThunderforestApiKey,
  long? MapTilesCacheSize,
  string? AdminApiKey,
  string? StravaSession) : IAppConfig
{
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

    _config = new AppConfig(
      webrootDirPath,
      logDirPath,
      dataDirPath,
      bindIp,
      bindPort,
      maxPathPointsPerRoom,
      maxPathPointAgeHours,
      minPathPointIntervalMs,
      firebaseServiceAccountJsonPath,
      firebaseProjectId,
      Environment.GetEnvironmentVariable("ROADNIK_TF_API_KEY"),
      mapTilesCacheSize,
      Environment.GetEnvironmentVariable("ROADNIK_ADMIN_API_KEY"),
      Environment.GetEnvironmentVariable("ROADNIK_STRAVA_SESSION"));

    return true;
  }

}
