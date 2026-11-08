using System.Text.Json;
using AwesomeAssertions;
using Koto.Domain;

namespace Koto.Application.Tests;

public class EnvelopeTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 2, 11, 3, TimeSpan.Zero);
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private sealed record Quota(int MaxItems) : Error("test.envelope.quota", "Quota exceeded.");
    private sealed record Dto(string Name, int Count);

    [Fact]
    public void Ok_has_success_true_and_empty_errors()
    {
        var envelope = Envelope.Ok(new Dto("a", 1), Now, "corr-1");

        envelope.Success.Should().BeTrue();
        envelope.Result.Should().Be(new Dto("a", 1));
        envelope.Errors.Should().BeEmpty();
        envelope.CorrelationId.Should().Be("corr-1");
        envelope.Timestamp.Should().Be(Now);
    }

    [Fact]
    public void Fail_flattens_errors_with_details()
    {
        var envelope = Envelope.Fail([new Quota(5).ForField("items")], Now);

        envelope.Success.Should().BeFalse();
        envelope.Result.Should().BeNull();
        var error = envelope.Errors.Should().ContainSingle().Subject;
        error.Code.Should().Be("test.envelope.quota");
        error.Message.Should().Be("Quota exceeded.");
        error.Field.Should().Be("items");
        error.Details.Should().ContainKey("maxItems").WhoseValue.Should().Be(5);
    }

    [Fact]
    public void Json_uses_camel_case_keys_and_iso_timestamp()
    {
        var json = JsonSerializer.Serialize(Envelope.Ok(new Dto("a", 1), Now, "c"), Web);

        json.Should().Be("""{"success":true,"result":{"name":"a","count":1},"errors":[],"correlationId":"c","timestamp":"2026-11-08T02:11:03+00:00"}""");
    }

    [Fact]
    public void Unit_result_serializes_as_null()
    {
        JsonSerializer.Serialize(AppResult.Success().ToEnvelope(Now), Web).Should().Contain("\"result\":null");
        JsonSerializer.Serialize(Envelope.Ok(Unit.Value, Now), Web).Should().Contain("\"result\":null");
    }

    [Fact]
    public void Failed_envelope_for_struct_result_serializes_null_result()
    {
        var envelope = AppResult<Guid>.Failure(new Quota(1).Enrich()).ToEnvelope(Now);

        JsonSerializer.Serialize(envelope, Web).Should().Contain("\"result\":null");
    }

    [Fact]
    public void Failed_error_json_has_code_message_field_details()
    {
        var json = JsonSerializer.Serialize(Envelope.Fail([new Quota(5).ForField("items")], Now), Web);

        json.Should().Contain("""{"code":"test.envelope.quota","message":"Quota exceeded.","field":"items","details":{"maxItems":5}}""");
    }

    [Fact]
    public void Round_trips_through_json()
    {
        var original = Envelope.Fail<Dto>([new Quota(5).ForField("items")], Now, "c");

        var back = JsonSerializer.Deserialize<Envelope<Dto>>(JsonSerializer.Serialize(original, Web), Web)!;

        back.Success.Should().BeFalse();
        back.Result.Should().BeNull();
        back.Errors.Should().ContainSingle().Which.Code.Should().Be("test.envelope.quota");
        back.Errors[0].Field.Should().Be("items");
        back.CorrelationId.Should().Be("c");
        back.Timestamp.Should().Be(Now);

        var okBack = JsonSerializer.Deserialize<Envelope<Dto>>(JsonSerializer.Serialize(Envelope.Ok(new Dto("a", 1), Now), Web), Web)!;
        okBack.Result.Should().Be(new Dto("a", 1));
    }

    [Fact]
    public void ToEnvelope_maps_app_results()
    {
        AppResult<int>.Success(3).ToEnvelope(Now, "c").Result.Should().Be(3);
        AppResult<int>.Failure(new Quota(1).Enrich()).ToEnvelope(Now).Errors.Should().ContainSingle();
    }
}
