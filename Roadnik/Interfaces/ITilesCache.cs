﻿using System.Diagnostics.CodeAnalysis;

namespace Roadnik.Interfaces;

public interface ITilesCache
{
  void EnqueueUrl(int _x, int _y, int _z, string _type, string _url);
  Stream? GetOrDefault(int _x, int _y, int _z, string _type);
  bool TryGet(int _x, int _y, int _z, string _type, [NotNullWhen(true)] out Stream? _stream, [NotNullWhen(true)] out string? _hash);
}
