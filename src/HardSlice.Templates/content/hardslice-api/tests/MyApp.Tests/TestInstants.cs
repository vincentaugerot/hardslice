namespace MyApp.Tests;

// The instant from which every test of the solution builds its dates: far in the future, so that a rule about the past, such as
// a period that may not start before now, never breaks a test written for something else. Use it rather than a date of your own.
internal static class TestInstants
{
    public static readonly DateTimeOffset Start = new(2100, 1, 4, 9, 0, 0, TimeSpan.Zero);
}
