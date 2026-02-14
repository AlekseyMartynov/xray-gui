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
            proc_console = 123
            """
        );
        AppConfig.Load();
        Assert.Equal(123, AppConfig.SelectedServerIndex);
        Assert.Equal("test", AppConfig.ProxyAddr);
        Assert.Equal(1234, AppConfig.ProxyPort);
        Assert.False(AppConfig.TunMode);
        Assert.True(AppConfig.ProcConsole);
    }

    [Fact]
    public void Compat() {
        File.WriteAllLines(AppConfig.FilePath, [
            "tap_mode = 1",
            "tap_mode_badvpn = 1",
        ]);
        AppConfig.Load();
        Assert.True(AppConfig.TunMode);
    }

    void DeleteFileAndReset() {
        if(File.Exists(AppConfig.FilePath)) {
            File.Delete(AppConfig.FilePath);
        }
        AppConfig.Reset();
    }
}
