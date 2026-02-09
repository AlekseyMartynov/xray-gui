namespace Project;

partial class XrayOutbound {

    class StreamSettings {
        public const string SAMPLE = $"type={TYPE_XHTTP}&path=/path&mode={MODE_STREAM_UP}&security={SECURITY_TLS}&fp=chrome";

        const string TYPE_XHTTP = "xhttp";
        const string MODE_PACKET_UP = "packet-up";
        const string MODE_STREAM_UP = "stream-up";
        const string SECURITY_TLS = "tls";

        // WARNING names below are used as nameof
        string allowInsecure = "0";
        string fp = "";
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

        public void Validate(string host) {
            if(type == TYPE_XHTTP) {
                ValidateParamNotBlank(CATEGORY_QUERY_STRING, nameof(path), path);
                ValidateParam(CATEGORY_QUERY_STRING, nameof(mode), mode, [MODE_PACKET_UP, MODE_STREAM_UP]);
            } else {
                ThrowParamNotSupported(CATEGORY_QUERY_STRING, nameof(type), type);
            }

            ValidateParam(CATEGORY_QUERY_STRING, nameof(allowInsecure), allowInsecure, ["0", "1"]);
            ValidateParam(CATEGORY_QUERY_STRING, nameof(security), security, SECURITY_TLS);
            ValidateParamNotBlank(CATEGORY_QUERY_STRING, nameof(fp), fp);

            if(NativeIPAddress.TryParse(host, out _)) {
                ValidateParamNotBlank(CATEGORY_QUERY_STRING, nameof(sni), sni);
            }
        }

        public JsonObject ToJson() {
            var tlsSettings = new JsonObject {
                ["fingerprint"] = fp
            };

            if(allowInsecure == "1") {
                tlsSettings["allowInsecure"] = true;
            }

            if(!String.IsNullOrWhiteSpace(pcs)) {
                tlsSettings["pinnedPeerCertSha256"] = pcs;
            }

            if(!String.IsNullOrWhiteSpace(sni)) {
                tlsSettings["serverName"] = sni;
            }

            return new() {
                ["security"] = security,
                ["tlsSettings"] = tlsSettings,
                ["network"] = type,
                ["xhttpSettings"] = new JsonObject {
                    ["mode"] = mode,
                    ["path"] = path
                },
            };
        }
    }
}
