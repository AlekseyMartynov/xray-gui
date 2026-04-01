namespace Project;

static class TunModeServerInfo {
    public static string Address { get; private set; } = "";
    public static bool IsDomainName { get; private set; }
    public static IReadOnlyList<NativeIPAddress> IPList { get; private set; } = [];

    public static void Refresh(string address) {
        Address = address;
        IsDomainName = false;

        if(NativeIPAddress.TryParse(address, out var ip)) {
            IPList = [ip];
        } else {
            IsDomainName = true;
            IPList = NativeDns.QueryIP(address);
        }

        if(IPList.Count < 1) {
            throw new UIException("Unknown address: " + address);
        }
    }
}
