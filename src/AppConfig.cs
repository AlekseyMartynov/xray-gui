namespace Project;

static class AppConfig {
    public static int SelectedServerIndex { get; set; }

    public static bool ProcConsole { get; set; }
    public static bool TapMode { get; set; }

    public static string ProxyAddr { get; private set; } = "127.0.0.1";
    public static int ProxyPort { get; private set; } = 1080;

    public static string SIP003Addr { get; private set; } = "127.0.0.1";
    public static int SIP003Port { get; private set; } = 1984;
}
