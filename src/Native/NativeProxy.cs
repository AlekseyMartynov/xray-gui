using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Networking.WinInet;

namespace Project;

class NativeProxyConfig {
    public required uint Flags { get; init; }
    public required string PacUrl { get; init; }
    public required string ProxyUrl { get; init; }
    public required string ProxyBypass { get; init; }
}

static class NativeProxyManager {

    public static unsafe NativeProxyConfig GetConfig() {
        var options = stackalloc INTERNET_PER_CONN_OPTIONW[] {
            new() { dwOption = INTERNET_PER_CONN.INTERNET_PER_CONN_FLAGS },
            new() { dwOption = INTERNET_PER_CONN.INTERNET_PER_CONN_AUTOCONFIG_URL },
            new() { dwOption = INTERNET_PER_CONN.INTERNET_PER_CONN_PROXY_SERVER },
            new() { dwOption = INTERNET_PER_CONN.INTERNET_PER_CONN_PROXY_BYPASS },
        };

        var optionListSize = (uint)Marshal.SizeOf<INTERNET_PER_CONN_OPTION_LISTW>();

        var optionList = new INTERNET_PER_CONN_OPTION_LISTW {
            dwSize = optionListSize,
            dwOptionCount = 4,
            pOptions = options,
        };

        NativeUtils.MustSucceed(
            PInvoke.InternetQueryOption(
                default,
                PInvoke.INTERNET_OPTION_PER_CONNECTION_OPTION,
                Unsafe.AsPointer(ref optionList),
                ref optionListSize
            )
        );

        return new NativeProxyConfig {
            Flags = options[0].Value.dwValue,
            PacUrl = ReadStringAndFree(options[1]),
            ProxyUrl = ReadStringAndFree(options[2]),
            ProxyBypass = ReadStringAndFree(options[3])
        };
    }

    public static unsafe void SetConfig(NativeProxyConfig config) {
        fixed(char* pacUrlPtr = config.PacUrl)
        fixed(char* proxyUrlPtr = config.ProxyUrl)
        fixed(char* proxyBypassPtr = config.ProxyBypass) {

            var options = stackalloc INTERNET_PER_CONN_OPTIONW[] {
                new()  {
                    dwOption = INTERNET_PER_CONN.INTERNET_PER_CONN_FLAGS,
                    Value = new() {
                        dwValue = config.Flags
                    }
                },
                new()  {
                    dwOption = INTERNET_PER_CONN.INTERNET_PER_CONN_PROXY_SERVER,
                    Value = new() {
                        pszValue = proxyUrlPtr
                    }
                },
                new() {
                    dwOption = INTERNET_PER_CONN.INTERNET_PER_CONN_PROXY_BYPASS,
                    Value = new()  {
                        pszValue = proxyBypassPtr
                    }
                },
                new() {
                    dwOption = INTERNET_PER_CONN.INTERNET_PER_CONN_AUTOCONFIG_URL,
                    Value = new() {
                        pszValue = pacUrlPtr
                    }
                }
            };

            var optionListSize = (uint)Marshal.SizeOf<INTERNET_PER_CONN_OPTION_LISTW>();

            var optionList = new INTERNET_PER_CONN_OPTION_LISTW {
                dwSize = optionListSize,
                dwOptionCount = 4,
                pOptions = options,
            };

            NativeUtils.MustSucceed(
                PInvoke.InternetSetOption(
                    default,
                    PInvoke.INTERNET_OPTION_PER_CONNECTION_OPTION,
                    Unsafe.AsPointer(ref optionList),
                    optionListSize
                )
            );

            NativeUtils.MustSucceed(PInvoke.InternetSetOption(default, PInvoke.INTERNET_OPTION_SETTINGS_CHANGED, default, default));
            NativeUtils.MustSucceed(PInvoke.InternetSetOption(default, PInvoke.INTERNET_OPTION_REFRESH, default, default));
        }
    }

    public static void SetConfig(string proxyUrl, string proxyBypass = "<local>") {
        SetConfig(new() {
            Flags = PInvoke.PROXY_TYPE_DIRECT | PInvoke.PROXY_TYPE_PROXY,
            PacUrl = "",
            ProxyUrl = proxyUrl,
            ProxyBypass = proxyBypass
        });
    }

    static unsafe string ReadStringAndFree(INTERNET_PER_CONN_OPTIONW option) {
        var p = option.Value.pszValue;
        if(p != null) {
            var text = p.ToString();
            // https://learn.microsoft.com/en-us/windows/win32/api/wininet/ns-wininet-internet_per_conn_optiona#remarks
            PInvoke.GlobalFree((HGLOBAL)p.Value);
            return text;
        }
        return "";
    }
}
