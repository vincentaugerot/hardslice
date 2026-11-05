namespace HardSlice.Tool.Tests;

// The diff that dotnet hardslice upgrade prints for each file of the application that differs from the template (PB-086):
// the hunks of git diff, three lines of context around each change, under the headers of git, so that git apply takes it (PB-096).
public sealed class UnifiedDiffTests
{
    [Fact]
    public void Equal_lines_write_no_hunk()
    {
        var (hunks, text) = Diff("a\nb\n", "a\r\nb\r\n");

        hunks.ShouldBe(0);
        text.ShouldBeEmpty();
    }

    [Fact]
    public void A_changed_line_comes_with_three_lines_of_context_on_each_side()
    {
        var (hunks, text) = Diff("1\n2\n3\n4\n5\n6\n7\n8\n9\n", "1\n2\n3\n4\nfive\n6\n7\n8\n9\n");

        hunks.ShouldBe(1);
        text.ShouldBe("@@ -2,7 +2,7 @@\n 2\n 3\n 4\n-5\n+five\n 6\n 7\n 8\n");
    }

    [Fact]
    public void Changes_far_apart_get_one_hunk_each_and_close_ones_share_one()
    {
        var before = Numbered(20);

        var (far, farText) = Diff(before, Numbered(20, (3, "three"), (18, "eighteen")));
        var (close, closeText) = Diff(before, Numbered(20, (3, "three"), (9, "nine")));

        far.ShouldBe(2);
        farText.ShouldStartWith("@@ -1,6 +1,6 @@\n 1\n 2\n-3\n+three\n 4\n 5\n 6\n@@ -15,6 +15,6 @@\n 15\n 16\n 17\n-18\n+eighteen\n 19\n 20\n");
        close.ShouldBe(1);
        closeText.ShouldStartWith("@@ -1,12 +1,12 @@\n");
    }

    [Fact]
    public void Lines_added_at_the_start_and_removed_at_the_end_number_the_side_without_lines_from_the_line_before()
    {
        Diff("a\nb\n", "new\na\nb\n").Text.ShouldBe("@@ -1,2 +1,3 @@\n+new\n a\n b\n");
        Diff("a\nb\nc\n", "a\nb\n").Text.ShouldBe("@@ -1,3 +1,2 @@\n a\n b\n-c\n");
        Diff("", "a\n").Text.ShouldBe("@@ -0,0 +1 @@\n+a\n");
        Diff("1\n2\n3\n4\n5\n", "1\n2\n3\n4\n5\n6\n").Text.ShouldBe("@@ -3,3 +3,4 @@\n 3\n 4\n 5\n+6\n");
        Diff(Numbered(10), Numbered(10).Replace("5\n", "")).Text.ShouldBe("@@ -2,7 +2,6 @@\n 2\n 3\n 4\n-5\n 6\n 7\n 8\n");
    }

    // The lines 1 to count, some of them replaced.
    private static string Numbered(int count, params (int Line, string Text)[] replaced) =>
        string.Concat(Enumerable.Range(1, count).Select(line => (replaced.FirstOrDefault(r => r.Line == line).Text ?? line.ToString()) + "\n"));

    [Fact]
    public void A_moved_block_is_matched_on_the_longest_common_lines()
    {
        var (_, text) = Diff("using A;\nusing B;\n\nclass C\n{\n}\n", "using B;\nusing C;\n\nclass C\n{\n}\n");

        text.ShouldBe("@@ -1,5 +1,5 @@\n-using A;\n using B;\n+using C;\n \n class C\n {\n");
    }

    // Ghostline: the headers '--- <path> (solution)' and '+++ <path> (template <version>)' were not those of git apply, and a script
    // cut the output by file before patch took each diff.
    [Fact]
    public void The_diff_of_a_file_carries_the_headers_of_git()
    {
        var writer = new StringWriter { NewLine = "\n" };

        var written = UnifiedDiff.Write(writer, "src/Demo/Program.cs", "src/Demo/Program.cs", "a\nb\n", "a\nc\n");

        written.ShouldBeTrue();
        writer.ToString().ShouldBe("diff --git a/src/Demo/Program.cs b/src/Demo/Program.cs\n--- a/src/Demo/Program.cs\n+++ b/src/Demo/Program.cs\n@@ -1,2 +1,2 @@\n a\n-b\n+c\n");
    }

    [Fact]
    public void Equal_texts_write_no_diff_nor_header()
    {
        var writer = new StringWriter { NewLine = "\n" };

        UnifiedDiff.Write(writer, "AGENTS.md", "AGENTS.md", "a\nb\n", "a\r\nb\r\n").ShouldBeFalse();
        writer.ToString().ShouldBeEmpty();
    }

    // As git: a last line without a line break is followed by '\ No newline at end of file', and differs from the same line with one.
    [Fact]
    public void A_last_line_without_a_line_break_is_marked_as_git_marks_it()
    {
        Diff("a\nb", "a\nb\n").Text.ShouldBe("@@ -1,2 +1,2 @@\n a\n-b\n\\ No newline at end of file\n+b\n");
        Diff("a\nb\n", "a\nc").Text.ShouldBe("@@ -1,2 +1,2 @@\n a\n-b\n+c\n\\ No newline at end of file\n");
        Diff("a\nb", "x\na\nb").Text.ShouldBe("@@ -1,2 +1,3 @@\n+x\n a\n b\n\\ No newline at end of file\n");
        Diff("a", "a").Hunks.ShouldBe(0);
    }

    // The diff of each case, cut out of the output, applies with git apply on a real repository and gives the text after.
    [Theory]
    [InlineData("1\n2\n3\n4\n5\n6\n7\n8\n9\n10\n11\n12\n", "1\ntwo\n3\n4\n5\n6\n7\n8\n9\n10\neleven\n12\n13\n")]
    [InlineData("a\nb", "a\nb\n")]
    [InlineData("a\nb\n", "a\nc")]
    [InlineData("", "a\nb\n")]
    [InlineData("using A;\n\nclass C\n{\n}\n", "using B;\nusing A;\n\nclass D\n{\n}")]
    public void The_diff_applies_with_git_apply(string before, string after)
    {
        var repository = Directory.CreateTempSubdirectory("hardslice-diff-");
        try
        {
            var runner = new ProcessRunner(Environment.GetEnvironmentVariable);
            const string path = "src/Demo/Shared/Http/Program.cs";
            var file = Path.Combine(repository.FullName, path);
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, before);
            runner.Capture("git", ["init", "--quiet"], repository.FullName).ExitCode.ShouldBe(0);
            var writer = new StringWriter { NewLine = "\n" };
            writer.WriteLine("==> Diff of " + path);
            UnifiedDiff.Write(writer, path, path, before, after).ShouldBeTrue();
            writer.WriteLine("==> Upgrade done.");
            File.WriteAllText(Path.Combine(repository.FullName, "file.diff"), writer.ToString());

            var check = runner.Capture("git", ["apply", "--check", "file.diff"], repository.FullName);
            check.ExitCode.ShouldBe(0, check.Error);
            var apply = runner.Capture("git", ["apply", "file.diff"], repository.FullName);
            apply.ExitCode.ShouldBe(0, apply.Error);
            File.ReadAllText(file).ShouldBe(after);
        }
        finally
        {
            repository.Delete(recursive: true);
        }
    }

    private static (int Hunks, string Text) Diff(string before, string after)
    {
        var writer = new StringWriter { NewLine = "\n" };
        var hunks = UnifiedDiff.WriteHunks(writer, before, after);
        return (hunks, writer.ToString());
    }
}
