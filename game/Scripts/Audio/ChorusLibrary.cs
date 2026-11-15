using System.Collections.Generic;
using BlueprintFestival.Simulation.Show;
using Godot;

namespace BlueprintFestival.Audio;

/// <summary>
/// Knows where the choruses of every song are, so the crowd can sing along. A song's choruses are
/// read from the ".chorus" file beside it (made with the chorus marker on the main menu), or from
/// the copy in the player's save folder when the music folder cannot be written to. A song with
/// neither has no sing-along. Also finds a song's singing on its own (a vocal stem), if there is one.
/// </summary>
public static class ChorusLibrary
{
    private const string SaveFolder = "user://choruses";

    private static readonly Dictionary<string, Choruses> Known = [];
    private static readonly Dictionary<string, string?> Vocals = [];

    /// <summary>The choruses of a song; none if it has not been marked.</summary>
    public static Choruses Of(string artist, SongFile song)
    {
        var key = Key(artist, song.File);
        if (!Known.TryGetValue(key, out var choruses))
        {
            choruses = ReadSaved(artist, song.File);
            Known[key] = choruses;
        }

        return choruses;
    }

    /// <summary>The path of a song's singing on its own, or null if there is none beside it.</summary>
    public static string? VocalsOf(string artist, SongFile song)
    {
        var key = Key(artist, song.File);
        if (!Vocals.TryGetValue(key, out var path))
        {
            path = null;
            foreach (var file in song.VocalsFiles())
            {
                var at = $"{MusicPlayer.Folder}/{artist}/{file}";
                if (ResourceLoader.Exists(at))
                {
                    path = at;
                    break;
                }
            }

            Vocals[key] = path;
        }

        return path;
    }

    /// <summary>
    /// Saves a song's choruses: beside the song when the music folder is a real folder that can be
    /// written to (it is when the game is run from Godot), otherwise in the player's own save folder.
    /// Saving none deletes the file, so the song has no sing-along. Returns where it was saved.
    /// </summary>
    public static string Save(string artist, SongFile song, Choruses choruses)
    {
        Known[Key(artist, song.File)] = choruses;
        var name = Choruses.FileFor(song.File);
        var folder = ProjectSettings.GlobalizePath($"{MusicPlayer.Folder}/{artist}");
        if (!folder.StartsWith("res://", System.StringComparison.Ordinal) && System.IO.Directory.Exists(folder))
        {
            try
            {
                return Write(System.IO.Path.Combine(folder, name), choruses);
            }
            catch (System.Exception e) when (e is System.IO.IOException or System.UnauthorizedAccessException)
            {
                // Fall through to the save folder.
            }
        }

        var saveTo = ProjectSettings.GlobalizePath($"{SaveFolder}/{artist}");
        System.IO.Directory.CreateDirectory(saveTo);
        return Write(System.IO.Path.Combine(saveTo, name), choruses);
    }

    private static string Write(string path, Choruses choruses)
    {
        if (choruses.Any)
        {
            System.IO.File.WriteAllText(path, choruses.Write());
        }
        else if (System.IO.File.Exists(path))
        {
            System.IO.File.Delete(path);
        }

        return path;
    }

    private static string Key(string artist, string file) => $"{artist}/{file}";

    /// <summary>The chorus file beside the song, then the copy in the save folder.</summary>
    private static Choruses ReadSaved(string artist, string file)
    {
        foreach (var path in new[] { $"{MusicPlayer.Folder}/{artist}/{Choruses.FileFor(file)}", $"{SaveFolder}/{artist}/{Choruses.FileFor(file)}" })
        {
            if (FileAccess.FileExists(path))
            {
                return Choruses.Read(FileAccess.GetFileAsString(path));
            }
        }

        return Choruses.None;
    }
}
