using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Project.Tests;

public sealed class ProxyIntegrationTests : IDisposable {
    const string PROXY_HOST = "127.0.0.1";
    const int PROXY_PORT = 1080;

    const string TEST_IP = "192.0.2.123";
    const string TEST_COUNTRY_CODE = "XX";

    readonly TcpListener ProxyListener;
    readonly Task ProxyListenerLoopTask;

    public ProxyIntegrationTests() {
        ProxyListener = new TcpListener(IPAddress.Parse(PROXY_HOST), PROXY_PORT);
        ProxyListener.Start();
        ProxyListenerLoopTask = ProxyListenerLoopAsync(ProxyListener);
    }

    public void Dispose() {
        ProxyListener.Dispose();
        ProxyListenerLoopTask.Wait();
    }

    [Fact]
    public async Task RunAsync() {
        ProxyBackup.TryRestore();

        var wanInfoReadyTCS = new TaskCompletionSource<WanInfoEventArgs>();

        void WanInfo_Ready(object? s, WanInfoEventArgs e) {
            wanInfoReadyTCS.SetResult(e);
        }

        try {
            ProxyBackup.TrySave();
            WanInfo.Ready += WanInfo_Ready;

            NativeProxyManager.SetConfig(PROXY_HOST + ':' + PROXY_PORT);
            WanInfo.RequestUpdate(default);

            var e = await wanInfoReadyTCS.Task;

            Assert.Equal(TEST_IP, e.IP);
            Assert.Equal(TEST_COUNTRY_CODE, e.CountryCode);
        } finally {
            WanInfo.Ready -= WanInfo_Ready;
            ProxyBackup.TryRestore();
        }
    }

    static async Task ProxyListenerLoopAsync(TcpListener listener) {
        while(true) {
            try {
                using var client = await listener.AcceptTcpClientAsync();
                using var stream = client.GetStream();
                using var reader = new StreamReader(stream, leaveOpen: true);

                var status = 503;
                var body = "";

                var line = await reader.ReadLineAsync();

                if(line != null && line.StartsWith("GET http://example.net/test/")) {
                    status = 200;
                    body = JsonSerializer.Serialize(new Dictionary<string, string> {
                        ["test_ip"] = TEST_IP,
                        ["test_country_code"] = TEST_COUNTRY_CODE,
                    });
                }

                await stream.WriteAsync(
                    Encoding.ASCII.GetBytes(
                        String.Join("\r\n",
                            "HTTP/1.1 " + status,
                            "Content-Length: " + body.Length,
                            "", body
                        )
                    )
                );
            } catch {
                break;
            }
        }
    }
}
