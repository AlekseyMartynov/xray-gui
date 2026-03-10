namespace Project;

partial class XrayOutbound {
    static JsonObject FromHttpsUri(Uri uri) {
        var streamSettings = new StreamSettings();
        streamSettings.Set("security", "tls"); // implied by https

        uri.Query.ParseQueryString((key, value) => {
            if(key == "type") {
                ValidateParam(CATEGORY_QUERY_STRING, key, value, TYPE_RAW);
            }
            streamSettings.Set(key, value);
        });

        _ = uri.UserInfo.TrySplit(':', out var user, out var pass);

        streamSettings.Validate(uri.Host, true);

        return new JsonObject {
            ["protocol"] = "http",
            ["settings"] = new JsonObject {
                ["address"] = uri.Host,
                ["port"] = uri.Port,
                ["user"] = user,
                ["pass"] = pass,
            },
            ["streamSettings"] = streamSettings.ToJson()
        };
    }
}
