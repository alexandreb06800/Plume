using System.IO;
using System.Reflection;
using System.Windows;
using Plume.Services;

namespace Plume.Views;

/// <summary>Logique de l'onglet « À propos » : version courante, réouverture de l'onboarding et mise à jour
/// à la demande (bouton « Vérifier les mises à jour » — aucun appel réseau sans clic).</summary>
public partial class SettingsWindow
{
    private UpdateInfo? _pendingUpdate; // version trouvée, en attente du clic « Installer et redémarrer »
    private bool _updateBusy;

    private void InitAbout()
    {
        // À propos : rouvre l'assistant de prise en main (V8-B) à la demande.
        PanelAbout.ReplayOnboardingButton.Click += (_, _) => ((App)System.Windows.Application.Current).OpenOnboarding();

        // Version (onglet À propos).
        var ver = Assembly.GetExecutingAssembly().GetName().Version;
        PanelAbout.VersionText.Text = ver is null ? "Version" : $"Version {ver.Major}.{ver.Minor}.{ver.Build}";

        // Mise à jour : bouton en deux temps (dépôt public, aucun jeton).
        PanelAbout.UpdateButton.Click += async (_, _) => await OnUpdateClickedAsync();
    }

    private void ShowUpdateStatus(string text)
    {
        PanelAbout.UpdateStatus.Text = text;
        PanelAbout.UpdateStatus.Visibility = Visibility.Visible;
    }

    /// <summary>1er clic : cherche une version plus récente. 2e clic (si trouvée) : télécharge, vérifie
    /// l'empreinte, installe et redémarre Plume. Ne lève jamais : toute erreur s'affiche sous le bouton.</summary>
    private async Task OnUpdateClickedAsync()
    {
        if (_updateBusy) return;
        _updateBusy = true;
        PanelAbout.UpdateButton.IsEnabled = false;
        try
        {
            ShowUpdateStatus(_pendingUpdate is null
                ? "Recherche d'une nouvelle version…"
                : $"Téléchargement de la version {_pendingUpdate.Version}… Plume va redémarrer toute seule.");

            if (_pendingUpdate is null)
            {
                var latest = await UpdateService.GetLatestAsync();
                var current = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);
                var shown = $"{current.Major}.{current.Minor}.{current.Build}";
                if (latest is null || !UpdateService.IsNewer(current, latest.Version))
                {
                    ShowUpdateStatus($"Plume est à jour (version {shown}).");
                    return;
                }
                _pendingUpdate = latest;
                // Bouton mis en évidence et libellé explicite : au premier essai réel (2026-10-10), « Plume se fermera
                // puis se relancera » a été compris comme « redémarre Plume toi-même » — rien n'était installé.
                PanelAbout.UpdateButton.Content = $"Installer la version {latest.Version}";
                PanelAbout.UpdateButton.Style = (Style)FindResource("PrimaryButton");
                ShowUpdateStatus($"Version {latest.Version} disponible (tu as la {shown}). "
                               + "Clique sur le bouton ci-dessus pour l'installer : Plume la télécharge et redémarre toute seule.");
                return;
            }

            var installDir = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
            var zip = await UpdateService.DownloadAsync(_pendingUpdate, Path.Combine(Path.GetTempPath(), "Plume-update"));
            ShowUpdateStatus("Installation…");
            await Task.Run(() => UpdateService.ApplyArchive(zip, installDir));
            try { File.Delete(zip); } catch { }
            ((App)System.Windows.Application.Current).RestartAfterUpdate();
        }
        catch (UpdateException ex) { ShowUpdateStatus(ex.Message); }
        catch (Exception) { ShowUpdateStatus("La mise à jour a échoué. Réessaie plus tard."); }
        finally
        {
            _updateBusy = false;
            PanelAbout.UpdateButton.IsEnabled = true;
        }
    }
}
