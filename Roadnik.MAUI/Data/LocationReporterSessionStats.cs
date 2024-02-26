namespace Roadnik.MAUI.Data;

public record LocationReporterSessionStats(
  int Total, 
  int Successful,
  DateTimeOffset? LastLocationFixTime,
  int LastLocationFixAccuracy,
  DateTimeOffset? LastSuccessfulReportTime)
{
  public static LocationReporterSessionStats Empty { get; } = new LocationReporterSessionStats(0, 0, null, 1000, null);
}
