namespace Project;

static class XrayConfig {
    const string LOG_LEVEL = "warning";
    const string TAG_BLACKHOLE = "blackhole";
    const string TAG_BYPASS = "bypass";
    const string TAG_DNS = "dns";

    public static readonly string FilePath = Path.Join(AppContext.BaseDirectory, "xray_config.json");

    public static void WriteFile(object outbound) {
        var outboundList = new JsonArray { outbound };
        var routingRules = new JsonArray();

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
            outboundList.AddRange(
                CreateBlackholeOutbound(),
                CreateDnsOutbound()
            );
            root["dns"] = CreateDnsModule();
        }

        if(AppConfig.HasBypassByIP) {
            outboundList.Add(CreateBypassOutbound());
        }

        PopulateRoutingRules(routingRules);

        if(routingRules.Count > 0) {
            root["routing"] = CreateRoutingModule(routingRules);
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

    static JsonObject CreateBlackholeOutbound() {
        return CreateTaggedOutbound("blackhole", TAG_BLACKHOLE);
    }

    static JsonObject CreateDnsOutbound() {
        // https://xtls.github.io/en/config/outbounds/dns.html
        var obj = CreateTaggedOutbound("dns", TAG_DNS);
        obj["settings"] = new JsonObject {
            ["nonIPQuery"] = "reject"
        };
        return obj;
    }

    static JsonObject CreateBypassOutbound() {
        var obj = CreateTaggedOutbound("freedom", TAG_BYPASS);
        if(AppConfig.TunMode) {
            obj["streamSettings"] = new JsonObject {
                ["sockopt"] = new JsonObject {
                    // https://github.com/XTLS/Xray-core/blob/v26.2.6/transport/internet/sockopt_windows.go#L36
                    // https://github.com/golang/go/blob/go1.26.0/src/net/interface.go#L169
                    // https://github.com/golang/go/blob/go1.26.0/src/net/interface_windows.go#L62
                    ["interface"] = TunModeAdapters.PrimaryName
                }
            };
        }
        return obj;
    }

    static JsonObject CreateTaggedOutbound(string protocol, string tag) {
        return new() {
            ["protocol"] = protocol,
            ["tag"] = tag
        };
    }

    static JsonObject CreateDnsModule() {
        var obj = new JsonObject {
            ["servers"] = new JsonArray {
                "https://1.1.1.1/dns-query",
                "https://9.9.9.9/dns-query",
            }
        };

        if(TunModeServerInfo.IsDomainName) {
            obj["hosts"] = new JsonObject {
                [TunModeServerInfo.Address] = TunModeServerInfo.IPList.ConvertAll(i => i.ToString())
            };
        }

        return obj;
    }

    static JsonObject CreateRoutingModule(JsonArray rules) {
        return new() {
            ["domainStrategy"] = "IPOnDemand",
            ["rules"] = rules
        };
    }

    static void PopulateRoutingRules(JsonArray rules) {
        if(AppConfig.TunMode) {
            rules.AddRange(
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
                new JsonObject {
                    ["ip"] = new JsonArray {
                        TunModeAdapters.TunDns.ToString()
                    },
                    ["port"] = 53,
                    ["outboundTag"] = TAG_DNS
                }
            );
        }
        if(AppConfig.HasBypassByIP) {
            var ipList = new JsonArray();
            if(AppConfig.BypassRU) {
                ipList.Add("geoip:ru");
            }
            if(AppConfig.BypassPrivate) {
                ipList.Add("geoip:private");
            }
            rules.Add(new JsonObject {
                ["ip"] = ipList,
                ["outboundTag"] = TAG_BYPASS
            });
        }
    }
}
