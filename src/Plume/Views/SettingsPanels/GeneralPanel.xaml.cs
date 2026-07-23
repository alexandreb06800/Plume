using System.Windows.Controls;
using System.Windows.Input;

namespace Plume.Views.SettingsPanels;

/// <summary>Onglet « Général » des Réglages : dictée, sons, diagnostic, assistant vocal, modèles &amp; connexion,
/// apparence &amp; système. La logique d'état (auto-enregistrement) vit dans <see cref="SettingsWindow"/>
/// (voir SettingsWindow.General.cs), qui référence les éléments nommés ici via le champ généré
/// <c>PanelGeneral</c> (modificateur par défaut <c>internal</c>, même assembly). Seuls les deux gestionnaires
/// liés en XAML (clic sur la ligne du combo) doivent vivre ici : WPF les résout contre la classe du fichier
/// XAML qui les déclare.</summary>
public partial class GeneralPanel : UserControl
{
    public GeneralPanel()
    {
        InitializeComponent();
    }

    /// <summary>Ouvre le menu déroulant si le clic n'est pas dans la zone de saisie éditable.</summary>
    private static void OpenComboOnRowClick(ComboBox combo, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is TextBox) return; // laisse éditer le texte
        combo.IsDropDownOpen = true;
    }

    private void SttRow_Click(object sender, MouseButtonEventArgs e) => OpenComboOnRowClick(SttCombo, e);
    private void CleanupRow_Click(object sender, MouseButtonEventArgs e) => OpenComboOnRowClick(CleanupCombo, e);

    /// <summary>Ouvre une URL dans le navigateur par défaut. Jamais de crash (navigateur absent).</summary>
    private static void OpenUrl(string url)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { /* pas de navigateur configuré : on n'interrompt rien */ }
    }

    private void OpenGroqKeysLink(object sender, System.Windows.RoutedEventArgs e) => OpenUrl("https://console.groq.com/keys");
    private void OpenOpenRouterKeysLink(object sender, System.Windows.RoutedEventArgs e) => OpenUrl("https://openrouter.ai/keys");
}
