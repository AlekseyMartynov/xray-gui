namespace Project;

static class XrayConfig {
    const string LOG_LEVEL = "warning";
    const string TAG_BLACKHOLE = "blackhole";

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

        if(AppConfig.TapMode) {
            if(TapModeServerInfo.IsDomainName) {
                root["dns"] = new JsonObject {
                    ["hosts"] = new JsonObject {
                        [TapModeServerInfo.Host] = TapModeServerInfo.IPv4List.ConvertAll(i => i.ToString())
                    }
                };
                var sockopt = outbound.GetChildObject("streamSettings", "sockopt");
                sockopt["domainStrategy"] = "UseIPv4";
            }

            outboundList.Add(new() {
                ["protocol"] = "blackhole",
                ["tag"] = TAG_BLACKHOLE
            });

            root["routing"] = new JsonObject {
                ["rules"] = CreateTapModeRoutingRules()
            };
        }

        using var stream = File.OpenWrite(FilePath);
        stream.SetLength(0);
        AotFriendlyJsonSerializer.Serialize(root, stream);
    }

    static JsonObject CreateInbound() {
        var inbound = new JsonObject {
            ["protocol"] = AppConfig.TapMode ? "socks" : "http",
            ["listen"] = AppConfig.ProxyAddr,
            ["port"] = AppConfig.ProxyPort
        };

        if(AppConfig.TapMode) {
            inbound["settings"] = new JsonObject {
                ["udp"] = true

            };
            inbound["sniffing"] = new JsonObject {
                ["enabled"] = true
            };
        }

        return inbound;
    }

    static JsonArray CreateTapModeRoutingRules() {
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
            }
        ];
    }
}
