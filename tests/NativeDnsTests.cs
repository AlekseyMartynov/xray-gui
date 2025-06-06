namespace Project.Tests;

public class NativeDnsTests {

    [Fact]
    public void Localhost() {
        var ipList = NativeDns.QueryIPv4("localhost");
        Assert.Contains(new NativeIPAddress(127, 0, 0, 1), ipList);
    }

    [Fact]
    public void Invalid() {
        var ipList = NativeDns.QueryIPv4("example.invalid");
        Assert.Empty(ipList);
    }
}
