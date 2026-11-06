using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MyApp.Domain.ModuleName;

namespace MyApp.Features.ModuleName;

// Stores an identifier AggregateNameId as its Guid, for every mapping that holds one: each child entity of AggregateName, and the root
// itself with HasConversion<AggregateNameIdConverter>(). dotnet new hardslice-entity creates it with the first child and reuses it.
internal sealed class AggregateNameIdConverter() : ValueConverter<AggregateNameId, Guid>(id => id.Value, value => new AggregateNameId(value));
