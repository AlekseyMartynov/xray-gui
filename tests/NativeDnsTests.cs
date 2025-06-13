namespace Project.Tests;

public class NativeDnsTests {

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Localhost(bool v4, bool v6) {
        var ipList = NativeDns.QueryIP("localhost", v4, v6);
        Assert.Equal(v4, ipList.Contains(NativeIPAddress.IPv4Loopback));
        Assert.Equal(v6, ipList.Contains(NativeIPAddress.IPv6Loopback));
    }

    [Fact]
    public void Invalid() {
        var ipList = NativeDns.QueryIP("example.invalid");
        Assert.Empty(ipList);
    }
}
