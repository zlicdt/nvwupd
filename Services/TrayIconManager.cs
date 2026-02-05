using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;

namespace NvwUpd.Services;

public sealed class TrayIconManager : IDisposable
{
    private const int WmApp = 0x8000;
    private const int WmTrayIcon = WmApp + 1;
    private const int WmLButtonUp = 0x0202;
    private const int WmRButtonUp = 0x0205;

    private const int NifMessage = 0x00000001;
    private const int NifIcon = 0x00000002;
    private const int NifTip = 0x00000004;

    private const int NimAdd = 0x00000000;
    private const int NimDelete = 0x00000002;

    private const uint TpmRightButton = 0x0002;
    private const uint TpmNoNotify = 0x0080;
    private const uint TpmReturnCmd = 0x0100;

    private const uint LrLoadFromFile = 0x0010;
    private const uint LrDefaultSize = 0x0040;

    private const int MenuOpen = 1001;
    private const int MenuCheck = 1002;
    private const int MenuExit = 1003;

    private readonly IntPtr _hwnd;
    private readonly Action _onOpen;
    private readonly Func<Task> _onCheck;
    private readonly Action _onExit;
    private readonly WndProc _wndProc;
    private readonly IntPtr _oldWndProc;

    private IntPtr _menu;
    private IntPtr _icon;
    private bool _isAdded;

    public TrayIconManager(Window window, Action onOpen, Func<Task> onCheck, Action onExit)
    {
        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        _onOpen = onOpen;
        _onCheck = onCheck;
        _onExit = onExit;

        _wndProc = WndProcHandler;
        _oldWndProc = SetWindowLongPtr(_hwnd, -4, Marshal.GetFunctionPointerForDelegate(_wndProc));

        CreateMenu();
        AddTrayIcon();
    }

    public void Dispose()
    {
        RemoveTrayIcon();

        if (_menu != IntPtr.Zero)
        {
            DestroyMenu(_menu);
            _menu = IntPtr.Zero;
        }

        if (_icon != IntPtr.Zero)
        {
            DestroyIcon(_icon);
            _icon = IntPtr.Zero;
        }

        if (_oldWndProc != IntPtr.Zero)
        {
            SetWindowLongPtr(_hwnd, -4, _oldWndProc);
        }
    }

    private void CreateMenu()
    {
        _menu = CreatePopupMenu();
        AppendMenu(_menu, 0, MenuOpen, "打开主窗口");
        AppendMenu(_menu, 0, MenuCheck, "检查更新");
        AppendMenu(_menu, 0, MenuExit, "退出");
    }

    private void AddTrayIcon()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "nvidia.ico");
        Console.WriteLine($"[Tray] Loading icon from: {iconPath}");

        if (File.Exists(iconPath))
        {
            _icon = LoadImage(IntPtr.Zero, iconPath, 1, 0, 0, LrLoadFromFile | LrDefaultSize);
            Console.WriteLine($"[Tray] LoadImage result: {_icon}");
        }
        else
        {
            Console.WriteLine("[Tray] Icon file NOT found.");
        }

        if (_icon == IntPtr.Zero)
        {
            Console.WriteLine("[Tray] Warning: Icon handle is Zero. Tray icon may not appear.");
        }

        var data = new NotifyIconData
        {
            cbSize = Marshal.SizeOf<NotifyIconData>(),
            hWnd = _hwnd,
            uID = 1,
            uFlags = NifMessage | NifIcon | NifTip,
            uCallbackMessage = WmTrayIcon,
            hIcon = _icon,
            szTip = "NvwUpd - NVIDIA Driver Updater"
        };

        _isAdded = Shell_NotifyIcon(NimAdd, ref data);
        Console.WriteLine($"[Tray] Shell_NotifyIcon result: {_isAdded}");
    }

    private void RemoveTrayIcon()
    {
        if (!_isAdded)
        {
            return;
        }

        var data = new NotifyIconData
        {
            cbSize = Marshal.SizeOf<NotifyIconData>(),
            hWnd = _hwnd,
            uID = 1
        };

        Shell_NotifyIcon(NimDelete, ref data);
        _isAdded = false;
    }

    private IntPtr WndProcHandler(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WmTrayIcon)
        {
            var lParamValue = lParam.ToInt32();
            if (lParamValue == WmLButtonUp)
            {
                _onOpen();
                return IntPtr.Zero;
            }

            if (lParamValue == WmRButtonUp)
            {
                ShowContextMenu();
                return IntPtr.Zero;
            }
        }

        return CallWindowProc(_oldWndProc, hWnd, msg, wParam, lParam);
    }

    private void ShowContextMenu()
    {
        if (_menu == IntPtr.Zero)
        {
            return;
        }

        GetCursorPos(out var point);
        SetForegroundWindow(_hwnd);

        var cmd = TrackPopupMenuEx(
            _menu,
            TpmRightButton | TpmNoNotify | TpmReturnCmd,
            point.X,
            point.Y,
            _hwnd,
            IntPtr.Zero);

        switch (cmd)
        {
            case MenuOpen:
                _onOpen();
                break;
            case MenuCheck:
                _ = _onCheck();
                break;
            case MenuExit:
                _onExit();
                break;
        }
    }

    private delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public int cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(uint dwMessage, ref NotifyIconData lpData);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadImage(IntPtr hInst, string name, uint type, int cx, int cy, uint fuLoad);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, uint uIDNewItem, string lpNewItem);

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll")]
    private static extern uint TrackPopupMenuEx(IntPtr hMenu, uint fuFlags, int x, int y, IntPtr hwnd, IntPtr lptpm);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out Point lpPoint);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
}
