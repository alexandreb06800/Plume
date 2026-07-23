namespace Plume.Services;

/// <summary>Assemble le system prompt d'un style « avec voix » : cadre du style + instruction dictée.
/// Le texte sélectionné (s'il existe) n'est PAS ici : il est passé séparément à CleanAsync.</summary>
public static class CommandPromptBuilder
{
    public static string Build(string stylePrompt, string instruction)
        => Build(stylePrompt, instruction, hasSelection: true);

    public static string Build(string stylePrompt, string instruction, bool hasSelection)
    {
        var frame = string.IsNullOrWhiteSpace(stylePrompt) ? StyleCatalog.CommandPrompt : stylePrompt;
        var instr = (instruction ?? "").Trim();
        if (instr.Length == 0) return frame;

        return hasSelection
            ? frame + "\n\nInstruction de l'utilisateur (à appliquer au texte) : \"" + instr + "\""
            : frame + "\n\nIl n'y a pas de texte sélectionné : produis directement le texte demandé. "
                    + "Instruction de l'utilisateur : \"" + instr + "\"";
    }
}
