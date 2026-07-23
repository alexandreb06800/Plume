using System.Windows.Controls;

namespace Plume.Views.SettingsPanels;

/// <summary>Onglet « À propos » des Réglages : version, changelog, lien vers la prise en main. Markup pur ;
/// la logique (version courante, réouverture de l'onboarding) vit dans <see cref="SettingsWindow"/>
/// (voir SettingsWindow.APropos.cs).</summary>
public partial class AProposPanel : UserControl
{
    public AProposPanel()
    {
        InitializeComponent();
    }
}
