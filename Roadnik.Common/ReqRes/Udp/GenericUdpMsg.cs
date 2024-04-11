using System.Numerics;
using System.Runtime.InteropServices;

namespace Roadnik.Common.ReqRes.Udp;

public readonly struct GenericUdpMsg
{
  public static readonly int Size = Marshal.SizeOf<GenericUdpMsg>();
  public const int MagicWordRef = 0xFAD4623;
  public const int MaxPayloadSize = 256;

  public readonly int MagicWord;
  public readonly int CryptSerial;
  public readonly int PayloadHash;
  public readonly int PayloadSize;

  [MarshalAs(UnmanagedType.ByValArray, SizeConst = MaxPayloadSize)]
  public readonly byte[] Payload;

  public GenericUdpMsg(
    int _cryptSerial,
    ReadOnlySpan<byte> _payload)
  {
    MagicWord = MagicWordRef;
    CryptSerial = _cryptSerial;

    if (_payload.Length > MaxPayloadSize)
      throw new FormatException($"Payload is too big; max size: {MaxPayloadSize}");

    PayloadSize = _payload.Length;
    Payload = new byte[MaxPayloadSize];
    _payload.CopyTo(Payload);
    PayloadHash = CalculatePayloadHash(Payload);
  }

  public ReadOnlySpan<byte> ToByteArray()
  {
    var size = Marshal.SizeOf(this);
    var array = new byte[size];
    var pointer = Marshal.AllocHGlobal(size);
    try
    {
      Marshal.StructureToPtr(this, pointer, true);
      Marshal.Copy(pointer, array, 0, size);

      return array;
    }
    finally
    {
      Marshal.FreeHGlobal(pointer);
    }
  }

  public static bool TryGetFromByteArray(byte[] _bytes, out GenericUdpMsg _msg)
  {
    _msg = default;
    if (_bytes.Length != Size)
      return false;

    var handle = GCHandle.Alloc(_bytes, GCHandleType.Pinned);
    try
    {
      var obj = Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(GenericUdpMsg));
      if (obj == null)
        return false;

      var value = (GenericUdpMsg)obj;

      if (value.MagicWord != MagicWordRef)
        return false;
      if (CalculatePayloadHash(value.Payload) != value.PayloadHash)
        return false;

      _msg = value;
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

  private static unsafe int CalculatePayloadHash(ReadOnlySpan<byte> _payload)
  {
    unchecked
    {
      uint rawHash = 0;
      foreach (var b in _payload)
        rawHash = BitOperations.Crc32C(rawHash, b);

      int result = (int)rawHash;
      return result;
    }
  }

}
