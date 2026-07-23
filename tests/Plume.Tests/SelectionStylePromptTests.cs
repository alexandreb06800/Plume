using Plume.Services;
using Xunit;

namespace Plume.Tests;

public class SelectionStylePromptTests
{
    [Fact]
    public void Build_AvecSelection_Contient_Le_Cadre_Et_L_Instruction()
    {
        var p = CommandPromptBuilder.Build("CADRE", "mets en gras", hasSelection: true);
        Assert.Contains("CADRE", p);
        Assert.Contains("mets en gras", p);
    }

    [Fact]
    public void Build_SansSelection_Mentionne_La_Redaction_De_Zero()
    {
        var p = CommandPromptBuilder.Build("CADRE", "rédige un mail de relance", hasSelection: false);
        Assert.Contains("CADRE", p);
        Assert.Contains("rédige un mail de relance", p);
    }

    [Fact]
    public void Build_PromptVide_UtiliseLeCadreParDefaut()
    {
        var p = CommandPromptBuilder.Build("", "x", hasSelection: true);
        Assert.Contains(StyleCatalog.CommandPrompt, p);
    }
}
