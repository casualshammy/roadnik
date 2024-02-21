using System.Diagnostics.CodeAnalysis;

namespace Roadnik.Interfaces;

public interface ITilesCache
{
  Stream? GetOrDefault(int _x, int _y, int _z, string _type);
  Task StoreAsync(int _x, int _y, int _z, string _type, Stream _tileStream, CancellationToken _ct);
  bool TryGet(int _x, int _y, int _z, string _type, [NotNullWhen(true)] out Stream? _stream, [NotNullWhen(true)] out string? _hash);
}
