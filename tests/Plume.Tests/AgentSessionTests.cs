using Plume.Models;
using Xunit;

namespace Plume.Tests;

public class AgentSessionTests
{
    [Fact]
    public void NouvelleSession_EstVide()
    {
        var s = new AgentSession();
        Assert.True(s.IsEmpty);
        Assert.Empty(s.Turns);
    }

    [Fact]
    public void AddTurn_EmpileDansLOrdre()
    {
        var s = new AgentSession();
        s.AddTurn("Quel jour est-il ?", "Lundi.");
        s.AddTurn("Et demain ?", "Mardi.");

        Assert.False(s.IsEmpty);
        Assert.Equal(2, s.Turns.Count);
        Assert.Equal("Quel jour est-il ?", s.Turns[0].Question);
        Assert.Equal("Lundi.", s.Turns[0].Answer);
        Assert.Equal("Et demain ?", s.Turns[1].Question);
        Assert.Equal("Mardi.", s.Turns[1].Answer);
    }

    [Fact]
    public void AddTurn_PlafonneAuxDerniersTours()
    {
        var s = new AgentSession();
        for (int i = 1; i <= AgentSession.MaxTurns + 5; i++)
            s.AddTurn($"Q{i}", $"R{i}");

        Assert.Equal(AgentSession.MaxTurns, s.Turns.Count);
        Assert.Equal("Q6", s.Turns[0].Question);                       // les 5 plus anciens évincés
        Assert.Equal($"Q{AgentSession.MaxTurns + 5}", s.Turns[^1].Question); // les récents conservés
    }
}
