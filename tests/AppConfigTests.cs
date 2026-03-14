namespace Project.Tests;

public sealed class AppConfigTests : IDisposable {
    static readonly string ConfigFilePath = Path.Join(AppContext.BaseDirectory, nameof(AppConfigTests) + ".ini");

    readonly AppConfigFile ConfigFile = new(ConfigFilePath);

    public AppConfigTests() {
        DeleteFile();
    }

    public void Dispose() {
        DeleteFile();
    }

    [Fact]
    public void CreateFileOnLoad() {
        ConfigFile.Load();
        Assert.True(File.Exists(ConfigFilePath));
    }

    [Fact]
    public void Load() {
        var tab = '\t';
        File.WriteAllText(ConfigFilePath, $"""
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
        ConfigFile.Load();
        Assert.Equal(123, ConfigFile.SelectedServerIndex);
        Assert.Equal("test", ConfigFile.ProxyAddr);
        Assert.Equal(1234, ConfigFile.ProxyPort);
        AssertFlag(false, AppConfigFlags.TunMode);
        AssertFlag(true, AppConfigFlags.TunModeIPv6);
        AssertFlag(true, AppConfigFlags.TunModeUnsetProxy);
        AssertFlag(true, AppConfigFlags.BypassRU);
        AssertFlag(true, AppConfigFlags.ProcConsole);
        Assert.Equal(1, ConfigFile.Mux);
    }

    [Fact]
    public void Compat() {
        File.WriteAllLines(ConfigFilePath, [
            "tap_mode = 1",
            "tap_mode_badvpn = 1",
            "tun_mode_bypass_proxy = 1",
            "tun_mode_bypass_ru = 1",
        ]);
        ConfigFile.Load();
        AssertFlag(true, AppConfigFlags.TunMode);
        AssertFlag(true, AppConfigFlags.TunModeUnsetProxy);
        AssertFlag(true, AppConfigFlags.BypassRU);
    }

    static void DeleteFile() {
        if(File.Exists(ConfigFilePath)) {
            File.Delete(ConfigFilePath);
        }
    }

    void AssertFlag(bool expected, AppConfigFlags flag) {
        Assert.Equal(expected, ConfigFile.Flags.HasFlag(flag));
    }
}
