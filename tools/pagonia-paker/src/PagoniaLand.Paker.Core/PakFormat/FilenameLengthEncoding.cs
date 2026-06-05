using System.Buffers.Binary;

namespace PagoniaLand.Paker;

/// <summary>
/// The pak format stores almost everything little-endian, with one exception:
/// per-entry filename lengths are written as big-endian uint32. Wrapping the
/// I/O in one place keeps that footgun out of the rest of the code.
/// </summary>
public static class FilenameLengthEncoding
{
    public static uint Read(Stream stream)
    {
        Span<byte> buffer = stackalloc byte[4];
        stream.ReadExactly(buffer);
        return BinaryPrimitives.ReadUInt32BigEndian(buffer);
    }

    public static void Write(Stream stream, uint length)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(buffer, length);
        stream.Write(buffer);
    }
}
