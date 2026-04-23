using Windows.Win32;
using Windows.Win32.Networking.WinSock;
using Windows.Win32.NetworkManagement.Ndis;
using Windows.Win32.System.Registry;

namespace Project;

static class TunModeAdapters {
    const int IPv4TunPrefixLen = 24;
    const int IPv6TunPrefixLen = 64; // serverfault.com/a/218432

    static readonly NativeIPAddress IPv4TunAddr;
    static readonly NativeIPAddress IPv6TunAddr;

    public static readonly NativeIPAddress TunDns;
    public static readonly NativeIPAddress TunBroadcast;

    static TunModeAdapters() {
        // https://github.com/eycorsican/go-tun2socks/blob/v1.16.11/cmd/tun2socks/main.go#L92-L94
        var v4TunPrefix = new NativeIPAddress(10, 255, 0, 0);
        var v6TunPrefix = new NativeIPAddress([0xfd00, 0x10, 0x255]);

        IPv4TunAddr = new(v4TunPrefix, 2);
        IPv6TunAddr = new(v6TunPrefix, 2);

        TunDns = new(v4TunPrefix, 53);
        TunBroadcast = new(v4TunPrefix, 255);
    }

    public static uint IPv4TunIndex { get; private set; }
    public static uint IPv6TunIndex { get; private set; }

    public static bool IPv6TunEnabled { get; private set; }

    public static uint IPv6LoopbackIndex { get; private set; }

    public static string PrimaryName { get; private set; } = "";

    public static void Refresh() {
        IPv4TunIndex = default;
        IPv6TunIndex = default;
        IPv6TunEnabled = default;
        IPv6LoopbackIndex = default;
        PrimaryName = "";

        var tunFound = false;
        var ip6LoopbackFound = false;

        var ip4PrimaryName = "";
        var ip6PrimaryName = "";

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
            if(ip4PrimaryName.Length < 1 && IsIPv4Primary(in info)) {
                ip4PrimaryName = info.Name.ToString();
            }
            if(ip6PrimaryName.Length < 1 && IsIPv6Primary(in info)) {
                ip6PrimaryName = info.Name.ToString();
            }
        });

        if(!tunFound) {
            throw new InvalidOperationException();
        }

        if(!ip6LoopbackFound) {
            IPv6LoopbackIndex = 1;
        }

        if(ip4PrimaryName.Length > 0) {
            PrimaryName = ip4PrimaryName;
        } else if(ip6PrimaryName.Length > 0) {
            PrimaryName = ip6PrimaryName;
        } else {
            throw new UIException("Failed to detect primary adapter");
        }

        if(AppConfig.HasBypass) {
            if(PrimaryName != ip4PrimaryName) {
                // Prevent 'failed to set IP_UNICAST_IF' error loop on
                // curl -4 192.168.1.1
                throw new UIException($"Bypass requires IPv4 on primary adapter (detected as '{PrimaryName}')");
            }
            if(AppConfig.TunModeIPv6 && ip6PrimaryName != ip4PrimaryName) {
                // Prevent 'failed to set IPV6_UNICAST_IF' error loop on
                // curl -6 [fe80::1234]
                // (repeat until displayed in logs)
                throw new UIException($"Bypass + IPv6 require dual stack primary adapter (detected as '{PrimaryName}')");
            }
        }
    }

    public static void SetTunParams(bool dhcp) {
        var keyName = $@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\{{{Wintun.Guid}}}";

        NativeRegistry.DeleteTree(HKEY.HKEY_LOCAL_MACHINE, keyName, false);

        if(!dhcp) {
            var key = NativeRegistry.OpenOrCreateKey(HKEY.HKEY_LOCAL_MACHINE, keyName, true);
            try {
                NativeRegistry.SetValue(key, "EnableDHCP", 0);
                NativeRegistry.SetValue(key, "NameServer", TunDns.ToString());
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

    static bool IsIPv4Primary(ref readonly NativeAdapterInfo info) {
        return TunModeRouting.DefaultV4 != null
            && TunModeRouting.DefaultV4.AdapterIndex == info.IPv4Index
            && info.Status == IF_OPER_STATUS.IfOperStatusUp;
    }

    static bool IsIPv6Primary(ref readonly NativeAdapterInfo info) {
        return TunModeRouting.DefaultV6 != null
            && TunModeRouting.DefaultV6.AdapterIndex == info.IPv6Index
            && info.Status == IF_OPER_STATUS.IfOperStatusUp;
    }
}
