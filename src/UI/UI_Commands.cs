using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Project;

partial class UI {
    const uint IDM_START = 1000;
    const uint IDM_STOP = 1001;
    const uint IDM_TUN_MODE = 1002;
    const uint IDM_RELOAD_CONFIG = 1003;
    const uint IDM_QUIT = 1004;
    const uint IDM_ENABLE_IPv6 = 1005;

    const uint ID_TRAY_ICON_DBLCLK = 1100;
    const uint ID_SERVER_LIST_START = 1200;

    static void AddMenuItems(HMENU menu) {
        if(Program.Started) {
            PInvoke.AppendMenu(menu, MENU_ITEM_FLAGS.MF_STRING, IDM_STOP, "Stop");
        } else {
            PInvoke.AppendMenu(menu, MENU_ITEM_FLAGS.MF_STRING, IDM_START, "Start");
        }

        AppendSeparator(menu);

        var serverCount = ServerList.Count;

        if(serverCount > 0) {
            for(var i = 0; i < serverCount; i++) {
                PInvoke.AppendMenu(
                    menu,
                    GetMenuItemFlags(isChecked: i == AppConfig.SelectedServerIndex),
                    ID_SERVER_LIST_START + (uint)i,
                    ServerList.DisplayName[i]
                );
                if(i < serverCount - 1 && ServerList.Separator[i]) {
                    AppendSeparator(menu);
                }
            }
        } else {
            PInvoke.AppendMenu(
                menu,
                MENU_ITEM_FLAGS.MF_STRING | MENU_ITEM_FLAGS.MF_DISABLED,
                ID_SERVER_LIST_START,
                "No items in " + Path.GetFileName(ServerList.FilePath)
            );
        }

        AppendSeparator(menu);

        PInvoke.AppendMenu(
            menu,
            GetMenuItemFlags(isDisabled: Program.Started),
            IDM_RELOAD_CONFIG,
            "Reload config"
        );

        if(AppConfig.TunMode) {
            PInvoke.AppendMenu(
                menu,
                GetMenuItemFlags(isDisabled: Program.Started, isChecked: AppConfig.TunModeIPv6),
                IDM_ENABLE_IPv6,
                "Enable IPv6"
            );
        }

        PInvoke.AppendMenu(
            menu,
            GetMenuItemFlags(isDisabled: Program.Started, isChecked: AppConfig.TunMode),
            IDM_TUN_MODE,
            "TUN mode"
        );

        PInvoke.AppendMenu(menu, MENU_ITEM_FLAGS.MF_STRING, IDM_QUIT, "Quit");

        var bold = new MENUITEMINFOW {
            cbSize = (uint)Marshal.SizeOf<MENUITEMINFOW>(),
            fMask = MENU_ITEM_MASK.MIIM_STATE,
            fState = MENU_ITEM_STATE.MFS_DEFAULT
        };

        PInvoke.SetMenuItemInfo(menu, Program.Started ? IDM_STOP : IDM_START, false, in bold);
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
        switch(id) {
            case IDM_START:
                Program.Start();
                break;

            case IDM_STOP:
                Program.Stop();
                break;

            case IDM_TUN_MODE:
                Program.EnsureStopped();
                AppConfig.TunMode = !AppConfig.TunMode;
                AppConfig.Save();
                break;

            case IDM_ENABLE_IPv6:
                Program.EnsureStopped();
                AppConfig.TunModeIPv6 = !AppConfig.TunModeIPv6;
                AppConfig.Save();
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
                        var restart = Program.Started;
                        if(restart) {
                            Program.Stop();
                        }
                        AppConfig.SelectedServerIndex = newIndex;
                        AppConfig.Save();
                        if(restart) {
                            Program.Start();
                        }
                    }
                }
                break;

            default:
                throw new NotSupportedException();
        }
    }
}
