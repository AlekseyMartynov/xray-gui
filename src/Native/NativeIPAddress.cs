using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Networking.WinSock;
using Windows.Win32.NetworkManagement.Dns;

namespace Project;

[StructLayout(LayoutKind.Sequential)]
readonly partial struct NativeIPAddress {
    readonly ulong Lower;
    readonly ulong Upper;

    NativeIPAddress(ulong lower, ulong upper) {
        Lower = lower;
        Upper = upper;
    }

    public NativeIPAddress(ReadOnlySpan<ushort> hextets, bool networkOrder = false) {
        var inputLen = hextets.Length;
        ArgumentOutOfRangeException.ThrowIfGreaterThan(inputLen, 8);

        var span = NativeUtils.Cast<NativeIPAddress, ushort>(ref this);

        if(networkOrder) {
            hextets.CopyTo(span);
        } else {
            BinaryPrimitives.ReverseEndianness(hextets, span);
        }
    }

    public NativeIPAddress(byte a, byte b, byte c, byte d)
        : this(a | (uint)(b << 8) | (uint)(c << 16) | (uint)(d << 24)) {
    }

    public NativeIPAddress(uint ip4) {
        // https://en.wikipedia.org/wiki/IPv6#IPv4-mapped_IPv6_addresses
        var upperSpan = NativeUtils.Cast<ulong, uint>(ref Upper);
        upperSpan[0] = 0xFFFF0000;
        upperSpan[1] = ip4;
    }

    public bool IsIPv4() {
        return Lower == 0
            && Upper << 32 == 0xFFFF_0000_0000_0000;
    }

    public ADDRESS_FAMILY GetFamily() {
        return IsIPv4() ? ADDRESS_FAMILY.AF_INET : ADDRESS_FAMILY.AF_INET6;
    }

    public void WriteTo(ref SOCKADDR_INET addr) {
        var bytes = AsBytes();
        if(bytes.Length == 4) {
            addr.si_family = ADDRESS_FAMILY.AF_INET;
            addr.Ipv4.sin_addr.S_un.S_addr = BitConverter.ToUInt32(bytes);
        } else {
            addr.si_family = ADDRESS_FAMILY.AF_INET6;
            bytes.CopyTo(addr.Ipv6.sin6_addr.u.Byte.AsSpan());
        }
    }

    public uint ToUInt32() {
        var bytes = AsBytes();
        if(bytes.Length != 4) {
            throw new InvalidOperationException();
        }
        return BitConverter.ToUInt32(bytes);
    }

    public override unsafe string ToString() {
        var family = GetFamily();

        var maxLen = family == ADDRESS_FAMILY.AF_INET
            ? NativeUtils.INET_ADDRSTRLEN
            : NativeUtils.INET6_ADDRSTRLEN;

        var chars = (stackalloc char[maxLen]);
        chars.Clear(); // https://github.com/dotnet/runtime/discussions/74860

        fixed(void* bytes = AsBytes()) {
            PInvoke.InetNtop((int)family, bytes, chars);
        }

        return chars.TrimEnd('\0').ToString();
    }

#if DEBUG
    public System.Net.IPAddress ToIPAddress() {
        return new(AsBytes());
    }
#endif

    [UnscopedRef]
    unsafe ReadOnlySpan<byte> AsBytes() {
        fixed(void* ptr = &this) {
            var span = new ReadOnlySpan<byte>(ptr, 16);
            if(IsIPv4()) {
                span = span.Slice(12);
            }
            return span;
        }
    }
}

partial struct NativeIPAddress : IEquatable<NativeIPAddress> {

    public override bool Equals(object? obj) {
        return obj is NativeIPAddress ip && Equals(ip);
    }

    public bool Equals(NativeIPAddress other) {
        return Lower == other.Lower
            && Upper == other.Upper;
    }

    public override int GetHashCode() {
        return HashCode.Combine(Lower, Upper);
    }
}

partial struct NativeIPAddress {
    public static readonly NativeIPAddress
        IPv4Zero = new(0),
        IPv6Zero = new([]),
        IPv4Loopback = new(127, 0, 0, 1),
        IPv6Loopback = new([0, 0, 0, 0, 0, 0, 0, 1]);

    public static NativeIPAddress operator |(NativeIPAddress x, NativeIPAddress y) {
        return new(x.Lower | y.Lower, x.Upper | y.Upper);
    }

    public static unsafe bool TryParse(string text, out NativeIPAddress result) {
        if(!PreParseValidate(text)) {
            result = default;
            return false;
        }
        fixed(char* p = text) {
            return TryParse(p, out result);
        }
    }

    public static unsafe bool TryParse(ReadOnlySpan<char> text, out NativeIPAddress result) {
        if(!PreParseValidate(text)) {
            result = default;
            return false;
        }

        var len = text.Length;
        var nullTerminated = (stackalloc char[1 + len]);
        text.CopyTo(nullTerminated);
        nullTerminated[len] = '\0';

        fixed(char* p = &MemoryMarshal.GetReference(nullTerminated)) {
            return TryParse(p, out result);
        }
    }

    static unsafe bool TryParse(char* pszAddrString, out NativeIPAddress result) {
        var ip4 = default(uint);
        if(PInvoke.InetPton((int)ADDRESS_FAMILY.AF_INET, pszAddrString, &ip4) == 1) {
            result = new(ip4);
            return true;
        }

        var ip6 = default(NativeIPAddress);
        if(PInvoke.InetPton((int)ADDRESS_FAMILY.AF_INET6, pszAddrString, &ip6) == 1) {
            result = ip6;
            return true;
        }

        result = default;
        return false;
    }

    static bool PreParseValidate(ReadOnlySpan<char> text) {
        return text.Length <= NativeUtils.INET6_ADDRSTRLEN
            && text.ContainsAny('.', ':');
    }

    public static unsafe NativeIPAddress From(DNS_RECORDA* p) {
        return (DNS_TYPE)p->wType switch {
            DNS_TYPE.DNS_TYPE_A => new(p->Data.A.IpAddress),
            DNS_TYPE.DNS_TYPE_AAAA => new(p->Data.AAAA.Ip6Address.IP6Word.AsReadOnlySpan(), true),
            _ => throw new NotSupportedException()
        };
    }

    public static NativeIPAddress From(in SOCKADDR_INET addr) {
        if(addr.si_family == ADDRESS_FAMILY.AF_INET) {
            return From(in addr.Ipv4.sin_addr);
        } else {
            return From(in addr.Ipv6.sin6_addr);
        }
    }

    public static unsafe NativeIPAddress From(SOCKADDR* p) {
        if(p->sa_family == ADDRESS_FAMILY.AF_INET) {
            var p4 = (SOCKADDR_IN*)p;
            return From(in p4->sin_addr);
        } else {
            var p6 = (SOCKADDR_IN6*)p;
            return From(in p6->sin6_addr);
        }
    }

    static NativeIPAddress From(in IN_ADDR addr) {
        return new(addr.S_un.S_addr);
    }

    static NativeIPAddress From(in IN6_ADDR addr) {
        return new(addr.u.Word.AsReadOnlySpan(), true);
    }
}
