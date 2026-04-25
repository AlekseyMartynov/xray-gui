using System.Globalization;
using System.Runtime.CompilerServices;

namespace Windows.Win32.NetworkManagement.Ndis;

partial struct NET_LUID_LH {

    public static NET_LUID_LH FromValue(ulong value) {
        return Unsafe.As<ulong, NET_LUID_LH>(ref value);
    }

    public static bool TryParse(ReadOnlySpan<char> text, out NET_LUID_LH result) {
        var ok = ulong.TryParse(text, NumberStyles.HexNumber, default, out var value);
        result = FromValue(value);
        return ok;
    }

    public readonly bool IsEmpty => Value == 0;

    public readonly bool Equals(NET_LUID_LH other) {
        return other.Value == Value;
    }

    public override readonly bool Equals(object? obj) {
        return obj is NET_LUID_LH luid && Equals(luid);
    }

    public override readonly int GetHashCode() {
        return Value.GetHashCode();
    }

    public override readonly string ToString() {
        return Value.ToString("x");
    }

    public static bool operator ==(NET_LUID_LH x, NET_LUID_LH y) => x.Value == y.Value;
    public static bool operator !=(NET_LUID_LH x, NET_LUID_LH y) => x.Value != y.Value;
}
