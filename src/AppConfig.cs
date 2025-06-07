namespace Project;

static class AppConfig {
    public static bool ProcConsole { get; set; }
    public static bool TapMode { get; set; }

    public static string SIP003Addr { get; private set; } = "127.0.0.1";
    public static int SIP003Port { get; private set; } = 1984;
}
