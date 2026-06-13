namespace MDRAVA.BLL.ControlPlane.Http1;

public static partial class Http1RequestParser
{
    private static int IndexOfCrlf(ReadOnlySpan<byte> bytes)
    {
        for (var index = 1; index < bytes.Length; index++)
        {
            if (bytes[index - 1] == (byte)'\r' && bytes[index] == (byte)'\n')
            {
                return index - 1;
            }
        }

        return -1;
    }

    private static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> bytes)
    {
        while (bytes.Length > 0 && IsOptionalWhitespace(bytes[0]))
        {
            bytes = bytes[1..];
        }

        while (bytes.Length > 0 && IsOptionalWhitespace(bytes[^1]))
        {
            bytes = bytes[..^1];
        }

        return bytes;
    }

    private static bool IsOptionalWhitespace(byte value)
    {
        return value is (byte)' ' or (byte)'\t';
    }

    private static bool TryParseNonNegativeInt64(ReadOnlySpan<byte> bytes, out long value)
    {
        value = 0;

        if (bytes.Length == 0)
        {
            return false;
        }

        foreach (var digit in bytes)
        {
            if (digit is < (byte)'0' or > (byte)'9')
            {
                return false;
            }

            var next = value * 10 + digit - (byte)'0';
            if (next < value)
            {
                return false;
            }

            value = next;
        }

        return true;
    }

    private static bool AsciiEquals(ReadOnlySpan<byte> bytes, string text)
    {
        if (bytes.Length != text.Length)
        {
            return false;
        }

        for (var index = 0; index < bytes.Length; index++)
        {
            if (bytes[index] != text[index])
            {
                return false;
            }
        }

        return true;
    }

    private static bool AsciiEqualsIgnoreCase(ReadOnlySpan<byte> bytes, string text)
    {
        if (bytes.Length != text.Length)
        {
            return false;
        }

        for (var index = 0; index < bytes.Length; index++)
        {
            var left = ToLowerAscii(bytes[index]);
            var right = ToLowerAscii((byte)text[index]);
            if (left != right)
            {
                return false;
            }
        }

        return true;
    }

    private static byte ToLowerAscii(byte value)
    {
        return value is >= (byte)'A' and <= (byte)'Z'
            ? (byte)(value + 32)
            : value;
    }
}
