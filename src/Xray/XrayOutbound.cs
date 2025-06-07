using System.Diagnostics.CodeAnalysis;

namespace Project;

static partial class XrayOutbound {
    const string CATEGORY_QUERY_STRING = "Query string param";
    const string CATEGORY_USERINFO_PARAM = "UserInfo param";

    public static JsonObject FromUri(Uri uri) {
        if(String.IsNullOrEmpty(uri.Host) || uri.Port < 0) {
            throw new UIException("Must be absolute URI with host and port");
        }
        if(uri.AbsolutePath != "/") {
            throw new UIException("URI path must be empty");
        }
        if(uri.Scheme == "vless") {
            return FromVlessUri(uri);
        }
        if(uri.Scheme == "ss") {
            return FromShadowsocksUri(uri);
        }
        throw new UIException($"URI scheme '{uri.Scheme}' is not supported");
    }

    public static SIP003? ExtractSIP003(JsonObject outbound) {
        if(!outbound.TryGetValue(SIP003.KEY, out var value)) {
            return null;
        }

        outbound.Remove(SIP003.KEY);

        if(value is not SIP003 sip003) {
            throw new InvalidOperationException();
        }

        return sip003;
    }

    [DoesNotReturn]
    static void ThrowParamException(string category, string name, string problem) {
        throw new UIException($"{category} '{name}' {problem}");
    }

    static void ThrowParamNotSupported(string category, string name, string? value) {
        ThrowParamException(category, name, $"with the value '{value}' is not supported");
    }

    static void ValidateParam(string category, string name, string? value, params ReadOnlySpan<string?> allowedValues) {
        if(allowedValues.Length == 1) {
            if(value != allowedValues[0]) {
                ThrowParamException(category, name, $"must be set to '{allowedValues[0]}'");
            }
        } else {
            if(allowedValues.IndexOf(value) < 0) {
                ThrowParamException(category, name, "must be one of: " + String.Join(", ", allowedValues));
            }
        }
    }

    static void ValidateParamNotBlank(string category, string name, [NotNull] string? value) {
        if(String.IsNullOrWhiteSpace(value)) {
            ThrowParamException(category, name, "must not be blank");
        }
    }
}
