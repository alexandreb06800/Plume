using Plume.Models;
using Plume.Services;
using Xunit;

namespace Plume.Tests;

public class ReplacementServiceTests
{
    private static List<ReplacementRule> Rules(params (string from, string to)[] r)
        => r.Select(x => new ReplacementRule { From = x.from, To = x.to }).ToList();

    [Fact]
    public void Apply_MotEntier_Remplace()
    {
        var txt = "je vais sur emevem demain";
        var res = ReplacementService.Apply(txt, Rules(("emevem", "exemple.fr")));
        Assert.Equal("je vais sur exemple.fr demain", res);
    }

    [Fact]
    public void Apply_InsensibleALaCasse()
    {
        var res = ReplacementService.Apply("Emevem ok", Rules(("emevem", "exemple.fr")));
        Assert.Equal("exemple.fr ok", res);
    }

    [Fact]
    public void Apply_NeRemplacePasUnSousMot()
    {
        var res = ReplacementService.Apply("emevemxyz", Rules(("emevem", "X")));
        Assert.Equal("emevemxyz", res);
    }

    [Fact]
    public void Apply_PlusieursRegles()
    {
        var res = ReplacementService.Apply("sftp vers motmetier",
            Rules(("sftp", "SFTP"), ("motmetier", "MotMetier")));
        Assert.Equal("SFTP vers MotMetier", res);
    }

    [Fact]
    public void Apply_RegleFromVide_Ignoree()
    {
        var res = ReplacementService.Apply("texte", Rules(("", "X")));
        Assert.Equal("texte", res);
    }

    [Fact]
    public void Apply_TexteVide_RenvoieVide()
    {
        Assert.Equal("", ReplacementService.Apply("", Rules(("a", "b"))));
    }

    [Fact]
    public void Apply_ToAvecDollar_PasDInterpretationRegex()
    {
        var res = ReplacementService.Apply("prix abc", Rules(("abc", "$100")));
        Assert.Equal("prix $100", res);
    }

    [Fact]
    public void ApplyLineBreaks_ALaLigne_SautSimpleEtMajuscule()
    {
        var res = ReplacementService.ApplyLineBreaks("Bonjour à la ligne merci");
        Assert.Equal("Bonjour.\nMerci", res);
    }

    [Fact]
    public void ApplyLineBreaks_RetourALaLigne_MemeComportementQueALaLigne()
    {
        var res = ReplacementService.ApplyLineBreaks("Voici le point retour à la ligne suite");
        Assert.Equal("Voici le point.\nSuite", res);
    }

    [Fact]
    public void ApplyLineBreaks_NouvelleLigne_SautSimple()
    {
        var res = ReplacementService.ApplyLineBreaks("Section un nouvelle ligne section deux");
        Assert.Equal("Section un.\nSection deux", res);
    }

    [Fact]
    public void ApplyLineBreaks_NouveauParagraphe_SautDouble()
    {
        var res = ReplacementService.ApplyLineBreaks("Premier bloc nouveau paragraphe deuxième bloc");
        Assert.Equal("Premier bloc.\n\nDeuxième bloc", res);
    }

    [Fact]
    public void ApplyLineBreaks_InsensibleALaCasse()
    {
        var res = ReplacementService.ApplyLineBreaks("Test À LA LIGNE suite");
        Assert.Equal("Test.\nSuite", res);
    }

    [Fact]
    public void ApplyLineBreaks_PonctuationAvantEtApresAbsorbee()
    {
        var res = ReplacementService.ApplyLineBreaks("Voici le point, à la ligne, ensuite la suite");
        Assert.Equal("Voici le point.\nEnsuite la suite", res);
    }

    [Fact]
    public void ApplyLineBreaks_PointDejaPresent_PasDePointDouble()
    {
        var res = ReplacementService.ApplyLineBreaks("Voici le point. à la ligne ensuite");
        Assert.Equal("Voici le point.\nEnsuite", res);
    }

    [Fact]
    public void ApplyLineBreaks_SansDeclencheur_TexteInchange()
    {
        var txt = "Un texte tout à fait normal sans commande de saut de ligne.";
        Assert.Equal(txt, ReplacementService.ApplyLineBreaks(txt));
    }

    [Fact]
    public void ApplyLineBreaks_TexteVide_RenvoieVide()
    {
        Assert.Equal("", ReplacementService.ApplyLineBreaks(""));
    }

    [Fact]
    public void ApplyLineBreaks_DeclencheurEnFinDeTexte_PasDeCrash()
    {
        var res = ReplacementService.ApplyLineBreaks("Merci beaucoup à la ligne");
        Assert.Equal("Merci beaucoup.\n", res);
    }

    [Fact]
    public void ApplyLineBreaks_PlusieursDeclencheursDansLeMemeTexte()
    {
        var res = ReplacementService.ApplyLineBreaks("Un à la ligne deux nouveau paragraphe trois");
        Assert.Equal("Un.\nDeux.\n\nTrois", res);
    }

    [Fact]
    public void Apply_ToNull_SupprimeLeMot()
    {
        var res = ReplacementService.Apply("un emevem là", Rules(("emevem", null!)));
        Assert.Equal("un  là", res);
    }

    [Fact]
    public void Apply_VersMultiligne_ConserveLesSauts()
    {
        // Champ « Vers » multi-ligne (signatures, livré en 5.1.11).
        var res = ReplacementService.Apply("voici masign merci",
            Rules(("masign", "Jean EXEMPLE\nACME")));
        Assert.Equal("voici Jean EXEMPLE\nACME merci", res);
    }

    [Fact]
    public void ApplyLineBreaks_PuisApply_LesDeuxEtapesSeComposent()
    {
        // Ordre réel du contrôleur : ApplyLineBreaks d'abord, puis Apply (substitutions).
        var apresSauts = ReplacementService.ApplyLineBreaks("envoie à emevem à la ligne merci");
        var res = ReplacementService.Apply(apresSauts, Rules(("emevem", "exemple.fr")));
        Assert.Equal("envoie à exemple.fr.\nMerci", res);
    }

    [Fact]
    public void TrimLineEnds_RetireLesEspacesDeFinDeLigne()
    {
        Assert.Equal("Points :\n\n- Budget\n- Planning", ReplacementService.TrimLineEnds("Points :  \r\n  \n- Budget   \n- Planning\t"));
    }

    [Fact]
    public void TrimLineEnds_GardeLesEspacesDeDebutEtLeTexteSimple()
    {
        Assert.Equal("  - Sous-point", ReplacementService.TrimLineEnds("  - Sous-point "));
        Assert.Equal("Bonjour.", ReplacementService.TrimLineEnds("Bonjour."));
    }

    [Fact]
    public void ApplyLineBreaks_CommandeEnTeteDeLigne_PasDePointEnDouble()
    {
        // Le nettoyage a placé la commande en début de ligne, après une phrase déjà close.
        Assert.Equal("Merci pour ton retour.\nJe regarde ça demain.",
            ReplacementService.ApplyLineBreaks("Merci pour ton retour.\nÀ la ligne, je regarde ça demain."));
    }

    [Fact]
    public void ApplyLineBreaks_ApresQuestion_PasDePointAjoute()
    {
        Assert.Equal("Tu viens ?\nMerci.", ReplacementService.ApplyLineBreaks("Tu viens ? À la ligne merci."));
    }

    [Fact]
    public void ApplyLineBreaks_EnTeteDeTexte_PasDePoint()
    {
        Assert.Equal("\nBonjour", ReplacementService.ApplyLineBreaks("à la ligne bonjour"));
    }

    [Theory]
    [InlineData("je code en c++ tous les jours", "c++", "C++", "je code en C++ tous les jours")]
    [InlineData("merci @sig", "@sig", "Jean", "merci Jean")]
    [InlineData("voir (test) ici", "(test)", "[ok]", "voir [ok] ici")]
    public void Apply_FromAvecSymboles_RemplaceLitteralement(string texte, string de, string vers, string attendu)
        => Assert.Equal(attendu, ReplacementService.Apply(texte, Rules((de, vers))));

    [Fact]
    public void Apply_FromAvecSymboles_NeMatchePasDansUnMot()
        => Assert.Equal("contact@sigma.fr", ReplacementService.Apply("contact@sigma.fr", Rules(("@sig", "X"))));

    [Theory]
    [InlineData("Corrige l'erreur à la ligne 3 du tableau")]
    [InlineData("Regarde la nouvelle ligne 12 de la facture")]
    public void ApplyLineBreaks_SuiviDUnNombre_TexteInchange(string texte)
        => Assert.Equal(texte, ReplacementService.ApplyLineBreaks(texte));
}
