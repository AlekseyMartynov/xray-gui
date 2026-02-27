namespace Project;

partial class XrayOutbound {

    static JsonObject FromTrojanUri(Uri uri) {
        var streamSettings = new StreamSettings();
        uri.Query.ParseQueryString(streamSettings.Set);
        streamSettings.Validate(uri.Host);

        return new JsonObject {
            ["protocol"] = "trojan",
            ["settings"] = new JsonObject {
                ["address"] = uri.Host,
                ["port"] = uri.Port,
                ["password"] = uri.UserInfo,
            },
            ["streamSettings"] = streamSettings.ToJson()
        };
    }
}
