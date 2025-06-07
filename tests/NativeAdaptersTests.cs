namespace Project.Tests;

public class NativeAdaptersTests {

    [Fact]
    public void Enumerate() {
        var loopbackFound = false;

        NativeAdapters.Enumerate(info => {
            loopbackFound = loopbackFound || info.Unicast.Contains(NativeIPAddress.IPv4Loopback);
        });

        Assert.True(loopbackFound);
    }
}
