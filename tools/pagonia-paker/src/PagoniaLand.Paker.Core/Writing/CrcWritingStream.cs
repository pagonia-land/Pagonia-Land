using System.IO.Hashing;

namespace PagoniaLand.Paker;

/// <summary>
/// Write-only Stream wrapper that extends a running <see cref="Crc32"/> over
/// every byte it forwards to the inner stream. Used by <see cref="PakPacker"/>
/// so the footer CRC covers the gzip-compressed bytes that actually end up on
/// disk, not the uncompressed source bytes.
/// </summary>
internal sealed class CrcWritingStream : Stream
{
    private readonly Stream _inner;
    private readonly Crc32 _crc;

    public CrcWritingStream(Stream inner, Crc32 crc)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(crc);
        _inner = inner;
        _crc = crc;
    }

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        _crc.Append(buffer.AsSpan(offset, count));
        _inner.Write(buffer, offset, count);
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        _crc.Append(buffer);
        _inner.Write(buffer);
    }

    public override void Flush() => _inner.Flush();
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
}
