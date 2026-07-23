namespace Plume.Services;

/// <summary>Un son du catalogue des tops début/fin d'écoute (V8-K) : identifiant persisté + nom affiché.</summary>
public record SoundInfo(string Id, string DisplayName);

/// <summary>Catalogue des sons disponibles pour les tops début/fin d'écoute. Aucun fichier audio :
/// les sons sont générés en code par <see cref="TickSoundService"/>.</summary>
public static class SoundCatalog
{
    /// <summary>Identifiant représentant « aucun son ».</summary>
    public const string None = "";

    public static IReadOnlyList<SoundInfo> All { get; } = new[]
    {
        new SoundInfo(None, "Aucun"),
        new SoundInfo("discret", "Discret"),
        new SoundInfo("clair", "Clair"),
    };
}
