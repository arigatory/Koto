using AwesomeAssertions;
using Koto.Application;

namespace Koto.Application.Tests;

public sealed class PagedListTests
{
    [Fact]
    public void Total_pages_rounds_up()
    {
        var page = new PagedList<int>([1, 2, 3], page: 1, pageSize: 3, totalCount: 7);

        page.TotalPages.Should().Be(3);
        page.HasNextPage.Should().BeTrue();
        page.HasPreviousPage.Should().BeFalse();
    }

    [Fact]
    public void Last_page_has_no_next()
    {
        var page = new PagedList<int>([7], page: 3, pageSize: 3, totalCount: 7);

        page.HasNextPage.Should().BeFalse();
        page.HasPreviousPage.Should().BeTrue();
    }

    [Fact]
    public void Empty_page_has_zero_totals()
    {
        var page = PagedList<string>.Empty();

        page.TotalCount.Should().Be(0);
        page.TotalPages.Should().Be(0);
        page.HasNextPage.Should().BeFalse();
    }

    [Fact]
    public void Map_projects_items_and_keeps_metadata()
    {
        var page = new PagedList<int>([1, 2], page: 2, pageSize: 2, totalCount: 5);

        var mapped = page.Map(i => i.ToString());

        mapped.Items.Should().Equal("1", "2");
        mapped.Page.Should().Be(2);
        mapped.TotalCount.Should().Be(5);
    }

    [Theory]
    [InlineData(0, 10, 0)]
    [InlineData(1, 0, 0)]
    [InlineData(1, 10, -1)]
    public void Invalid_arguments_throw(int page, int pageSize, int totalCount)
    {
        var act = () => new PagedList<int>([], page, pageSize, totalCount);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
