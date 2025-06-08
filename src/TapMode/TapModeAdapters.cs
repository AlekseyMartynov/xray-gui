using Microsoft.Win32;
using System.Security;
using Windows.Win32.NetworkManagement.Ndis;

namespace Project;

static class TapModeAdapters {
    // https://github.com/eycorsican/go-tun2socks/blob/v1.16.11/tun/tun_windows.go#L23
    const string REQUIRED_TAP_COMPONENT_ID = "tap0901";

    // https://github.com/eycorsican/go-tun2socks/blob/v1.16.11/cmd/tun2socks/main.go#L93
    public static readonly NativeIPAddress TapGateway = new(10, 255, 0, 1);

    // https://github.com/Jigsaw-Code/outline-apps/blob/manager_windows/v1.17.2/client/electron/go_vpn_tunnel.ts#L35
    public static readonly string TapDns = "1.1.1.1,9.9.9.9";

    static readonly List<Guid> RegistryTapGuidList = [];

    public static string TapName { get; private set; } = "";
    public static uint TapIndex { get; private set; }

    public static uint IPv6LoopbackIndex { get; private set; }

    public static void Refresh() {
        TapName = "";
        TapIndex = default;
        IPv6LoopbackIndex = default;

        ReadRegistryTapGuidList();

        var tapFound = false;
        var ip6LoopbackFound = false;

        NativeAdapters.Enumerate(info => {
            if(!tapFound && IsGoodTap(info)) {
                TapName = info.Name.ToString();
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

    static void ReadRegistryTapGuidList() {
        RegistryTapGuidList.Clear();
        using var netKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}");
        if(netKey is null) {
            return;
        }
        foreach(var subKeyName in netKey.GetSubKeyNames()) {
            try {
                using var subKey = netKey.OpenSubKey(subKeyName);
                if(subKey is null) {
                    continue;
                }
                var componentId = subKey.GetValue("ComponentId") as string;
                if(componentId != REQUIRED_TAP_COMPONENT_ID) {
                    continue;
                }
                if(subKey.GetValue("NetCfgInstanceId") is string id) {
                    RegistryTapGuidList.Add(Guid.Parse(id));
                }
            } catch(SecurityException) {
                // Ignore
            }
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
