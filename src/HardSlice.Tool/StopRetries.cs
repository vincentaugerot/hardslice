using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace HardSlice.Tool;

// How many times the end hook of a session has blocked the end of the agent for each failure of the check, since the agent
// last finished on its own. One small file per session in the state directory, removed once the check passes.
internal sealed partial class StopRetries(string stateDirectory, string sessionId)
{
    // The end hook relaunches the agent at most this many times for the same failure, then lets it hand back.
    public const int Max = 3;

    private string File => Path.Combine(stateDirectory, $"claude-{SafeName().Replace(sessionId, "_")}.json");

    // The relaunches already made for the failure. A first end of turn, when no end hook is active, starts from none.
    public int Made(string failure, bool stopHookActive)
    {
        if (!stopHookActive)
        {
            Clear();
            return 0;
        }

        return Load().GetValueOrDefault(Fingerprint(failure));
    }

    public void Record(string failure)
    {
        var retries = Load();
        var key = Fingerprint(failure);
        retries[key] = retries.GetValueOrDefault(key) + 1;
        Directory.CreateDirectory(stateDirectory);
        System.IO.File.WriteAllText(File, JsonSerializer.Serialize(retries));
    }

    public void Clear()
    {
        if (System.IO.File.Exists(File))
            System.IO.File.Delete(File);
    }

    // The same failure gives the same fingerprint from one check to the next: only its error and failure lines count, in any
    // order and once each, with their numbers left out, so that a moved line or another duration is not a new failure.
    public static string Fingerprint(string failure)
    {
        var lines = failure.Split('\n')
            .Where(line => FailureLine().IsMatch(line))
            .Select(line => Number().Replace(line.Trim(), "#"))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var text = lines.Length > 0 ? string.Join('\n', lines) : Number().Replace(failure.Trim(), "#");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16];
    }

    private Dictionary<string, int> Load()
    {
        try
        {
            return System.IO.File.Exists(File)
                ? JsonSerializer.Deserialize<Dictionary<string, int>>(System.IO.File.ReadAllText(File)) ?? []
                : [];
        }
        catch (JsonException)
        {
            // A file cut short by an interrupted hook counts no relaunch.
            return [];
        }
    }

    [GeneratedRegex(@"error|fail|TODO\(hardslice\):", RegexOptions.IgnoreCase)]
    private static partial Regex FailureLine();

    [GeneratedRegex(@"\d+")]
    private static partial Regex Number();

    [GeneratedRegex(@"[^A-Za-z0-9_-]")]
    private static partial Regex SafeName();
}
