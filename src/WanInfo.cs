using System.Text.Json;
using System.Threading.Channels;

namespace Project;

class WanInfoEventArgs {
    public required string IP { get; init; }
    public required string CountryCode { get; init; }
}

static class WanInfo {
    static readonly IReadOnlyList<(Uri, string, string)> Services = [
        (new("https://ipwho.is?fields=ip,country_code"), "ip", "country_code"),
        (new("https://api.ip.sb/geoip"), "ip", "country_code"),
        (new("http://ip-api.com/json?fields=query,countryCode"), "query", "countryCode"),
#if DEBUG
        (new("http://example.net/test/"), "test_ip", "test_country_code"),
#endif
    ];

    static readonly Channel<CancellationToken> PendingUpdateRequests;

    static WanInfo() {
        var channelOptions = new UnboundedChannelOptions {
            SingleReader = true
        };

        PendingUpdateRequests = Channel.CreateUnbounded<CancellationToken>(channelOptions);

        Task.Run(async delegate {
            await foreach(var ct in PendingUpdateRequests.Reader.ReadAllAsync()) {
                if(ct.IsCancellationRequested) {
                    continue;
                }
                if(AppConfig.TunMode) {
                    await WhenTunDnsReadyAsync(ct);
                }
                Update(ct);
            }
        });
    }

    public static event EventHandler<WanInfoEventArgs>? Ready;

    public static void RequestUpdate(CancellationToken ct) {
        PendingUpdateRequests.Writer.TryWrite(ct);
    }

    static async Task WhenTunDnsReadyAsync(CancellationToken ct) {
        while(!ct.IsCancellationRequested) {
            try {
                if(NativeDns.QueryIP("cp.cloudflare.com", true, false, [TunModeAdapters.TunDns]).Count > 0) {
                    return;
                }
            } catch {
            }
            await Task.Yield();
        }
    }

    static void Update(CancellationToken ct) {
        using var mem = new MemoryStream();

        var ip = "";
        var countryCode = "";

        foreach(var (uri, ipField, countryCodeField) in Services) {
            if(ct.IsCancellationRequested) {
                return;
            }
            try {
                mem.Position = 0;
                mem.SetLength(0);

                var req = new NativeHttpRequest {
                    Uri = uri,
                    Timeouts = 3000,
                    Output = mem,
                };

                NativeHttpClient.SendRequest(req);

                if(!req.StatusCodeIsSuccess) {
                    continue;
                }

                var memSlice = mem.GetBuffer().AsSpan().Slice(0, (int)mem.Length);
                var reader = new Utf8JsonReader(memSlice);
                var element = JsonElement.ParseValue(ref reader);

                ip = element.GetProperty(ipField).GetString() ?? "";
                countryCode = element.GetProperty(countryCodeField).GetString() ?? "";

                if(ip.Length > 0 && countryCode.Length > 0) {
                    break;
                }
            } catch {
                continue;
            }
        }

        Ready?.Invoke(default, new() {
            IP = ip,
            CountryCode = countryCode,
        });
    }
}
