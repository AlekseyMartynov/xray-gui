using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Project;

static partial class UI {
    const uint WM_TRAY_ICON_CALLBACK = PInvoke.WM_USER + 1;
    const uint WM_PENDING_ACTIONS_DRAIN = PInvoke.WM_USER + 2;

    static readonly LRESULT LRESULT_OK = (LRESULT)0;
    static readonly HICON IconBlue, IconSilver, IconError;
    static readonly HWND MainWindow;
    static readonly Channel<Action> PendingActions;

    static UI() {
        var module = PInvoke.GetModuleHandle(default(PCWSTR));
        IconBlue = PInvoke.LoadIcon(module, NativeUtils.MAKEINTRESOURCE(102));
        IconSilver = PInvoke.LoadIcon(module, NativeUtils.MAKEINTRESOURCE(103));
        IconError = PInvoke.LoadIcon(default, PInvoke.IDI_ERROR);
        MainWindow = CreateMainWindow();
        PendingActions = Channel.CreateUnbounded<Action>();
    }

    public static void Run() {
        try {
            UpdateTrayIcon();
            while(PInvoke.GetMessage(out MSG msg, MainWindow, default, default) > 0) {
                PInvoke.TranslateMessage(in msg);
                PInvoke.DispatchMessage(in msg);
            }
        } finally {
            UpdateTrayIcon(visible: false);
            PInvoke.DestroyWindow(MainWindow);
        }
    }

    public static void ShowBalloon(string text, bool error = false) {
        PendingActions.Writer.TryWrite(delegate {
            UpdateTrayIcon(text, error);
        });
        MustPostMessage(WM_PENDING_ACTIONS_DRAIN);
    }

    public static void ShowMessageBox(string text) {
        PendingActions.Writer.TryWrite(delegate {
            PInvoke.SetForegroundWindow(MainWindow);
            PInvoke.MessageBox(MainWindow, text, default, default);
        });
        MustPostMessage(WM_PENDING_ACTIONS_DRAIN);
    }

    public static void ShowMessageBox(Exception x) {
        if(x is UIException) {
            ShowMessageBox(x.Message);
        } else {
            ShowMessageBox(x.ToString());
        }
    }

    static unsafe HWND CreateMainWindow() {
        fixed(char* wndClassNamePtr = "MainWindowClass") {
            var wndClass = new WNDCLASSW {
                lpszClassName = wndClassNamePtr,
                lpfnWndProc = &WndProc,
            };

            PInvoke.RegisterClass(in wndClass);

            return PInvoke.CreateWindowEx(
                default, wndClassNamePtr,
                default, default, default,
                default, default, default,
                default, default, default
            );
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    static LRESULT WndProc(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam) {
        switch(msg) {
            case WM_TRAY_ICON_CALLBACK:
                if(lParam == PInvoke.WM_RBUTTONUP) {
                    ShowMenu();
                } else if(lParam == PInvoke.WM_LBUTTONDBLCLK) {
                    HandleCommand(ID_TRAY_ICON_DBLCLK);
                }
                return LRESULT_OK;
            case WM_PENDING_ACTIONS_DRAIN:
                while(PendingActions.Reader.TryRead(out var action)) {
                    action();
                }
                return LRESULT_OK;
            case PInvoke.WM_COMMAND:
                HandleCommand((uint)wParam);
                return LRESULT_OK;
            case PInvoke.WM_ENTERIDLE:
                return LRESULT_OK;
        }
        return PInvoke.DefWindowProc(hwnd, msg, wParam, lParam);
    }

    static void ShowMenu() {
        var menu = PInvoke.CreatePopupMenu();
        try {
            AddMenuItems(menu);
            PInvoke.GetCursorPos(out var cursorPos);
            PInvoke.SetForegroundWindow(MainWindow);
            PInvoke.TrackPopupMenu(menu, TRACK_POPUP_MENU_FLAGS.TPM_RIGHTALIGN, cursorPos.X, cursorPos.Y, MainWindow, default);
        } finally {
            PInvoke.DestroyMenu(menu);
        }
    }

    static void HandleCommand(uint id) {
        try {
            HandleCommandCore(id);
        } catch(Exception x) {
            ShowMessageBox(x);
        } finally {
            UpdateTrayIcon();
        }
    }

    static void UpdateTrayIcon(string? info = default, bool errorIcon = false, bool visible = true) {
        var data = new NOTIFYICONDATAW {
            cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATAW>(),

            // This pair is used as a system-wide ID
            hWnd = MainWindow,
            uID = default,
        };

        if(visible) {
            data.uFlags = NOTIFY_ICON_DATA_FLAGS.NIF_MESSAGE
                        | NOTIFY_ICON_DATA_FLAGS.NIF_ICON
                        | NOTIFY_ICON_DATA_FLAGS.NIF_TIP
                        | NOTIFY_ICON_DATA_FLAGS.NIF_INFO;

            if(errorIcon) {
                data.hIcon = IconError;
            } else {
                data.hIcon = Program.Started ? IconBlue : IconSilver;
            }

            data.szInfo = info;
            data.szTip = SelectedServer.GetDisplayName();
            data.uCallbackMessage = WM_TRAY_ICON_CALLBACK;

            if(!PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_ADD, in data)) {
                PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_MODIFY, in data);
            }
        } else {
            PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_DELETE, in data);
        }
    }

    static void MustPostMessage(uint msg) {
        NativeUtils.MustSucceed(
            PInvoke.PostMessage(MainWindow, msg, default, default)
        );
    }
}
