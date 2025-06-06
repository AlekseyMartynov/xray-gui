namespace Project;

static class ProxyBackup {
    static readonly string BackupFile = Path.Join(AppContext.BaseDirectory, "proxy.bak");

    public static bool TrySave() {
        if(File.Exists(BackupFile)) {
            return false;
        }
        var config = NativeProxyManager.GetConfig();
        File.WriteAllLines(BackupFile, [
            config.Flags.ToString(),
            config.PacUrl,
            config.ProxyUrl,
            config.ProxyBypass
        ]);
        return true;
    }

    public static bool TryRestore() {
        if(!File.Exists(BackupFile)) {
            return false;
        }
        var lines = File.ReadAllLines(BackupFile);
        var config = new NativeProxyConfig {
            Flags = uint.Parse(lines[0]),
            PacUrl = lines[1],
            ProxyUrl = lines[2],
            ProxyBypass = lines[3]
        };
        NativeProxyManager.SetConfig(config);
        File.Delete(BackupFile);
        return true;
    }
}
