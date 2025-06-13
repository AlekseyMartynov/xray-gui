namespace Project;

static class TapModeServerInfo {
    public static string Host { get; private set; } = "";
    public static bool IsDomainName { get; private set; }
    public static IReadOnlyList<NativeIPAddress> IPv4List { get; private set; } = [];

    public static void Refresh(string host) {
        Host = host;
        IsDomainName = false;

        if(NativeIPAddress.TryParse(host, out var ip)) {
            IPv4List = ip.IsIPv4() ? [ip] : [];
        } else {
            IsDomainName = true;
            IPv4List = NativeDns.QueryIP(host, v6: false);
        }

        if(IPv4List.Count < 1) {
            throw new UIException("In TAP mode, server must be reachable via IPv4");
        }
    }
}
