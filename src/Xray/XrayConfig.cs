namespace Project;

static class XrayConfig {
    const string LOG_LEVEL = "warning";
    const string TAG_BLACKHOLE = "blackhole";
    const string TAG_DNS = "dns";

    public static readonly string FilePath = Path.Join(AppContext.BaseDirectory, "xray_config.json");

    public static void WriteFile(JsonObject outbound) {
        var outboundList = new List<JsonObject> { outbound };

        var root = new JsonObject {
            ["log"] = new JsonObject {
                ["loglevel"] = LOG_LEVEL
            },
            ["inbounds"] = new JsonArray {
                CreateInbound()
            },
            ["outbounds"] = outboundList
        };

        if(AppConfig.TunMode) {
            var dns = new JsonObject {
                ["servers"] = new JsonArray {
                    "https://1.1.1.1/dns-query",
                    "https://9.9.9.9/dns-query",
                }
            };

            root["dns"] = dns;

            if(TunModeServerInfo.IsDomainName) {
                dns["hosts"] = new JsonObject {
                    [TunModeServerInfo.Host] = TunModeServerInfo.IPList.ConvertAll(i => i.ToString())
                };
                var sockopt = outbound.GetChildObject("streamSettings", "sockopt");
                sockopt["domainStrategy"] = "UseIP";
            }

            outboundList.Add(new() {
                ["protocol"] = "blackhole",
                ["tag"] = TAG_BLACKHOLE
            });

            // https://xtls.github.io/en/config/outbounds/dns.html
            outboundList.Add(new() {
                ["protocol"] = "dns",
                ["settings"] = new JsonObject {
                    ["nonIPQuery"] = "reject"
                },
                ["tag"] = TAG_DNS
            });

            root["routing"] = new JsonObject {
                ["rules"] = CreateTunModeRoutingRules()
            };
        }

        using var stream = File.OpenWrite(FilePath);
        stream.SetLength(0);
        AotFriendlyJsonSerializer.Serialize(root, stream);
    }

    static JsonObject CreateInbound() {
        if(AppConfig.TunMode) {
            return new() {
                ["protocol"] = "tun",
                ["settings"] = new JsonObject {
                    ["name"] = Wintun.Name
                },
                ["sniffing"] = new JsonObject {
                    ["enabled"] = true
                }
            };
        } else {
            return new() {
                ["protocol"] = "http",
                ["listen"] = AppConfig.ProxyAddr,
                ["port"] = AppConfig.ProxyPort
            };
        }
    }

    static JsonArray CreateTunModeRoutingRules() {
        return [
            new JsonObject {
                // Disable QUIC – no benefit when proxied
                ["network"] = "udp",
                ["port"] = 443,
                ["outboundTag"] = TAG_BLACKHOLE
            },
            new JsonObject {
                ["protocol"] = new JsonArray { "bittorrent" },
                ["outboundTag"] = TAG_BLACKHOLE
            },
            new JsonObject{
                ["ip"] = new JsonArray {
                    TunModeAdapters.TunDns.ToString()
                },
                ["port"] = 53,
                ["outboundTag"] = TAG_DNS
            }
        ];
    }
}
