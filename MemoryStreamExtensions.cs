using System.IO;
using System.Text;

namespace SboxCllGui;

public static class MemoryStreamExtensions
{
    public static void WriteInt32(this MemoryStream ms, int value)
    {
        var data = BitConverter.GetBytes(value);
        ms.Write(data, 0, data.Length);
    }

    public static void WriteLengthPrependedAsciiString(this MemoryStream ms, string? value)
    {
        if (value == null) value = "";
        var bytes = Encoding.ASCII.GetBytes(value);
        ms.WriteInt32(bytes.Length);
        ms.Write(bytes, 0, bytes.Length);
    }

    public static int? ReadInt32(this MemoryStream ms)
    {
        if (ms.Position + 4 > ms.Length) return null;
        var buf = new byte[4];
        ms.ReadExactly(buf, 0, 4);
        return BitConverter.ToInt32(buf, 0);
    }

    public static string? ReadLengthPrependedAsciiString(this MemoryStream ms)
    {
        var len = ms.ReadInt32();
        if (len == null || len < 0) return null;
        if (ms.Position + len > ms.Length) return null;

        var buf = new byte[len.Value];
        ms.ReadExactly(buf, 0, len.Value);
        return Encoding.ASCII.GetString(buf);
    }
}