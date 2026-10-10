using Plume.Models;
using Plume.Services;

namespace Plume;

/// <summary>Styles du menu de la pastille : application écrite (prompt figé sur la sélection) ou avec voix
/// (instruction dictée = « Commande vocale »), et affichage du résultat le cas échéant.</summary>
public partial class DictationController
{
    /// <summary>Attend, de façon bornée (~800 ms) et non bloquante, que Ctrl, Alt, Maj et Win soient
    /// physiquement relâchés. Évite que le Ctrl+C de capture soit parasité par un modificateur du raccourci
    /// encore enfoncé. En cas de dépassement, on tente la capture quand même (au pire « Aucune sélection »).</summary>
    private static async Task WaitForModifiersReleasedAsync()
    {
        const int VK_SHIFT = 0x10, VK_CONTROL = 0x11, VK_MENU = 0x12, VK_LWIN = 0x5B, VK_RWIN = 0x5C;
        for (int waited = 0; waited < 800; waited += 25)
        {
            bool down =
                (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0 ||
                (GetAsyncKeyState(VK_MENU) & 0x8000) != 0 ||
                (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0 ||
                (GetAsyncKeyState(VK_LWIN) & 0x8000) != 0 ||
                (GetAsyncKeyState(VK_RWIN) & 0x8000) != 0;
            if (!down) return;
            await Task.Delay(25);
        }
    }

    /// <summary>Mode « Commande vocale » (V4-B) : transcrit l'instruction, l'applique à la sélection capturée
    /// via le LLM, puis remplace la sélection. Restaure toujours le presse-papiers. Ne crashe jamais.</summary>
    private async Task ProcessCommand(byte[] wav)
    {
        var sel = _commandSelection;
        bool hasSelection = sel is not null && !string.IsNullOrWhiteSpace(sel.Text);

        var cfg = _getConfig();
        var apiKey = _configService.ResolveApiKey(cfg) ?? ""; // vide si flux 100 % Groq (jamais utilisé alors)
        var groqKey = _configService.ResolveGroqKey(cfg);
        int session = _sessionId;
        var cts = _cts = new CancellationTokenSource();

        try
        {
            SetVisual(DictationVisual.Processing);
            var instruction = await _stt.TranscribeAsync(wav, cfg.SttModel, apiKey, groqKey, cfg.Vocabulary, cts.Token, TranscriptionService.TimeoutFor(wav.Length));
            if (string.IsNullOrWhiteSpace(instruction))
            {
                if (sel is not null) _selection.RestoreClipboard(sel);
                SetVisual(DictationVisual.Idle);
                return;
            }

            var style = _activeStyle ?? new SelectionStyle { Prompt = StyleCatalog.CommandPrompt };
            var system = CommandPromptBuilder.Build(style.Prompt, instruction, hasSelection);
            var inputText = hasSelection ? sel!.Text! : "";
            var model = string.IsNullOrWhiteSpace(style.Model) ? cfg.CleanupModel : style.Model!; // modèle du style, sinon global
            var result = await _cleanup.CleanAsync(inputText, system, model, apiKey, groqKey,
                cfg.Vocabulary, cts.Token, CleanupTimeout,
                hasSelection ? CleanupKind.Selection : CleanupKind.NoSelection, style.Temperature);

            SetVisual(DictationVisual.Idle);
            LastText = result;
            if (style.ResultInWindow)
            {
                // Mode fenêtre : le document et la sélection ne sont jamais touchés.
                if (sel is not null) _selection.RestoreClipboard(sel);
                ShowStyleResult(instruction, result);
            }
            else if (hasSelection)
            {
                // Remplace la sélection par le résultat, puis restaure le presse-papiers initial.
                if (!_insertion.PasteAndRestore(result, _targetWindow, sel!.Original))
                {
                    _selection.RestoreClipboard(sel);
                    _tray.ShowBalloon("Plume", "Presse-papiers indisponible : commande non insérée.");
                }
            }
            else
            {
                // Rien n'était sélectionné : insérer au curseur (non destructif).
                if (!_insertion.PasteAndRestore(result, _targetWindow, sel?.Original))
                {
                    // Échec du collage : restaurer le presse-papiers d'origine (sinon il garde le sentinel de capture).
                    if (sel is not null) _selection.RestoreClipboard(sel);
                    _tray.ShowBalloon("Plume", "Presse-papiers indisponible : texte non inséré.");
                }
            }
        }
        catch (OperationCanceledException) { if (sel is not null) _selection.RestoreClipboard(sel); SetVisual(DictationVisual.Idle); }
        catch (OpenRouterException ex)
        {
            if (sel is not null) _selection.RestoreClipboard(sel);
            SetVisual(DictationVisual.Idle);
            _tray.ShowBalloon("Plume", ex.Message);
            if (ex.IsUnauthorized) OpenSettingsRequested?.Invoke();
        }
        catch
        {
            if (sel is not null) _selection.RestoreClipboard(sel);
            SetVisual(DictationVisual.Idle);
            _tray.ShowBalloon("Plume", "Une erreur est survenue, réessaie.");
        }
        finally
        {
            if (session == _sessionId)
            {
                _hotkey.UnregisterEscape();
                _activeStyle = null;
                _commandSelection = null;
                _state = State.Idle;
            }
            // Décorrélé du test de session ci-dessus : voir le commentaire équivalent dans StopAndProcess.
            if (_cts == cts) _cts = null;
            cts.Dispose();
        }
    }

    /// <summary>Affiche le résultat d'un style « Résultat dans une fenêtre » (non destructif) : fixe la cible
    /// du bouton « Insérer au curseur » sur la fenêtre du document, puis réutilise la fenêtre de réponse.</summary>
    private void ShowStyleResult(string title, string result)
    {
        _agentOrigin = _targetWindow;
        ShowAnswer(title, result);
    }

    /// <summary>Applique un style du menu à la sélection courante. WithVoice=false → applique le prompt figé
    /// (sélection requise) ; WithVoice=true → écoute une instruction avant d'appliquer (sélection optionnelle).
    /// Exclusif de la dictée. Ne crashe jamais ; restaure toujours le presse-papiers.</summary>
    /// <summary>targetWindow, si renseignée (menu de la pastille : mémorisée au clic sur la pastille, avant
    /// l'activation du menu), fixe la fenêtre cible au lieu d'un GetForegroundWindow tardif qui renverrait
    /// le menu lui-même (fenêtre à activation désactivée mise à part).</summary>
    public void ApplyStyle(SelectionStyle style, IntPtr targetWindow = default)
    {
        if (_state != State.Idle) return;

        var cfg = _getConfig();
        var styleModel = string.IsNullOrWhiteSpace(style.Model) ? cfg.CleanupModel : style.Model!;
        if (_configService.MissingKeyMessage(cfg, styleModel) is string keyErr)
        {
            _tray.ShowBalloon("Plume", keyErr);
            return;
        }

        if (style.WithVoice) { _ = StartVoiceStyleAsync(style, targetWindow); return; }
        _ = ApplyWrittenStyleAsync(style, cfg, targetWindow);
    }

    /// <summary>Style écrit : capture la sélection, applique le prompt, remplace la sélection.</summary>
    private async Task ApplyWrittenStyleAsync(SelectionStyle style, AppConfig cfg, IntPtr targetWindow = default)
    {
        _state = State.Working;
        int session = ++_sessionId; // nouvelle session : un Cancel() (Échap) l'invalidera
        var cts = _cts = new CancellationTokenSource(); // capture locale : jamais relu depuis le champ après un await
        _hotkey.RegisterEscape(); // Échap annule pendant l'application (l'appel réseau peut durer jusqu'à 30 s)
        // Le try couvre TOUT le corps (y compris la capture de sélection, avant l'ancien try local) : sans
        // ça, une exception imprévue avant le nettoyage laisserait _state bloqué sur Working pour toujours
        // (spinner permanent, raccourcis morts, aucun moyen de s'en sortir sans redémarrer Plume).
        try
        {
            _targetWindow = targetWindow != IntPtr.Zero ? targetWindow : GetForegroundWindow();
            SetVisual(DictationVisual.Processing); // « Application… »

            // Redonne le focus à la fenêtre cible avant la capture (Ctrl+C synthétique) : sans ça, la capture
            // peut viser la mauvaise fenêtre si le focus a bougé entre le clic sur la pastille et ici (menu activé).
            _insertion.FocusWindow(_targetWindow);
            await Task.Delay(30);

            var sel = await _selection.CaptureSelectionAsync();
            if (string.IsNullOrWhiteSpace(sel.Text))
            {
                _selection.RestoreClipboard(sel);
                SetVisual(DictationVisual.Idle);
                return;
            }

            var apiKey = _configService.ResolveApiKey(cfg) ?? ""; // vide si flux 100 % Groq (jamais utilisé alors)
            var groqKey = _configService.ResolveGroqKey(cfg);
            try
            {
                var model = string.IsNullOrWhiteSpace(style.Model) ? cfg.CleanupModel : style.Model!; // modèle du style, sinon global
                var result = await _cleanup.CleanAsync(sel.Text!, style.Prompt, model, apiKey, groqKey,
                    cfg.Vocabulary, cts.Token, CleanupTimeout, CleanupKind.Selection, style.Temperature);
                SetVisual(DictationVisual.Idle);
                LastText = result;
                if (style.ResultInWindow)
                {
                    // Mode fenêtre : le document et la sélection ne sont jamais touchés.
                    _selection.RestoreClipboard(sel);
                    ShowStyleResult(style.Name, result);
                }
                else if (!_insertion.PasteAndRestore(result, _targetWindow, sel.Original))
                {
                    _selection.RestoreClipboard(sel);
                    _tray.ShowBalloon("Plume", "Presse-papiers indisponible : style non appliqué.");
                }
            }
            catch (OperationCanceledException)
            {
                // Échap pendant l'appel réseau : Cancel() a déjà remis l'état et le visuel à Idle ;
                // il reste à rendre son presse-papiers à l'utilisateur (il contient encore la sélection).
                _selection.RestoreClipboard(sel);
            }
            catch (OpenRouterException ex)
            {
                _selection.RestoreClipboard(sel);
                SetVisual(DictationVisual.Idle);
                _tray.ShowBalloon("Plume", ex.Message);
                if (ex.IsUnauthorized) OpenSettingsRequested?.Invoke();
            }
            catch
            {
                _selection.RestoreClipboard(sel);
                SetVisual(DictationVisual.Idle);
            }
        }
        finally
        {
            // Uniquement si aucune session plus récente n'a pris le relais (même garde que StopAndProcess).
            if (session == _sessionId)
            {
                _hotkey.UnregisterEscape();
                _state = State.Idle;
            }
            if (_cts == cts) _cts = null;
            cts.Dispose();
        }
    }

    /// <summary>Style avec voix : capture la sélection (optionnelle), puis écoute une instruction dictée.</summary>
    private async Task StartVoiceStyleAsync(SelectionStyle style, IntPtr targetWindow = default)
    {
        _state = State.Working;
        try
        {
            _activeStyle = style;
            _targetWindow = targetWindow != IntPtr.Zero ? targetWindow : GetForegroundWindow();
            SetVisual(DictationVisual.Processing); // « Préparation… »

            await WaitForModifiersReleasedAsync();
            // Redonne le focus à la fenêtre cible avant la capture (Ctrl+C synthétique) : sans ça, la capture
            // peut viser la mauvaise fenêtre si le focus a bougé entre le clic sur la pastille et ici (menu activé).
            _insertion.FocusWindow(_targetWindow);
            await Task.Delay(30);
            _commandSelection = await _selection.CaptureSelectionAsync(); // Text peut être null (pas de sélection)

            _state = State.Idle;
            StartListening(Mode.Command, 0, style);

            // Si l'écoute n'a pas démarré (pas de clé API), StartListening est sorti tôt sans passer _mode à Command :
            // les nettoyages ultérieurs (conditionnés à _mode == Command) ne restaureraient jamais le presse-papiers.
            // On le restaure ici et on oublie la sélection capturée. (Le cas « micro indisponible », lui, est
            // asynchrone depuis V8-G : _state passe à Listening avant ce résultat, donc géré dans OnMicOpened.)
            if (_state != State.Listening)
            {
                if (_commandSelection is not null) _selection.RestoreClipboard(_commandSelection);
                _commandSelection = null;
                _activeStyle = null;
            }
        }
        catch
        {
            // Filet de sécurité : le chemin normal ne passe pas par ici (il se termine soit en Listening,
            // soit en Idle ci-dessus). Une exception imprévue avant ce point laisserait sinon _state bloqué
            // sur Working pour toujours (spinner permanent, Échap non enregistré, aucun raccourci actif).
            SetVisual(DictationVisual.Idle);
            if (_commandSelection is not null)
            {
                _selection.RestoreClipboard(_commandSelection);
                _commandSelection = null;
            }
            _activeStyle = null;
            _state = State.Idle;
        }
    }
}
