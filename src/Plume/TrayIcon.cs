using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace Plume;

/// <summary>Icône et menu dans la zone de notification, en Win32 pur (Shell_NotifyIcon).
/// Remplace le NotifyIcon WinForms — même API publique — pour ne plus charger System.Windows.Forms.</summary>
public class TrayIcon : IDisposable
{
    private const int WM_TRAYICON = 0x8001;      // WM_APP + 1
    private const int WM_LBUTTONDBLCLK = 0x0203;
    private const int WM_RBUTTONUP = 0x0205;
    private const uint WM_NULL = 0x0000;

    private const uint NIM_ADD = 0, NIM_MODIFY = 1, NIM_DELETE = 2;
    private const uint NIF_MESSAGE = 0x01, NIF_ICON = 0x02, NIF_TIP = 0x04, NIF_INFO = 0x10;
    private const uint NIIF_INFO = 0x01;

    private const uint MF_STRING = 0x0000, MF_GRAYED = 0x0001, MF_CHECKED = 0x0008, MF_SEPARATOR = 0x0800;
    private const uint TPM_RIGHTBUTTON = 0x0002, TPM_RETURNCMD = 0x0100;

    private const uint IMAGE_ICON = 1, LR_LOADFROMFILE = 0x10;
    private static readonly IntPtr HWND_MESSAGE = new(-3);
    private static readonly IntPtr IDI_APPLICATION = new(32512);

    // Identifiants des entrées du menu contextuel.
    private const int CMD_TOGGLE = 1, CMD_SETTINGS = 2, CMD_COPYLAST = 3, CMD_STARTUP = 4, CMD_QUIT = 5;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
        public uint uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(uint message, ref NOTIFYICONDATA data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadImage(IntPtr hInstance, string name, uint type, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr iconName);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool AppendMenu(IntPtr hMenu, uint flags, IntPtr idNewItem, string? newItem);

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll")]
    private static extern int TrackPopupMenuEx(IntPtr hMenu, uint flags, int x, int y, IntPtr hWnd, IntPtr tpmParams);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT pt);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string message);

    private readonly HwndSource _source;
    private readonly IntPtr _hIcon;
    private readonly uint _taskbarCreatedMsg;
    private readonly Action _onToggleDictation;
    private readonly Action _onOpenSettings;
    private readonly Action _onQuit;
    private readonly Action<bool> _onSetStartup;
    private readonly Func<string?> _getLastText;
    private bool _startupChecked;
    private bool _disposed;

    public TrayIcon(Action onToggleDictation, Action onOpenSettings, Action onQuit,
                    Func<bool> isStartupEnabled, Action<bool> onSetStartup,
                    Func<string?> getLastText)
    {
        _onToggleDictation = onToggleDictation;
        _onOpenSettings = onOpenSettings;
        _onQuit = onQuit;
        _onSetStartup = onSetStartup;
        _getLastText = getLastText;
        _startupChecked = isStartupEnabled(); // état initial de la coche ; RefreshStartup la met à jour ensuite

        // Fenêtre message-only pour recevoir les clics de l'icône (même motif que HotkeyService).
        var parameters = new HwndSourceParameters("PlumeTrayWindow")
        {
            ParentWindow = HWND_MESSAGE,
            WindowStyle = 0
        };
        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);

        // Explorer peut redémarrer : il faut alors ré-ajouter l'icône (NotifyIcon le faisait tout seul).
        _taskbarCreatedMsg = RegisterWindowMessage("TaskbarCreated");

        _hIcon = LoadTrayIcon();
        AddIcon();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_TRAYICON)
        {
            switch ((int)lParam)
            {
                case WM_LBUTTONDBLCLK: _onOpenSettings(); handled = true; break;
                case WM_RBUTTONUP: ShowMenu(); handled = true; break;
            }
        }
        else if (_taskbarCreatedMsg != 0 && msg == (int)_taskbarCreatedMsg && !_disposed)
        {
            AddIcon();
        }
        return IntPtr.Zero;
    }

    private NOTIFYICONDATA BaseData() => new()
    {
        cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
        hWnd = _source.Handle,
        uID = 1,
        szTip = "",
        szInfo = "",
        szInfoTitle = ""
    };

    private void AddIcon()
    {
        var data = BaseData();
        data.uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP;
        data.uCallbackMessage = WM_TRAYICON;
        data.hIcon = _hIcon;
        data.szTip = "Plume — dictée vocale";
        Shell_NotifyIcon(NIM_ADD, ref data);
    }

    private static IntPtr LoadTrayIcon()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Resources", "plume.ico");
            if (File.Exists(path))
            {
                var h = LoadImage(IntPtr.Zero, path, IMAGE_ICON, 16, 16, LR_LOADFROMFILE);
                if (h != IntPtr.Zero) return h;
            }
        }
        catch { /* icône illisible : repli système, jamais de crash */ }
        return LoadIcon(IntPtr.Zero, IDI_APPLICATION);
    }

    /// <summary>Construit et affiche le menu contextuel Win32 au curseur, puis exécute la commande choisie.</summary>
    private void ShowMenu()
    {
        var menu = CreatePopupMenu();
        if (menu == IntPtr.Zero) return;
        try
        {
            AppendMenu(menu, MF_STRING, new IntPtr(CMD_TOGGLE), "Activer/Désactiver la dictée");
            AppendMenu(menu, MF_STRING, new IntPtr(CMD_SETTINGS), "Paramètres");
            // « Copier le dernier message » grisé tant qu'aucune dictée n'a été faite (même règle qu'avant).
            bool hasLast = !string.IsNullOrEmpty(_getLastText());
            AppendMenu(menu, MF_STRING | (hasLast ? 0 : MF_GRAYED), new IntPtr(CMD_COPYLAST), "Copier le dernier message");
            AppendMenu(menu, MF_STRING | (_startupChecked ? MF_CHECKED : 0), new IntPtr(CMD_STARTUP), "Démarrer avec Windows");
            AppendMenu(menu, MF_SEPARATOR, IntPtr.Zero, null);
            AppendMenu(menu, MF_STRING, new IntPtr(CMD_QUIT), "Quitter");

            GetCursorPos(out var pt);
            SetForegroundWindow(_source.Handle); // requis : sinon le menu ne se ferme pas au clic ailleurs (motif MSDN)
            int cmd = TrackPopupMenuEx(menu, TPM_RIGHTBUTTON | TPM_RETURNCMD, pt.X, pt.Y, _source.Handle, IntPtr.Zero);
            PostMessage(_source.Handle, WM_NULL, IntPtr.Zero, IntPtr.Zero); // motif MSDN post-TrackPopupMenu

            switch (cmd)
            {
                case CMD_TOGGLE: _onToggleDictation(); break;
                case CMD_SETTINGS: _onOpenSettings(); break;
                case CMD_COPYLAST: CopyLast(); break;
                case CMD_STARTUP:
                    _startupChecked = !_startupChecked;
                    _onSetStartup(_startupChecked);
                    break;
                case CMD_QUIT: _onQuit(); break;
            }
        }
        finally
        {
            DestroyMenu(menu);
        }
    }

    /// <summary>Copie la dernière dictée dans le presse-papiers ; notification discrète, jamais de crash.</summary>
    private void CopyLast()
    {
        var text = _getLastText();
        if (string.IsNullOrEmpty(text)) return; // entrée grisée en principe : double garde
        try
        {
            Clipboard.SetText(text);
            ShowBalloon("Plume", "Copié ✓");
        }
        catch { /* presse-papiers verrouillé : pas de crash */ }
    }

    /// <summary>Reflète l'état « Démarrer avec Windows » dans le menu (sans redéclencher la sauvegarde).</summary>
    public void RefreshStartup(bool enabled) => _startupChecked = enabled;

    public void ShowBalloon(string title, string message)
    {
        var data = BaseData();
        data.uFlags = NIF_INFO;
        data.szInfoTitle = title.Length > 63 ? title[..63] : title;
        data.szInfo = message.Length > 255 ? message[..255] : message;
        data.uTimeoutOrVersion = 3000;
        data.dwInfoFlags = NIIF_INFO;
        Shell_NotifyIcon(NIM_MODIFY, ref data);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        var data = BaseData();
        Shell_NotifyIcon(NIM_DELETE, ref data);
        _source.RemoveHook(WndProc);
        _source.Dispose();
        if (_hIcon != IntPtr.Zero) DestroyIcon(_hIcon);
    }
}
