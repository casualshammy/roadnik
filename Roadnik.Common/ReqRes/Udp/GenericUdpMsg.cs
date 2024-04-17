using Ax.Fw;
using System.Runtime.InteropServices;

namespace Roadnik.Common.ReqRes.Udp;

public readonly struct GenericUdpMsg
{
  public static readonly int Size = Marshal.SizeOf<GenericUdpMsg>();
  public const int MagicWordRef = 0xFAD4623;
  public const int MaxPayloadSize = 256;

  public readonly int MagicWord;
  public readonly byte Type;
  public readonly int PayloadHash;
  public readonly int PayloadSize;

  [MarshalAs(UnmanagedType.ByValArray, SizeConst = MaxPayloadSize)]
  public readonly byte[] Payload;

  public GenericUdpMsg(byte _type, ReadOnlySpan<byte> _payload)
  {
    MagicWord = MagicWordRef;
    Type = _type;

    if (_payload.Length > MaxPayloadSize)
      throw new FormatException($"Payload is too big; max size: {MaxPayloadSize}");

    PayloadSize = _payload.Length;
    Payload = new byte[MaxPayloadSize];
    _payload.CopyTo(Payload);
    PayloadHash = Cryptography.CalculateCrc32(Payload);
  }

  public byte[] ToByteArray()
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
      if (Cryptography.CalculateCrc32(value.Payload) != value.PayloadHash)
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

}
