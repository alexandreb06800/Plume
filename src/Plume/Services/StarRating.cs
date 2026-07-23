namespace Plume.Services;

/// <summary>Formate une note de fiabilité en étoiles pleines/vides sur 5.</summary>
public static class StarRating
{
    public static string Format(int filled)
    {
        int n = Math.Clamp(filled, 0, 5);
        return new string('★', n) + new string('☆', 5 - n);
    }
}
