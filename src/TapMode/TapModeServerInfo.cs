namespace Project;

static class TapModeServerInfo {
    public static string Host { get; private set; } = "";
    public static bool IsDomainName { get; private set; }
    public static IReadOnlyList<NativeIPAddress> IPv4List { get; private set; } = [];

    public static void Refresh(string host) {
        Host = host;
        IsDomainName = false;

        if(host.Contains(':')) {
            IPv4List = [];
        } else if(NativeIPAddress.TryParseV4(host, out var ip4)) {
            IPv4List = [ip4];
        } else {
            IsDomainName = true;
            IPv4List = NativeDns.QueryIP(host, v6: false);
        }

        if(IPv4List.Count < 1) {
            throw new UIException("In TAP mode, server must be reachable via IPv4");
        }
    }
}
