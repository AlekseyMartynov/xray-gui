using Windows.Win32;
using Windows.Win32.NetworkManagement.Ndis;
using Windows.Win32.System.Registry;

namespace Project;

static class TapModeAdapters {
    // https://github.com/eycorsican/go-tun2socks/blob/v1.16.11/tun/tun_windows.go#L23
    public const string REQUIRED_TAP_COMPONENT_ID = "tap0901";

    const int TAP_PREFIX_LEN = 24;

    // https://github.com/eycorsican/go-tun2socks/blob/v1.16.11/cmd/tun2socks/main.go#L92-L94
    public static readonly NativeIPAddress
        TapPrefix = new(10, 255, 0, 0),
        TapMask = new(uint.MaxValue >> (32 - TAP_PREFIX_LEN)),
        TapAddr = TapPrefix | new NativeIPAddress(0, 0, 0, 2),
        TapGateway = TapPrefix | new NativeIPAddress(0, 0, 0, 1);

    // https://github.com/Jigsaw-Code/outline-apps/blob/manager_windows/v1.17.2/client/electron/go_vpn_tunnel.ts#L35
    public static readonly string TapDns = "1.1.1.1,9.9.9.9";

    static readonly List<Guid> RegistryTapGuidList = [];

    public static string TapName { get; private set; } = "";
    public static Guid TapGuid { get; private set; }
    public static uint TapIndex { get; private set; }

    public static uint IPv6LoopbackIndex { get; private set; }

    public static void Refresh() {
        TapName = "";
        TapGuid = default;
        TapIndex = default;
        IPv6LoopbackIndex = default;

        ReadRegistryTapGuidList();

        var tapFound = false;
        var ip6LoopbackFound = false;

        NativeAdapters.Enumerate(info => {
            if(!tapFound && IsGoodTap(info)) {
                TapName = info.Name.ToString();
                TapGuid = info.Guid;
                TapIndex = info.IPv4Index;
                tapFound = true;
            }
            if(!ip6LoopbackFound && IsIPv6Loopback(info)) {
                IPv6LoopbackIndex = info.IPv6Index;
                ip6LoopbackFound = true;
            }
        });

        if(!tapFound) {
            throw new UIException(
                "No free TAP adapters found" + "\n" +
                "Install from github.com/OpenVPN/tap-windows6" + "\n" +
                "Command: devcon install OemVista.inf " + REQUIRED_TAP_COMPONENT_ID
            );
        }

        if(!ip6LoopbackFound) {
            IPv6LoopbackIndex = 1;
        }
    }

    public static void SetTapParams(bool dhcp) {
        var keyName = $@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\{{{TapGuid}}}";

        NativeRegistry.DeleteTree(HKEY.HKEY_LOCAL_MACHINE, keyName, false);

        if(!dhcp) {
            var key = NativeRegistry.OpenOrCreateKey(HKEY.HKEY_LOCAL_MACHINE, keyName, true);
            try {
                NativeRegistry.SetValue(key, "EnableDHCP", 0);
                NativeRegistry.SetValue(key, "NameServer", TapDns);
            } finally {
                PInvoke.RegCloseKey(key);
            }
            NativeUnicastAddressTable.AssignStatic(TapIndex, TapAddr, TAP_PREFIX_LEN);
        }
    }

    static void ReadRegistryTapGuidList() {
        RegistryTapGuidList.Clear();
        var netKey = NativeRegistry.OpenOrCreateKey(HKEY.HKEY_LOCAL_MACHINE, @"SYSTEM\CurrentControlSet\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}");
        try {
            foreach(var subKeyName in NativeRegistry.GetSubKeyNames(netKey)) {
                if(subKeyName.AsSpan().ContainsAnyExceptInRange('0', '9')) {
                    continue;
                }
                var componentId = NativeRegistry.GetValue(netKey, subKeyName, "ComponentId") as string;
                if(componentId != REQUIRED_TAP_COMPONENT_ID) {
                    continue;
                }
                if(NativeRegistry.GetValue(netKey, subKeyName, "NetCfgInstanceId") is string id) {
                    RegistryTapGuidList.Add(Guid.Parse(id));
                }
            }
        } finally {
            PInvoke.RegCloseKey(netKey);
        }
    }

    static bool IsGoodTap(NativeAdapterInfo info) {
        return info.IPv4Enabled
            && info.Status == IF_OPER_STATUS.IfOperStatusDown
            && info.Description.StartsWith("TAP-Windows Adapter ")
            && RegistryTapGuidList.Contains(info.Guid);
    }

    static bool IsIPv6Loopback(NativeAdapterInfo info) {
        return info.IPv6Enabled
            && info.Unicast.Contains(NativeIPAddress.IPv6Loopback);
    }
}
