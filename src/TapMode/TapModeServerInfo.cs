namespace Project;

static class TapModeServerInfo {
    public static string Host { get; private set; } = "";
    public static bool IsDomainName { get; private set; }
    public static IReadOnlyList<NativeIPAddress> IPList { get; private set; } = [];

    public static void Refresh(string host) {
        Host = host;
        IsDomainName = false;

        if(NativeIPAddress.TryParse(host, out var ip)) {
            IPList = [ip];
        } else {
            IsDomainName = true;
            IPList = NativeDns.QueryIP(host);
        }

        if(IPList.Count < 1) {
            throw new UIException("Unknown host: " + host);
        }
    }
}
