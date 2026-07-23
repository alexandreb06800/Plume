using System.Linq;
using Plume.Services;
using Xunit;

namespace Plume.Tests;

public class StyleCatalogTests
{
    [Fact]
    public void BuiltIns_Contient_6_Styles_Tous_Integres_Avec_Prompt()
    {
        var styles = StyleCatalog.BuiltIns();
        Assert.Equal(6, styles.Count);
        Assert.All(styles, s => Assert.True(s.IsBuiltIn));
        Assert.All(styles, s => Assert.False(string.IsNullOrWhiteSpace(s.Prompt)));
        Assert.All(styles, s => Assert.True(s.ShowInMenu));
        // Ids uniques
        Assert.Equal(6, styles.Select(s => s.Id).Distinct().Count());
    }

    [Fact]
    public void BuiltIns_Favoris_Et_Reglages_Par_Defaut()
    {
        var byId = StyleCatalog.BuiltIns().ToDictionary(s => s.Id);
        Assert.True(byId[StyleCatalog.CommandeVocaleId].IsFavorite);
        Assert.True(byId[StyleCatalog.AffinerId].IsFavorite);
        Assert.True(byId[StyleCatalog.FinaliserEmailId].IsFavorite);
        Assert.False(byId[StyleCatalog.EmailProId].IsFavorite);
        Assert.False(byId[StyleCatalog.TonProId].IsFavorite);
        // Email pro : résultat en fenêtre (non destructif) ; Finaliser : remplacement en place.
        Assert.True(byId[StyleCatalog.EmailProId].ResultInWindow);
        Assert.False(byId[StyleCatalog.FinaliserEmailId].ResultInWindow);
        Assert.Equal(0.3, byId[StyleCatalog.EmailProId].Temperature);
        Assert.Equal(0.3, byId[StyleCatalog.FinaliserEmailId].Temperature);
    }

    [Fact]
    public void CommandeVocale_Est_Le_Seul_Style_Avec_Voix()
    {
        var voice = StyleCatalog.BuiltIns().Where(s => s.WithVoice).ToList();
        var single = Assert.Single(voice);
        Assert.Equal(StyleCatalog.CommandeVocaleId, single.Id);
    }

    [Fact]
    public void DefaultPromptFor_Integre_RetourneLePrompt_Inconnu_RetourneNull()
    {
        Assert.False(string.IsNullOrWhiteSpace(StyleCatalog.DefaultPromptFor(StyleCatalog.AffinerId)));
        Assert.Null(StyleCatalog.DefaultPromptFor("inconnu"));
    }

    [Fact]
    public void BuiltIns_TousLesPrompts_InterdisentLeMarkdown()
    {
        // La sortie des styles remplace la sélection en texte brut : des ** ou # colleraient littéralement
        // (adaptation Llama 3.3, 2026-07-06).
        Assert.All(StyleCatalog.BuiltIns(), s => Assert.Contains("markdown", s.Prompt));
    }

    [Fact]
    public void DictationPrompt_NonVide()
        => Assert.False(string.IsNullOrWhiteSpace(StyleCatalog.DictationPrompt));

    [Fact]
    public void DictationPrompt_Contient_Les_Trois_Invariants_Durcis_V8I0()
    {
        Assert.Contains("Conserve strictement le tutoiement ou le vouvoiement", StyleCatalog.DictationPrompt);
        Assert.Contains("Conserve le vocabulaire du locuteur", StyleCatalog.DictationPrompt);
        Assert.Contains("phonétiquement proche", StyleCatalog.DictationPrompt);
    }
}
