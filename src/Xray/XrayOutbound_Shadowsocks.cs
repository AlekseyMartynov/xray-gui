using System.Text;

namespace Project;

// https://github.com/shadowsocks/shadowsocks-org/wiki/SIP002-URI-Scheme

partial class XrayOutbound {

    static JsonObject FromShadowsocksUri(Uri uri) {
        var sip003 = default(SIP003);
        var streamSettings = default(StreamSettings);

        uri.Query.ParseQueryString((key, value) => {
            if(key == "plugin") {
                ValidateParamNotBlank(CATEGORY_QUERY_STRING, key, value);
                var remoteAddr = uri.Host;
                var remotePort = uri.Port;
                if(AppConfig.TunMode && !NativeIPAddress.TryParse(remoteAddr, out _)) {
                    throw new UIException("Plugin won't be able to resolve host name, use IP address");
                }
                sip003 = new SIP003(remoteAddr, remotePort, value);
            } else {
                streamSettings ??= new();
                streamSettings.Set(key, value);
            }
        });

        if(sip003 != null && streamSettings != null) {
            throw new InvalidOperationException();
        }

        var userInfo = uri.UserInfo;

        if(!userInfo.TrySplit(':', out var method, out var password)) {
            if(!Base64UrlDecode(userInfo).TrySplit(':', out method, out password)) {
                ThrowParamException(CATEGORY_USERINFO_PARAM, userInfo, "has no colon");
            }
        }

        var isGoodMethodAlgo = method.StartsWith("2022-blake3-") || method.StartsWith("aes-") || method.Contains("chacha20-");
        var isGoodMethodScheme = method.EndsWith("-gcm") || method.EndsWith("-poly1305");

        if(!isGoodMethodAlgo || !isGoodMethodScheme) {
            ThrowParamNotSupported(CATEGORY_USERINFO_PARAM, nameof(method), method);
        }

        var result = new JsonObject {
            ["protocol"] = "shadowsocks",
            ["settings"] = new JsonObject {
                ["address"] = sip003 != null ? AppConfig.SIP003Addr : uri.Host,
                ["port"] = sip003 != null ? AppConfig.SIP003Port : uri.Port,
                ["method"] = method,
                ["password"] = password
            }
        };

        if(sip003 != null) {
            result[SIP003.KEY] = sip003;
        } else if(streamSettings != null) {
            streamSettings.Validate(uri.Host, false);
            result["streamSettings"] = streamSettings.ToJson();
        } else {
            throw new UIException("Raw Shadowsocks is disabled");
        }

        return result;
    }

    static string Base64UrlDecode(string text) {
        text = text
            .Replace('-', '+')
            .Replace('_', '/');

        var requiredLen = ((text.Length + 3) / 4) * 4;
        text = text.PadRight(requiredLen, '=');

        return Encoding.ASCII.GetString(Convert.FromBase64String(text));
    }
}
