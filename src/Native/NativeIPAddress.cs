using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;
using Windows.Win32.Networking.WinSock;

namespace Project;

[StructLayout(LayoutKind.Sequential)]
readonly partial struct NativeIPAddress {
    readonly ulong Lower;
    readonly ulong Upper;

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

    public bool TryWriteBytes(Span<byte> destination) {
        return AsBytes().TryCopyTo(destination);
    }

    public override string ToString() {
        var textHead = (stackalloc char[39]);
        var textCurr = textHead;

        var bytes = AsBytes();
        var bytesLen = bytes.Length;

        var v4 = bytes.Length == 4;

        var (sep, step) = v4 ? ('.', 1) : (':', 2);

        for(var i = 0; i < bytesLen; i += step) {
            if(i > 0) {
                textCurr[0] = sep;
                textCurr = textCurr.Slice(1);
            }
            int written;
            if(v4) {
                bytes[i].TryFormat(textCurr, out written);
            } else {
                (256 * bytes[i] + bytes[i + 1]).TryFormat(textCurr, out written, "x");
            }
            textCurr = textCurr.Slice(written);
        }

        var textLen = textHead.Length - textCurr.Length;

        return new String(textHead.Slice(0, textLen));
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

    public static bool TryParse(ReadOnlySpan<char> text, out NativeIPAddress result) {
        return TryParseV4(text, out result)
            || TryParseV6(text, out result);
    }

    public static bool TryParseV4(ReadOnlySpan<char> text, out NativeIPAddress result) {
        var octetList = (stackalloc byte[4]);
        var count = 0;

        if(text.Length >= 7 && text.Length <= 15) {
            foreach(var r in text.Split('.')) {
                if(!byte.TryParse(text[r], out var octet)) {
                    break;
                }
                count++;
                if(count > octetList.Length) {
                    break;
                }
                octetList[count - 1] = octet;
            }
        }

        if(count == octetList.Length) {
            result = new(octetList[0], octetList[1], octetList[2], octetList[3]);
            return true;
        } else {
            result = default;
            return false;
        }
    }

    public static bool TryParseV6(ReadOnlySpan<char> text, out NativeIPAddress result) {
        var hextetList = (stackalloc ushort[8]);
        var count = 0;

        foreach(var r in text.Split(':')) {
            var slice = text[r];
            if(slice.IsEmpty) {
                throw new NotSupportedException();
            }
            if(!ushort.TryParse(slice, NumberStyles.HexNumber, default, out var hextet)) {
                break;
            }
            count++;
            if(count > hextetList.Length) {
                break;
            }
            hextetList[count - 1] = hextet;
        }

        if(count == hextetList.Length) {
            result = new(hextetList);
            return true;
        } else {
            result = default;
            return false;
        }
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
