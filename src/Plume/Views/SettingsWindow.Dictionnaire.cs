namespace Plume.Views;

/// <summary>Logique de l'onglet « Dictionnaire ». Le panneau (<see cref="SettingsPanels.DictionnairePanel"/>)
/// est autonome (vocabulaire, remplacements, commandes de saut de ligne) ; ce fichier ne fait que charger la
/// config initiale et relayer les changements vers <see cref="Persist"/>.</summary>
public partial class SettingsWindow
{
    private void InitDictionnaire()
    {
        PanelDico.Load(_config);
        PanelDico.Changed += Persist;
    }
}
