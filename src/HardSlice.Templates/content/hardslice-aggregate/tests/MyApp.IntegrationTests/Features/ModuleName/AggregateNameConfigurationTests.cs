using Microsoft.EntityFrameworkCore;
using MyApp.Domain.ModuleName;

namespace MyApp.IntegrationTests.Features.ModuleName;

public sealed class AggregateNameConfigurationTests(PostgresDatabase database)
{
    // The concurrency token of AggregateNameConfiguration, read from the model of AppDbContext. Without it, a command that changes the
    // aggregate AggregateName after another request changed it since it loaded it overwrites that change without error, and a
    // migration that records its removal leaves the other tests green, those of the 409 aside. The test of the 409 comes with the
    // first command that changes the aggregate AggregateName.
    [Fact]
    public async Task Maps_the_Version_of_AggregateName_on_xmin_as_its_concurrency_token()
    {
        await using var db = database.CreateDbContext();

        var version = db.Model.FindEntityType(typeof(AggregateName))?.FindProperty("Version");

        version.ShouldNotBeNull("AggregateNameConfiguration maps no concurrency token: keep builder.Property<uint>(\"Version\").IsRowVersion();.");
        version.IsShadowProperty().ShouldBeTrue();
        version.ClrType.ShouldBe(typeof(uint));
        version.IsConcurrencyToken.ShouldBeTrue();
        version.ValueGenerated.ShouldBe(Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.OnAddOrUpdate);
        version.GetColumnName().ShouldBe("xmin");
    }
}
