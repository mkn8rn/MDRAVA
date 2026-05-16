using System.Net;

namespace MDRAVA.API.Models.Configuration.Runtime;

public sealed record RuntimeTrustedProxy(string Entry, IPAddress NetworkAddress, int PrefixLength)
{
    public bool Contains(IPAddress address)
    {
        var normalizedAddress = NormalizeAddress(address);
        var normalizedNetwork = NormalizeAddress(NetworkAddress);
        if (normalizedAddress.AddressFamily != normalizedNetwork.AddressFamily)
        {
            return false;
        }

        var addressBytes = normalizedAddress.GetAddressBytes();
        var networkBytes = normalizedNetwork.GetAddressBytes();
        var remainingBits = PrefixLength;

        for (var index = 0; index < addressBytes.Length; index++)
        {
            if (remainingBits <= 0)
            {
                return true;
            }

            var bitsInByte = Math.Min(8, remainingBits);
            var mask = (byte)(0xff << (8 - bitsInByte));
            if ((addressBytes[index] & mask) != (networkBytes[index] & mask))
            {
                return false;
            }

            remainingBits -= bitsInByte;
        }

        return true;
    }

    public static bool TryParse(string entry, out RuntimeTrustedProxy? trustedProxy)
    {
        trustedProxy = null;
        if (string.IsNullOrWhiteSpace(entry))
        {
            return false;
        }

        var trimmed = entry.Trim();
        var slashIndex = trimmed.IndexOf('/');
        var addressText = slashIndex < 0 ? trimmed : trimmed[..slashIndex];
        if (!IPAddress.TryParse(addressText, out var address))
        {
            return false;
        }

        var normalized = NormalizeAddress(address);
        var maxPrefix = normalized.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128;
        var prefixLength = maxPrefix;
        if (slashIndex >= 0)
        {
            var prefixText = trimmed[(slashIndex + 1)..];
            if (!int.TryParse(prefixText, out prefixLength) || prefixLength < 0 || prefixLength > maxPrefix)
            {
                return false;
            }
        }

        trustedProxy = new RuntimeTrustedProxy(trimmed, normalized, prefixLength);
        return true;
    }

    private static IPAddress NormalizeAddress(IPAddress address)
    {
        return address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
    }
}
