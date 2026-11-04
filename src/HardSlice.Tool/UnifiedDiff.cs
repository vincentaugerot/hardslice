namespace HardSlice.Tool;

// The unified diff of two texts, line by line, as git diff prints it: hunks of the changed lines with three lines of context
// around them. dotnet hardslice upgrade prints it for each file of the application that differs from the template.
internal static class UnifiedDiff
{
    public const int Context = 3;

    // Above this many pairs of lines left after their common start and end, the lines between are shown as removed then added
    // instead of being matched: a longest common subsequence would need too much memory.
    private const long MaxMatchedPairs = 4_000_000;

    // Writes the hunks that turn the lines of before into the lines of after, and returns how many it wrote: none for equal lines.
    public static int Write(TextWriter writer, IReadOnlyList<string> before, IReadOnlyList<string> after)
    {
        var edits = Edits(before, after);
        var hunks = 0;
        var index = 0;
        while (index < edits.Count)
        {
            if (edits[index].Kind == EditKind.Same)
            {
                index++;
                continue;
            }

            // A hunk starts three lines of context before its first change, and goes on while the next change is within
            // twice the context of the last one, so that two hunks never share a line.
            var start = Math.Max(0, index - Context);
            var end = index;
            while (true)
            {
                while (end < edits.Count && edits[end].Kind != EditKind.Same)
                    end++;
                var next = end;
                while (next < edits.Count && edits[next].Kind == EditKind.Same)
                    next++;
                if (next < edits.Count && next - end <= 2 * Context)
                {
                    end = next;
                    continue;
                }

                end = Math.Min(edits.Count, end + Context);
                break;
            }

            WriteHunk(writer, edits, start, end);
            hunks++;
            index = end;
        }

        return hunks;
    }

    // The lines of a text, without their line breaks, \r\n or \n: a text that ends with a line break has no empty last line.
    public static IReadOnlyList<string> Lines(string text)
    {
        var lines = text.Split('\n').Select(line => line.EndsWith('\r') ? line[..^1] : line).ToList();
        if (lines.Count > 0 && lines[^1].Length == 0)
            lines.RemoveAt(lines.Count - 1);
        return lines;
    }

    private static void WriteHunk(TextWriter writer, IReadOnlyList<Edit> edits, int start, int end)
    {
        var hunk = edits.Skip(start).Take(end - start).ToArray();
        var beforeCount = hunk.Count(edit => edit.Kind != EditKind.Added);
        var afterCount = hunk.Count(edit => edit.Kind != EditKind.Removed);
        // The line numbers of the first line of the hunk, or of the line before it when the hunk has no line on that side.
        var beforeStart = hunk[0].Before + (beforeCount == 0 ? 0 : 1);
        var afterStart = hunk[0].After + (afterCount == 0 ? 0 : 1);
        writer.WriteLine($"@@ -{Range(beforeStart, beforeCount)} +{Range(afterStart, afterCount)} @@");
        foreach (var edit in hunk)
        {
            var prefix = edit.Kind switch
            {
                EditKind.Removed => '-',
                EditKind.Added => '+',
                _ => ' ',
            };
            writer.WriteLine($"{prefix}{edit.Text}");
        }
    }

    private static string Range(int start, int count) => count == 1 ? $"{start}" : $"{start},{count}";

    // Every line of both texts in order, each the same in both, removed from before or added from after. Before and After are
    // the indexes of the line in each text, or of the next line of that text for a line that the text does not hold.
    private static List<Edit> Edits(IReadOnlyList<string> before, IReadOnlyList<string> after)
    {
        var prefix = 0;
        while (prefix < before.Count && prefix < after.Count && before[prefix] == after[prefix])
            prefix++;
        var suffix = 0;
        while (suffix < before.Count - prefix && suffix < after.Count - prefix && before[^(suffix + 1)] == after[^(suffix + 1)])
            suffix++;

        var edits = new List<Edit>(before.Count + after.Count);
        for (var line = 0; line < prefix; line++)
            edits.Add(new Edit(EditKind.Same, line, line, before[line]));

        var beforeEnd = before.Count - suffix;
        var afterEnd = after.Count - suffix;
        var rows = beforeEnd - prefix;
        var columns = afterEnd - prefix;
        if ((long)rows * columns > MaxMatchedPairs)
        {
            for (var line = prefix; line < beforeEnd; line++)
                edits.Add(new Edit(EditKind.Removed, line, prefix, before[line]));
            for (var line = prefix; line < afterEnd; line++)
                edits.Add(new Edit(EditKind.Added, beforeEnd, line, after[line]));
        }
        else
        {
            // common[i, j]: the length of the longest common subsequence of the lines from i in before and from j in after.
            var common = new int[(rows + 1) * (columns + 1)];
            for (var i = rows - 1; i >= 0; i--)
            {
                for (var j = columns - 1; j >= 0; j--)
                {
                    common[i * (columns + 1) + j] = before[prefix + i] == after[prefix + j]
                        ? common[(i + 1) * (columns + 1) + j + 1] + 1
                        : Math.Max(common[(i + 1) * (columns + 1) + j], common[i * (columns + 1) + j + 1]);
                }
            }

            var (b, a) = (0, 0);
            while (b < rows || a < columns)
            {
                if (b < rows && a < columns && before[prefix + b] == after[prefix + a])
                {
                    edits.Add(new Edit(EditKind.Same, prefix + b, prefix + a, before[prefix + b]));
                    b++;
                    a++;
                }
                else if (a == columns || (b < rows && common[(b + 1) * (columns + 1) + a] >= common[b * (columns + 1) + a + 1]))
                {
                    edits.Add(new Edit(EditKind.Removed, prefix + b, prefix + a, before[prefix + b]));
                    b++;
                }
                else
                {
                    edits.Add(new Edit(EditKind.Added, prefix + b, prefix + a, after[prefix + a]));
                    a++;
                }
            }
        }

        for (var line = 0; line < suffix; line++)
            edits.Add(new Edit(EditKind.Same, beforeEnd + line, afterEnd + line, before[beforeEnd + line]));
        return edits;
    }

    private enum EditKind
    {
        Same,
        Removed,
        Added,
    }

    private sealed record Edit(EditKind Kind, int Before, int After, string Text);
}
