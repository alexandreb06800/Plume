using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Plume.Models;

namespace Plume.Views.SettingsPanels;

/// <summary>Onglet « Dictionnaire » des Réglages : vocabulaire (jetons) et règles de remplacement (De → Vers).
/// Entièrement autonome (état + événements) : <see cref="SettingsWindow"/> ne fait que charger la config
/// initiale via <see cref="Load"/> et persister sur <see cref="Changed"/>.</summary>
public partial class DictionnairePanel : UserControl
{
    private readonly ObservableCollection<string> _vocab = new();
    private readonly ObservableCollection<ReplRow> _repl = new();

    /// <summary>Levé après chaque modification qui doit déclencher l'auto-enregistrement des Réglages.</summary>
    public event Action? Changed;

    public DictionnairePanel()
    {
        InitializeComponent();

        VocabChips.ItemsSource = _vocab;
        ReplList.ItemsSource = _repl;
        // États vides : visibles seulement quand la collection l'est (invitation à agir, jamais un cadre vide).
        UpdateDicoEmptyStates();
        _vocab.CollectionChanged += (_, _) => UpdateDicoEmptyStates();
        _repl.CollectionChanged += (_, _) => UpdateDicoEmptyStates();

        VocabAdd.Click += (_, _) => AddVocabWord();
        VocabInput.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) { AddVocabWord(); e.Handled = true; }
        };

        ReplAdd.Click += (_, _) => AddReplacementRule();
        // Ctrl+Entrée ajoute (Entrée seule insère un saut de ligne dans le « Vers » multiligne).
        ReplTo.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
            { AddReplacementRule(); e.Handled = true; }
        };
        // Suppr sur une ligne sélectionnée : équivalent clavier du × au survol.
        ReplList.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Delete && ReplList.SelectedItem is ReplRow r)
            { _repl.Remove(r); Changed?.Invoke(); }
        };
        LineBreakCommandsBox.Checked += (_, _) => Changed?.Invoke();
        LineBreakCommandsBox.Unchecked += (_, _) => Changed?.Invoke();
    }

    /// <summary>Charge l'état initial depuis la config (une seule fois, à l'ouverture des Réglages).</summary>
    public void Load(AppConfig config)
    {
        foreach (var v in config.Vocabulary) _vocab.Add(v);
        foreach (var r in config.Replacements) _repl.Add(new ReplRow { Rule = r });
        LineBreakCommandsBox.IsChecked = config.LineBreakCommandsEnabled;
    }

    public IEnumerable<string> Vocabulary => _vocab;
    public List<ReplacementRule> GetReplacementRules() => _repl.Select(x => x.Rule).ToList();
    public bool LineBreakCommandsEnabled => LineBreakCommandsBox.IsChecked == true;

    private void AddVocabWord()
    {
        var t = VocabInput.Text.Trim();
        if (t.Length > 0 && !_vocab.Contains(t)) { _vocab.Add(t); Changed?.Invoke(); }
        VocabInput.Clear();
        VocabInput.Focus();
    }

    private void AddReplacementRule()
    {
        var f = ReplFrom.Text.Trim();
        var to = ReplTo.Text.Trim();
        if (f.Length == 0) return;
        _repl.Add(new ReplRow { Rule = new ReplacementRule { From = f, To = to } });
        Changed?.Invoke();
        ReplFrom.Clear();
        ReplTo.Clear();
        ReplFrom.Focus();
    }

    private void UpdateDicoEmptyStates()
    {
        VocabEmpty.Visibility = _vocab.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ReplEmpty.Visibility = _repl.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void VocabChipRemove_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is string s) { _vocab.Remove(s); Changed?.Invoke(); }
    }

    private void ReplRowRemove_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is ReplRow r) { _repl.Remove(r); Changed?.Invoke(); }
    }

    /// <summary>Ligne d'affichage d'une règle de remplacement : « De » + aperçu mono-ligne du « Vers »
    /// (les sauts de ligne d'une signature deviennent « ↵ »).</summary>
    private sealed class ReplRow
    {
        public ReplacementRule Rule { get; init; } = new();
        public string From => Rule.From;
        public string ToPreview => string.IsNullOrEmpty(Rule.To)
            ? "(supprime le mot)"
            : Rule.To.Replace("\r\n", "\n").Replace("\n", " ↵ ");
    }
}
