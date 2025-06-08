namespace Project.Tests;

public class NativeIcmpTests {

    [Fact]
    public void Ping() {
        using var icmp = new NativeIcmp();
        Assert.True(icmp.Ping(NativeIPAddress.IPv4Loopback));
    }
}
