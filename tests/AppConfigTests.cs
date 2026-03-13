namespace Project.Tests;

public sealed class AppConfigTests : IDisposable {

    public AppConfigTests() {
        DeleteFileAndReset();
    }

    public void Dispose() {
        DeleteFileAndReset();
    }

    [Fact]
    public void CreateFileOnLoad() {
        AppConfig.Load();
        Assert.True(File.Exists(AppConfig.FilePath));
    }

    [Fact]
    public void Load() {
        var tab = '\t';
        File.WriteAllText(AppConfig.FilePath, $"""
            {tab} selected_server {tab} = {tab} 123 {tab}
            proxy = test:1234
            tun_mode = nonsense
            tun_mode_ipv6 = 1
            tun_mode_unset_proxy = 1
            bypass_ru = 1
            proc_console = 123
            mux = 1
            """
        );
        AppConfig.Load();
        Assert.Equal(123, AppConfig.SelectedServerIndex);
        Assert.Equal("test", AppConfig.ProxyAddr);
        Assert.Equal(1234, AppConfig.ProxyPort);
        Assert.False(AppConfig.TunMode);
        Assert.True(AppConfig.TunModeIPv6);
        Assert.True(AppConfig.TunModeUnsetProxy);
        Assert.True(AppConfig.BypassRU);
        Assert.True(AppConfig.ProcConsole);
        Assert.Equal(1, AppConfig.Mux);
    }

    [Fact]
    public void Compat() {
        File.WriteAllLines(AppConfig.FilePath, [
            "tap_mode = 1",
            "tap_mode_badvpn = 1",
            "tun_mode_bypass_proxy = 1",
            "tun_mode_bypass_ru = 1",
        ]);
        AppConfig.Load();
        Assert.True(AppConfig.TunMode);
        Assert.True(AppConfig.TunModeUnsetProxy);
        Assert.True(AppConfig.BypassRU);
    }

    void DeleteFileAndReset() {
        if(File.Exists(AppConfig.FilePath)) {
            File.Delete(AppConfig.FilePath);
        }
        AppConfig.Reset();
    }
}
