namespace Project;

// https://github.com/shadowsocks/shadowsocks-org/wiki/Plugin

class SIP003 {
    public const string KEY = nameof(SIP003);

    public readonly string RemoteAddr;
    public readonly int RemotePort;

    public readonly string PluginName;
    public readonly string PluginOptions;

    public SIP003(string remoteAddr, int remotePort, string pluginInfo) {
        RemoteAddr = remoteAddr;
        RemotePort = remotePort;

        pluginInfo.TrySplit(';', out PluginName, out PluginOptions);
    }

    public string[] CreateEnv() {
        return [
            // https://stackoverflow.com/a/68841844
            "SystemRoot=" + Environment.GetEnvironmentVariable("SystemRoot"),

            "SS_LOCAL_HOST=" + AppConfig.SIP003Addr,
            "SS_LOCAL_PORT=" + AppConfig.SIP003Port,
            "SS_REMOTE_HOST=" + RemoteAddr,
            "SS_REMOTE_PORT=" + RemotePort,
            "SS_PLUGIN_OPTIONS=" + PluginOptions,
        ];
    }
}
