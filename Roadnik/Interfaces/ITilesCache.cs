﻿using System.Diagnostics.CodeAnalysis;

namespace Roadnik.Server.Interfaces;

public interface ITilesCache
{
  void EnqueueUrl(
    int _x,
    int _y,
    int _z,
    string _type,
    string _url);

  bool TryGet(
    int _x,
    int _y,
    int _z,
    string _type,
    [NotNullWhen(true)] out Stream? _stream,
    [NotNullWhen(true)] out string? _hash);

}
