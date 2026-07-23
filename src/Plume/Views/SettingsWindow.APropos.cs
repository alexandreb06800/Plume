using System.Reflection;

namespace Plume.Views;

/// <summary>Logique de l'onglet « À propos » : version courante + réouverture de l'onboarding.</summary>
public partial class SettingsWindow
{
    private void InitAbout()
    {
        // À propos : rouvre l'assistant de prise en main (V8-B) à la demande.
        PanelAbout.ReplayOnboardingButton.Click += (_, _) => ((App)System.Windows.Application.Current).OpenOnboarding();

        // Version (onglet À propos).
        var ver = Assembly.GetExecutingAssembly().GetName().Version;
        PanelAbout.VersionText.Text = ver is null ? "Version" : $"Version {ver.Major}.{ver.Minor}.{ver.Build}";
    }
}
