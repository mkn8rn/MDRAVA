using System.Globalization;
using System.Text;
using MDRAVA.API.Proxy.Protocol;

namespace MDRAVA.API.Proxy.Http3;

public static class Http3PreviewCodec
{
    public const long DataFrame = 0x0;
    public const long HeadersFrame = 0x1;
    public const long SettingsFrame = 0x4;
    public const long ControlStream = 0x0;

    private static readonly HeaderField[] StaticTable =
    [
        new(":authority", ""),
        new(":path", "/"),
        new("age", "0"),
        new("content-disposition", ""),
        new("content-length", "0"),
        new("cookie", ""),
        new("date", ""),
        new("etag", ""),
        new("if-modified-since", ""),
        new("if-none-match", ""),
        new("last-modified", ""),
        new("link", ""),
        new("location", ""),
        new("referer", ""),
        new("set-cookie", ""),
        new(":method", "CONNECT"),
        new(":method", "DELETE"),
        new(":method", "GET"),
        new(":method", "HEAD"),
        new(":method", "OPTIONS"),
        new(":method", "POST"),
        new(":method", "PUT"),
        new(":scheme", "http"),
        new(":scheme", "https"),
        new(":status", "103"),
        new(":status", "200"),
        new(":status", "304"),
        new(":status", "404"),
        new(":status", "503")
    ];

    public static byte[] EncodeHeaderBlock(IReadOnlyList<Http1HeaderField> headers)
    {
        using var memory = new MemoryStream();
        memory.WriteByte(0);
        memory.WriteByte(0);
        foreach (var header in headers)
        {
            WriteLiteralHeader(memory, header.Name.ToLowerInvariant(), header.Value);
        }

        return memory.ToArray();
    }

    public static bool TryDecodeHeaderBlock(
        ReadOnlySpan<byte> block,
        int maxHeaderBytes,
        out IReadOnlyList<Http1HeaderField> headers,
        out string reason)
    {
        headers = [];
        reason = "invalid_qpack";
        if (block.Length > maxHeaderBytes)
        {
            reason = "header_list_too_large";
            return false;
        }

        var offset = 0;
        if (!TryReadPrefixedInteger(block, 8, ref offset, out _)
            || !TryReadPrefixedInteger(block, 7, ref offset, out _))
        {
            return false;
        }

        List<Http1HeaderField> decoded = [];
        while (offset < block.Length)
        {
            var first = block[offset];
            if ((first & 0x80) != 0)
            {
                var isStatic = (first & 0x40) != 0;
                if (!isStatic
                    || !TryReadPrefixedInteger(block, 6, ref offset, out var index)
                    || !TryGetStaticField(index, out var field))
                {
                    reason = "unsupported_qpack_index";
                    return false;
                }

                decoded.Add(new Http1HeaderField(field.Name, field.Value));
                continue;
            }

            if ((first & 0x40) != 0)
            {
                var isStatic = (first & 0x10) != 0;
                if (!isStatic
                    || !TryReadPrefixedInteger(block, 4, ref offset, out var nameIndex)
                    || !TryGetStaticField(nameIndex, out var namedField)
                    || !TryReadString(block, ref offset, out var value, out reason))
                {
                    reason = "unsupported_qpack_name_ref";
                    return false;
                }

                decoded.Add(new Http1HeaderField(namedField.Name, value));
                continue;
            }

            if ((first & 0x20) != 0)
            {
                if (!TryReadLiteralHeader(block, ref offset, out var literal, out reason))
                {
                    return false;
                }

                decoded.Add(literal);
                continue;
            }

            reason = "unsupported_qpack_field";
            return false;
        }

        headers = decoded;
        return true;
    }

    public static void WriteFrame(Stream stream, long type, ReadOnlySpan<byte> payload)
    {
        WriteVarInt(stream, type);
        WriteVarInt(stream, payload.Length);
        stream.Write(payload);
    }

    public static void WriteVarInt(Stream stream, long value)
    {
        if (value < 0 || value >= 1L << 62)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        if (value < 64)
        {
            stream.WriteByte((byte)value);
            return;
        }

        if (value < 16_384)
        {
            stream.WriteByte((byte)(0x40 | (value >> 8)));
            stream.WriteByte((byte)value);
            return;
        }

        if (value < 1_073_741_824)
        {
            stream.WriteByte((byte)(0x80 | (value >> 24)));
            stream.WriteByte((byte)(value >> 16));
            stream.WriteByte((byte)(value >> 8));
            stream.WriteByte((byte)value);
            return;
        }

        stream.WriteByte((byte)(0xC0 | (value >> 56)));
        stream.WriteByte((byte)(value >> 48));
        stream.WriteByte((byte)(value >> 40));
        stream.WriteByte((byte)(value >> 32));
        stream.WriteByte((byte)(value >> 24));
        stream.WriteByte((byte)(value >> 16));
        stream.WriteByte((byte)(value >> 8));
        stream.WriteByte((byte)value);
    }

    public static bool TryReadVarInt(ReadOnlySpan<byte> bytes, ref int offset, out long value)
    {
        value = 0;
        if (offset >= bytes.Length)
        {
            return false;
        }

        var first = bytes[offset++];
        var length = 1 << (first >> 6);
        value = first & 0x3f;
        if (offset + length - 1 > bytes.Length)
        {
            return false;
        }

        for (var index = 1; index < length; index++)
        {
            value = (value << 8) | bytes[offset++];
        }

        return true;
    }

    public static bool TryReadFrame(
        ReadOnlySpan<byte> bytes,
        ref int offset,
        out long type,
        out ReadOnlyMemory<byte> payload)
    {
        type = 0;
        payload = ReadOnlyMemory<byte>.Empty;
        if (!TryReadVarInt(bytes, ref offset, out type)
            || !TryReadVarInt(bytes, ref offset, out var length)
            || length < 0
            || length > int.MaxValue
            || offset + (int)length > bytes.Length)
        {
            return false;
        }

        payload = bytes.Slice(offset, (int)length).ToArray();
        offset += (int)length;
        return true;
    }

    private static bool TryReadLiteralHeader(
        ReadOnlySpan<byte> block,
        ref int offset,
        out Http1HeaderField header,
        out string reason)
    {
        header = null!;
        reason = "invalid_qpack_literal";
        if (!TryReadPrefixedInteger(block, 3, ref offset, out var nameLength)
            || nameLength < 0
            || nameLength > int.MaxValue
            || offset + (int)nameLength > block.Length)
        {
            return false;
        }

        var name = Encoding.ASCII.GetString(block.Slice(offset, (int)nameLength));
        offset += (int)nameLength;
        if (!TryReadString(block, ref offset, out var value, out reason))
        {
            return false;
        }

        header = new Http1HeaderField(name, value);
        return true;
    }

    private static void WriteLiteralHeader(Stream stream, string name, string value)
    {
        WritePrefixedInteger(stream, 0x20, 3, Encoding.ASCII.GetByteCount(name));
        stream.Write(Encoding.ASCII.GetBytes(name));
        WriteString(stream, value);
    }

    private static bool TryReadString(
        ReadOnlySpan<byte> block,
        ref int offset,
        out string value,
        out string reason)
    {
        value = "";
        reason = "invalid_qpack_string";
        if (offset >= block.Length || (block[offset] & 0x80) != 0)
        {
            reason = "unsupported_huffman";
            return false;
        }

        if (!TryReadPrefixedInteger(block, 7, ref offset, out var length)
            || length < 0
            || length > int.MaxValue
            || offset + (int)length > block.Length)
        {
            return false;
        }

        value = Encoding.ASCII.GetString(block.Slice(offset, (int)length));
        offset += (int)length;
        return true;
    }

    private static void WriteString(Stream stream, string value)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        WritePrefixedInteger(stream, 0, 7, bytes.Length);
        stream.Write(bytes);
    }

    private static bool TryReadPrefixedInteger(
        ReadOnlySpan<byte> block,
        int prefixBits,
        ref int offset,
        out long value)
    {
        value = 0;
        if (offset >= block.Length)
        {
            return false;
        }

        var mask = (1 << prefixBits) - 1;
        value = block[offset++] & mask;
        if (value < mask)
        {
            return true;
        }

        var multiplier = 0;
        while (offset < block.Length)
        {
            var next = block[offset++];
            value += (long)(next & 0x7f) << multiplier;
            if ((next & 0x80) == 0)
            {
                return true;
            }

            multiplier += 7;
            if (multiplier > 56)
            {
                return false;
            }
        }

        return false;
    }

    private static void WritePrefixedInteger(Stream stream, byte prefix, int prefixBits, int value)
    {
        var maxPrefix = (1 << prefixBits) - 1;
        if (value < maxPrefix)
        {
            stream.WriteByte((byte)(prefix | value));
            return;
        }

        stream.WriteByte((byte)(prefix | maxPrefix));
        value -= maxPrefix;
        while (value >= 128)
        {
            stream.WriteByte((byte)(value % 128 + 128));
            value /= 128;
        }

        stream.WriteByte((byte)value);
    }

    private static bool TryGetStaticField(long index, out HeaderField field)
    {
        field = default;
        if (index < 0 || index >= StaticTable.Length)
        {
            return false;
        }

        field = StaticTable[(int)index];
        return true;
    }

    public static string StatusText(int statusCode)
    {
        return statusCode.ToString(CultureInfo.InvariantCulture);
    }

    private readonly record struct HeaderField(string Name, string Value);
}
