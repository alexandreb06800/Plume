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

        // Mise à jour : jeton facultatif (enregistré dès qu'on quitte le champ) + bouton en deux temps.
        PanelAbout.UpdateTokenBox.Password = _config.UpdateToken ?? "";
        PanelAbout.UpdateTokenBox.LostFocus += (_, _) => SaveUpdateToken();
        PanelAbout.UpdateButton.Click += async (_, _) => await OnUpdateClickedAsync();
    }

    private void SaveUpdateToken()
    {
        var typed = PanelAbout.UpdateTokenBox.Password.Trim();
        var value = typed.Length == 0 ? null : typed;
        if (value == _config.UpdateToken) return;
        _config.UpdateToken = value;
        try { _configService.Save(_config); } catch { /* disque indisponible : le jeton reste valable pour la session */ }
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
            SaveUpdateToken();
            ShowUpdateStatus(_pendingUpdate is null ? "Recherche d'une nouvelle version…" : "Téléchargement…");

            // Hors du thread UI : la lecture de la connexion Git lance un processus.
            var token = await Task.Run(() => UpdateService.ResolveToken(_config.UpdateToken));
            if (token is null)
            {
                ShowUpdateStatus("Aucun accès à GitHub : renseigne un jeton ci-dessous, ou connecte Git sur ce poste.");
                return;
            }

            if (_pendingUpdate is null)
            {
                var latest = await UpdateService.GetLatestAsync(token);
                var current = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);
                var shown = $"{current.Major}.{current.Minor}.{current.Build}";
                if (latest is null || !UpdateService.IsNewer(current, latest.Version))
                {
                    ShowUpdateStatus($"Plume est à jour (version {shown}).");
                    return;
                }
                _pendingUpdate = latest;
                PanelAbout.UpdateButton.Content = "Installer et redémarrer";
                ShowUpdateStatus($"Version {latest.Version} disponible (tu as la {shown}). Plume se fermera puis se relancera.");
                return;
            }

            var installDir = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
            var zip = await UpdateService.DownloadAsync(token, _pendingUpdate, Path.Combine(Path.GetTempPath(), "Plume-update"));
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
