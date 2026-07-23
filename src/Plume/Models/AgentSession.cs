namespace Plume.Models;

/// <summary>Un tour de conversation de l'assistant vocal : question posée + réponse reçue.</summary>
public sealed class AgentTurn
{
    public string Question { get; init; } = "";
    public string Answer { get; init; } = "";
}

/// <summary>Historique d'une conversation de l'assistant vocal (V4-C). Mémoire seule : jamais persisté ni loggué.
/// Borné aux <see cref="MaxTurns"/> derniers tours : limite la mémoire et le coût/latence API des très longues conversations.</summary>
public sealed class AgentSession
{
    /// <summary>Nombre maximal de tours conservés (les plus anciens sont évincés).</summary>
    public const int MaxTurns = 20;

    public List<AgentTurn> Turns { get; } = new();
    public bool IsEmpty => Turns.Count == 0;

    public void AddTurn(string question, string answer)
    {
        Turns.Add(new AgentTurn { Question = question, Answer = answer });
        if (Turns.Count > MaxTurns) Turns.RemoveAt(0);
    }
}
