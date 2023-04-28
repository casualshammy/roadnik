namespace Roadnik.Interfaces;

public interface ITilesCache
{
  Task<Stream?> GetOrDefaultAsync(int _x, int _y, int _z, string _type, CancellationToken _ct);
  Task StoreAsync(int _x, int _y, int _z, string _type, Stream _tileStream, CancellationToken _ct);
}
