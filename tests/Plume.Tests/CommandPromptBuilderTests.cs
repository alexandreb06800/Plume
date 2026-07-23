using Plume.Services;
using Xunit;

namespace Plume.Tests;

public class CommandPromptBuilderTests
{
    [Fact]
    public void Build_AvecInstruction_InjecteCadreEtInstruction()
    {
        var s = CommandPromptBuilder.Build("CADRE", "mets en liste");
        Assert.Contains("CADRE", s);
        Assert.Contains("mets en liste", s);
        Assert.Contains("Instruction de l'utilisateur", s);
    }

    [Fact]
    public void Build_InstructionVide_RetourneCadreSeul()
    {
        var s = CommandPromptBuilder.Build("CADRE", "   ");
        Assert.Equal("CADRE", s);
    }

    [Fact]
    public void Build_PromptVide_RepliSurCommandPrompt()
    {
        var s = CommandPromptBuilder.Build("  ", "range par ordre alphabétique");
        Assert.Contains(StyleCatalog.CommandPrompt, s);
        Assert.Contains("range par ordre alphabétique", s);
    }

    [Fact]
    public void Build_InstructionRognee_SupprimeEspacesAutour()
    {
        var s = CommandPromptBuilder.Build("CADRE", "  reformule poliment  ");
        Assert.Contains("\"reformule poliment\"", s);
    }
}
