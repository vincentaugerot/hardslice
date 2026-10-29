namespace MyApp.ArchitectureTests;

// HS012: a module uses another module only through its public contract, the types declared directly in
// '<App>.Features.<Module>' as HS016 defines it. HS016 already covers a slice using another slice; this rule also covers
// a module's own contract, registration or mapping reaching into the slices of another module.
public sealed class ModuleTests
{
    [Fact]
    public void Modules_use_each_other_only_through_their_public_contract()
    {
        Violations.ShouldBeNone(
            from type in Application.TypesOf(Application.WebAssembly)
            let source = Location.Of(type)
            where source.Module is not null
            from used in Application.TypesUsedBy(type)
            where Application.IsDeclaredIn(used, Application.WebAssembly)
            let target = Location.Of(used)
            where target.InFeatures && target.Module != source.Module && target.Slice is not null
            select $"HS012: '{Application.DisplayName(type)}' {source.Describe()} uses '{Application.DisplayName(used)}' from slice '{target.Slice}' "
                + $"of module '{target.Module}'. A module uses another module only through its public contract. "
                + $"Fix: use a type declared directly in namespace '{Application.WebName}.Features.{target.Module}', react to a domain event, "
                + "or move the shared rule into the domain. Doc: 'docs/rules/HS012.md'.");
    }
}
