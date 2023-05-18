namespace Roadnik.Interfaces;

public interface ITilesCache
{
  Stream? GetOrDefault(int _x, int _y, int _z, string _type);
  Task StoreAsync(int _x, int _y, int _z, string _type, Stream _tileStream, CancellationToken _ct);
}
