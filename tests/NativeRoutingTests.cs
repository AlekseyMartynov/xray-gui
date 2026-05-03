namespace Project.Tests;

public class NativeRoutingTests {

    private static readonly string[] ProxyEnvVarNames = [
        "HTTP_PROXY",
        "http_proxy",
        "HTTPS_PROXY",
        "https_proxy",
    ];

    [Fact]
    public void FindDefaultRoutes() {
        var routes = NativeRouting.FindDefaultRoutes();

        if (routes.Count == 0) {
            // Some CI/containerized environments route network traffic through a
            // system-wide proxy and do not expose a default route to the guest.
            // In that case, assert proxy env vars are present so the reason for
            // skipping route-specific assertions is explicit in test output.
            var hasProxyEnvVar = ProxyEnvVarNames.Any(name =>
                !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name)));

            Assert.True(hasProxyEnvVar,
                "No default routes were found; expected HTTP(S)_PROXY env vars to explain proxy-routed networking.");
            return;
        }

        Assert.All(routes, r => {
            var (prefix, prefixLen) = r.Dest;
            Assert.True(prefix.Equals(NativeIPAddress.IPv4Zero) || prefix.Equals(NativeIPAddress.IPv6Zero));
            Assert.Equal(0, prefixLen);
        });

        // Not necessarily but usually so
        Assert.Contains(routes, r => r.Gateway.IsRfc1918());
    }
}
