using System.Security.Claims;
using AwesomeAssertions;
using FastEndpoints;
using Koto.Api.FastEndpoints.Endpoints;
using Koto.Api.FastEndpoints.Extensions;
using Microsoft.AspNetCore.Http;
using App = Koto.Application;

namespace Koto.Api.FastEndpoints.Tests;

/// <summary>
/// Verifies the request→command/query mapping: server-derived fields (here, the user id) are
/// taken from the endpoint context (claims) rather than the request DTO.
/// </summary>
public class MappedEndpointTests
{
    private static ClaimsPrincipal PrincipalWith(Guid userId) =>
        new(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) }, "test"));

    // ── Result-bearing command ───────────────────────────────────────────────────

    private sealed record JudgmentResult(Guid JudgeId);
    private sealed record SubmitRequest(Guid SubmissionId, int Score);              // no JudgeId on the wire
    private sealed record SubmitCommand(Guid JudgeId, Guid SubmissionId, int Score) : App.ICommand<JudgmentResult>;

    private sealed class SubmitEndpoint : MappedCommandEndpoint<SubmitRequest, SubmitCommand, JudgmentResult>
    {
        public override void Configure() => Post("/judgments");
        protected override SubmitCommand ToCommand(SubmitRequest r) => new(User.GetUserId(), r.SubmissionId, r.Score);
        public SubmitCommand Map(SubmitRequest r) => ToCommand(r);
    }

    [Fact]
    public void MappedCommandEndpoint_builds_command_with_server_derived_user_id()
    {
        var userId = Guid.NewGuid();
        var submissionId = Guid.NewGuid();
        var ctx = new DefaultHttpContext { User = PrincipalWith(userId) };
        var ep = Factory.Create<SubmitEndpoint>(ctx);

        var cmd = ep.Map(new SubmitRequest(submissionId, 7));

        cmd.JudgeId.Should().Be(userId);          // from claims, not bindable from the request
        cmd.SubmissionId.Should().Be(submissionId);
        cmd.Score.Should().Be(7);
    }

    // ── Void command ─────────────────────────────────────────────────────────────

    private sealed record DeleteRequest(Guid Id);
    private sealed record DeleteCommand(Guid Id, Guid ActorId) : App.ICommand;

    private sealed class DeleteEndpoint : MappedCommandEndpoint<DeleteRequest, DeleteCommand>
    {
        public override void Configure() => Delete("/things/{id}");
        protected override DeleteCommand ToCommand(DeleteRequest r) => new(r.Id, User.GetUserId());
        public DeleteCommand Map(DeleteRequest r) => ToCommand(r);
    }

    [Fact]
    public void MappedCommandEndpoint_void_builds_command_with_server_derived_actor()
    {
        var userId = Guid.NewGuid();
        var thingId = Guid.NewGuid();
        var ctx = new DefaultHttpContext { User = PrincipalWith(userId) };
        var ep = Factory.Create<DeleteEndpoint>(ctx);

        var cmd = ep.Map(new DeleteRequest(thingId));

        cmd.Id.Should().Be(thingId);
        cmd.ActorId.Should().Be(userId);
    }

    // ── Query ────────────────────────────────────────────────────────────────────

    private sealed record MyRatingRequest;
    private sealed record MyRatingQuery(Guid UserId) : App.IQuery<int>;

    private sealed class MyRatingEndpoint : MappedQueryEndpoint<MyRatingRequest, MyRatingQuery, int>
    {
        public override void Configure() => Get("/me/rating");
        protected override MyRatingQuery ToQuery(MyRatingRequest r) => new(User.GetUserId());
        public MyRatingQuery Map(MyRatingRequest r) => ToQuery(r);
    }

    [Fact]
    public void MappedQueryEndpoint_builds_query_with_server_derived_user_id()
    {
        var userId = Guid.NewGuid();
        var ctx = new DefaultHttpContext { User = PrincipalWith(userId) };
        var ep = Factory.Create<MyRatingEndpoint>(ctx);

        var query = ep.Map(new MyRatingRequest());

        query.UserId.Should().Be(userId);
    }
}
