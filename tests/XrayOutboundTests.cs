namespace Project.Tests;

public class XrayOutboundTests {

    public XrayOutboundTests() {
        Assert.False(AppConfig.TunMode);
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

    [Theory]
    [InlineData("trojan")]
    [InlineData("vless")]
    public void TlsSettings(string scheme) {
        var builder = new UriBuilder(scheme, "192.0.2.1", 443) {
            Query = String.Join('&', "type=xhttp", "path=/", "mode=stream-up")
        };

        {
            var error = Record.Exception(delegate {
                XrayOutbound.FromUri(builder.Uri);
            });
            Assert.Contains("'security' must be set to 'tls'", error.Message);
        }

        builder.Query += "&security=tls";

        {
            var error = Record.Exception(delegate {
                XrayOutbound.FromUri(builder.Uri);
            });
            Assert.Contains("'fp' must not be blank", error.Message);
        }

        builder.Query += "&fp=android";

        {
            var error = Record.Exception(delegate {
                XrayOutbound.FromUri(builder.Uri);
            });
            Assert.Contains("'sni' must not be blank", error.Message);
        }

        builder.Query += '&' + String.Join('&',
            "sni=example.net",
            "allowInsecure=1",
            "alpn=h3,h2",
            "pcs=abc"
        );

        var outbound = XrayOutbound.FromUri(builder.Uri);
        var tlsSettings = outbound.GetChildObject("streamSettings", "tlsSettings");

        var expectedTlsSettingsJson = """
        {
            "fingerprint": "android",
            "allowInsecure": true,
            "alpn": [
                "h3",
                "h2"
            ],
            "pinnedPeerCertSha256": "abc",
            "serverName": "example.net"
        }
        """;

        AssertJson(expectedTlsSettingsJson, tlsSettings);
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
