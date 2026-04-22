using Windows.Win32.NetworkManagement.IpHelper;
using Windows.Win32.NetworkManagement.WindowsFilteringPlatform;

namespace Project;

readonly partial record struct CIDR(NativeIPAddress Prefix, byte PrefixLen) {

    public bool IsIPv4() {
        return Prefix.IsIPv4();
    }

    public override string ToString() {
        return String.Join('/', Prefix, PrefixLen);
    }
}

partial record struct CIDR {

    public static implicit operator CIDR(NativeIPAddress ip) {
        return new(ip, ip.GetMaxPrefixLen());
    }

    public static implicit operator CIDR((NativeIPAddress, byte) pair) {
        return new(pair.Item1, pair.Item2);
    }

    public static bool TryParse(ReadOnlySpan<char> text, out CIDR cidr) {
        var hasLen = text.TrySplit('/', out var prefixText, out var prefixLenText);
        if(NativeIPAddress.TryParse(prefixText, out var prefix)) {
            if(!hasLen) {
                cidr = prefix;
                return true;
            }
            if(byte.TryParse(prefixLenText, out var prefixLen)) {
                cidr = (prefix, prefixLen);
                return true;
            }
        }
        cidr = default;
        return false;
    }

    public static bool TryParse(string text, out CIDR cidr) {
        return TryParse(text.AsSpan(), out cidr);
    }
}

partial record struct CIDR {

    public void WriteTo(ref FWP_V4_ADDR_AND_MASK dest) {
        var addrSpan = NativeUtils.Cast<uint, byte>(ref dest.addr);
        Prefix.WriteTo(addrSpan);

        // "Specifies IPv4 address and mask in host order"
        // https://learn.microsoft.com/windows/win32/api/fwptypes/
        addrSpan.Reverse();

        dest.mask = uint.MaxValue << (32 - PrefixLen);
    }

    public void WriteTo(ref FWP_V6_ADDR_AND_MASK dest) {
        Prefix.WriteTo(dest.addr.AsSpan());
        dest.prefixLength = PrefixLen;
    }

    public void WriteTo(ref IP_ADDRESS_PREFIX dest) {
        Prefix.WriteTo(ref dest.Prefix);
        dest.PrefixLength = PrefixLen;
    }
}
