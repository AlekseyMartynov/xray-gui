namespace Project;

partial class XrayOutbound {
    public const string VLESS_SAMPLE = $"vless://user@host:443?{StreamSettings.SAMPLE}#remarks";

    const string ENCRYPTION_NONE = "none";
    const string FLOW_VISION = "xtls-rprx-vision";

    static JsonObject FromVlessUri(Uri uri) {
        var encryption = ENCRYPTION_NONE;
        var flow = "";
        var streamSettings = new StreamSettings();

        uri.Query.ParseQueryString((key, value) => {
            if(key == nameof(encryption)) {
                encryption = value;
            } else if(key == nameof(flow)) {
                flow = value;
            } else {
                streamSettings.Set(key, value);
            }
        });

        if(encryption != ENCRYPTION_NONE && !encryption.StartsWith("mlkem768x25519plus.")) {
            ThrowParamNotSupported(CATEGORY_QUERY_STRING, nameof(encryption), encryption);
        }

        ValidateParam(CATEGORY_QUERY_STRING, nameof(flow), flow, ["", FLOW_VISION]);


        streamSettings.Validate(uri.Host, encryption == ENCRYPTION_NONE);

        var settings = new JsonObject {
            ["address"] = uri.Host,
            ["port"] = uri.Port,
            ["id"] = uri.UserInfo,
            ["encryption"] = encryption
        };

        if(flow != "") {
            settings["flow"] = flow;
        }

        return new JsonObject {
            ["protocol"] = "vless",
            ["settings"] = settings,
            ["streamSettings"] = streamSettings.ToJson()
        };
    }
}
