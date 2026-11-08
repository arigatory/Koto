using AwesomeAssertions;
using Koto.Domain;

namespace Koto.Domain.Tests;

public class ErrorListTests
{
    private static readonly Error A = new TestError("test.list.a", "A");
    private static readonly Error B = new TestError("test.list.b", "B");

    [Fact]
    public void Default_is_empty()
    {
        ErrorList<Error> list = default;

        list.Count.Should().Be(0);
        list.Should().BeEmpty();
        ErrorList<Error>.Empty.Count.Should().Be(0);
    }

    [Fact]
    public void Constructed_from_enumerable_copies_in_order()
    {
        var source = new List<Error> { A, B };
        var list = new ErrorList<Error>(source);
        source.Clear();

        list.Should().ContainInOrder(A, B);
        list[1].Should().Be(B);
    }

    [Fact]
    public void Rejects_null_elements()
    {
        var act = () => new ErrorList<Error>([A, null!]);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Select_maps_preserving_order()
    {
        var list = new ErrorList<Error>([A, B]);

        var mapped = list.Select(e => new TestError(e.Code, e.Message + "!"));

        mapped.Should().HaveCount(2);
        mapped[0].Message.Should().Be("A!");
        mapped[1].Message.Should().Be("B!");
    }

    [Fact]
    public void Single_error_converts_implicitly()
    {
        ErrorList<Error> list = A;

        list.Should().ContainSingle().Which.Should().Be(A);
    }
}
