namespace Project.Tests;

public class NativeIPAddressTests {

    [Theory]
    [InlineData("1.2.3.4")]
    [InlineData("101.102.103.104")]
    [InlineData("1:2:3:4:50:60:70:80")]
    [InlineData("1::8")]
    [InlineData("1fff:2fff:3fff:4fff:5fff:6fff:7fff:8fff")]
    public void Roundtrip(string text) {
        Assert.True(NativeIPAddress.TryParse(text, out var ip));
        Assert.Equal(text, ip.ToString());
        Assert.Equal(text, ip.ToIPAddress().ToString());
    }

    [Theory]
    [InlineData("1.2.3")]
    [InlineData("1.2.3.444")]
    [InlineData("1.2.3.4.5")]
    [InlineData("1:2:3:4:5:6:7")]
    [InlineData("1:2:3:4:5:6:7:FFFFF")]
    [InlineData("1:2:3:4:5:6:7:8:9")]
    public void Negative(string text) {
        Assert.False(NativeIPAddress.TryParse(text, out _));
    }

    [Fact]
    public void Equality() {
        var list = new[] {
            new NativeIPAddress(1, 2, 3, 4),
            new NativeIPAddress(1, 2, 3, 4),
            new NativeIPAddress(1, 2, 3, 5),
        };

        var distinct = list.Distinct();

        Assert.Equal(2, distinct.Count());
    }

    [Fact]
    public void FromNetworkOrder() {
        var ip = new NativeIPAddress([1, 2, 3, 4, 4, 3, 2, 1]);
        var networkOrderHextets = NativeUtils.Cast<NativeIPAddress, ushort>(ref ip);
        var ip2 = new NativeIPAddress(networkOrderHextets, true);
        Assert.Equal(ip, ip2);
    }

    [Fact]
    public void ParseSlice() {
        var text = "123.0.0.123";
        var slice = text.AsSpan().Slice(2, 7);
        Assert.True(NativeIPAddress.TryParse(slice, out var ip));
        Assert.Equal("3.0.0.1", ip.ToString());
    }
}
