using System.Security.Claims;
using AwesomeAssertions;
using Koto.Api.FastEndpoints.Extensions;

namespace Koto.Api.FastEndpoints.Tests;

public class ClaimsPrincipalExtensionsTests
{
    private static ClaimsPrincipal With(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, authenticationType: "test"));

    [Fact]
    public void GetUserId_reads_name_identifier()
    {
        var id = Guid.NewGuid();
        var user = With(new Claim(ClaimTypes.NameIdentifier, id.ToString()));

        user.GetUserId().Should().Be(id);
    }

    [Fact]
    public void GetUserId_throws_when_claim_absent()
    {
        var user = With();

        var act = () => user.GetUserId();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void GetUserId_throws_when_claim_not_a_guid()
    {
        var user = With(new Claim(ClaimTypes.NameIdentifier, "not-a-guid"));

        var act = () => user.GetUserId();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void TryGetUserId_returns_true_when_present()
    {
        var id = Guid.NewGuid();
        var user = With(new Claim(ClaimTypes.NameIdentifier, id.ToString()));

        user.TryGetUserId(out var parsed).Should().BeTrue();
        parsed.Should().Be(id);
    }

    [Fact]
    public void TryGetUserId_returns_false_when_absent()
    {
        var user = With();

        user.TryGetUserId(out _).Should().BeFalse();
    }
}
