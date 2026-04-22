namespace Project.Tests;

public class CidrTests {

    [Theory]
    [InlineData("1.2.3.4", "1.2.3.4/32")]
    [InlineData("1.2.3.4/5", "1.2.3.4/5")]
    [InlineData("2000::", "2000::/128")]
    [InlineData("2000::/123", "2000::/123")]
    [InlineData("-", "::/0")]
    public void TryParse(string text, string expectedToString) {
        var ok = CIDR.TryParse(text, out var cidr);
        Assert.Equal(text != "-", ok);
        Assert.Equal(expectedToString, cidr.ToString());
    }


    [Theory]
    [InlineData("1.2.3.4", 32)]
    [InlineData("2000::", 128)]
    public void Implicit(string ipText, int maxPrefixLen) {
        _ = NativeIPAddress.TryParse(ipText, out var ip);
        Assert.Equal(ipText + "/" + maxPrefixLen, ((CIDR)ip).ToString());
        Assert.Equal(ipText + "/1", ((CIDR)(ip, 1)).ToString());
    }
}
