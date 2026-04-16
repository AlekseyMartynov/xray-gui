namespace Project;

static class Extensions {

    public static string Quote(this string text) {
        if(text.Contains('"')) {
            throw new NotSupportedException();
        }
        return "\"" + text + "\"";
    }

    public static ReadOnlySpan<T> Truncate<T>(this ReadOnlySpan<T> span, int maxLen) {
        if(span.Length <= maxLen) {
            return span;
        }
        return span.Slice(0, maxLen);
    }

    public static bool TrySplit(this ReadOnlySpan<char> text, char separator, out ReadOnlySpan<char> left, out ReadOnlySpan<char> right) {
        var index = text.IndexOf(separator);
        if(index < 0) {
            left = text;
            right = default;
            return false;
        } else {
            left = text.Slice(0, index);
            right = text.Slice(1 + index);
            return true;
        }
    }

    public static bool TrySplit(this string text, char separator, out string left, out string right) {
        var index = text.IndexOf(separator);
        if(index < 0) {
            left = text;
            right = "";
            return false;
        } else {
            left = text.Substring(0, index);
            right = text.Substring(1 + index);
            return true;
        }
    }

    public static void ParseQueryString(this string text, Action<string, string> callback) {
        var span = text.AsSpan().TrimStart('?');
        foreach(var r in span.Split('&')) {
            span[r].TrySplit('=', out var name, out var value);
            if(name.IsEmpty) {
                continue;
            }
            callback(
                Uri.UnescapeDataString(name),
                Uri.UnescapeDataString(value)
            );
        }
    }

    public static JsonObject GetChildObject(this JsonObject obj, params ReadOnlySpan<string> path) {
        return GetChildObject(obj, path, false);
    }

    public static JsonObject EnsureChildObject(this JsonObject obj, params ReadOnlySpan<string> path) {
        return GetChildObject(obj, path, true);
    }

    static JsonObject GetChildObject(JsonObject obj, ReadOnlySpan<string> path, bool create) {
        foreach(var key in path) {
            if(obj.TryGetValue(key, out var value)) {
                if(value is JsonObject childObj) {
                    obj = childObj;
                } else {
                    throw new InvalidOperationException();
                }
            } else {
                if(create) {
                    var newChild = new JsonObject();
                    obj[key] = newChild;
                    obj = newChild;
                } else {
                    throw new KeyNotFoundException();
                }
            }
        }
        return obj;
    }

    public static IReadOnlyList<T> ConvertAll<S, T>(this IReadOnlyList<S> list, Converter<S, T> converter) {
        var count = list.Count;
        if(count < 1) {
            return [];
        }
        var result = new T[count];
        for(var i = 0; i < count; i++) {
            result[i] = converter(list[i]);
        }
        return result;
    }

    public static string GetAddress(this Uri uri) {
        // Inspired by https://github.com/XTLS/Xray-core/blob/v26.3.27/common/net/address.go#L80
        var host = uri.Host;
        if(host.StartsWith('[')) {
            return host[1..^1];
        }
        return host;
    }
}
