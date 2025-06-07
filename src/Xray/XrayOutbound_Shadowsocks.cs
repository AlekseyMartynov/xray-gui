using System.Text;
using System.Web;

namespace Project;

// https://github.com/shadowsocks/shadowsocks-org/wiki/SIP002-URI-Scheme

partial class XrayOutbound {

    static JsonObject FromShadowsocksUri(Uri uri) {
        var qs = HttpUtility.ParseQueryString(uri.Query);

        var sip003 = default(SIP003);

        foreach(var key in qs.AllKeys) {
            if(key == null) {
                continue;
            }
            var value = qs[key];
            if(key == "plugin") {
                ValidateParamNotBlank(CATEGORY_QUERY_STRING, key, value);
                var remoteAddr = uri.Host;
                var remotePort = uri.Port;
                if(AppConfig.TapMode && !remoteAddr.IsIPAddress()) {
                    throw new UIException("Plugin won't be able to resolve host name, use IP address");
                }
                sip003 = new SIP003(remoteAddr, remotePort, value);
            } else {
                ThrowParamNotSupported(CATEGORY_QUERY_STRING, key, value);
            }
        }

        var userInfo = uri.UserInfo;
        var userInfoRaw = userInfo;

        var colonIndex = userInfo.IndexOf(':');

        if(colonIndex < 0) {
            userInfo = Base64UrlDecode(userInfo);
            colonIndex = userInfo.IndexOf(':');
        }

        if(colonIndex < 0) {
            ThrowParamException(CATEGORY_USERINFO_PARAM, userInfoRaw, "has no colon");
        }

        var method = userInfo.Substring(0, colonIndex);
        var password = userInfo.Substring(1 + colonIndex);

        ValidateParam(CATEGORY_USERINFO_PARAM, nameof(method), method, [
            "chacha20-ietf-poly1305",
            "aes-256-gcm"
        ]);

        var result = new JsonObject {
            ["protocol"] = "shadowsocks",
            ["settings"] = new JsonObject {
                ["servers"] = new JsonArray {
                    new JsonObject{
                        ["address"] = sip003 != null ? AppConfig.SIP003Addr : uri.Host,
                        ["port"] = sip003 != null ? AppConfig.SIP003Port : uri.Port,
                        ["method"] = method,
                        ["password"] = password
                    }
                }
            }
        };

        if(sip003 != null) {
            result[SIP003.KEY] = sip003;
        } else {
            throw new UIException("Shadowsocks without plugin is disabled");
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
