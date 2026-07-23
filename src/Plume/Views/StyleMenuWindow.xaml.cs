using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Plume.Models;
using Plume.Services;
// Alias locaux : lève l'ambiguïté WPF / WinForms (ImplicitUsings + UseWindowsForms importent les deux),
// cf. GlobalUsings.cs pour Application/Clipboard/MessageBox — ceux-ci sont propres à cette fenêtre.
using Button = System.Windows.Controls.Button;
using Border = System.Windows.Controls.Border;
using Brushes = System.Windows.Media.Brushes;
using Brush = System.Windows.Media.Brush;
using Cursors = System.Windows.Input.Cursors;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using FontFamily = System.Windows.Media.FontFamily;
using Orientation = System.Windows.Controls.Orientation;

namespace Plume.Views;

public partial class StyleMenuWindow : Window
{
    public event Action? AgentChosen;
    public event Action<SelectionStyle>? StyleChosen;

    // Empêche un double Close() : un clic appelle Close(), ce qui désactive la fenêtre et déclenche
    // Deactivated → Close() une 2e fois pendant la fermeture (InvalidOperationException « Window en cours
    // de fermeture »). Toutes les fermetures passent par CloseOnce() (idempotent).
    private bool _closing;

    private void CloseOnce()
    {
        if (_closing) return;
        _closing = true;
        Close();
    }

    public StyleMenuWindow(IReadOnlyList<SelectionStyle> styles, double anchorX, double anchorY)
    {
        InitializeComponent();

        AddItem("🎤  Assistant vocal", () => { CloseOnce(); AgentChosen?.Invoke(); });
        AddSeparator();

        var (favs, others) = StyleService.MenuOrder(styles);
        foreach (var s in favs) AddStyle(s);
        if (favs.Count > 0 && others.Count > 0) AddSeparator();
        foreach (var s in others) AddStyle(s);

        // Positionnement : ancré près de la pastille, borné à la zone de travail de SON écran (V8-J :
        // plusieurs pastilles, une par écran — SystemParameters.WorkArea ne donnerait que l'écran principal).
        Loaded += (_, _) =>
        {
            // anchorX/anchorY sont en DIP (Left/Top de la pastille) : zone de travail convertie en DIP aussi,
            // sinon le menu s'ouvre décalé (voire hors écran) sur un écran à 125/150 %.
            var (l, t, w, h) = ScreenMetrics.WorkAreaDip(ScreenMetrics.ScreenFromDip(anchorX, anchorY));
            Left = Math.Min(anchorX, l + w - ActualWidth - 4);
            Top  = Math.Min(anchorY - ActualHeight, t + h - ActualHeight - 4);
            if (Top < t) Top = t + 4;
            Activate();
        };
        Deactivated += (_, _) => CloseOnce();
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) CloseOnce(); };
    }

    // Glyphe Segoe Fluent Icons/MDL2 (IconFont) préfixant les styles favoris dans le menu.
    private const string FavoriteGlyph = "";

    private void AddStyle(SelectionStyle s)
    {
        var label = s.Name + (s.WithVoice ? "  🎤" : "");
        AddItem(label, () => { CloseOnce(); StyleChosen?.Invoke(s); }, s.IsFavorite ? FavoriteGlyph : null);
    }

    private void AddItem(string text, Action onClick, string? icon = null)
    {
        object content = text;
        if (icon is not null)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(new TextBlock
            {
                Text = icon,
                FontFamily = (FontFamily)FindResource("IconFont"),
                FontSize = 12,
                Foreground = (Brush)FindResource("SecondaryBrush"),
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            });
            row.Children.Add(new TextBlock { Text = text, VerticalAlignment = System.Windows.VerticalAlignment.Center });
            content = row;
        }

        var btn = new Button
        {
            Content = content,
            Style = (Style)FindResource("MenuItem") // survol lisible (voir StyleMenuWindow.xaml)
        };
        btn.Click += (_, _) => onClick();
        ItemsHost.Children.Add(btn);
    }

    private void AddSeparator()
        => ItemsHost.Children.Add(new Border
        {
            Height = 1,
            Background = (Brush)FindResource("HairlineBrush"),
            Margin = new Thickness(8, 4, 8, 4)
        });
}
