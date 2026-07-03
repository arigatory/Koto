using AwesomeAssertions;
using Koto.Application;

namespace Koto.Application.Tests;

public class AssemblyScanningTests
{
    [Fact]
    public void GetLoadableTypes_returns_types_of_a_healthy_assembly()
    {
        var types = AssemblyScanning.GetLoadableTypes(typeof(AssemblyScanningTests).Assembly);

        types.Should().Contain(typeof(AssemblyScanningTests));
    }

    [Fact]
    public void GetLoadableTypes_throws_on_null()
    {
        var act = () => AssemblyScanning.GetLoadableTypes(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
