namespace Project.Tests;

public class NativeRoutingTests {

    [Fact]
    public void FindRoute_DefaultV4() {
        var route = NativeRouting.FindRoute(NativeIPAddress.IPv4Zero, 0);
        Assert.NotNull(route);

        // Not necessarily but usually so
        var gateway = route.Gateway.ToString();
        Assert.True(gateway.StartsWith("10.") || gateway.StartsWith("172.") || gateway.StartsWith("192."));
    }

    [Fact]
    public void FindRoute_OnLink() {
        Assert.NotNull(
            NativeRouting.FindRoute(NativeIPAddress.IPv4Loopback, 32)
        );
        Assert.NotNull(
            NativeRouting.FindRoute(NativeIPAddress.IPv6Loopback, 128)
        );
        Assert.NotNull(
            // v4 multicast
            NativeRouting.FindRoute(new(224), 4)
        );
        Assert.NotNull(
            // v6 multicast
            NativeRouting.FindRoute(new([0xff00]), 8)
        );
    }
}
