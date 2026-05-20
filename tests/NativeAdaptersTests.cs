namespace Project.Tests;

public class NativeAdaptersTests {

    [Fact]
    public void Enumerate() {
        var loopbackFound = false;
        var fe80Cidr = new CIDR(new([0xfe80]), 64);

        NativeAdapters.Enumerate((ref readonly NativeAdapterInfo info) => {
            loopbackFound = loopbackFound || info.Unicast.Contains(NativeIPAddress.IPv4Loopback);
            if(info.IPv6Enabled && !info.Unicast.Contains(NativeIPAddress.IPv6Loopback)) {
                Assert.True(info.Nets.Contains(fe80Cidr));
            }
        });

        Assert.True(loopbackFound);
    }
}
