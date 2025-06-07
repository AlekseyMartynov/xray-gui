namespace Project;

static class Extensions {

    public static string Quote(this string text) {
        if(text.Contains('"')) {
            throw new NotSupportedException();
        }
        return "\"" + text + "\"";
    }

    public static bool IsIPAddress(this string host) {
        if(host.Contains(':')) {
            // IPv6 fuzzy check
            return true;
        }
        return NativeIPAddress.TryParseV4(host, out _);
    }
}
