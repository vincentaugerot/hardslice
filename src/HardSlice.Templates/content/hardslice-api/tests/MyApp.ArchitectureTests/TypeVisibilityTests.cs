using System.Reflection;

namespace MyApp.ArchitectureTests;

public sealed class TypeVisibilityTests
{
    [Theory]
    [InlineData("Public", true)]
    [InlineData("Protected", true)]
    [InlineData("ProtectedInternal", true)]
    [InlineData("Internal", false)]
    [InlineData("Private", false)]
    [InlineData("PrivateProtected", false)]
    public void Recognizes_nested_types_an_external_consumer_can_access(string name, bool accessible)
    {
        var type = typeof(PublicContainer).GetNestedType(name, BindingFlags.Public | BindingFlags.NonPublic)!;

        TypeVisibility.IsExternallyAccessible(type).ShouldBe(accessible);
    }

    [Fact]
    public void Follows_the_entire_declaring_type_chain()
    {
        var protectedContainer = typeof(PublicContainer).GetNestedType("Protected", BindingFlags.NonPublic)!;

        TypeVisibility.IsExternallyAccessible(protectedContainer.GetNestedType("PublicChild")!).ShouldBeTrue();
        TypeVisibility.IsExternallyAccessible(typeof(InternalContainer.PublicChild)).ShouldBeFalse();
        TypeVisibility.IsExternallyAccessible(typeof(PrivateContainer.PublicChild)).ShouldBeFalse();
    }

    public class PublicContainer
    {
        public class Public;
        protected class Protected { public class PublicChild; }
        protected internal class ProtectedInternal;
        internal class Internal;
        private class Private;
        private protected class PrivateProtected;
    }

    internal class InternalContainer { public class PublicChild; }
    private class PrivateContainer { public class PublicChild; }
}
