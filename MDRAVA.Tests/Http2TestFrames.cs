using System.Buffers.Binary;

namespace MDRAVA.Tests;

internal readonly record struct Http2TestFrame(
    Http2TestFrameType Type,
    byte Flags,
    int StreamId,
    ReadOnlyMemory<byte> Payload);

internal enum Http2TestFrameType : byte
{
    Data = 0x0,
    Headers = 0x1,
    RstStream = 0x3,
    Settings = 0x4,
    Ping = 0x6,
    GoAway = 0x7,
    WindowUpdate = 0x8,
    Continuation = 0x9
}

internal static class Http2TestFlags
{
    public const byte EndStream = 0x1;
    public const byte Ack = 0x1;
    public const byte EndHeaders = 0x4;
}

internal static class Http2TestFrames
{
    public static async Task<Http2TestFrame> ReadAsync(Stream stream, CancellationToken cancellationToken)
    {
        var header = await ReadExactAsync(stream, 9, cancellationToken);
        var length = header[0] << 16 | header[1] << 8 | header[2];
        var payload = length == 0 ? [] : await ReadExactAsync(stream, length, cancellationToken);
        return new Http2TestFrame(
            (Http2TestFrameType)header[3],
            header[4],
            (int)(BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(5, 4)) & 0x7fffffff),
            payload);
    }

    public static async Task WriteAsync(
        Stream stream,
        Http2TestFrameType type,
        byte flags,
        int streamId,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken)
    {
        var header = new byte[9];
        header[0] = (byte)((payload.Length >> 16) & 0xff);
        header[1] = (byte)((payload.Length >> 8) & 0xff);
        header[2] = (byte)(payload.Length & 0xff);
        header[3] = (byte)type;
        header[4] = flags;
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(5, 4), (uint)streamId & 0x7fffffff);
        await stream.WriteAsync(header, cancellationToken);
        if (payload.Length > 0)
        {
            await stream.WriteAsync(payload, cancellationToken);
        }
    }

    public static async Task<byte[]> ReadExactAsync(
        Stream stream,
        int length,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[length];
        var offset = 0;
        while (offset < length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, length - offset), cancellationToken);
            if (read == 0)
            {
                throw new IOException("Connection closed while reading HTTP/2 data.");
            }

            offset += read;
        }

        return buffer;
    }
}
