namespace Roadnik.MAUI.Data;

public record LocationReporterSessionStats(int Total, int Successful)
{
  public static LocationReporterSessionStats Empty { get; } = new LocationReporterSessionStats(0, 0);
}
