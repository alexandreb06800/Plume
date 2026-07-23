namespace Plume.Services;

/// <summary>Calcule la position par défaut d'une pastille et la ramène dans la zone de travail visible
/// de son écran si nécessaire (« jamais hors écran »). Fonction pure : aucune dépendance WPF/Win32,
/// testable sans écran réel.</summary>
public static class BadgePlacementService
{
    /// <summary>Position par défaut : coin bas-droit de la zone de travail donnée (mêmes marges qu'avant V8-J).</summary>
    public static (double X, double Y) DefaultPosition(double workAreaLeft, double workAreaTop,
        double workAreaWidth, double workAreaHeight, double badgeWidth, double badgeHeight)
        => (workAreaLeft + workAreaWidth - badgeWidth - 24, workAreaTop + workAreaHeight - badgeHeight - 96);

    /// <summary>Ramène (x, y) dans la zone de travail donnée si la pastille en dépasse (écran débranché,
    /// résolution changée, position orpheline…).</summary>
    public static (double X, double Y) Clamp(double x, double y, double badgeWidth, double badgeHeight,
        double workAreaLeft, double workAreaTop, double workAreaWidth, double workAreaHeight)
    {
        var maxX = Math.Max(workAreaLeft, workAreaLeft + workAreaWidth - badgeWidth);
        var maxY = Math.Max(workAreaTop, workAreaTop + workAreaHeight - badgeHeight);
        var clampedX = Math.Min(Math.Max(x, workAreaLeft), maxX);
        var clampedY = Math.Min(Math.Max(y, workAreaTop), maxY);
        return (clampedX, clampedY);
    }
}
