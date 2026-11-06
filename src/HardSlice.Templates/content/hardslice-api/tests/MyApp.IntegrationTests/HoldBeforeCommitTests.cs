using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using MyApp.Domain;
using MyApp.Shared.Messaging;
using MyApp.Shared.Persistence;

namespace MyApp.IntegrationTests;

// AppFactory.HoldBeforeCommit<TCommand>(), on commands that change a counter of these tests only, mapped as an aggregate is,
// with its concurrency token on xmin, in a model that only these instances of the application see.
public sealed class HoldBeforeCommitTests(PostgresDatabase database)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task Commits_a_second_change_of_the_same_row_and_answers_a_conflict_to_the_held_one_released_after_it()
    {
        var id = await StoreCounterAsync();
        await using var factory = new AppFactory(database);
        await using var app = WithCounters(factory);
        using var hold = factory.HoldBeforeCommit<IncrementCounter>();

        var first = SendInScopeAsync(app.Services, new IncrementCounter(id));
        await hold.WaitUntilHeldAsync(TestContext.Current.CancellationToken);
        (await SendInScopeAsync(app.Services, new IncrementCounter(id)).WaitAsync(Timeout, TestContext.Current.CancellationToken)).ShouldBeTrue();
        hold.Release();

        await Should.ThrowAsync<ConflictException>(first);
        (await StoredCountAsync(id)).ShouldBe(1, "The held command committed its increment too.");
    }

    [Fact]
    public async Task Never_holds_a_command_of_another_type()
    {
        var id = await StoreCounterAsync();
        await using var factory = new AppFactory(database);
        await using var app = WithCounters(factory);
        using var hold = factory.HoldBeforeCommit<IncrementCounter>();

        (await SendInScopeAsync(app.Services, new AddToCounter(id, 10)).WaitAsync(Timeout, TestContext.Current.CancellationToken)).ShouldBeTrue();

        var increment = SendInScopeAsync(app.Services, new IncrementCounter(id));
        await hold.WaitUntilHeldAsync(TestContext.Current.CancellationToken);
        hold.Release();
        (await increment).ShouldBeTrue();
        (await StoredCountAsync(id)).ShouldBe(11);
    }

    [Fact]
    public async Task Refuses_a_query()
    {
        await using var app = new AppFactory(database);

        var refusal = Should.Throw<InvalidOperationException>(() => app.HoldBeforeCommit<ReadCounter>());

        refusal.Message.ShouldBe("HoldBeforeCommit: ReadCounter is no ICommand<TResponse>. Only a command runs in a transaction that commits.");
    }

    // The instance of the tests of the counter: its model maps Counter, and its handlers change it.
    private static WebApplicationFactory<Program> WithCounters(AppFactory factory) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services
            .ConfigureDbContext<AppDbContext>(options => options.ReplaceService<IModelCustomizer, CounterModel>())
            .AddScoped<IRequestHandler<IncrementCounter, bool>, IncrementCounterHandler>()
            .AddScoped<IRequestHandler<AddToCounter, bool>, AddToCounterHandler>()));

    // Each command in a scope of its own, as two concurrent requests.
    private static async Task<bool> SendInScopeAsync(IServiceProvider services, ICommand<bool> command)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(command, TestContext.Current.CancellationToken);
    }

    private async Task<Guid> StoreCounterAsync()
    {
        var id = Guid.NewGuid();
        await using var db = database.CreateDbContext();
        await db.Database.ExecuteSqlRawAsync(
            "CREATE TABLE IF NOT EXISTS \"HoldBeforeCommitCounters\" (\"Id\" uuid PRIMARY KEY, \"Count\" integer NOT NULL)",
            TestContext.Current.CancellationToken);
        await db.Database.ExecuteSqlAsync(
            $"INSERT INTO \"HoldBeforeCommitCounters\" (\"Id\", \"Count\") VALUES ({id}, 0)", TestContext.Current.CancellationToken);
        return id;
    }

    private async Task<int> StoredCountAsync(Guid id)
    {
        await using var db = database.CreateDbContext();
        return await db.Database
            .SqlQuery<int>($"SELECT \"Count\" AS \"Value\" FROM \"HoldBeforeCommitCounters\" WHERE \"Id\" = {id}")
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    public sealed class Counter
    {
        private Counter()
        {
        }

        public Guid Id { get; private set; }

        public int Count { get; private set; }

        public void Increment() => Count++;

        public void Add(int amount) => Count += amount;
    }

    private sealed class CounterModel(ModelCustomizerDependencies dependencies) : RelationalModelCustomizer(dependencies)
    {
        public override void Customize(ModelBuilder modelBuilder, DbContext context)
        {
            base.Customize(modelBuilder, context);
            modelBuilder.Entity<Counter>(builder =>
            {
                builder.ToTable("HoldBeforeCommitCounters");
                builder.HasKey(counter => counter.Id);
                builder.Property<uint>("Version").IsRowVersion();
            });
        }
    }

    public sealed record IncrementCounter(Guid Id) : ICommand<bool>;

    private sealed class IncrementCounterHandler(AppDbContext db) : IRequestHandler<IncrementCounter, bool>
    {
        public async Task<bool> Handle(IncrementCounter command, CancellationToken cancellationToken)
        {
            var counter = await db.Set<Counter>().SingleAsync(counter => counter.Id == command.Id, cancellationToken);
            counter.Increment();
            return true;
        }
    }

    public sealed record AddToCounter(Guid Id, int Amount) : ICommand<bool>;

    private sealed class AddToCounterHandler(AppDbContext db) : IRequestHandler<AddToCounter, bool>
    {
        public async Task<bool> Handle(AddToCounter command, CancellationToken cancellationToken)
        {
            var counter = await db.Set<Counter>().SingleAsync(counter => counter.Id == command.Id, cancellationToken);
            counter.Add(command.Amount);
            return true;
        }
    }

    public sealed record ReadCounter(Guid Id) : IQuery<int>;
}
