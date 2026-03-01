namespace Project;

partial class XrayOutbound {
    public const string TYPE_XHTTP = "xhttp";
    public const string TYPE_WS = "ws";

    class StreamSettings {
        public const string SAMPLE = $"type={TYPE_XHTTP}&path=/path&mode={MODE_STREAM_UP}&security={SECURITY_TLS}&fp=chrome";

        const string MODE_PACKET_UP = "packet-up";
        const string MODE_STREAM_UP = "stream-up";
        const string SECURITY_TLS = "tls";

        // WARNING names below are used as nameof
        string allowInsecure = "0";
        string alpn = "";
        string fp = "";
        string host = "";
        string mode = "";
        string path = "";
        string pcs = "";
        string security = "";
        string sni = "";
        string type = "";

        public void Set(string key, string value) {
            switch(key) {
                case nameof(allowInsecure):
                    allowInsecure = value;
                    break;
                case nameof(alpn):
                    alpn = value;
                    break;
                case nameof(fp):
                    fp = value;
                    break;
                case nameof(host):
                    host = value;
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

        public void Validate(string uriHost, bool requireTLS) {
            if(type == TYPE_XHTTP) {
                ValidateParamNotBlank(CATEGORY_QUERY_STRING, nameof(path), path);
                ValidateParam(CATEGORY_QUERY_STRING, nameof(mode), mode, [MODE_PACKET_UP, MODE_STREAM_UP]);
            } else if(type != TYPE_WS) {
                ThrowParamNotSupported(CATEGORY_QUERY_STRING, nameof(type), type);
            }

            ValidateParam(CATEGORY_QUERY_STRING, nameof(security), security, requireTLS
                ? [SECURITY_TLS]
                : [SECURITY_TLS, ""]
            );

            if(security == SECURITY_TLS) {
                ValidateParam(CATEGORY_QUERY_STRING, nameof(allowInsecure), allowInsecure, ["0", "1"]);
                ValidateParamNotBlank(CATEGORY_QUERY_STRING, nameof(fp), fp);

                if(String.IsNullOrWhiteSpace(host) && NativeIPAddress.TryParse(uriHost, out _)) {
                    ValidateParamNotBlank(CATEGORY_QUERY_STRING, nameof(sni), sni);
                }
            }
        }

        public JsonObject ToJson() {
            var result = new JsonObject();

            if(security != "") {
                result["security"] = security;
            }

            if(security == SECURITY_TLS) {
                var tlsSettings = new JsonObject {
                    ["fingerprint"] = fp
                };

                if(allowInsecure == "1") {
                    tlsSettings["allowInsecure"] = true;
                }

                if(!String.IsNullOrWhiteSpace(alpn)) {
                    tlsSettings["alpn"] = alpn.Split(',');
                }

                if(!String.IsNullOrWhiteSpace(pcs)) {
                    tlsSettings["pinnedPeerCertSha256"] = pcs;
                }

                foreach(var candidate in (ReadOnlySpan<string>)[host, sni]) {
                    if(!String.IsNullOrWhiteSpace(candidate)) {
                        tlsSettings["serverName"] = candidate;
                    }
                }

                result["tlsSettings"] = tlsSettings;
            }

            result["network"] = type;

            var networkSettings = new JsonObject();

            if(type == TYPE_XHTTP) {
                if(!String.IsNullOrWhiteSpace(host)) {
                    networkSettings["host"] = host;
                }
                networkSettings["mode"] = mode;
                networkSettings["path"] = path;
            } else if(type == TYPE_WS) {
                if(!String.IsNullOrWhiteSpace(host)) {
                    networkSettings["host"] = host;
                }
                if(!String.IsNullOrWhiteSpace(path)) {
                    networkSettings["path"] = path;
                }
            }

            if(networkSettings.Count > 0) {
                result[type + "Settings"] = networkSettings;
            }

            if(TunModeServerInfo.IsDomainName) {
                result["sockopt"] = new JsonObject {
                    ["domainStrategy"] = "UseIP"
                };
            }

            return result;
        }
    }
}
