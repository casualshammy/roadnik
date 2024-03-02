using Ax.Fw.Cache;
using Ax.Fw.DependencyInjection;
using Ax.Fw.Extensions;
using Ax.Fw.SharedTypes.Interfaces;
using Roadnik.Interfaces;
using Roadnik.Server.Interfaces;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Roadnik.Modules.TilesCache;

internal class TilesCacheImpl : ITilesCache, IAppModule<ITilesCache>
{
  public static ITilesCache ExportInstance(IAppDependencyCtx _ctx)
  {
    return _ctx.CreateInstance((ISettingsController _settingsController, IReadOnlyLifetime _lifetime) => new TilesCacheImpl(_settingsController, _lifetime));
  }

  private readonly IRxProperty<FileCache?> p_cacheProp;

  private TilesCacheImpl(
    ISettingsController _settingsController,
    IReadOnlyLifetime _lifetime)
  {
    p_cacheProp = _settingsController.Settings
      .WhereNotNull()
      .Alive(_lifetime, (_conf, _life) =>
      {
        var folder = Path.Combine(_conf.DataDirPath, "tiles-cache");
        var cache = new FileCache(
          _lifetime,
          folder,
          TimeSpan.FromDays(30),
          _conf.MapTilesCacheSize,
          TimeSpan.FromHours(6),
          true);

        return cache;
      })
      .ToProperty(_lifetime, null);
  }

  public async Task StoreAsync(int _x, int _y, int _z, string _type, Stream _tileStream, CancellationToken _ct)
  {
    if (p_cacheProp.Value == null)
      return;

    var key = GetKey(_x, _y, _z, _type);
    await p_cacheProp.Value.StoreAsync(key, _tileStream, false, _ct);
  }

  public Stream? GetOrDefault(int _x, int _y, int _z, string _type)
  {
    if (p_cacheProp.Value == null)
      return null;

    var key = GetKey(_x, _y, _z, _type);
    return p_cacheProp.Value.Get(key);
  }

  public bool TryGet(
    int _x,
    int _y,
    int _z,
    string _type,
    [NotNullWhen(true)] out Stream? _stream,
    [NotNullWhen(true)] out string? _hash)
  {
    _stream = null;
    _hash = null;

    var cache = p_cacheProp.Value;

    if (cache == null)
      return false;

    var key = GetKey(_x, _y, _z, _type);
    if (!cache.TryGet(key, out var stream, out _, out var hash))
      return false;

    _stream = stream;
    _hash = hash;
    return true;
  }

  private static string GetKey(int _x, int _y, int _z, string _type)
  {
    var sb = new StringBuilder();
    sb.Append(_x);
    sb.Append('.');
    sb.Append(_y);
    sb.Append('.');
    sb.Append(_z);
    sb.Append('.');
    sb.Append(_type);
    return sb.ToString();
  }

}
