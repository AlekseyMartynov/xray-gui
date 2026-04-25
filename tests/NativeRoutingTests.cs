namespace Project.Tests;

public class NativeRoutingTests {

    [Fact]
    public void FindRoute_DefaultV4() {
        var routes = NativeRouting.FindRoutes(NativeIPAddress.IPv4Zero, 0);
        Assert.NotEmpty(routes);

        // Not necessarily but usually so
        Assert.Contains(routes, r => r.Gateway.IsRfc1918());
    }

    [Fact]
    public void FindRoute_OnLink() {
        Assert.NotEmpty(
            NativeRouting.FindRoutes(NativeIPAddress.IPv4Loopback, 32)
        );
        Assert.NotEmpty(
            NativeRouting.FindRoutes(NativeIPAddress.IPv6Loopback, 128)
        );
        Assert.NotEmpty(
            // v4 multicast
            NativeRouting.FindRoutes(new(224), 4)
        );
        Assert.NotEmpty(
            // v6 multicast
            NativeRouting.FindRoutes(new([0xff00]), 8)
        );
    }
}
