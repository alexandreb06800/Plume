using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Plume.Models;
using Plume.Services;

namespace Plume.Views;

/// <summary>Logique de l'onglet « Styles » : liste, éditeur, glisser-déposer. Référence les éléments du
/// panneau via <c>PanelStyle.X</c> ; seuls les deux gestionnaires liés en XAML dans le DataTemplate de la
/// liste (favori, visible dans le menu) vivent directement sur <see cref="SettingsPanels.StylePanel"/>
/// (contrainte WPF : ils sont résolus contre la classe du fichier XAML qui les déclare) et relaient ici
/// via l'événement <c>Changed</c>.</summary>
public partial class SettingsWindow
{
    private readonly ObservableCollection<StyleRow> _styles = new();
    private SelectionStyle? _editingStyle;
    private SelectionStyle? _draftStyle;
    private StyleRow? _dragRow;      // glisser-déposer de la liste des styles
    private System.Windows.Point _dragStart;

    private void InitStyles()
    {
        foreach (var s in _config.SelectionStyles) _styles.Add(new StyleRow { Style = s });
        PanelStyle.StyleList.ItemsSource = _styles;
        PanelStyle.StyleList.SelectionChanged += (_, _) => OnStyleSelected();
        if (PanelStyle.StyleList.Items.Count > 0) PanelStyle.StyleList.SelectedIndex = 0;
        PanelStyle.StyleNewButton.Click += (_, _) => AddStyle();
        PanelStyle.SaveStyleButton.Click += (_, _) => SaveStyle();
        PanelStyle.CancelStyleButton.Click += (_, _) => CancelStyleDraft();
        Closing += (_, _) => CommitStyleEdits(); // fermeture sans « Enregistrer le style » : rien n'est perdu
        PanelStyle.StyleDeleteButton.Click += (_, _) => DeleteStyle();
        PanelStyle.StyleTempSlider.ValueChanged += (_, _) => PanelStyle.StyleTempValue.Text = PanelStyle.StyleTempSlider.Value.ToString("0.0");
        PopulateModelCombo(PanelStyle.StyleModelBox); // choix du modèle par style (persisté au Save du style)

        // Réordonnancement par glisser-déposer : l'ordre de _styles = l'ordre du menu de la pastille.
        PanelStyle.StyleList.PreviewMouseLeftButtonDown += StyleList_PreviewMouseLeftButtonDown;
        PanelStyle.StyleList.PreviewMouseMove += StyleList_PreviewMouseMove;
        PanelStyle.StyleList.Drop += StyleList_Drop;

        PanelStyle.Changed += Persist;
    }

    /// <summary>Reporte silencieusement les champs de l'éditeur vers le style en cours d'édition, comme
    /// l'auto-enregistrement du reste des Réglages : appelé quand la sélection change ou que la fenêtre se
    /// ferme, pour ne jamais perdre une modification faite sans cliquer « Enregistrer le style ».
    /// Un brouillon (nouveau style) reste soumis au bouton explicite ; une saisie invalide (nom ou prompt
    /// vide) n'écrase rien.</summary>
    private void CommitStyleEdits()
    {
        if (_editingStyle is null) return;
        if (_draftStyle is not null && _editingStyle.Id == _draftStyle.Id) return; // brouillon : bouton explicite

        var name = PanelStyle.StyleNameBox.Text.Trim();
        var prompt = PanelStyle.StylePromptBox.Text;
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(prompt)) return;

        bool withVoice = PanelStyle.StyleWithVoiceRow.Visibility == Visibility.Visible
            ? PanelStyle.StyleWithVoiceBox.IsChecked == true
            : _editingStyle.WithVoice;
        bool changed = _editingStyle.Name != name
            || _editingStyle.Prompt != prompt
            || _editingStyle.WithVoice != withVoice
            || _editingStyle.ResultInWindow != (PanelStyle.StyleWindowBox.IsChecked == true)
            || Math.Abs(_editingStyle.Temperature - PanelStyle.StyleTempSlider.Value) > 0.001
            || _editingStyle.Model != ModelComboId(PanelStyle.StyleModelBox.SelectedIndex);
        if (!changed) return;

        _editingStyle.Name = name;
        _editingStyle.Prompt = prompt;
        _editingStyle.WithVoice = withVoice;
        _editingStyle.ResultInWindow = PanelStyle.StyleWindowBox.IsChecked == true;
        _editingStyle.Temperature = PanelStyle.StyleTempSlider.Value;
        _editingStyle.Model = ModelComboId(PanelStyle.StyleModelBox.SelectedIndex);
        RefreshStyleRow(_editingStyle);
        Persist();
    }

    private void OnStyleSelected()
    {
        // La sélection vient de changer mais les champs affichent encore l'ANCIEN style : dernier moment
        // pour sauver ses modifications avant que les champs soient réécrits ci-dessous.
        CommitStyleEdits();
        if (PanelStyle.StyleList.SelectedItem is not StyleRow row) { _editingStyle = null; return; }
        _editingStyle = row.Style;

        PanelStyle.StyleNameBox.Text = row.Style.Name;
        PanelStyle.StyleNameBox.IsReadOnly = row.Style.IsBuiltIn; // nom des intégrés non éditable
        PanelStyle.StylePromptBox.Text = row.Style.Prompt;
        PanelStyle.StyleWithVoiceBox.IsChecked = row.Style.WithVoice;
        // La case n'a de sens que pour un style qui écoute déjà une instruction dictée (Commande vocale
        // et ses éventuels migrés) : bruit inutile pour tous les autres, donc masquée pour eux.
        PanelStyle.StyleWithVoiceRow.Visibility = row.Style.WithVoice ? Visibility.Visible : Visibility.Collapsed;
        PanelStyle.StyleWindowBox.IsChecked = row.Style.ResultInWindow;
        PanelStyle.StyleTempSlider.Value = row.Style.Temperature;
        PanelStyle.StyleTempValue.Text = row.Style.Temperature.ToString("0.0");
        PanelStyle.StyleModelBox.SelectedIndex = ModelComboIndex(row.Style.Model);
        PanelStyle.StyleFormError.Text = "";

        // Tous les styles sont supprimables SAUF « Commande vocale » (intégré protégé).
        PanelStyle.StyleDeleteButton.Visibility = IsProtected(row.Style) ? Visibility.Collapsed : Visibility.Visible;

        bool estBrouillon = _draftStyle is not null && row.Style.Id == _draftStyle.Id;
        PanelStyle.CancelStyleButton.Visibility = estBrouillon ? Visibility.Visible : Visibility.Collapsed;
    }

    private void AddStyle()
    {
        if (_draftStyle is not null) CancelStyleDraft();
        _draftStyle = new SelectionStyle { Id = System.Guid.NewGuid().ToString("N"), Name = "", Prompt = "", ShowInMenu = true };
        var row = new StyleRow { Style = _draftStyle };
        _styles.Add(row);
        PanelStyle.StyleList.SelectedItem = row;
        PanelStyle.StyleNameBox.IsReadOnly = false;
        PanelStyle.StyleWithVoiceBox.IsChecked = false;
        PanelStyle.StyleWithVoiceRow.Visibility = Visibility.Collapsed; // un nouveau style n'écoute jamais d'instruction dictée
        PanelStyle.StyleWindowBox.IsChecked = false;
        PanelStyle.StyleModelBox.SelectedIndex = 0; // par défaut : modèle de nettoyage global
        PanelStyle.CancelStyleButton.Visibility = Visibility.Visible;
        PanelStyle.StyleDeleteButton.Visibility = Visibility.Collapsed;
    }

    private void SaveStyle()
    {
        if (_editingStyle is null) return;
        _editingStyle.Name = PanelStyle.StyleNameBox.Text.Trim();
        _editingStyle.Prompt = PanelStyle.StylePromptBox.Text;
        // La case est masquée pour tout style qui n'écoute pas déjà une instruction dictée : dans ce cas,
        // on ne lit pas sa valeur et WithVoice garde son état actuel (false pour un style normal/nouveau).
        if (PanelStyle.StyleWithVoiceRow.Visibility == Visibility.Visible)
            _editingStyle.WithVoice = PanelStyle.StyleWithVoiceBox.IsChecked == true;
        _editingStyle.ResultInWindow = PanelStyle.StyleWindowBox.IsChecked == true;
        _editingStyle.Temperature = PanelStyle.StyleTempSlider.Value;
        _editingStyle.Model = ModelComboId(PanelStyle.StyleModelBox.SelectedIndex);

        if (string.IsNullOrWhiteSpace(_editingStyle.Name)) { PanelStyle.StyleFormError.Text = "Donne un nom au style."; return; }
        if (string.IsNullOrWhiteSpace(_editingStyle.Prompt)) { PanelStyle.StyleFormError.Text = "Le prompt ne peut pas être vide."; return; }

        _draftStyle = null;
        PanelStyle.StyleFormError.Text = "";
        PanelStyle.CancelStyleButton.Visibility = Visibility.Collapsed;
        PanelStyle.StyleDeleteButton.Visibility = IsProtected(_editingStyle) ? Visibility.Collapsed : Visibility.Visible;
        RefreshStyleRow(_editingStyle);
        Persist();
    }

    private void CancelStyleDraft()
    {
        if (_draftStyle is not null)
        {
            var r = _styles.FirstOrDefault(x => x.Style.Id == _draftStyle!.Id);
            if (r is not null) _styles.Remove(r);
            _draftStyle = null;
        }
        _editingStyle = null;
        PanelStyle.StyleFormError.Text = "";
        PanelStyle.CancelStyleButton.Visibility = Visibility.Collapsed;
        if (PanelStyle.StyleList.Items.Count > 0) PanelStyle.StyleList.SelectedIndex = 0;
    }

    /// <summary>Style protégé contre la suppression : uniquement l'intégré « Commande vocale ».</summary>
    private static bool IsProtected(SelectionStyle s)
        => s.IsBuiltIn && s.Id == StyleCatalog.CommandeVocaleId;

    private void DeleteStyle()
    {
        if (_editingStyle is null || IsProtected(_editingStyle)) return;
        var r = _styles.FirstOrDefault(x => x.Style.Id == _editingStyle.Id);
        if (r is not null) _styles.Remove(r);
        _editingStyle = null;
        if (PanelStyle.StyleList.Items.Count > 0) PanelStyle.StyleList.SelectedIndex = 0;
        Persist();
    }

    /// <summary>Étoile favori dans la liste : effet immédiat.
    /// Le binding TwoWay + le setter de StyleRow.IsFavorite ont déjà rafraîchi le glyphe en place ;
    /// on ne touche ni à la collection ni à la sélection courante.</summary>
    /// <summary>Rafraîchit en place (sans remplacer l'élément de la collection, pour ne pas perdre la
    /// sélection de la ListBox) la ligne d'affichage correspondant au style `s`.</summary>
    private void RefreshStyleRow(SelectionStyle s)
    {
        var row = _styles.FirstOrDefault(r => r.Style.Id == s.Id);
        row?.NotifyChanged();
    }

    // --- Réordonnancement de la liste des styles par glisser-déposer ---
    // L'ordre d'affichage = l'ordre de `_styles` ; StyleService.MenuOrder() (Where préserve l'ordre)
    // range ensuite les favoris en tête sans re-trier le reste : reordonner ici suffit à réordonner le menu.

    private void StyleList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(null);
        _dragRow = (e.OriginalSource as FrameworkElement)?.DataContext as StyleRow;
    }

    private void StyleList_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragRow is null || e.LeftButton != MouseButtonState.Pressed) return;
        var pos = e.GetPosition(null);
        if (Math.Abs(pos.X - _dragStart.X) < 4 && Math.Abs(pos.Y - _dragStart.Y) < 4) return; // seuil clic vs glisser

        var dragged = _dragRow;
        _dragRow = null; // évite de relancer un drag tant que le précédent n'est pas terminé
        DragDrop.DoDragDrop(PanelStyle.StyleList, dragged, DragDropEffects.Move);
    }

    private void StyleList_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(StyleRow)) is not StyleRow dragged) return;
        var target = (e.OriginalSource as FrameworkElement)?.DataContext as StyleRow;
        if (target is null || ReferenceEquals(target, dragged)) return;

        int from = _styles.IndexOf(dragged);
        int targetIndex = _styles.IndexOf(target);
        if (from < 0 || targetIndex < 0) return;

        // Si on lâche sur la moitié basse de la cible, insérer après elle.
        var container = PanelStyle.StyleList.ItemContainerGenerator.ContainerFromItem(target) as System.Windows.Controls.ListBoxItem;
        bool below = container is not null && e.GetPosition(container).Y > container.ActualHeight / 2;

        int insertAt = targetIndex + (below ? 1 : 0);
        if (insertAt > from) insertAt--; // ObservableCollection.Move retire d'abord l'élément : compenser le décalage
        insertAt = Math.Max(0, Math.Min(insertAt, _styles.Count - 1));

        _styles.Move(from, insertAt);
        Persist(); // sauvegarde immédiate : _config.SelectionStyles est reconstruit dans le nouvel ordre
    }

    /// <summary>Ligne d'affichage d'un style dans la liste.</summary>
    public class StyleRow : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        private void Raise(string n) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(n));

        public SelectionStyle Style { get; init; } = new();
        public string Name => Style.Name;
        public string FavGlyph => Style.IsFavorite ? "★" : "☆";
        /// <summary>Pastille d'icône de la ligne : plume (écrit) ou micro (avec voix).</summary>
        public string IconGlyph => Style.WithVoice ? "" : "";
        public bool IsFavorite { get => Style.IsFavorite; set { Style.IsFavorite = value; Raise(nameof(FavGlyph)); } }
        public bool ShowInMenu { get => Style.ShowInMenu; set => Style.ShowInMenu = value; }
        public string Sub
        {
            get
            {
                var s = Style.IsBuiltIn ? "Intégré" : "Perso";
                if (Style.WithVoice) s += " · avec voix";
                if (Style.ResultInWindow) s += " · fenêtre";
                return s;
            }
        }

        /// <summary>Rafraîchit les propriétés calculées après un changement fait hors binding (ex. Save de l'éditeur).</summary>
        public void NotifyChanged()
        {
            Raise(nameof(Name));
            Raise(nameof(FavGlyph));
            Raise(nameof(IconGlyph));
            Raise(nameof(Sub));
        }
    }
}
