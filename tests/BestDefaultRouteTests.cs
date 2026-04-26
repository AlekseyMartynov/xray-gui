using Windows.Win32.Networking.WinSock;
using Windows.Win32.NetworkManagement.Ndis;

namespace Project.Tests;

public class BestDefaultRouteTests {
    const ulong
        LUID_EMPTY = 0,
        LUID_ETH = 0x6000001000000,
        LUID_WIFI = 0x47000002000000;

    const string
        GW_ETH_4 = "10.0.0.1",
        GW_ETH_6 = "fe80::1",
        GW_WIFI_4 = "10.0.0.2",
        GW_WIFI_6 = "fe80::2";

    [Theory]
    // Empty
    [InlineData(0, 0, 0, 0, LUID_EMPTY, "", "")]
    // Single
    [InlineData(1, 0, 0, 0, LUID_ETH, GW_ETH_4, "")]
    [InlineData(0, 0, 0, 1, LUID_WIFI, "", GW_WIFI_6)]
    // Metric wins
    [InlineData(1, 1, 1, 1, LUID_ETH, GW_ETH_4, GW_ETH_6)]
    // Dual stack wins
    [InlineData(0, 1, 1, 1, LUID_WIFI, GW_WIFI_4, GW_WIFI_6)]
    // IPv4 wins
    [InlineData(0, 1, 1, 0, LUID_WIFI, GW_WIFI_4, "")]
    public void Run(int eth4, int eth6, int wifi4, int wifi6, ulong expectedLuid, string expectedGw4, string expectedGw6) {
        var expectedHas4 = expectedGw4 != "";
        var expectedHas6 = expectedGw6 != "";

        var routes = new List<MeasuredNativeRoute>();

        if(eth4 > 0) {
            routes.Add(new() {
                Dest = (NativeIPAddress.IPv4Zero, 0),
                Gateway = IP(GW_ETH_4),
                AdapterLuid = LUID(LUID_ETH),
                TotalMetric = 25
            });
        }

        if(eth6 > 0) {
            routes.Add(new() {
                Dest = (NativeIPAddress.IPv6Zero, 0),
                Gateway = IP(GW_ETH_6),
                AdapterLuid = LUID(LUID_ETH),
                TotalMetric = 281
            });
        }

        if(wifi4 > 0) {
            routes.Add(new() {
                Dest = (NativeIPAddress.IPv4Zero, 0),
                Gateway = IP(GW_WIFI_4),
                AdapterLuid = LUID(LUID_WIFI),
                TotalMetric = 35
            });
        }

        if(wifi6 > 0) {
            routes.Add(new() {
                Dest = (NativeIPAddress.IPv6Zero, 0),
                Gateway = IP(GW_WIFI_6),
                AdapterLuid = LUID(LUID_WIFI),
                TotalMetric = 291
            });
        }

        var best = new BestDefaultRoute(routes);

        Assert.Equal(LUID(expectedLuid), best.Luid);

        Assert.Equal(expectedHas4, best.HasIPv4);
        Assert.Equal(expectedHas6, best.HasIPv6);

        Assert.Equal(expectedHas4, best.TryGetGateway(ADDRESS_FAMILY.AF_INET, out var gw4));
        Assert.Equal(expectedHas6, best.TryGetGateway(ADDRESS_FAMILY.AF_INET6, out var gw6));

        Assert.Equal(expectedHas4 ? IP(expectedGw4) : NativeIPAddress.IPv4Zero, gw4);
        Assert.Equal(expectedHas6 ? IP(expectedGw6) : NativeIPAddress.IPv6Zero, gw6);
    }

    static NativeIPAddress IP(string text) {
        Assert.True(NativeIPAddress.TryParse(text, out var ip));
        return ip;
    }

    static NET_LUID_LH LUID(ulong value) {
        return NET_LUID_LH.FromValue(value);
    }
}
