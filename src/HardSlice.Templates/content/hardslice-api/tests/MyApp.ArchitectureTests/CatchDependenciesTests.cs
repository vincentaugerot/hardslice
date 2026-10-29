namespace MyApp.ArchitectureTests;

public sealed class CatchDependenciesTests
{
    [Theory]
    [InlineData(typeof(MethodProbe))]
    [InlineData(typeof(ConstructorProbe))]
    [InlineData(typeof(StaticConstructorProbe))]
    [InlineData(typeof(AsyncProbe))]
    [InlineData(typeof(IteratorProbe))]
    [InlineData(typeof(LambdaProbe))]
    [InlineData(typeof(GenericProbe<>))]
    public void Includes_catch_only_dependencies_without_executing_application_code(Type source) =>
        CatchDependencies.Of(source).ShouldContain(typeof(ProbeException));

    [Fact]
    public void Includes_the_arguments_of_a_generic_exception() =>
        CatchDependencies.Of(typeof(GenericCatchProbe)).ShouldContain(typeof(ProbeArgument));

    [Fact]
    public void Does_not_attribute_an_ordinary_nested_types_catches_to_its_parent() =>
        CatchDependencies.Of(typeof(OuterProbe)).ShouldBeEmpty();

    [Fact]
    public void Ignores_filters_finally_blocks_and_methods_without_bodies() =>
        CatchDependencies.Of(typeof(FilterAndFinallyProbe)).ShouldBeEmpty();

    private sealed class ProbeException : Exception;
    private sealed class GenericException<T> : Exception;
    private sealed class ProbeArgument;

    private sealed class MethodProbe
    {
        public void Run(Action action) { try { action(); } catch (ProbeException) { } }
    }

    private sealed class ConstructorProbe
    {
        public ConstructorProbe(Action action) { try { action(); } catch (ProbeException) { } }
    }

    private sealed class StaticConstructorProbe
    {
        static StaticConstructorProbe()
        {
            try { throw new InvalidOperationException("Metadata inspection must never execute this initializer."); }
            catch (ProbeException) { }
        }
    }

    private sealed class AsyncProbe
    {
        public async Task Run(Func<Task> action) { try { await action(); } catch (ProbeException) { } }
    }

    private sealed class IteratorProbe
    {
        public IEnumerable<int> Run(Action action)
        {
            try { action(); } catch (ProbeException) { }
            yield return 1;
        }
    }

    private sealed class LambdaProbe
    {
        public Action Create(Action action) => () => { try { action(); } catch (ProbeException) { } };
    }

    private sealed class GenericProbe<T>
    {
        public void Run(Action action) { try { action(); } catch (ProbeException) { } }
    }

    private sealed class GenericCatchProbe
    {
        public void Run(Action action) { try { action(); } catch (GenericException<ProbeArgument>) { } }
    }

    private sealed class OuterProbe
    {
        public sealed class NestedProbe
        {
            public void Run(Action action) { try { action(); } catch (ProbeException) { } }
        }
    }

    private abstract class FilterAndFinallyProbe
    {
        public abstract void Run();

        public void Run(Action action, bool handle)
        {
            try { action(); }
            catch (ProbeException) when (handle) { }
            finally { GC.KeepAlive(action); }
        }
    }
}
