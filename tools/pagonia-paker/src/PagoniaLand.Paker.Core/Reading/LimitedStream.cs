namespace PagoniaLand.Paker;

/// <summary>
/// A read-only Stream that exposes at most N bytes of a wrapped stream and
/// returns EOF once that quota is exhausted. Used to feed a compressed entry
/// region into <see cref="System.IO.Compression.GZipStream"/> without letting
/// the decompressor read into the next entry's bytes.
/// </summary>
internal sealed class LimitedStream : Stream
{
    private readonly Stream _inner;
    private long _remaining;

    public LimitedStream(Stream inner, long maxBytes)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
        _remaining = maxBytes;
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_remaining <= 0)
        {
            return 0;
        }

        var toRead = (int)Math.Min(count, _remaining);
        var read = _inner.Read(buffer, offset, toRead);
        _remaining -= read;
        return read;
    }

    public override int Read(Span<byte> buffer)
    {
        if (_remaining <= 0)
        {
            return 0;
        }

        var toRead = (int)Math.Min(buffer.Length, _remaining);
        var read = _inner.Read(buffer[..toRead]);
        _remaining -= read;
        return read;
    }

    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
