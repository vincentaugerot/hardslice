namespace HardSlice.Tool.Tests;

// The diff that dotnet hardslice upgrade prints for each file of the application that differs from the template (PB-086):
// the hunks of git diff, three lines of context around each change.
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

    private static (int Hunks, string Text) Diff(string before, string after)
    {
        var writer = new StringWriter { NewLine = "\n" };
        var hunks = UnifiedDiff.Write(writer, UnifiedDiff.Lines(before), UnifiedDiff.Lines(after));
        return (hunks, writer.ToString());
    }
}
