using Plume.Services;

namespace Plume;

/// <summary>Flux de dictée classique : arrêt de l'écoute, dispatch selon le mode, puis
/// transcription → nettoyage → règles de remplacement → insertion pour la dictée elle-même.</summary>
public partial class DictationController
{
    private async void StopAndProcess()
    {
        if (_state != State.Listening) return;
        // Micro encore en cours d'ouverture (2e appui très rapide) : reporter le traitement à OnMicOpened,
        // _audio n'est pas encore prête à être arrêtée (accès concurrent avec le thread d'ouverture).
        if (_micOpening) { _stopRequestedDuringOpen = true; return; }
        _releaseTimer?.Stop(); // fin de l'écoute : plus besoin de surveiller le relâchement
        _state = State.Working;
        // Échap reste actif pendant le traitement : permet d'annuler la transcription/nettoyage en cours.

        var wav = _audio.Stop();
        if (wav.Length == 0)
        {
            _hotkey.UnregisterEscape();
            if (_mode == Mode.Command && _commandSelection is not null)
            {
                _selection.RestoreClipboard(_commandSelection);
                _commandSelection = null;
            }
            SetVisual(DictationVisual.Idle);
            _activeStyle = null;
            _state = State.Idle;
            return;
        }

        if (_mode == Mode.Agent) { await ProcessAgent(wav); return; }
        if (_mode == Mode.Command) { await ProcessCommand(wav); return; }
        await ProcessDictation(wav);
    }

    private async Task ProcessDictation(byte[] wav)
    {
        int session = _sessionId; // ce flux continue la session en cours ; Cancel() l'invalidera
        var cfg = _getConfig();
        var apiKey = _configService.ResolveApiKey(cfg) ?? ""; // vide si flux 100 % Groq (jamais utilisé alors)
        var groqKey = _configService.ResolveGroqKey(cfg); // utilisée pour la STT et le nettoyage « groq/… »
        var cts = _cts = new CancellationTokenSource(); // capture locale : jamais relu depuis le champ après un await

        try
        {
            SetVisual(DictationVisual.Processing);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var raw = await _stt.TranscribeAsync(wav, cfg.SttModel, apiKey, groqKey, cfg.Vocabulary, cts.Token, TranscriptionService.TimeoutFor(wav.Length));
            var voie = string.IsNullOrWhiteSpace(groqKey) ? "OpenRouter" : "Groq direct";
            LogTiming($"Transcription : {sw.ElapsedMilliseconds} ms ({wav.Length / 1024} Ko, {voie}, {cfg.SttModel})");
            _ = _diagLog.Log(cfg.DiagnosticsLogEnabled, "Transcription", $"{sw.ElapsedMilliseconds} ms ({voie}, {cfg.SttModel})");

            // Whisper peut renvoyer une chaîne vide sur un souffle/bruit : sans cette garde, elle partirait
            // quand même au nettoyage, qui peut alors inventer du contenu à partir de rien.
            if (string.IsNullOrWhiteSpace(raw))
            {
                SetVisual(DictationVisual.Idle);
                return;
            }

            string finalText = raw;
            sw.Restart();
            try { finalText = await _cleanup.CleanAsync(raw, cfg.DictationPrompt, cfg.CleanupModel, apiKey, groqKey, cfg.Vocabulary, cts.Token, CleanupTimeout, temperature: cfg.DictationTemperature); }
            catch (OpenRouterException ex)
            {
                finalText = raw;
                _tray.ShowBalloon("Plume", "Nettoyage indisponible : texte brut inséré.");
                _ = _diagLog.Log(cfg.DiagnosticsLogEnabled, "Nettoyage — erreur", $"{ex.GetType().Name} : {ex.Message}");
            }
            var voieClean = cfg.CleanupModel.StartsWith(CleanupService.GroqPrefix, StringComparison.OrdinalIgnoreCase)
                ? "Groq direct" : "OpenRouter";
            LogTiming($"Nettoyage : {sw.ElapsedMilliseconds} ms ({voieClean}, {cfg.CleanupModel})");
            _ = _diagLog.Log(cfg.DiagnosticsLogEnabled, "Nettoyage", $"{sw.ElapsedMilliseconds} ms ({voieClean}, {cfg.CleanupModel})");

            // Commandes vocales de saut de ligne (« à la ligne », « nouveau paragraphe »…), avant les remplacements.
            if (cfg.LineBreakCommandsEnabled) finalText = ReplacementService.ApplyLineBreaks(finalText);

            // Substitutions exactes du dictionnaire, après le nettoyage.
            finalText = ReplacementService.Apply(finalText, cfg.Replacements);

            SetVisual(DictationVisual.Idle);
            LastText = finalText; // mémoire seule : alimente « Copier le dernier texte »
            bool inserted = _insertion.Paste(finalText, _targetWindow);
            if (inserted)
            {
                // Top sonore « insertion réussie » (V8-K), dictée classique uniquement.
                if (cfg.TickSoundsEnabled) TickSoundService.Play(cfg.EndTickSound);

                // Statistiques : compteurs cumulés, sauvegarde hors du chemin de latence (collage déjà fait).
                // Le verrou de ConfigService.Save garantit l'intégrité du fichier ; en mémoire, dernier écrivain gagne (au pire une dictée perdue sur course rare, jamais de corruption).
                cfg.Stats.DictationCount++;
                cfg.Stats.WordCount += UsageStatsService.CountWords(finalText);
                try { _configService.Save(cfg); } catch { /* jamais de crash sur l'écriture des stats */ }

                // Agrégat journalier (fichier séparé, jamais lu au démarrage)
                var days = DailyUsageService.Load();
                DailyUsageService.Record(days, DateTime.Today, UsageStatsService.CountWords(finalText));
                DailyUsageService.Prune(days, DateTime.Today);
                DailyUsageService.Save(days);
            }
            else
            {
                // Presse-papiers verrouillé par une autre application : le texte reste récupérable en
                // mémoire (LastText, menu tray) mais rien n'a été collé — sans ce message, l'échec passait
                // totalement inaperçu (la dictée semblait avoir marché).
                _tray.ShowBalloon("Plume",
                    "Presse-papiers indisponible : texte non inséré. Récupère-le via « Copier le dernier texte » (menu Plume).");
            }
        }
        catch (OperationCanceledException) { SetVisual(DictationVisual.Idle); }
        catch (OpenRouterException ex)
        {
            SetVisual(DictationVisual.Idle);
            _tray.ShowBalloon("Plume", ex.Message);
            if (ex.IsUnauthorized) OpenSettingsRequested?.Invoke();
            _ = _diagLog.Log(cfg.DiagnosticsLogEnabled, "Dictée — erreur", $"{ex.GetType().Name} : {ex.Message}");
        }
        catch (Exception ex)
        {
            SetVisual(DictationVisual.Idle);
            _tray.ShowBalloon("Plume", "Une erreur est survenue, réessaie.");
            _ = _diagLog.Log(cfg.DiagnosticsLogEnabled, "Dictée — erreur", $"{ex.GetType().Name} : {ex.Message}");
        }
        finally
        {
            // Uniquement si aucune session plus récente n'a pris le relais : sinon on écraserait l'état
            // (Échap, style, écoute en cours) de la nouvelle session avec la fin tardive de l'ancienne.
            if (session == _sessionId)
            {
                _hotkey.UnregisterEscape(); // fin du traitement : Échap n'a plus à annuler
                _activeStyle = null;
                _state = State.Idle;
            }
            // Décorrélé du test de session ci-dessus : si une session plus récente a déjà installé son propre
            // _cts, ne pas l'écraser (Cancel() de la nouvelle session ne l'annulerait plus). Sinon (personne
            // n'a pris le relais), le remettre à null pour qu'un futur Cancel() ne retombe pas sur ce cts-ci,
            // déjà disposé juste après.
            if (_cts == cts) _cts = null;
            cts.Dispose();
        }
    }
}
