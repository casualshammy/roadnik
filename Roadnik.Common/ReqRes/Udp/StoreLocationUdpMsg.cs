﻿using Roadnik.Common.Toolkit;
using System.Runtime.InteropServices;

namespace Roadnik.Common.ReqRes.Udp;

[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Unicode)]
public readonly struct StoreLocationUdpMsg : IEquatable<StoreLocationUdpMsg>
{
  public static readonly byte Type = 1;
  public static readonly int Size = Marshal.SizeOf<StoreLocationUdpMsg>();

  public StoreLocationUdpMsg(
    int _sessionId,
    string _roomId,
    string _username,
    float _lat,
    float _lng,
    float _alt,
    float _speed,
    float _acc,
    float _battery,
    float _gsmSignal,
    float _bearing,
    bool _wipeOldPath) : this()
  {
    SessionId = _sessionId;
    RoomId = _roomId;
    Username = _username;
    Lat = _lat;
    Lng = _lng;
    Alt = _alt;
    Speed = _speed;
    Acc = _acc;
    Battery = _battery;
    GsmSignal = _gsmSignal;
    Bearing = _bearing;
    WipeOldPath = _wipeOldPath;
  }

  public readonly int SessionId;

  [MarshalAs(UnmanagedType.ByValTStr, SizeConst = ReqResUtil.MaxRoomIdLength)]
  public readonly string RoomId;

  [MarshalAs(UnmanagedType.ByValTStr, SizeConst = ReqResUtil.MaxUsernameLength)]
  public readonly string Username;

  public readonly float Lat;
  public readonly float Lng;
  public readonly float Alt;
  public readonly float Speed;
  public readonly float Acc;
  public readonly float Battery;
  public readonly float GsmSignal;
  public readonly float Bearing;

  [MarshalAs(UnmanagedType.I1)]
  public readonly bool WipeOldPath;

  public static StoreLocationUdpMsg FromStorePathPointReq(StorePathPointReq _req)
  {
    return new StoreLocationUdpMsg(
      _req.SessionId,
      _req.RoomId,
      _req.Username,
      _req.Lat,
      _req.Lng,
      _req.Alt,
      _req.Speed ?? float.MinValue,
      _req.Acc ?? float.MinValue,
      _req.Battery ?? float.MinValue,
      _req.GsmSignal ?? float.MinValue,
      _req.Bearing ?? float.MinValue,
      _req.WipeOldPath ?? false);
  }

  public static bool TryGetFromByteArray(byte[] _data, out StoreLocationUdpMsg _msg)
  {
    _msg = default;

    try
    {
      if (_data.Length != Size)
        return false;

      var handle = GCHandle.Alloc(_data, GCHandleType.Pinned);
      try
      {
        var obj = Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(StoreLocationUdpMsg));
        if (obj == null)
          return false;

        _msg = (StoreLocationUdpMsg)obj;
        return true;
      }
      catch
      {
        return false;
      }
      finally
      {
        handle.Free();
      }
    }
    catch
    {
      return false;
    }
  }

  public StorePathPointReq ToStorePathPointReq()
  {
    return new StorePathPointReq()
    {
      SessionId = SessionId,
      RoomId = RoomId,
      Username = Username,
      Lat = Lat,
      Lng = Lng,
      Alt = Alt,
      Speed = Speed == float.MinValue ? null : Speed,
      Acc = Acc == float.MinValue ? null : Acc,
      Battery = Battery == float.MinValue ? null : Battery,
      GsmSignal = GsmSignal == float.MinValue ? null : GsmSignal,
      Bearing = Bearing == float.MinValue ? null : Bearing,
      WipeOldPath = WipeOldPath,
    };
  }

  public bool Equals(StoreLocationUdpMsg _other)
  {
    return
      RoomId == _other.RoomId &&
      Username == _other.Username &&
      Lat == _other.Lat &&
      Lng == _other.Lng &&
      Alt == _other.Alt &&
      Speed == _other.Speed &&
      Acc == _other.Acc &&
      Battery == _other.Battery &&
      GsmSignal == _other.GsmSignal &&
      Bearing == _other.Bearing &&
      WipeOldPath == _other.WipeOldPath;
  }

  public override bool Equals(object? _obj) => _obj is StoreLocationUdpMsg msg && Equals(msg);

  public override int GetHashCode()
  {
    var hash0 = HashCode.Combine(RoomId, Username, Lat, Lng, Alt, Speed, Acc, Battery);
    var hash1 = HashCode.Combine(hash0, GsmSignal, Bearing, WipeOldPath);

    return hash1;
  }

}
