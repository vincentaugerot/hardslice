using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace HardSlice.Tool;

// How many times the end hook of a session has blocked the end of the agent for each failure of the check, and for all of them,
// since the agent last finished on its own. One small file per CLI and session in the state directory, removed once the check
// passes. The state is best effort: a file that cannot be read counts no relaunch, and Record says when it cannot be written.
internal sealed partial class StopRetries(string stateDirectory, string cli, string sessionId)
{
    // The end hook relaunches the agent at most this many times for the same failure, then lets it hand back.
    public const int Max = 3;

    // And at most this many times in a session, all failures together: a failure whose text changes at each check, with a new
    // identifier in its message, would otherwise never reach Max. Copilot CLI stops at 8 on its own.
    public const int SessionMax = 8;

    // The key of the count for all failures, which no fingerprint, 16 hexadecimal digits, can take.
    private const string SessionKey = "session";

    private string File => Path.Combine(stateDirectory, $"{cli}-{SafeName().Replace(sessionId, "_")}.json");

    // The relaunches already made for the failure and in the session. A first end of turn, when no end hook is active, starts from none.
    public (int ForFailure, int InSession) Made(string failure, bool stopHookActive)
    {
        if (!stopHookActive)
        {
            Clear();
            return (0, 0);
        }

        var retries = Load();
        return (retries.GetValueOrDefault(Fingerprint(failure)), retries.GetValueOrDefault(SessionKey));
    }

    // Counts one more relaunch for the failure. Null once recorded, otherwise why the state directory refused it.
    public string? Record(string failure)
    {
        try
        {
            var retries = Load();
            var key = Fingerprint(failure);
            retries[key] = retries.GetValueOrDefault(key) + 1;
            retries[SessionKey] = retries.GetValueOrDefault(SessionKey) + 1;
            Directory.CreateDirectory(stateDirectory);
            System.IO.File.WriteAllText(File, JsonSerializer.Serialize(retries));
            return null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return $"{exception.GetType().Name}: {exception.Message}";
        }
    }

    public void Clear()
    {
        try
        {
            if (System.IO.File.Exists(File))
                System.IO.File.Delete(File);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A count that cannot be removed is reset by the next end of turn where no end hook is active.
        }
    }

    // The same failure gives the same fingerprint from one check to the next: only its error and failure lines count, in any
    // order and once each, with their numbers, GUIDs and hexadecimal identifiers left out, so that a moved line, another
    // duration or the name of a new test container is not a new failure.
    public static string Fingerprint(string failure)
    {
        var lines = failure.Split('\n')
            .Where(line => FailureLine().IsMatch(line))
            .Select(line => Normalize(line.Trim()))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var text = lines.Length > 0 ? string.Join('\n', lines) : Normalize(failure.Trim());
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16];
    }

    private static string Normalize(string text) =>
        Number().Replace(HexIdentifier().Replace(Guid().Replace(text, "#"), "#"), "#");

    private Dictionary<string, int> Load()
    {
        try
        {
            return System.IO.File.Exists(File)
                ? JsonSerializer.Deserialize<Dictionary<string, int>>(System.IO.File.ReadAllText(File)) ?? []
                : [];
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            // A file cut short by an interrupted hook, or that cannot be read, counts no relaunch.
            return [];
        }
    }

    [GeneratedRegex(@"error|fail|TODO\(hardslice\):", RegexOptions.IgnoreCase)]
    private static partial Regex FailureLine();

    [GeneratedRegex(@"[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}")]
    private static partial Regex Guid();

    // Eight hexadecimal digits or more, one of them a decimal digit, not inside a word, with or without 0x: a container, object
    // or address identifier.
    [GeneratedRegex(@"(?<![0-9A-Za-z])(?:0[xX])?(?=[0-9A-Fa-f]*[0-9])[0-9A-Fa-f]{8,}(?![0-9A-Za-z])")]
    private static partial Regex HexIdentifier();

    [GeneratedRegex(@"\d+")]
    private static partial Regex Number();

    [GeneratedRegex(@"[^A-Za-z0-9_-]")]
    private static partial Regex SafeName();
}
