using System.Runtime.InteropServices;

namespace Plume.Services;

/// <summary>Conversions pixels physiques (Win32Screens.ScreenInfo) ↔ unités WPF (DIP), par moniteur.
/// L'app est PerMonitorV2 (app.manifest) : Window.Left/Top est en DIP alors que Win32Screens.ScreenInfo
/// est en pixels physiques — sans conversion, un écran à 125/150 % décale pastille et menus (voire hors
/// écran). Approximation assumée : division par le facteur d'échelle du moniteur (exacte pour l'écran
/// principal, très proche pour les autres) — suffisant pour « jamais hors écran ». Ne lève jamais.</summary>
public static class ScreenMetrics
{
    private const int MDT_EFFECTIVE_DPI = 0;
    private const uint MONITOR_DEFAULTTONEAREST = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT pt, uint flags);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    /// <summary>Facteur d'échelle (1.0 = 100 %) du moniteur contenant ce point PHYSIQUE. Ne lève jamais.</summary>
    public static double ScaleAtPhysicalPoint(int x, int y)
    {
        try
        {
            var mon = MonitorFromPoint(new POINT { X = x, Y = y }, MONITOR_DEFAULTTONEAREST);
            if (GetDpiForMonitor(mon, MDT_EFFECTIVE_DPI, out var dpiX, out _) == 0 && dpiX > 0)
                return dpiX / 96.0;
        }
        catch { /* shcore indisponible (Windows très ancien) : 100 % */ }
        return 1.0;
    }

    /// <summary>Zone de travail d'un écran, convertie des pixels physiques vers les DIP WPF.</summary>
    public static (double Left, double Top, double Width, double Height) WorkAreaDip(ScreenInfo screen)
    {
        var scale = ScaleAtPhysicalPoint(screen.Left + screen.Width / 2, screen.Top + screen.Height / 2);
        return (screen.Left / scale, screen.Top / scale, screen.Width / scale, screen.Height / scale);
    }

    /// <summary>Écran contenant un point exprimé en DIP (position d'une fenêtre WPF). Repli : écran principal
    /// (ou le point le plus proche en physique via le facteur du moniteur principal) si hors de toute zone.</summary>
    public static ScreenInfo ScreenFromDip(double x, double y)
    {
        foreach (var s in Win32Screens.All())
        {
            var (l, t, w, h) = WorkAreaDip(s);
            if (x >= l && x < l + w && y >= t && y < t + h) return s;
        }
        // Point hors de toute zone de travail (bord d'écran, barre des tâches) : au plus proche en physique,
        // via le facteur du moniteur principal (meilleure approximation disponible sans le HWND).
        var primaryScale = ScaleAtPhysicalPoint(0, 0);
        return Win32Screens.FromPoint((int)(x * primaryScale), (int)(y * primaryScale));
    }
}
