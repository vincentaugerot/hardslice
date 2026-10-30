#!/usr/bin/env bash
# Adds the synthetic modules of the analyzer performance fixture to a solution generated from hardslice-api with its Bookings sample:
# 20 modules, Module01 to Module20, each with an aggregate in the Domain, its EF Core mapping, a module class and 10 slices
# (5 commands, 5 queries) written as the Bookings slices are. Deterministic: the same arguments always write the same files.
# Builds only: the modules have no tests and no migration, the fixture is not meant to pass check.sh.
# Used by measure-analyzers.sh; docs/performance/analyzers.md describes the fixture.
set -euo pipefail

if [[ $# -ne 2 ]]; then
  echo "usage: $0 <app-dir> <app-name>" >&2
  exit 2
fi

app_dir="$(cd "$1" && pwd)"
app="$2"
module_count=20

domain_dir="$app_dir/src/$app.Domain"
features_dir="$app_dir/src/$app/Features"
program="$app_dir/src/$app/Program.cs"

if [[ ! -f "$program" || ! -d "$features_dir/Bookings" ]]; then
  echo "error: $app_dir is not a $app solution generated with the Bookings sample." >&2
  exit 1
fi

write_domain() {
  local module="$1"
  local dir="$domain_dir/$module"
  mkdir -p "$dir/Events"

  cat > "$dir/ItemId.cs" <<EOF
namespace $app.Domain.$module;

public readonly record struct ItemId(Guid Value)
{
    public static ItemId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
EOF

  cat > "$dir/ItemStatus.cs" <<EOF
namespace $app.Domain.$module;

public enum ItemStatus
{
    Active,
    Archived
}
EOF

  cat > "$dir/Events/ItemCreated.cs" <<EOF
namespace $app.Domain.$module.Events;

public sealed record ItemCreated(ItemId ItemId, string Name) : IDomainEvent;
EOF

  # The public API edited by scenario B of measure-analyzers.sh is marked by its comment.
  cat > "$dir/Item.cs" <<EOF
using $app.Domain.$module.Events;

namespace $app.Domain.$module;

public sealed class Item : IAggregateRoot
{
    private readonly List<IDomainEvent> _domainEvents = [];

    private Item(ItemId id, string name, int quantity)
    {
        Id = id;
        Name = name;
        Quantity = quantity;
        Status = ItemStatus.Active;
    }

    // Rehydration only.
    private Item()
    {
        Name = null!;
    }

    public ItemId Id { get; }

    public string Name { get; private set; }

    public int Quantity { get; private set; }

    public ItemStatus Status { get; private set; }

    public int Revision() => 0; // measure-analyzers: public API

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public static Item Create(string name, int quantity)
    {
        if (quantity < 0)
            throw new DomainException("An item quantity cannot be negative.");

        var item = new Item(ItemId.New(), name, quantity);
        item._domainEvents.Add(new ItemCreated(item.Id, name));
        return item;
    }

    public void Rename(string name)
    {
        EnsureActive();
        Name = name;
    }

    public void Adjust(int delta)
    {
        EnsureActive();
        if (Quantity + delta < 0)
            throw new DomainException(\$"Item '{Id}' cannot hold a negative quantity.");

        Quantity += delta;
    }

    public void Archive()
    {
        EnsureActive();
        Status = ItemStatus.Archived;
    }

    public void Restore()
    {
        if (Status != ItemStatus.Archived)
            throw new DomainException(\$"Item '{Id}' is not archived.");

        Status = ItemStatus.Active;
    }

    private void EnsureActive()
    {
        if (Status == ItemStatus.Archived)
            throw new DomainException(\$"Item '{Id}' is archived.");
    }
}
EOF
}

write_module() {
  local module="$1"
  local route="$2"
  local dir="$features_dir/$module"
  mkdir -p "$dir"

  cat > "$dir/ItemConfiguration.cs" <<EOF
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using $app.Domain.$module;

namespace $app.Features.$module;

internal sealed class ItemConfiguration : IEntityTypeConfiguration<Item>
{
    public void Configure(EntityTypeBuilder<Item> builder)
    {
        builder.ToTable("${module}Items");

        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id)
            .HasConversion(id => id.Value, value => new ItemId(value))
            .ValueGeneratedNever();

        builder.Property(item => item.Name).HasMaxLength(200);
        builder.Property(item => item.Quantity);
        builder.Property(item => item.Status)
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.Ignore(item => item.DomainEvents);
    }
}
EOF

  cat > "$dir/${module}Module.cs" <<EOF
using FluentValidation;
using $app.Domain.$module;
using $app.Features.$module.AdjustQuantity;
using $app.Features.$module.ArchiveItem;
using $app.Features.$module.CountItems;
using $app.Features.$module.CreateItem;
using $app.Features.$module.FindItemsByName;
using $app.Features.$module.GetItem;
using $app.Features.$module.ListArchivedItems;
using $app.Features.$module.ListItems;
using $app.Features.$module.RenameItem;
using $app.Features.$module.RestoreItem;
using $app.Shared.Messaging;

namespace $app.Features.$module;

public static class ${module}Module
{
    public static IServiceCollection Add${module}Module(this IServiceCollection services) =>
        services
            .AddScoped<IRequestHandler<CreateItemCommand, ItemId>, CreateItemHandler>()
            .AddScoped<IValidator<CreateItemCommand>, CreateItemValidator>()
            .AddScoped<IRequestHandler<RenameItemCommand, bool>, RenameItemHandler>()
            .AddScoped<IValidator<RenameItemCommand>, RenameItemValidator>()
            .AddScoped<IRequestHandler<AdjustQuantityCommand, bool>, AdjustQuantityHandler>()
            .AddScoped<IValidator<AdjustQuantityCommand>, AdjustQuantityValidator>()
            .AddScoped<IRequestHandler<ArchiveItemCommand, bool>, ArchiveItemHandler>()
            .AddScoped<IValidator<ArchiveItemCommand>, ArchiveItemValidator>()
            .AddScoped<IRequestHandler<RestoreItemCommand, bool>, RestoreItemHandler>()
            .AddScoped<IValidator<RestoreItemCommand>, RestoreItemValidator>()
            .AddScoped<IRequestHandler<GetItemQuery, ItemDto?>, GetItemHandler>()
            .AddScoped<IRequestHandler<ListItemsQuery, IReadOnlyList<ItemSummary>>, ListItemsHandler>()
            .AddScoped<IRequestHandler<CountItemsQuery, int>, CountItemsHandler>()
            .AddScoped<IRequestHandler<FindItemsByNameQuery, IReadOnlyList<ItemMatch>>, FindItemsByNameHandler>()
            .AddScoped<IRequestHandler<ListArchivedItemsQuery, IReadOnlyList<ArchivedItem>>, ListArchivedItemsHandler>();

    public static IEndpointRouteBuilder Map${module}Module(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/$route");
        CreateItemEndpoint.Map(group);
        RenameItemEndpoint.Map(group);
        AdjustQuantityEndpoint.Map(group);
        ArchiveItemEndpoint.Map(group);
        RestoreItemEndpoint.Map(group);
        GetItemEndpoint.Map(group);
        ListItemsEndpoint.Map(group);
        CountItemsEndpoint.Map(group);
        FindItemsByNameEndpoint.Map(group);
        ListArchivedItemsEndpoint.Map(group);
        return endpoints;
    }
}
EOF
}

write_create_slice() {
  local module="$1"
  local route="$2"
  local dir="$features_dir/$module/CreateItem"
  mkdir -p "$dir"
  cat > "$dir/CreateItem.cs" <<EOF
using FluentValidation;
using $app.Domain.$module;
using $app.Shared.Messaging;
using $app.Shared.Persistence;

namespace $app.Features.$module.CreateItem;

public sealed record CreateItemCommand(string Name, int Quantity) : ICommand<ItemId>;

internal sealed class CreateItemValidator : AbstractValidator<CreateItemCommand>
{
    public CreateItemValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(200);
        RuleFor(command => command.Quantity).GreaterThanOrEqualTo(0);
    }
}

internal sealed class CreateItemHandler(AppDbContext db) : IRequestHandler<CreateItemCommand, ItemId>
{
    public Task<ItemId> Handle(CreateItemCommand command, CancellationToken cancellationToken)
    {
        var item = Item.Create(command.Name.Trim(), command.Quantity);
        db.Set<Item>().Add(item);
        return Task.FromResult(item.Id);
    }
}

internal static class CreateItemEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/", async (CreateItemCommand command, IMediator mediator, CancellationToken cancellationToken) =>
            TypedResults.Created(\$"/$route/{await mediator.Send(command, cancellationToken)}"));
}
EOF
}

# A command that loads the item and calls one of its methods. Arguments: module, slice, extra command field ("" for none),
# validator rule for that field, the call on the item, HTTP route.
write_change_slice() {
  local module="$1"
  local slice="$2"
  local field="$3"
  local rule="$4"
  local call="$5"
  local route="$6"
  local dir="$features_dir/$module/$slice"
  local parameters="Guid ItemId"
  local endpoint_parameters="Guid id"
  local endpoint_arguments="id"
  local rules="        RuleFor(command => command.ItemId).NotEmpty();"
  if [[ -n "$field" ]]; then
    parameters="$parameters, $field"
    endpoint_parameters="$endpoint_parameters, $field"
    endpoint_arguments="$endpoint_arguments, ${field##* }"
    rules="$rules
        $rule"
  fi
  mkdir -p "$dir"
  cat > "$dir/$slice.cs" <<EOF
using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using $app.Domain.$module;
using $app.Shared.Messaging;
using $app.Shared.Persistence;

namespace $app.Features.$module.$slice;

public sealed record ${slice}Command($parameters) : ICommand<bool>;

internal sealed class ${slice}Validator : AbstractValidator<${slice}Command>
{
    public ${slice}Validator()
    {
$rules
    }
}

// Changes the aggregate only: TransactionBehavior saves and commits it.
internal sealed class ${slice}Handler(AppDbContext db) : IRequestHandler<${slice}Command, bool>
{
    public async Task<bool> Handle(${slice}Command command, CancellationToken cancellationToken)
    {
        var item = await db.Set<Item>().FindAsync([new ItemId(command.ItemId)], cancellationToken);
        if (item is null)
            return false;

        item.$call;
        return true;
    }
}

internal static class ${slice}Endpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/{id:guid}/$route", async Task<Results<NoContent, NotFound>> ($endpoint_parameters, IMediator mediator, CancellationToken cancellationToken) =>
            await mediator.Send(new ${slice}Command($endpoint_arguments), cancellationToken) ? TypedResults.NoContent() : TypedResults.NotFound());
}
EOF
}

write_get_slice() {
  local module="$1"
  local dir="$features_dir/$module/GetItem"
  mkdir -p "$dir"
  cat > "$dir/GetItem.cs" <<EOF
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using $app.Domain.$module;
using $app.Shared.Messaging;
using $app.Shared.Persistence;

namespace $app.Features.$module.GetItem;

public sealed record GetItemQuery(Guid ItemId) : IQuery<ItemDto?>;

public sealed record ItemDto(Guid Id, string Name, int Quantity, string Status);

internal sealed class GetItemHandler(AppDbContext db) : IRequestHandler<GetItemQuery, ItemDto?>
{
    public Task<ItemDto?> Handle(GetItemQuery query, CancellationToken cancellationToken)
    {
        var id = new ItemId(query.ItemId);
        return db.Set<Item>()
            .AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new ItemDto(item.Id.Value, item.Name, item.Quantity, item.Status.ToString()))
            .SingleOrDefaultAsync(cancellationToken);
    }
}

internal static class GetItemEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/{id:guid}", async Task<Results<Ok<ItemDto>, NotFound>> (Guid id, IMediator mediator, CancellationToken cancellationToken) =>
            await mediator.Send(new GetItemQuery(id), cancellationToken) is { } item ? TypedResults.Ok(item) : TypedResults.NotFound());
}
EOF
}

# The method body edited by scenario A of measure-analyzers.sh is marked by its comment, in ListItems.
write_list_slice() {
  local module="$1"
  local dir="$features_dir/$module/ListItems"
  mkdir -p "$dir"
  cat > "$dir/ListItems.cs" <<EOF
using Microsoft.EntityFrameworkCore;
using $app.Domain.$module;
using $app.Shared.Messaging;
using $app.Shared.Persistence;

namespace $app.Features.$module.ListItems;

public sealed record ListItemsQuery(int Page) : IQuery<IReadOnlyList<ItemSummary>>;

public sealed record ItemSummary(Guid Id, string Name, int Quantity);

internal sealed class ListItemsHandler(AppDbContext db) : IRequestHandler<ListItemsQuery, IReadOnlyList<ItemSummary>>
{
    public async Task<IReadOnlyList<ItemSummary>> Handle(ListItemsQuery query, CancellationToken cancellationToken)
    {
        const int pageSize = 50; // measure-analyzers: method body
        return await db.Set<Item>()
            .AsNoTracking()
            .Where(item => item.Status == ItemStatus.Active)
            .OrderBy(item => item.Name)
            .Skip(Math.Max(query.Page, 0) * pageSize)
            .Take(pageSize)
            .Select(item => new ItemSummary(item.Id.Value, item.Name, item.Quantity))
            .ToListAsync(cancellationToken);
    }
}

internal static class ListItemsEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/", async (int? page, IMediator mediator, CancellationToken cancellationToken) =>
            TypedResults.Ok(await mediator.Send(new ListItemsQuery(page ?? 0), cancellationToken)));
}
EOF
}

write_count_slice() {
  local module="$1"
  local dir="$features_dir/$module/CountItems"
  mkdir -p "$dir"
  cat > "$dir/CountItems.cs" <<EOF
using Microsoft.EntityFrameworkCore;
using $app.Domain.$module;
using $app.Shared.Messaging;
using $app.Shared.Persistence;

namespace $app.Features.$module.CountItems;

public sealed record CountItemsQuery(bool IncludeArchived) : IQuery<int>;

internal sealed class CountItemsHandler(AppDbContext db) : IRequestHandler<CountItemsQuery, int>
{
    public Task<int> Handle(CountItemsQuery query, CancellationToken cancellationToken) =>
        db.Set<Item>()
            .AsNoTracking()
            .CountAsync(item => query.IncludeArchived || item.Status == ItemStatus.Active, cancellationToken);
}

internal static class CountItemsEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/count", async (bool? includeArchived, IMediator mediator, CancellationToken cancellationToken) =>
            TypedResults.Ok(await mediator.Send(new CountItemsQuery(includeArchived ?? false), cancellationToken)));
}
EOF
}

write_find_slice() {
  local module="$1"
  local dir="$features_dir/$module/FindItemsByName"
  mkdir -p "$dir"
  cat > "$dir/FindItemsByName.cs" <<EOF
using Microsoft.EntityFrameworkCore;
using $app.Domain.$module;
using $app.Shared.Messaging;
using $app.Shared.Persistence;

namespace $app.Features.$module.FindItemsByName;

public sealed record FindItemsByNameQuery(string Prefix) : IQuery<IReadOnlyList<ItemMatch>>;

public sealed record ItemMatch(Guid Id, string Name);

internal sealed class FindItemsByNameHandler(AppDbContext db) : IRequestHandler<FindItemsByNameQuery, IReadOnlyList<ItemMatch>>
{
    public async Task<IReadOnlyList<ItemMatch>> Handle(FindItemsByNameQuery query, CancellationToken cancellationToken) =>
        await db.Set<Item>()
            .AsNoTracking()
            .Where(item => item.Name.StartsWith(query.Prefix))
            .OrderBy(item => item.Name)
            .Take(20)
            .Select(item => new ItemMatch(item.Id.Value, item.Name))
            .ToListAsync(cancellationToken);
}

internal static class FindItemsByNameEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/search", async (string prefix, IMediator mediator, CancellationToken cancellationToken) =>
            TypedResults.Ok(await mediator.Send(new FindItemsByNameQuery(prefix), cancellationToken)));
}
EOF
}

write_archived_slice() {
  local module="$1"
  local dir="$features_dir/$module/ListArchivedItems"
  mkdir -p "$dir"
  cat > "$dir/ListArchivedItems.cs" <<EOF
using Microsoft.EntityFrameworkCore;
using $app.Domain.$module;
using $app.Shared.Messaging;
using $app.Shared.Persistence;

namespace $app.Features.$module.ListArchivedItems;

public sealed record ListArchivedItemsQuery : IQuery<IReadOnlyList<ArchivedItem>>;

public sealed record ArchivedItem(Guid Id, string Name);

internal sealed class ListArchivedItemsHandler(AppDbContext db) : IRequestHandler<ListArchivedItemsQuery, IReadOnlyList<ArchivedItem>>
{
    public async Task<IReadOnlyList<ArchivedItem>> Handle(ListArchivedItemsQuery query, CancellationToken cancellationToken) =>
        await db.Set<Item>()
            .AsNoTracking()
            .Where(item => item.Status == ItemStatus.Archived)
            .OrderBy(item => item.Name)
            .Select(item => new ArchivedItem(item.Id.Value, item.Name))
            .ToListAsync(cancellationToken);
}

internal static class ListArchivedItemsEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/archived", async (IMediator mediator, CancellationToken cancellationToken) =>
            TypedResults.Ok(await mediator.Send(new ListArchivedItemsQuery(), cancellationToken)));
}
EOF
}

registrations=""
mappings=""
usings=""
for number in $(seq -f "%02g" 1 "$module_count"); do
  module="Module$number"
  route="module$number"
  write_domain "$module"
  write_module "$module" "$route"
  write_create_slice "$module" "$route"
  write_change_slice "$module" RenameItem "string Name" "RuleFor(command => command.Name).NotEmpty().MaximumLength(200);" "Rename(command.Name.Trim())" rename
  write_change_slice "$module" AdjustQuantity "int Delta" "RuleFor(command => command.Delta).NotEqual(0);" "Adjust(command.Delta)" adjust
  write_change_slice "$module" ArchiveItem "" "" "Archive()" archive
  write_change_slice "$module" RestoreItem "" "" "Restore()" restore
  write_get_slice "$module"
  write_list_slice "$module"
  write_count_slice "$module"
  write_find_slice "$module"
  write_archived_slice "$module"
  usings+="using $app.Features.$module;"$'\n'
  registrations+="builder.Services.Add${module}Module();"$'\n'
  mappings+="app.Map${module}Module();"$'\n'
done

# Program.cs registers and maps every module after Bookings.
program_text="$(cat "$program")"
for expected in "using $app.Features.Bookings;" "builder.Services.AddBookingsModule();" "app.MapBookingsModule();"; do
  if ! grep -qxF "$expected" "$program"; then
    echo "error: $program has no line '$expected' to add the synthetic modules after." >&2
    exit 1
  fi
done
program_text="${program_text/"using $app.Features.Bookings;"/"using $app.Features.Bookings;"$'\n'"${usings%$'\n'}"}"
program_text="${program_text/"builder.Services.AddBookingsModule();"/"builder.Services.AddBookingsModule();"$'\n'"${registrations%$'\n'}"}"
program_text="${program_text/"app.MapBookingsModule();"/"app.MapBookingsModule();"$'\n'"${mappings%$'\n'}"}"
printf '%s\n' "$program_text" > "$program"
