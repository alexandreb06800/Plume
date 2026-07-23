using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Plume.Services;
using Brush = System.Windows.Media.Brush;
using FontFamily = System.Windows.Media.FontFamily;

namespace Plume.Views;

/// <summary>Fenêtre de réponse de l'assistant vocal (V4-C) : lecture seule, conversation multi-tours,
/// Copier (dernière réponse) + Fermer. Non destructive : n'écrit jamais dans le document.</summary>
public partial class AnswerWindow : Window
{
    private string _lastAnswer = "";
    private System.Windows.Threading.DispatcherTimer? _copyTimer;

    private const string DefaultHint = "Repose une question avec ton raccourci.";

    /// <summary>Levé quand l'utilisateur clique « Insérer au curseur ». Le contrôleur fait le collage.</summary>
    public event Action? InsertRequested;

    public AnswerWindow()
    {
        InitializeComponent();
        AppLogo.Apply(this); // icône de fenêtre (barre des tâches / titre)

        SourceInitialized += (_, _) =>
        {
            var theme = ((App)Application.Current).Theme;
            Services.DarkTitleBar.Apply(this, theme?.CurrentDark ?? false);
            if (theme is not null)
            {
                Action<bool> onChanged = dark => Dispatcher.Invoke(() => Services.DarkTitleBar.Apply(this, dark));
                theme.Changed += onChanged;
                Closed += (_, _) => theme.Changed -= onChanged;
            }
        };

        Hint.Text = DefaultHint;
        InsertButton.Click += (_, _) => InsertRequested?.Invoke();
        CopyButton.Click += (_, _) => CopyLastAnswer();
        CloseButton.Click += (_, _) => Close();
        // Échap ferme la fenêtre (et donc termine la conversation côté contrôleur).
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
    }

    /// <summary>Copie la dernière réponse : texte brut (marqueurs markdown retirés) + HTML (gras, listes)
    /// pour conserver la mise en forme au collage dans Outlook/Word. Le presse-papiers peut être tenu un
    /// instant par un autre processus (OneDrive, Office…) : plusieurs tentatives, puis retour visuel sur le
    /// bouton — jamais un échec silencieux. Jamais de crash.</summary>
    private async void CopyLastAnswer()
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                var data = new DataObject();
                data.SetText(MarkdownHtml.ToPlainText(_lastAnswer));
                data.SetData(DataFormats.Html, new System.IO.MemoryStream(MarkdownHtml.ToCfHtmlBytes(_lastAnswer)), false);
                Clipboard.SetDataObject(data, true);
                ShowCopyFeedback("Copié ✓");
                return;
            }
            catch
            {
                await System.Threading.Tasks.Task.Delay(100);
            }
        }
        ShowCopyFeedback("Échec de la copie");
    }

    /// <summary>Affiche un message bref sur le bouton Copier, puis restaure son libellé (timer unique réutilisé).</summary>
    private void ShowCopyFeedback(string message)
    {
        CopyButton.Content = message;
        _copyTimer?.Stop();
        _copyTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
        _copyTimer.Tick += (_, _) => { _copyTimer!.Stop(); CopyButton.Content = "Copier"; };
        _copyTimer.Start();
    }

    /// <summary>Affiche un message discret dans le pied de fenêtre (échec d'insertion). Jamais bloquant.</summary>
    public void ShowStatus(string message) => Hint.Text = message;

    /// <summary>Ajoute une paire Question / Réponse, mémorise la dernière réponse (brut), défile en bas.
    /// La réponse est rendue en markdown léger (gras, italique, code, puces, numéros).</summary>
    public void AddTurn(string question, string answer)
    {
        _lastAnswer = answer;

        Conversation.Children.Add(new TextBlock
        {
            Text = question,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, Conversation.Children.Count == 0 ? 0 : 14, 0, 4),
            Foreground = (Brush)FindResource("SecondaryBrush")
        });
        Conversation.Children.Add(RenderAnswer(answer));

        Scroller.ScrollToEnd();
    }

    /// <summary>Construit l'affichage WPF d'une réponse à partir du markdown léger parsé.</summary>
    private StackPanel RenderAnswer(string answer)
    {
        var ink = (Brush)FindResource("InkBrush");
        var panel = new StackPanel();

        foreach (var block in LightMarkdown.Parse(answer))
        {
            var tb = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = ink };

            if (block.Kind == BlockKind.Bullet)
            {
                tb.Margin = new Thickness(12, 2, 0, 0);
                tb.Inlines.Add(new Run("•  "));
            }
            else if (block.Kind == BlockKind.Numbered)
            {
                tb.Margin = new Thickness(12, 2, 0, 0);
                tb.Inlines.Add(new Run($"{block.Number}.  "));
            }
            else
            {
                tb.Margin = new Thickness(0, 2, 0, 0);
            }

            AppendInlines(tb, block.Inlines);
            panel.Children.Add(tb);
        }
        return panel;
    }

    /// <summary>Mappe les fragments inline vers des Inline WPF natifs.</summary>
    private static void AppendInlines(TextBlock tb, IReadOnlyList<InlineRun> runs)
    {
        foreach (var r in runs)
        {
            switch (r.Kind)
            {
                case InlineKind.Bold:
                    tb.Inlines.Add(new Bold(new Run(r.Text)));
                    break;
                case InlineKind.Italic:
                    tb.Inlines.Add(new Italic(new Run(r.Text)));
                    break;
                case InlineKind.Code:
                    tb.Inlines.Add(new Run(r.Text) { FontFamily = new FontFamily("Consolas") });
                    break;
                default:
                    tb.Inlines.Add(new Run(r.Text));
                    break;
            }
        }
    }
}
