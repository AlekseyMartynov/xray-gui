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
                "address": "host",
                "port": 443,
                "id": "user",
                "encryption": "none"
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
    [InlineData("ss")]
    [InlineData("trojan")]
    [InlineData("vless")]
    public void TlsSettings(string scheme) {
        var builder = new UriBuilder(scheme, "192.0.2.1", 443) {
            Query = String.Join('&', "type=xhttp", "path=/", "mode=stream-up")
        };

        if(scheme == "ss") {
            builder.UserName = "aes-256-gcm:abc";
        }

        {
            var error = Record.Exception(delegate {
                XrayOutbound.FromUri(builder.Uri);
            });
            if(scheme == "ss") {
                Assert.Null(error);
            } else {
                Assert.Contains("'security' must be set to 'tls'", error.Message);
            }
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
    public void HostParam() {
        var builder = new UriBuilder(XrayOutbound.VLESS_SAMPLE) {
            Host = "192.0.2.1"
        };

        var sniError = Record.Exception(delegate {
            XrayOutbound.FromUri(builder.Uri);
        });

        Assert.Contains("'sni'", sniError.Message);

        builder.Query += "&host=example.net";

        var outbound = XrayOutbound.FromUri(builder.Uri);
        var streamSettings = outbound.GetChildObject("streamSettings");
        var tlsSettings = streamSettings.GetChildObject("tlsSettings");
        var xhttpSettings = streamSettings.GetChildObject("xhttpSettings");

        Assert.Equal("example.net", tlsSettings["serverName"]);
        Assert.Equal("example.net", xhttpSettings["host"]);
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
                "address": "127.0.0.1",
                "port": 1984,
                "method": "aes-256-gcm",
                "password": "j123"
            }
        }
        """;

        AssertJson(expectedJson, outbound);
    }

    [Fact]
    public void ShadowsocksOverWebSocketNoTLS() {
        var builder = new UriBuilder("ss://aes-256-gcm:abc@192.0.2.1:8080?type=ws");

        AssertJson(
            """
            {
                "protocol": "shadowsocks",
                "settings": {
                    "address": "192.0.2.1",
                    "port": 8080,
                    "method": "aes-256-gcm",
                    "password": "abc"
                },
                "streamSettings": {
                    "network": "ws"
                },
                "mux": {
                    "enabled": true
                }
            }
            """,
            XrayOutbound.FromUri(builder.Uri)
        );

        builder.Query += '&' + String.Join('&',
            "host=example.net",
            "path=/ws"
        );

        AssertJson(
            """
            {
                "network": "ws",
                "wsSettings": {
                    "host": "example.net",
                    "path": "/ws"
                }
            }
            """,
            XrayOutbound.FromUri(builder.Uri).GetChildObject("streamSettings")
        );
    }

    static void AssertJson(string expected, object obj) {
        Assert.Equal(
            expected.ReplaceLineEndings(),
            AotFriendlyJsonSerializer.DebugSerialize(obj).ReplaceLineEndings()
        );
    }
}
