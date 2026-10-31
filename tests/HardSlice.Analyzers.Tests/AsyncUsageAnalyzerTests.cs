using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using static HardSlice.Analyzers.Tests.AnalyzerHarness;

namespace HardSlice.Analyzers.Tests;

// HS011: code never blocks on asynchronous work and never declares async void.
public sealed class AsyncUsageAnalyzerTests
{
    private const string BlockingRule = "Code never blocks on asynchronous work.";

    private const string AsyncVoidRule =
        "Asynchronous code returns a task its caller awaits: nobody can await async void, and its exceptions crash the process.";

    private static Task<ImmutableArray<Diagnostic>> VerifyAsync(params string[] markedSources) =>
        VerifyAsync(OutputKind.DynamicallyLinkedLibrary, [], markedSources);

    private static Task<ImmutableArray<Diagnostic>> VerifyAsync(OutputKind outputKind, MetadataReference[] references, params string[] markedSources) =>
        AnalyzerHarness.VerifyAsync(new AsyncUsageAnalyzer(), AsyncUsageAnalyzer.DiagnosticId, references, outputKind, markedSources);

    // The first sentence of each message, without its rule and its fix.
    private static IEnumerable<string> Subjects(ImmutableArray<Diagnostic> diagnostics) =>
        diagnostics.Select(diagnostic => diagnostic.GetMessage().Split(". ")[0]);

    [Fact]
    public async Task Reports_a_handler_that_blocks_on_the_Result_of_a_task()
    {
        var diagnostics = await VerifyAsync(
            """
            namespace MyApp.Features.Bookings.ConfirmBooking;

            internal sealed class ConfirmBookingHandler
            {
                public Guid Handle(Func<Task<Guid>> confirm) => confirm().[|Result|];
            }
            """);

        diagnostics.ShouldHaveSingleItem().GetMessage().ShouldBe(
            "'ConfirmBookingHandler.Handle' blocks on 'Task<TResult>.Result'. "
            + "Code never blocks on asynchronous work. "
            + "Fix: make the caller async and await the task. Skill: fix-architecture-violation. Doc: 'docs/rules/HS011.md'.");
    }

    [Fact]
    public async Task Accepts_awaited_tasks_Task_Run_with_an_async_lambda_and_async_Task_methods()
    {
        await VerifyAsync(
            """
            namespace MyApp.Features.Bookings.ListBookings;

            internal sealed class ListBookingsHandler
            {
                public async Task<int> Handle(Task<List<int>> bookings, ValueTask<int> count, CancellationToken cancellationToken)
                {
                    var all = await bookings;
                    var first = await count.ConfigureAwait(false);
                    await Task.Run(async () => await Task.Delay(1, cancellationToken), cancellationToken);
                    await bookings.WaitAsync(TimeSpan.FromSeconds(1), cancellationToken);
                    await Task.WhenAll(bookings, Task.CompletedTask);
                    Func<Task> later = async () => await Task.Yield();
                    var natural = async () => await Task.Yield();
                    all.ForEach(booking => Console.WriteLine(booking));
                    await Local();
                    return all.Count + first + nameof(bookings.Result).Length;

                    async Task Local() => await later();
                }

                public async ValueTask Notify() => await Task.Yield();
            }
            """);
    }

    [Fact]
    public async Task Reports_the_three_blocking_calls_on_a_generic_task_and_a_value_task()
    {
        var diagnostics = await VerifyAsync(
            """
            namespace MyApp.Features.Bookings.ListBookings;

            internal sealed class ListBookingsHandler
            {
                public int Handle(Task<List<Guid>> bookings, ValueTask<int> count)
                {
                    var all = bookings.[|Result|];
                    bookings.[|Wait|]();
                    var again = bookings.GetAwaiter().[|GetResult|]();
                    var configured = bookings.ConfigureAwait(false).GetAwaiter().[|GetResult|]();
                    var total = count.[|Result|];
                    var awaited = count.GetAwaiter().[|GetResult|]();
                    var configuredCount = count.ConfigureAwait(false).GetAwaiter().[|GetResult|]();
                    return all.Count + again.Count + configured.Count + total + awaited + configuredCount;
                }

                public void Flush(Task flush, ValueTask written)
                {
                    flush.GetAwaiter().[|GetResult|]();
                    flush.ConfigureAwait(false).GetAwaiter().[|GetResult|]();
                    written.GetAwaiter().[|GetResult|]();
                    written.ConfigureAwait(false).GetAwaiter().[|GetResult|]();
                }
            }
            """);

        diagnostics.Select(diagnostic => diagnostic.GetMessage().Split(". Code never")[0].Split(" blocks on ")[1]).ShouldBe(
        [
            "'Task<TResult>.Result'",
            "'Task.Wait'",
            "'TaskAwaiter<TResult>.GetResult'",
            "'ConfiguredTaskAwaitable<TResult>.ConfiguredTaskAwaiter.GetResult'",
            "'ValueTask<TResult>.Result'",
            "'ValueTaskAwaiter<TResult>.GetResult'",
            "'ConfiguredValueTaskAwaitable<TResult>.ConfiguredValueTaskAwaiter.GetResult'",
            "'TaskAwaiter.GetResult'",
            "'ConfiguredTaskAwaitable.ConfiguredTaskAwaiter.GetResult'",
            "'ValueTaskAwaiter.GetResult'",
            "'ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter.GetResult'"
        ]);
        diagnostics.ShouldAllBe(diagnostic => diagnostic.GetMessage().Contains(BlockingRule)
            && diagnostic.GetMessage().Contains("Fix: make the caller async and await the task."));
    }

    // Every overload of Wait, WaitAll and WaitAny, a conditional access and a method group that blocks once invoked.
    [Fact]
    public async Task Reports_every_overload_of_Wait_WaitAll_and_WaitAny()
    {
        var diagnostics = await VerifyAsync(
            """
            namespace MyApp.Shared.Messaging;

            internal static class Outbox
            {
                public static void Flush(Task first, Task<int> second, Task[] all, CancellationToken cancellationToken)
                {
                    first.[|Wait|](TimeSpan.FromSeconds(1));
                    second.[|Wait|](1000, cancellationToken);
                    first?.[|Wait|](cancellationToken);
                    Task.[|WaitAll|](first, second);
                    Task.[|WaitAll|](all, cancellationToken);
                    Task.[|WaitAll|]((IEnumerable<Task>)all);
                    Task.[|WaitAny|](all, 1000);
                    Action wait = first.[|Wait|];
                    wait();
                }
            }
            """);

        diagnostics.Select(diagnostic => diagnostic.GetMessage().Split(" Fix: ")[1]).ShouldBe(
        [
            "make the caller async and await 'WaitAsync' with the same timeout or token. Skill: fix-architecture-violation. Doc: 'docs/rules/HS011.md'.",
            "make the caller async and await 'WaitAsync' with the same timeout or token. Skill: fix-architecture-violation. Doc: 'docs/rules/HS011.md'.",
            "make the caller async and await 'WaitAsync' with the same timeout or token. Skill: fix-architecture-violation. Doc: 'docs/rules/HS011.md'.",
            "make the caller async and await 'Task.WhenAll' with the same tasks. Skill: fix-architecture-violation. Doc: 'docs/rules/HS011.md'.",
            "make the caller async and await 'Task.WhenAll' with the same tasks. Skill: fix-architecture-violation. Doc: 'docs/rules/HS011.md'.",
            "make the caller async and await 'Task.WhenAll' with the same tasks. Skill: fix-architecture-violation. Doc: 'docs/rules/HS011.md'.",
            "make the caller async and await 'Task.WhenAny' with the same tasks. Skill: fix-architecture-violation. Doc: 'docs/rules/HS011.md'.",
            "make the caller async and await the task. Skill: fix-architecture-violation. Doc: 'docs/rules/HS011.md'."
        ]);
    }

    // A Result on a task already awaited is reported anyway: await it a second time.
    [Fact]
    public async Task Reports_nested_generic_tasks_and_a_Result_read_after_an_await()
    {
        var diagnostics = await VerifyAsync(
            """
            namespace MyApp.Features.Bookings.ImportBookings;

            internal sealed class ImportBookingsHandler<TBooking>
            {
                public async Task<int> Handle(Task<Task<List<TBooking>>> import, Task<int> count)
                {
                    var bookings = import.[|Result|].[|Result|];
                    await count;
                    return bookings.Count + count.[|Result|];
                }
            }
            """);

        Subjects(diagnostics).ShouldBe(
        [
            "'ImportBookingsHandler<TBooking>.Handle' blocks on 'Task<TResult>.Result'",
            "'ImportBookingsHandler<TBooking>.Handle' blocks on 'Task<TResult>.Result'",
            "'ImportBookingsHandler<TBooking>.Handle' blocks on 'Task<TResult>.Result'"
        ]);
    }

    [Fact]
    public async Task Names_the_constructor_the_property_the_field_and_the_top_level_statements_that_block()
    {
        var diagnostics = await VerifyAsync(
            OutputKind.ConsoleApplication,
            [],
            """
            var port = Settings.Load().[|Result|];
            Console.WriteLine(port);

            internal sealed class Settings
            {
                private static readonly int Default = Task.FromResult(80).[|Result|];

                public Settings(Task ready) => ready.[|Wait|]();

                static Settings() => Task.Delay(1).[|Wait|]();

                public int Port => Load().[|Result|];

                public static Task<int> Load() => Task.FromResult(Default);
            }
            """);

        Subjects(diagnostics).ShouldBe(
        [
            "the top-level statements of 'Program' blocks on 'Task<TResult>.Result'",
            "'Settings.Default' blocks on 'Task<TResult>.Result'",
            "the constructor of 'Settings' blocks on 'Task.Wait'",
            "the static constructor of 'Settings' blocks on 'Task.Wait'",
            "'Settings.Port' blocks on 'Task<TResult>.Result'"
        ]);
    }

    [Fact]
    public async Task Reports_an_async_void_method_a_local_function_and_a_lambda_given_to_List_ForEach()
    {
        var diagnostics = await VerifyAsync(
            """
            namespace MyApp.Features.Bookings.ConfirmBooking;

            internal sealed class BookingNotifier
            {
                public async void [|Notify|](Guid booking) => await Task.Delay(1);

                public void NotifyAll(List<Guid> bookings)
                {
                    bookings.ForEach([|async|] booking => await Task.Delay(1));
                    Send();

                    async void [|Send|]() => await Task.Yield();
                }
            }
            """);

        diagnostics.Select(diagnostic => diagnostic.GetMessage()).ShouldBe(
        [
            "'BookingNotifier.Notify' is async void. " + AsyncVoidRule
                + " Fix: return Task instead of void, and await it where it is called. Skill: fix-architecture-violation. Doc: 'docs/rules/HS011.md'.",
            "an async lambda of 'BookingNotifier.NotifyAll' is async void: it is converted to 'Action<Guid>', which returns void. " + AsyncVoidRule
                + " Fix: replace 'List<T>.ForEach' with a foreach loop that awaits each call. Skill: fix-architecture-violation. Doc: 'docs/rules/HS011.md'.",
            "local function 'Send' of 'BookingNotifier.NotifyAll' is async void. " + AsyncVoidRule
                + " Fix: return Task instead of void, and await it where it is called. Skill: fix-architecture-violation. Doc: 'docs/rules/HS011.md'."
        ]);
    }

    // Any delegate whose Invoke returns void: Action, EventHandler, a custom delegate, a thread start.
    [Fact]
    public async Task Reports_async_lambdas_and_anonymous_methods_converted_to_delegates_that_return_void()
    {
        var diagnostics = await VerifyAsync(
            """
            namespace MyApp.Shared.Messaging;

            public delegate void BookingHandler(Guid booking, string reason);

            internal sealed class Dispatcher
            {
                public event EventHandler? Dispatched;

                public void Start(BookingHandler handler)
                {
                    Action run = [|async|] () => await Task.Yield();
                    Dispatched += [|async|] (sender, args) => await Task.Yield();
                    Subscribe([|async|] (booking, reason) => await Task.Delay(1));
                    Action<int> wait = [|async|] delegate (int delay) { await Task.Delay(delay); };
                    new Thread([|async|] () => await Task.Yield()).Start();
                }

                private static void Subscribe(BookingHandler handler) => handler(Guid.Empty, "");
            }
            """);

        diagnostics.Select(diagnostic => diagnostic.GetMessage().Split(". Asynchronous code")[0]).ShouldBe(
        [
            "an async lambda of 'Dispatcher.Start' is async void: it is converted to 'Action', which returns void",
            "an async lambda of 'Dispatcher.Start' is async void: it is converted to 'EventHandler', which returns void",
            "an async lambda of 'Dispatcher.Start' is async void: it is converted to 'BookingHandler', which returns void",
            "an async anonymous method of 'Dispatcher.Start' is async void: it is converted to 'Action<int>', which returns void",
            "an async lambda of 'Dispatcher.Start' is async void: it is converted to 'ThreadStart', which returns void"
        ]);
        diagnostics.Select(diagnostic => diagnostic.GetMessage().Split(" Fix: ")[1]).ShouldBe(
        [
            "give it a delegate type that returns Task, such as 'Func<Task>', and await the task it returns. Skill: fix-architecture-violation. Doc: 'docs/rules/HS011.md'.",
            "subscribe a synchronous handler, and hand the asynchronous work to code that awaits it. Skill: fix-architecture-violation. Doc: 'docs/rules/HS011.md'.",
            "give it a delegate type that returns Task, such as 'Func<Guid, string, Task>', and await the task it returns. Skill: fix-architecture-violation. Doc: 'docs/rules/HS011.md'.",
            "give it a delegate type that returns Task, such as 'Func<int, Task>', and await the task it returns. Skill: fix-architecture-violation. Doc: 'docs/rules/HS011.md'.",
            "give it a delegate type that returns Task, such as 'Func<Task>', and await the task it returns. Skill: fix-architecture-violation. Doc: 'docs/rules/HS011.md'."
        ]);
    }

    // The signature of an override or an interface implementation comes from the member it overrides or implements.
    [Fact]
    public async Task Reports_async_void_overrides_and_interface_implementations()
    {
        var diagnostics = await VerifyAsync(
            """
            namespace MyApp.Shared.Behaviors;

            public interface IStartable
            {
                void Start();
            }

            public abstract class Component
            {
                public abstract void Stop();
            }

            internal sealed class Worker : Component, IStartable, IDisposable
            {
                public async void [|Start|]() => await Task.Yield();

                public override async void [|Stop|]() => await Task.Yield();

                async void IDisposable.[|Dispose|]() => await Task.Yield();
            }

            internal sealed partial class Poller
            {
                partial void Poll();

                async partial void [|Poll|]() => await Task.Yield();
            }
            """);

        diagnostics.Select(diagnostic => diagnostic.GetMessage().Split(" Fix: ")[1]).ShouldBe(
        [
            "make it synchronous, or, if the application declares 'IStartable.Start', make both return Task and await it where it is called. Skill: fix-architecture-violation. Doc: 'docs/rules/HS011.md'.",
            "make it synchronous, or, if the application declares 'Component.Stop', make both return Task and await it where it is called. Skill: fix-architecture-violation. Doc: 'docs/rules/HS011.md'.",
            "make it synchronous, or, if the application declares 'IDisposable.Dispose', make both return Task and await it where it is called. Skill: fix-architecture-violation. Doc: 'docs/rules/HS011.md'.",
            "return Task instead of void, and await it where it is called. Skill: fix-architecture-violation. Doc: 'docs/rules/HS011.md'."
        ]);
        Subjects(diagnostics).ShouldBe(
        [
            "'Worker.Start' is async void",
            "'Worker.Stop' is async void",
            "'Worker.Dispose' is async void",
            "'Poller.Poll' is async void"
        ]);
    }

    // Members named Result or Wait of other types, such as a result type of the application or an extension method of a
    // library, are not tasks.
    [Fact]
    public async Task Ignores_lookalike_Result_Wait_and_GetResult_members()
    {
        var library = Compile(
            "Polly.Like",
            [
                ("TaskExtensions.cs",
                    """
                    namespace Polly.Like;

                    public static class TaskExtensions
                    {
                        public static Task Wait(this Task task, string reason) => task;
                    }

                    public sealed class Awaiter
                    {
                        public int GetResult() => 0;
                    }
                    """),
                ("Usings.cs", ImplicitUsings)
            ]);

        await VerifyAsync(
            OutputKind.DynamicallyLinkedLibrary,
            [Reference(library, asMetadata: true)],
            """
            using Polly.Like;

            namespace MyApp.Features.Bookings.ConfirmBooking;

            public sealed record OperationResult(bool Result)
            {
                public void Wait() { }
            }

            internal sealed class ConfirmBookingHandler
            {
                public async Task<bool> Handle(OperationResult outcome, Task confirmed, Awaiter awaiter)
                {
                    outcome.Wait();
                    await confirmed.Wait("confirmation");
                    return outcome.Result && awaiter.GetResult() == 0;
                }
            }
            """);
    }

    // The domain is analyzed in its own compilation; a task it declares in a referenced assembly blocks the application.
    [Fact]
    public async Task Reports_async_void_in_the_domain_and_a_blocking_call_on_a_task_of_a_referenced_assembly()
    {
        var domainSource =
            """
            namespace MyApp.Domain.Bookings;

            public sealed class Booking
            {
                public async void Archive() => await Task.Yield();

                public static Task<Booking> LoadAsync() => Task.FromResult(new Booking());
            }
            """;

        var domainDiagnostics = await AnalyzerHarness.VerifyAsync(
            new AsyncUsageAnalyzer(),
            AsyncUsageAnalyzer.DiagnosticId,
            [],
            OutputKind.DynamicallyLinkedLibrary,
            domainSource.Replace("async void Archive", "async void [|Archive|]"));
        Subjects(domainDiagnostics).ShouldBe(["'Booking.Archive' is async void"]);

        var domain = Compile("MyApp.Domain", [("Booking.cs", domainSource), ("Usings.cs", ImplicitUsings)]);
        var diagnostics = await VerifyAsync(
            OutputKind.DynamicallyLinkedLibrary,
            [Reference(domain, asMetadata: true)],
            """
            using MyApp.Domain.Bookings;

            namespace MyApp.Features.Bookings.CancelBooking;

            internal sealed class CancelBookingHandler
            {
                public Booking Handle() => Booking.LoadAsync().[|Result|];
            }
            """);
        Subjects(diagnostics).ShouldBe(["'CancelBookingHandler.Handle' blocks on 'Task<TResult>.Result'"]);
    }

    // An auto-generated header does not exempt code from the rule.
    [Fact]
    public async Task Reports_generated_code()
    {
        await VerifyAsync(
            """
            // <auto-generated/>
            namespace MyApp.Shared.Messaging;

            internal static class Generated
            {
                public static int Read(Task<int> value) => value.[|Result|];
            }
            """);
    }

    [Fact]
    public async Task Does_not_apply_to_test_projects()
    {
        var compilation = Compile(
            "MyApp.Tests",
            [
                ("Tests.cs",
                    """
                    namespace MyApp.Tests;

                    public sealed class BookingTests
                    {
                        public async void Confirms() => await Task.Yield();

                        public int Counts(Task<int> count) => count.Result + count.GetAwaiter().GetResult();
                    }
                    """),
                ("Usings.cs", ImplicitUsings)
            ]);

        var diagnostics = await compilation
            .WithAnalyzers([new AsyncUsageAnalyzer()], Options(configuration: null, isTestProject: true))
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        diagnostics.ShouldBeEmpty();
    }
}
