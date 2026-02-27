namespace Project;

partial class XrayOutbound {
    public const string VLESS_SAMPLE = $"vless://user@host:443?{StreamSettings.SAMPLE}#remarks";

    const string ENCRYPTION_NONE = "none";

    static JsonObject FromVlessUri(Uri uri) {
        var encryption = ENCRYPTION_NONE;
        var streamSettings = new StreamSettings();

        uri.Query.ParseQueryString((key, value) => {
            if(key == nameof(encryption)) {
                encryption = value;
            } else {
                streamSettings.Set(key, value);
            }
        });

        ValidateParam(CATEGORY_QUERY_STRING, nameof(encryption), encryption, ENCRYPTION_NONE);

        streamSettings.Validate(uri.Host, true);

        var settings = new JsonObject {
            ["address"] = uri.Host,
            ["port"] = uri.Port,
            ["id"] = uri.UserInfo,
            ["encryption"] = encryption
        };

        return new JsonObject {
            ["protocol"] = "vless",
            ["settings"] = settings,
            ["streamSettings"] = streamSettings.ToJson()
        };
    }
}
