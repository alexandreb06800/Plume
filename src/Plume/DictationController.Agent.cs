using Plume.Models;
using Plume.Services;

namespace Plume;

/// <summary>Assistant vocal (V4-C) : pose une question à la voix, réponse affichée dans une fenêtre,
/// avec mémoire de conversation (tours de suivi) et insertion non destructive de la dernière réponse.</summary>
public partial class DictationController
{
    /// <summary>Appelé à l'appui du raccourci de l'assistant vocal (V4-C). Démarre une écoute en mode agent,
    /// ou un tour de suivi si une conversation est déjà ouverte.</summary>
    public void StartAgent(uint vk = 0)
        => Activate(Mode.Agent, vk, null);

    private void Activate(Mode mode, uint vk, SelectionStyle? style)
    {
        // La « Commande vocale » fonctionne TOUJOURS en tap (toggle), même si le mode global est « maintien » :
        // sa capture de sélection (Ctrl+C synthétique) exige que les modificateurs du raccourci soient relâchés,
        // ce qui est incompatible avec un maintien enfoncé. Appui = capture + écoute ; nouvel appui = arrêt.
        if (mode == Mode.Command)
        {
            switch (_state)
            {
                case State.Idle: _ = StartVoiceStyleAsync(style!); break;
                case State.Listening: if (mode == _mode) StopAndProcess(); break;
                case State.Working: break; // ignore pendant la capture / le traitement
            }
            return;
        }

        var dictMode = _getConfig().DictationMode;
        if (dictMode == "hold")
        {
            if (_state == State.Idle)
            {
                StartListening(mode, vk, style);
                if (_state == State.Listening) StartReleasePolling();
            }
        }
        else
        {
            switch (_state)
            {
                case State.Idle: StartListening(mode, vk, style); break;
                // Exclusion mutuelle : seul le MÊME mode peut arrêter l'écoute en cours.
                case State.Listening: if (mode == _mode) StopAndProcess(); break;
                case State.Working: break; // ignore pendant le traitement
            }
        }
    }

    /// <summary>Mode agent (V4-C) : transcrit la question, interroge le LLM avec l'historique,
    /// affiche la réponse en fenêtre. Ne modifie jamais le document. Ne crashe jamais.</summary>
    private async Task ProcessAgent(byte[] wav)
    {
        var cfg = _getConfig();
        var apiKey = _configService.ResolveApiKey(cfg) ?? ""; // vide si flux 100 % Groq (jamais utilisé alors)
        var groqKey = _configService.ResolveGroqKey(cfg);
        int session = _sessionId;
        var cts = _cts = new CancellationTokenSource();

        try
        {
            SetVisual(DictationVisual.Processing);
            var question = await _stt.TranscribeAsync(wav, cfg.SttModel, apiKey, groqKey, cfg.Vocabulary, cts.Token, TranscriptionService.TimeoutFor(wav.Length));
            if (string.IsNullOrWhiteSpace(question))
            {
                SetVisual(DictationVisual.Idle);
                return;
            }

            _session ??= new AgentSession();
            // Modèle de l'assistant vocal : celui choisi dans Général, sinon le modèle de nettoyage global.
            var agentModel = string.IsNullOrWhiteSpace(cfg.AgentModel) ? cfg.CleanupModel : cfg.AgentModel!;
            var answer = await _agent.AskAsync(_session, question, DateTime.Now, agentModel,
                apiKey, groqKey, cfg.Vocabulary, cts.Token, CleanupTimeout);

            _session.AddTurn(question, answer);
            LastText = answer; // mémoire seule : alimente « Copier le dernier texte »
            SetVisual(DictationVisual.Idle);
            ShowAnswer(question, answer);
        }
        catch (OperationCanceledException) { SetVisual(DictationVisual.Idle); }
        catch (OpenRouterException ex)
        {
            ReportError(ex.Message);
            if (ex.IsUnauthorized) OpenSettingsRequested?.Invoke();
        }
        catch
        {
            ReportError("Une erreur est survenue, réessaie.");
        }
        finally
        {
            if (session == _sessionId)
            {
                _hotkey.UnregisterEscape();
                _state = State.Idle;
            }
            // Décorrélé du test de session ci-dessus : voir le commentaire équivalent dans StopAndProcess.
            if (_cts == cts) _cts = null;
            cts.Dispose();
        }
    }

    /// <summary>Affiche/rafraîchit la fenêtre de réponse (thread UI, instance unique).
    /// À la fermeture, la conversation mémorisée est effacée.</summary>
    private void ShowAnswer(string question, string answer)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            if (_answerWindow is null)
            {
                _answerWindow = new Views.AnswerWindow();
                _answerWindow.Closed += (_, _) => { _answerWindow = null; _session = null; _agentOrigin = IntPtr.Zero; };
                _answerWindow.InsertRequested += InsertLastAnswer;
            }
            _answerWindow.AddTurn(question, answer);
            _answerWindow.Show();
            _answerWindow.Activate();
        });
    }

    /// <summary>Insère la dernière réponse au curseur dans la fenêtre d'origine (non destructif :
    /// pas de Ctrl+C, pas de remplacement de sélection). Ferme la fenêtre en cas de succès.
    /// Jamais de crash : fenêtre disparue / presse-papiers verrouillé → message discret, fenêtre conservée.</summary>
    private void InsertLastAnswer()
    {
        try
        {
            if (string.IsNullOrEmpty(LastText)) return;

            if (_agentOrigin == IntPtr.Zero || !IsWindow(_agentOrigin))
            {
                _answerWindow?.ShowStatus("Fenêtre d'origine introuvable.");
                return;
            }

            // Restaure le presse-papiers courant après le collage (insertion transparente).
            var previous = ClipboardSnapshot.Capture();

            if (_insertion.PasteAndRestore(LastText, _agentOrigin, previous))
                _answerWindow?.Close(); // succès → fermeture (Closed efface session + origine)
            else
                _answerWindow?.ShowStatus("Insertion impossible (presse-papiers occupé).");
        }
        catch
        {
            _answerWindow?.ShowStatus("Insertion impossible.");
        }
    }
}
