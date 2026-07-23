using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Color = System.Windows.Media.Color;

namespace Plume.Services;

/// <summary>Barre de titre sombre via DWM : mode immersif (Win10 1809+) + couleur exacte (Win11).</summary>
public static class DarkTitleBar
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE_OLD = 19; // builds < 18985
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_CAPTION_COLOR = 35;               // Win11 (build >= 22000)

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    /// <summary>Applique (ou retire) la barre de titre sombre. À appeler après SourceInitialized. Jamais d'exception.</summary>
    public static void Apply(Window window, bool dark)
    {
        try
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;

            int on = dark ? 1 : 0;
            if (DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref on, sizeof(int)) != 0)
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_OLD, ref on, sizeof(int));

            // Win11 : caler la couleur de la barre sur le fond de l'app (token CaptionColor).
            if (Environment.OSVersion.Version.Build >= 22000
                && Application.Current?.TryFindResource("CaptionColor") is Color c)
            {
                int colorref = c.R | (c.G << 8) | (c.B << 16); // COLORREF = 0x00BBGGRR
                DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref colorref, sizeof(int));
            }
        }
        catch { /* dégradé silencieux : barre par défaut */ }
    }
}
