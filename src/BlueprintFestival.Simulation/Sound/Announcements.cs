namespace BlueprintFestival.Simulation.Sound;

/// <summary>
/// The PA announcements that introduce each act a few minutes before they go on. Every artist on
/// the roster has two, each read by one of the crush announcers' voices; they take turns, so the
/// same act is not introduced the same way twice running. The lines and files are in the game's
/// Audio/Announcements folder, named intro_&lt;artist&gt;_&lt;1 or 2&gt;.
/// </summary>
public static class Announcements
{
    /// <summary>How many announcements each artist has.</summary>
    public const int Takes = 2;

    /// <summary>How many minutes before a set on the main stage the act is announced.</summary>
    public const int MainStageMinutes = 4;

    /// <summary>
    /// How many minutes before a set on the second stage the act is announced. Both stages start
    /// at the same times, so the second one is announced earlier and the two do not talk over each other.
    /// </summary>
    public const int SecondStageMinutes = 9;

    /// <summary>
    /// How many minutes before a set each stage's act is announced, by the stage (main stage
    /// first). Each stage gets its own moment so the announcements do not talk over each other.
    /// </summary>
    public static readonly IReadOnlyList<int> StageMinutes = new[] { MainStageMinutes, SecondStageMinutes, 14, 19 };

    /// <summary>The name of an artist's announcement, take 1 or 2.</summary>
    public static string IdFor(string artist, int take) => $"intro_{Slug(artist)}_{take}";

    /// <summary>An artist's name as it is written in a file name: "The Velvet Otters" is "the_velvet_otters".</summary>
    public static string Slug(string name)
    {
        var chars = new List<char>(name.Length);
        foreach (var c in name.ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(c))
            {
                chars.Add(c);
            }
            else if (chars.Count > 0 && chars[^1] != '_')
            {
                chars.Add('_');
            }
        }

        return new string(chars.ToArray()).TrimEnd('_');
    }
}
