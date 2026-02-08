namespace Project;

partial class XrayOutbound {
    public const string VLESS_SAMPLE = $"vless://user@host:443?type={NETWORK_TYPE_XHTTP}&path=/path&mode={XHTTP_MODE_STREAM_UP}&security={SECURITY_TLS}&fp=chrome#remarks";

    const string ENCRYPTION_NONE = "none";
    const string SECURITY_TLS = "tls";
    const string NETWORK_TYPE_XHTTP = "xhttp";
    const string XHTTP_MODE_PACKET_UP = "packet-up";
    const string XHTTP_MODE_STREAM_UP = "stream-up";

    static JsonObject FromVlessUri(Uri uri) {
        var allowInsecure = "0";
        var encryption = ENCRYPTION_NONE;
        var fp = "";
        var mode = "";
        var path = "";
        var pcs = "";
        var security = "";
        var sni = "";
        var type = "";

        foreach(var (key, value) in uri.Query.ParseQueryString()) {
            switch(key) {
                case nameof(allowInsecure):
                    allowInsecure = value;
                    break;
                case nameof(encryption):
                    encryption = value;
                    break;
                case nameof(fp):
                    fp = value;
                    break;
                case nameof(mode):
                    mode = value;
                    break;
                case nameof(path):
                    path = value;
                    break;
                case nameof(pcs):
                    pcs = value;
                    break;
                case nameof(security):
                    security = value;
                    break;
                case nameof(sni):
                    sni = value;
                    break;
                case nameof(type):
                    type = value;
                    break;
                default:
                    ThrowParamNotSupported(CATEGORY_QUERY_STRING, key, value);
                    break;
            }
        }

        ValidateParam(CATEGORY_QUERY_STRING, nameof(allowInsecure), allowInsecure, ["0", "1"]);
        ValidateParam(CATEGORY_QUERY_STRING, nameof(encryption), encryption, ENCRYPTION_NONE);
        ValidateParam(CATEGORY_QUERY_STRING, nameof(security), security, SECURITY_TLS);
        ValidateParamNotBlank(CATEGORY_QUERY_STRING, nameof(fp), fp);

        if(NativeIPAddress.TryParse(uri.Host, out _)) {
            ValidateParamNotBlank(CATEGORY_QUERY_STRING, nameof(sni), sni);
        }

        var vnext = new JsonArray {
            new JsonObject {
                ["address"] =  uri.Host,
                ["port"] = uri.Port,
                ["users"] = new JsonArray {
                    new JsonObject {
                        ["id"] = uri.UserInfo,
                        ["encryption"] = encryption
                    }
                }
            }
        };

        var tlsSettings = new JsonObject {
            ["fingerprint"] = fp
        };

        if(!String.IsNullOrWhiteSpace(sni)) {
            tlsSettings["serverName"] = sni;
        }

        if(allowInsecure == "1") {
            tlsSettings["allowInsecure"] = true;
        }

        if(!String.IsNullOrWhiteSpace(pcs)) {
            tlsSettings["pinnedPeerCertSha256"] = pcs;
        }

        var streamSettings = new JsonObject {
            ["security"] = security,
            ["tlsSettings"] = tlsSettings
        };

        if(type == NETWORK_TYPE_XHTTP) {
            ValidateParam(CATEGORY_QUERY_STRING, nameof(mode), mode, [XHTTP_MODE_PACKET_UP, XHTTP_MODE_STREAM_UP]);
            ValidateParamNotBlank(CATEGORY_QUERY_STRING, nameof(path), path);

            streamSettings["network"] = type;
            streamSettings["xhttpSettings"] = new JsonObject {
                ["mode"] = mode,
                ["path"] = path
            };
        } else {
            ThrowParamNotSupported(CATEGORY_QUERY_STRING, nameof(type), type);
        }

        return new JsonObject {
            ["protocol"] = "vless",
            ["settings"] = new JsonObject {
                ["vnext"] = vnext
            },
            ["streamSettings"] = streamSettings
        };
    }
}
