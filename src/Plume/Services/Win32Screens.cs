using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Plume.Services;

/// <summary>Zone de travail d'un écran, en pixels physiques (équivalent de Screen.WorkingArea WinForms).</summary>
public sealed record ScreenInfo(string DeviceName, int Left, int Top, int Width, int Height, bool IsPrimary)
{
    public int Right => Left + Width;
    public int Bottom => Top + Height;
}

/// <summary>Énumération des écrans en Win32 pur (EnumDisplayMonitors/GetMonitorInfo) — remplace
/// System.Windows.Forms.Screen sans charger WinForms. DeviceName est identique à Screen.DeviceName
/// (« \\.\DISPLAY1 ») : les positions de pastilles déjà en config restent valides.</summary>
public static class Win32Screens
{
    private const uint MONITORINFOF_PRIMARY = 1;
    private const uint MONITOR_DEFAULTTONEAREST = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MONITORINFOEX
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, ref RECT rect, IntPtr data);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc proc, IntPtr data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX info);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT pt, uint flags);

    /// <summary>Tous les écrans connectés.</summary>
    public static List<ScreenInfo> All()
    {
        var list = new List<ScreenInfo>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
            (IntPtr hMonitor, IntPtr hdc, ref RECT rect, IntPtr data) =>
            {
                var s = FromHandle(hMonitor);
                if (s is not null) list.Add(s);
                return true;
            },
            IntPtr.Zero);
        return list;
    }

    /// <summary>L'écran contenant ce point (pixels physiques), ou le plus proche.</summary>
    public static ScreenInfo FromPoint(int x, int y)
        => FromHandle(MonitorFromPoint(new POINT { X = x, Y = y }, MONITOR_DEFAULTTONEAREST))
           ?? new ScreenInfo(@"\\.\DISPLAY1", 0, 0, 1920, 1080, true); // repli improbable : jamais de crash

    /// <summary>L'écran principal, ou null s'il est indéterminable.</summary>
    public static ScreenInfo? Primary()
    {
        foreach (var s in All())
            if (s.IsPrimary) return s;
        return null;
    }

    private static ScreenInfo? FromHandle(IntPtr hMonitor)
    {
        if (hMonitor == IntPtr.Zero) return null;
        var info = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
        if (!GetMonitorInfo(hMonitor, ref info)) return null;
        return new ScreenInfo(info.szDevice,
                              info.rcWork.Left, info.rcWork.Top,
                              info.rcWork.Right - info.rcWork.Left,
                              info.rcWork.Bottom - info.rcWork.Top,
                              (info.dwFlags & MONITORINFOF_PRIMARY) != 0);
    }
}
