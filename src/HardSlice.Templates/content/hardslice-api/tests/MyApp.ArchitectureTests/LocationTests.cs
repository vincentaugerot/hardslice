namespace MyApp.ArchitectureTests;

public sealed class LocationTests
{
    [Fact]
    public void The_Features_root_is_in_Features_without_belonging_to_a_module()
    {
        var location = Location.Of($"{Application.WebName}.Features");

        location.InFeatures.ShouldBeTrue();
        location.Module.ShouldBeNull();
        location.Slice.ShouldBeNull();
        location.Describe().ShouldBe("in 'Features'");
    }

    [Theory]
    [InlineData("FeaturesExtra")]
    [InlineData("Other.Features")]
    [InlineData("Shared.Behaviors")]
    public void A_different_namespace_is_not_in_Features(string suffix) =>
        Location.Of($"{Application.WebName}.{suffix}").InFeatures.ShouldBeFalse();
}
