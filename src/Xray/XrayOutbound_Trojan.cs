namespace Project;

partial class XrayOutbound {

    static JsonObject FromTrojanUri(Uri uri) {
        var address = uri.GetAddress();
        var streamSettings = new StreamSettings();
        uri.Query.ParseQueryString(streamSettings.Set);
        streamSettings.Validate(address, true);

        return new JsonObject {
            ["protocol"] = "trojan",
            ["settings"] = new JsonObject {
                ["address"] = address,
                ["port"] = uri.Port,
                ["password"] = uri.UserInfo,
            },
            ["streamSettings"] = streamSettings.ToJson()
        };
    }
}
