using Windows.Win32;
using Windows.Win32.Networking.WinSock;
using Windows.Win32.NetworkManagement.Ndis;
using Windows.Win32.System.Registry;

namespace Project;

static class TunModeAdapters {
    const int IPv4TunPrefixLen = 24;
    const int IPv6TunPrefixLen = 64; // serverfault.com/a/218432

    // https://github.com/eycorsican/go-tun2socks/blob/v1.16.11/cmd/tun2socks/main.go#L92-L94
    static readonly NativeIPAddress IPv4TunAddr = new(10, 255, 0, 2);

    static readonly NativeIPAddress IPv6TunAddr = new([0xfd00, 0x10, 0x255, 0, 0, 0, 0, 2]);

    // https://github.com/Jigsaw-Code/outline-apps/blob/manager_windows/v1.17.2/client/electron/go_vpn_tunnel.ts#L35
    static readonly string TunDns = "1.1.1.1,9.9.9.9";

    public static uint IPv4TunIndex { get; private set; }
    public static uint IPv6TunIndex { get; private set; }

    public static bool IPv6TunEnabled { get; private set; }

    public static uint IPv6LoopbackIndex { get; private set; }

    public static void Refresh() {
        IPv4TunIndex = default;
        IPv6TunIndex = default;
        IPv6TunEnabled = default;
        IPv6LoopbackIndex = default;

        var tunFound = false;
        var ip6LoopbackFound = false;

        NativeAdapters.Enumerate((ref readonly NativeAdapterInfo info) => {
            if(!tunFound && IsGoodTun(in info)) {
                IPv4TunIndex = info.IPv4Index;
                IPv6TunIndex = info.IPv6Index;
                IPv6TunEnabled = info.IPv6Enabled;
                tunFound = true;
            }
            if(!ip6LoopbackFound && IsIPv6Loopback(in info)) {
                IPv6LoopbackIndex = info.IPv6Index;
                ip6LoopbackFound = true;
            }
        });

        if(!tunFound) {
            throw new InvalidOperationException();
        }

        if(!ip6LoopbackFound) {
            IPv6LoopbackIndex = 1;
        }
    }

    public static void SetTunParams(bool dhcp) {
        var keyName = $@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\{{{Wintun.Guid}}}";

        NativeRegistry.DeleteTree(HKEY.HKEY_LOCAL_MACHINE, keyName, false);

        if(!dhcp) {
            var key = NativeRegistry.OpenOrCreateKey(HKEY.HKEY_LOCAL_MACHINE, keyName, true);
            try {
                NativeRegistry.SetValue(key, "EnableDHCP", 0);
                NativeRegistry.SetValue(key, "NameServer", TunDns);
            } finally {
                PInvoke.RegCloseKey(key);
            }
            NativeUnicastAddressTable.AssignStatic(ADDRESS_FAMILY.AF_INET, IPv4TunIndex, IPv4TunAddr, IPv4TunPrefixLen);
            if(IPv6TunEnabled) {
                NativeUnicastAddressTable.AssignStatic(
                    ADDRESS_FAMILY.AF_INET6,
                    IPv6TunIndex,
                    AppConfig.TunModeIPv6 ? IPv6TunAddr : NativeIPAddress.IPv6Zero,
                    IPv6TunPrefixLen
                );
            }
        }
    }

    static bool IsGoodTun(ref readonly NativeAdapterInfo info) {
        return info.IPv4Enabled
            && info.Status == IF_OPER_STATUS.IfOperStatusDown
            && info.Guid == Wintun.Guid
            && info.Name.StartsWith(Wintun.Name);
    }

    static bool IsIPv6Loopback(ref readonly NativeAdapterInfo info) {
        return info.IPv6Enabled
            && info.Unicast.Contains(NativeIPAddress.IPv6Loopback);
    }
}
