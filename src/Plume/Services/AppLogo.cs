using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Plume.Services;

/// <summary>Charge le logo Plume (plume-logo.png, repli plume.ico) et l'applique aux fenêtres.
/// Image mise en cache et « gelée » (chargée en mémoire, fichier non verrouillé → redéploiement possible).</summary>
public static class AppLogo
{
    private static BitmapImage? _cached;
    private static bool _tried;

    /// <summary>Le logo, ou null s'il est introuvable. Chargé une seule fois.</summary>
    public static BitmapImage? Image
    {
        get
        {
            if (_tried) return _cached;
            _tried = true;
            try
            {
                var dir = Path.Combine(AppContext.BaseDirectory, "Resources");
                var png = Path.Combine(dir, "plume-logo.png");
                var ico = Path.Combine(dir, "plume.ico");
                var path = File.Exists(png) ? png : (File.Exists(ico) ? ico : null);
                if (path is not null)
                {
                    var img = new BitmapImage();
                    img.BeginInit();
                    img.CacheOption = BitmapCacheOption.OnLoad;   // charge tout en mémoire (ne verrouille pas le fichier)
                    img.DecodePixelWidth = 128;                   // affiché ≤ 54 px (pastille) : décoder 512×512 gaspillerait ~1 Mo
                    img.UriSource = new Uri(path);
                    img.EndInit();
                    img.Freeze();
                    _cached = img;
                }
            }
            catch { /* logo indisponible : les fenêtres gardent l'icône par défaut */ }
            return _cached;
        }
    }

    /// <summary>Applique le logo comme icône de la fenêtre (barre des tâches + barre de titre).</summary>
    public static void Apply(Window w)
    {
        var i = Image;
        if (i is not null) w.Icon = i;
    }
}
