using System.Runtime.CompilerServices;
using Windows.Win32;
using Windows.Win32.Networking.WinInet;

namespace Project;

class NativeHttpRequest {
    public string Verb { get; init; } = "GET";
    public required Uri Uri { get; init; }
    public uint Timeouts { get; init; }
    public required Stream Output { get; init; }

    public uint StatusCode { get; set; }
    public bool StatusCodeIsSuccess => StatusCode >= 200 && StatusCode < 400;
}

static class NativeHttpClient {

    public static unsafe void SendRequest(NativeHttpRequest req) {
        var uri = req.Uri;

        var hInet = PInvoke.InternetOpen(default, (uint)INTERNET_ACCESS_TYPE.INTERNET_OPEN_TYPE_PRECONFIG, default, default, default);
        NativeUtils.CheckHandle(hInet);
        try {
            SetTimeouts(hInet, req.Timeouts);

            var hConn = PInvoke.InternetConnect(hInet, uri.Host, GetPort(uri), default, default, PInvoke.INTERNET_SERVICE_HTTP, default, default);
            NativeUtils.CheckHandle(hConn);
            try {
                var flags = PInvoke.INTERNET_FLAG_NO_CACHE_WRITE
                    | PInvoke.INTERNET_FLAG_NO_COOKIES
                    | PInvoke.INTERNET_FLAG_RELOAD;

                if(uri.Scheme == Uri.UriSchemeHttps) {
                    flags |= PInvoke.INTERNET_FLAG_SECURE;
                }

                var hReq = PInvoke.HttpOpenRequest(hConn, req.Verb, uri.PathAndQuery, default, default, default, flags, default);
                NativeUtils.CheckHandle(hReq);
                try {
                    NativeUtils.MustSucceed(
                        // TODO headers and body
                        PInvoke.HttpSendRequest(hReq, default, default, default, default)
                    );

                    uint statusCode = default;
                    uint statusCodeSize = 4;

                    NativeUtils.MustSucceed(
                        PInvoke.HttpQueryInfo(hReq, PInvoke.HTTP_QUERY_STATUS_CODE | PInvoke.HTTP_QUERY_FLAG_NUMBER, &statusCode, &statusCodeSize, default)
                    );

                    req.StatusCode = statusCode;

                    var bufLen = 1024;
                    var buf = (stackalloc byte[bufLen]);

                    while(true) {
                        NativeUtils.MustSucceed(
                            PInvoke.InternetReadFile(hReq, buf, out var readLen)
                        );
                        if(readLen < 1) {
                            break;
                        }
                        req.Output.Write(buf.Slice(0, (int)readLen));
                    }
                } finally {
                    PInvoke.InternetCloseHandle(hReq);
                }
            } finally {
                PInvoke.InternetCloseHandle(hConn);
            }
        } finally {
            PInvoke.InternetCloseHandle(hInet);
        }
    }

    static unsafe void SetTimeouts(void* hInet, uint value) {
        if(value == default) {
            return;
        }

        // This option is not intended to represent a fine-grained, immediate timeout.
        // You can expect the timeout to occur up to six seconds after the set timeout value.
        // https://learn.microsoft.com/windows/win32/wininet/option-flags

        Span<uint> options = [
            PInvoke.INTERNET_OPTION_CONNECT_TIMEOUT,
            PInvoke.INTERNET_OPTION_RECEIVE_TIMEOUT,
            PInvoke.INTERNET_OPTION_SEND_TIMEOUT,
        ];

        foreach(var option in options) {
            PInvoke.InternetSetOption(hInet, option, &value, sizeof(uint));
        }
    }

    static ushort GetPort(Uri uri) {
        var uriPort = uri.Port;
        if(uriPort > -1) {
            return (ushort)uriPort;
        }
        if(uri.Scheme == Uri.UriSchemeHttps) {
            return 443;
        }
        return 80;
    }
}
