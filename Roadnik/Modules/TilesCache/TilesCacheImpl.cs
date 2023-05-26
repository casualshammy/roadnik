﻿using Ax.Fw.Attributes;
using Ax.Fw.Cache;
using Ax.Fw.Extensions;
using Ax.Fw.SharedTypes.Interfaces;
using Roadnik.Interfaces;
using System.Reactive.Subjects;
using System.Text;

namespace Roadnik.Modules.TilesCache;

[ExportClass(typeof(ITilesCache), Singleton: true)]
internal class TilesCacheImpl : ITilesCache
{
  private readonly IRxProperty<FileCache?> p_cacheProp;

  public TilesCacheImpl(
    ISettingsController _settingsController,
    IReadOnlyLifetime _lifetime)
  {
    var cacheFlow = new Subject<FileCache>();
    p_cacheProp = cacheFlow.ToProperty(_lifetime);

    _settingsController.Value
      .WhereNotNull()
      .HotAlive(_lifetime, (_conf, _life) =>
      {
        var folder = Path.Combine(_conf.DataDirPath, "tiles-cache");
        var cache = new FileCache(
          _lifetime,
          folder,
          TimeSpan.FromDays(30),
          _conf.ThunderforestCacheSize,
          TimeSpan.FromHours(6));

        cacheFlow.OnNext(cache);
      });
  }

  public async Task StoreAsync(int _x, int _y, int _z, string _type, Stream _tileStream, CancellationToken _ct)
  {
    if (p_cacheProp.Value == null)
      return;

    var key = GetKey(_x, _y, _z, _type);
    await p_cacheProp.Value.StoreAsync(key, _tileStream, _ct);
  }

  public Stream? GetOrDefault(int _x, int _y, int _z, string _type)
  {
    if (p_cacheProp.Value == null)
      return null;

    var key = GetKey(_x, _y, _z, _type);
    return p_cacheProp.Value.Get(key);
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
