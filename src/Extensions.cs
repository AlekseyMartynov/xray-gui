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

    public static JsonObject GetChildObject(this JsonObject obj, params ReadOnlySpan<string> path) {
        foreach(var key in path) {
            if(obj.TryGetValue(key, out var value)) {
                if(value is JsonObject childObj) {
                    obj = childObj;
                } else {
                    throw new InvalidOperationException();
                }
            } else {
                var newChild = new JsonObject();
                obj[key] = newChild;
                obj = newChild;
            }
        }
        return obj;
    }
}
