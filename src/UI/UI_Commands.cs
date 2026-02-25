using Windows.Win32;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Project;

partial class UI {
    const uint IDM_START = 1000;
    const uint IDM_STOP = 1001;
    const uint IDM_RELOAD_CONFIG = 1002;
    const uint IDM_QUIT = 1003;

    const uint IDM_TUN_ENABLE = 1004;
    const uint IDM_TUN_IPv6 = 1005;
    const uint IDM_TUN_BYPASS_PROXY = 1006;
    const uint IDM_TUN_BYPASS_RU = 1007;

    const uint ID_TRAY_ICON_DBLCLK = 1100;
    const uint ID_SERVER_LIST_START = 1200;

    static void AddMenuItems(HMENU menu) {
        if(Program.Started) {
            PInvoke.AppendMenu(menu, MENU_ITEM_FLAGS.MF_STRING, IDM_STOP, "Stop");
        } else {
            PInvoke.AppendMenu(menu, MENU_ITEM_FLAGS.MF_STRING, IDM_START, "Start");
        }

        PInvoke.SetMenuDefaultItem(menu, 0, 1);

        if(ServerList.Count > 0) {
            PInvoke.AppendMenu(
                menu,
                MENU_ITEM_FLAGS.MF_POPUP,
                (nuint)(nint)CreateServersSubMenu(),
                "Server: " + EscapeApmersand(SelectedServer.GetDisplayName("(none)"))
            );
        } else {
            PInvoke.AppendMenu(
                menu,
                MENU_ITEM_FLAGS.MF_STRING | MENU_ITEM_FLAGS.MF_DISABLED,
                ID_SERVER_LIST_START,
                "No items in " + EscapeApmersand(Path.GetFileName(ServerList.FilePath))
            );
        }

        PInvoke.AppendMenu(
            menu,
            MENU_ITEM_FLAGS.MF_POPUP | GetMenuItemFlags(isChecked: AppConfig.TunMode),
            (nuint)(nint)CreateTunModeSubMenu(),
            "TUN mode"
        );

        PInvoke.AppendMenu(
            menu,
            GetMenuItemFlags(isDisabled: Program.Started),
            IDM_RELOAD_CONFIG,
            "Reload config"
        );

        PInvoke.AppendMenu(menu, MENU_ITEM_FLAGS.MF_STRING, IDM_QUIT, "Quit");
    }

    static HMENU CreateServersSubMenu() {
        var subMenu = PInvoke.CreatePopupMenu();
        var serverCount = ServerList.Count;
        for(var i = 0; i < serverCount; i++) {
            PInvoke.AppendMenu(
                subMenu,
                GetMenuItemFlags(isChecked: i == AppConfig.SelectedServerIndex),
                ID_SERVER_LIST_START + (uint)i,
                EscapeApmersand(ServerList.DisplayName[i])
            );
            if(i < serverCount - 1 && ServerList.Separator[i]) {
                AppendSeparator(subMenu);
            }
        }
        return subMenu;
    }

    static HMENU CreateTunModeSubMenu() {
        var subMenu = PInvoke.CreatePopupMenu();
        ReadOnlySpan<(AppConfigFlags, uint, string)> items = [
            (AppConfigFlags.TunMode, IDM_TUN_ENABLE, "Enable"),
            (AppConfigFlags.TunModeIPv6, IDM_TUN_IPv6, "IPv6"),
            (AppConfigFlags.TunModeBypassProxy, IDM_TUN_BYPASS_PROXY, "Bypass system proxy"),
            (AppConfigFlags.TunModeBypassRU, IDM_TUN_BYPASS_RU, "Bypass geoip:ru"),
        ];
        foreach(var (flag, id, text) in items) {
            var isDisabled = !AppConfig.TunMode && flag != AppConfigFlags.TunMode;
            var isChecked = AppConfig.HasFlag(flag);
            PInvoke.AppendMenu(subMenu, GetMenuItemFlags(isDisabled, isChecked), id, text);
        }
        return subMenu;
    }

    static string EscapeApmersand(string text) {
        return text.Replace("&", "&&");
    }

    static void AppendSeparator(HMENU menu) {
        PInvoke.AppendMenu(menu, MENU_ITEM_FLAGS.MF_SEPARATOR, default, default);
    }

    static MENU_ITEM_FLAGS GetMenuItemFlags(bool isDisabled = false, bool isChecked = false) {
        var flags = MENU_ITEM_FLAGS.MF_STRING;
        if(isDisabled) {
            flags |= MENU_ITEM_FLAGS.MF_DISABLED;
        }
        if(isChecked) {
            flags |= MENU_ITEM_FLAGS.MF_CHECKED;
        }
        return flags;
    }

    static void HandleCommandCore(uint id) {
        if(TryConvertToFlag(id, out var flag)) {
            WithRestart(delegate {
                AppConfig.ToggleFlag(flag);
                AppConfig.Save();
            });
            return;
        }
        switch(id) {
            case IDM_START:
                Program.Start();
                break;

            case IDM_STOP:
                Program.Stop();
                break;

            case IDM_RELOAD_CONFIG: {
                    Program.EnsureStopped();
                    var selectedServerDisplayName = SelectedServer.GetDisplayName();
                    AppConfig.Load();
                    ServerList.Load();
                    if(SelectedServer.GetDisplayName() != selectedServerDisplayName) {
                        AppConfig.SelectedServerIndex = ServerList.FindByDisplayName(selectedServerDisplayName);
                        AppConfig.Save();
                    }
                }
                break;

            case IDM_QUIT:
                if(Program.Started) {
                    Program.Stop();
                }
                MustPostMessage(PInvoke.WM_QUIT);
                break;

            case ID_TRAY_ICON_DBLCLK:
                if(Program.Started) {
                    Program.Stop();
                } else {
                    Program.Start();
                }
                break;

            case >= ID_SERVER_LIST_START: {
                    var newIndex = (int)(id - ID_SERVER_LIST_START);
                    if(newIndex != AppConfig.SelectedServerIndex) {
                        WithRestart(delegate {
                            AppConfig.SelectedServerIndex = newIndex;
                            AppConfig.Save();
                        });
                    }
                }
                break;

            default:
                throw new NotSupportedException();
        }
    }

    static bool TryConvertToFlag(uint id, out AppConfigFlags result) {
        result = id switch {
            IDM_TUN_ENABLE => AppConfigFlags.TunMode,
            IDM_TUN_IPv6 => AppConfigFlags.TunModeIPv6,
            IDM_TUN_BYPASS_PROXY => AppConfigFlags.TunModeBypassProxy,
            IDM_TUN_BYPASS_RU => AppConfigFlags.TunModeBypassRU,
            _ => default
        };
        return result != default;
    }

    static void WithRestart(Action action) {
        var restart = Program.Started;
        if(restart) {
            ShowBalloon("Restarting...");
            Program.Stop();
        }
        action();
        if(restart) {
            Program.Start();
        }
    }
}
