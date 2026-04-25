namespace Project.Tests;

public class NativeRoutingTests {

    [Fact]
    public void FindDefaultRoutes() {
        var routes = NativeRouting.FindDefaultRoutes();
        Assert.NotEmpty(routes);

        Assert.All(routes, r => {
            var (prefix, prefixLen) = r.Dest;
            Assert.True(prefix.Equals(NativeIPAddress.IPv4Zero) || prefix.Equals(NativeIPAddress.IPv6Zero));
            Assert.Equal(0, prefixLen);
        });

        // Not necessarily but usually so
        Assert.Contains(routes, r => r.Gateway.IsRfc1918());
    }
}
