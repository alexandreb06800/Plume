using System.Windows;
using Microsoft.Win32;

namespace Plume.Services;

/// <summary>Applique le thème clair/sombre en suivant Windows, à chaud.</summary>
public class ThemeService : IDisposable
{
    private readonly Func<int?> _readAppsUseLightTheme;
    private bool _subscribed;
    private string _mode = "auto"; // "auto" | "light" | "dark"

    /// <summary>Vrai si le thème actuellement appliqué est le sombre.</summary>
    public bool CurrentDark { get; private set; }

    /// <summary>Levé après chaque application/bascule de thème, avec le nouvel état sombre.</summary>
    public event Action<bool>? Changed;

    public ThemeService() : this(ReadRegistryAppsUseLightTheme) { }

    /// <summary>Ctor testable : lecteur de la valeur registre injecté (null = clé absente).</summary>
    public ThemeService(Func<int?> readAppsUseLightTheme)
        => _readAppsUseLightTheme = readAppsUseLightTheme;

    /// <summary>Vrai si Windows est en thème sombre (AppsUseLightTheme = 0).</summary>
    public bool IsWindowsDark() => _readAppsUseLightTheme() == 0;

    /// <summary>Résout le sombre selon le mode : « dark » force, « light » force, « auto » suit Windows.</summary>
    private bool ResolveDark() => _mode == "dark" || (_mode != "light" && IsWindowsDark());

    private static int? ReadRegistryAppsUseLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") as int?;
        }
        catch { return null; } // clé indisponible : on suppose clair
    }

    /// <summary>Applique le thème selon le mode (« auto » | « light » | « dark ») et écoute Windows.</summary>
    public void Apply(string mode)
    {
        _mode = string.IsNullOrWhiteSpace(mode) ? "auto" : mode;
        SwapColors(ResolveDark());
        CurrentDark = ResolveDark();
        Changed?.Invoke(CurrentDark);
        if (!_subscribed)
        {
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
            _subscribed = true;
        }
    }

    /// <summary>Recharge les couleurs et lève <see cref="Changed"/> seulement si le résultat clair/sombre diffère de
    /// <see cref="CurrentDark"/> ; renvoie vrai dans ce cas. Windows signale « General » pour bien d'autres
    /// réglages que le thème : sans cette comparaison, chacun rechargeait tout le dictionnaire de couleurs.</summary>
    public bool RefreshFromWindows()
    {
        var dark = ResolveDark();
        if (dark == CurrentDark) return false;
        SwapColors(dark);
        CurrentDark = dark;
        Changed?.Invoke(CurrentDark);
        return true;
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        // En mode auto seulement : on suit le basculement clair/sombre de Windows.
        if (_mode == "auto" && e.Category == UserPreferenceCategory.General)
            Application.Current?.Dispatcher.Invoke(() => RefreshFromWindows());
    }

    private static void SwapColors(bool dark)
    {
        var app = Application.Current;
        if (app is null) return;
        var uri = new Uri(dark ? "Resources/Colors.Dark.xaml" : "Resources/Colors.Light.xaml",
                          UriKind.Relative);
        var dict = new ResourceDictionary { Source = uri };

        var dicts = app.Resources.MergedDictionaries;
        // Retire l'ancien dico de couleurs (repéré par la présence de BgColor).
        for (int i = dicts.Count - 1; i >= 0; i--)
            if (dicts[i].Contains("BgColor")) dicts.RemoveAt(i);
        dicts.Insert(0, dict); // couleurs avant Theme.xaml
    }

    public void Dispose()
    {
        if (_subscribed)
        {
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
            _subscribed = false;
        }
    }
}
