namespace Project.Tests;

public class XrayOutboundTests {

    public XrayOutboundTests() {
        Assert.False(AppConfig.TapMode);
    }

    [Fact]
    public void VlessSample() {
        var uri = new Uri(XrayOutbound.VLESS_SAMPLE);
        var outbound = XrayOutbound.FromUri(uri);

        var expectedJson = """
        {
            "protocol": "vless",
            "settings": {
                "vnext": [
                    {
                        "address": "host",
                        "port": 443,
                        "users": [
                            {
                                "id": "user",
                                "encryption": "none"
                            }
                        ]
                    }
                ]
            },
            "streamSettings": {
                "security": "tls",
                "tlsSettings": {
                    "fingerprint": "chrome"
                },
                "network": "xhttp",
                "xhttpSettings": {
                    "mode": "stream-up",
                    "path": "/path"
                }
            }
        }
        """;

        AssertJson(expectedJson, outbound);
    }

    [Fact]
    public void ShadowsocksWithPlugin() {
        var uri = new Uri("ss://YWVzLTI1Ni1nY206ajEyMw==@example.net:8388?plugin=plugin1;a=1;b=2");
        var outbound = XrayOutbound.FromUri(uri);

        var sip003 = XrayOutbound.ExtractSIP003(outbound);

        Assert.NotNull(sip003);
        Assert.Equal("example.net", sip003.RemoteAddr);
        Assert.Equal(8388, sip003.RemotePort);
        Assert.Equal("plugin1", sip003.PluginName);
        Assert.Equal("a=1;b=2", sip003.PluginOptions);

        var expectedJson = """
        {
            "protocol": "shadowsocks",
            "settings": {
                "servers": [
                    {
                        "address": "127.0.0.1",
                        "port": 1984,
                        "method": "aes-256-gcm",
                        "password": "j123"
                    }
                ]
            }
        }
        """;

        AssertJson(expectedJson, outbound);
    }

    static void AssertJson(string expected, object obj) {
        Assert.Equal(
            expected.ReplaceLineEndings(),
            AotFriendlyJsonSerializer.DebugSerialize(obj).ReplaceLineEndings()
        );
    }
}
