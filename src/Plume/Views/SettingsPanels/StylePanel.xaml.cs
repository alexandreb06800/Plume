using System.Windows;
using System.Windows.Controls;

namespace Plume.Views.SettingsPanels;

/// <summary>Onglet « Styles » des Réglages : liste + éditeur de style. Markup et deux gestionnaires liés en
/// XAML seulement (WPF les résout contre la classe du fichier XAML qui les déclare) ; le reste de la logique
/// (édition, glisser-déposer, persistance) vit dans <see cref="SettingsWindow"/> (voir SettingsWindow.Styles.cs),
/// qui référence les éléments nommés ici via le champ généré <c>PanelStyle</c>.</summary>
public partial class StylePanel : UserControl
{
    /// <summary>Levé après chaque modification qui doit déclencher l'auto-enregistrement des Réglages.</summary>
    public event Action? Changed;

    public StylePanel()
    {
        InitializeComponent();
    }

    /// <summary>Étoile favori dans la liste : effet immédiat (le binding TwoWay a déjà rafraîchi le glyphe).</summary>
    private void FavoriteToggle_Click(object sender, RoutedEventArgs e) => Changed?.Invoke();

    /// <summary>Interrupteur Visible dans le menu : effet immédiat.</summary>
    private void ShowInMenuToggle_Click(object sender, RoutedEventArgs e) => Changed?.Invoke();
}
