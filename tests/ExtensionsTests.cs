namespace Project.Tests;

public class ExtensionsTests {

    [Fact]
    public void ParseQueryString_IgnoreEmptyKeys() {
        var count = 0;
        "?&=&".ParseQueryString(delegate {
            count++;
        });
        Assert.Equal(0, count);
    }
}
