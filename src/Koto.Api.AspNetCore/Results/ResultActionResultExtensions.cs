using Koto.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Koto.Api.AspNetCore;

/// <summary>
/// Maps <see cref="Result{T}"/> to MVC <see cref="ActionResult"/> responses:
/// 200 OK (or 204 No Content for <see cref="Unit"/>) on success, RFC 7807 Problem Details
/// on failure. Status codes come from the <see cref="KotoHttpErrorOptions"/> registered
/// via <c>AddKotoAspNetCore()</c> (built-in defaults are used when not registered).
/// </summary>
public static class ResultActionResultExtensions
{
    /// <summary>Maps a result to 200 OK with the value, or Problem Details on failure.</summary>
    public static ActionResult<T> ToActionResult<T>(this Result<T> result, ControllerBase controller, string? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(controller);
        return result.IsSuccess
            ? controller.Ok(result.Value)
            : ToProblemActionResult(result.Errors, controller, correlationId);
    }

    /// <summary>Maps a void result to 204 No Content, or Problem Details on failure.</summary>
    public static IActionResult ToActionResult(this Result<Unit> result, ControllerBase controller, string? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(controller);
        return result.IsSuccess
            ? controller.NoContent()
            : ToProblemActionResult(result.Errors, controller, correlationId);
    }

    /// <summary>Awaits a dispatch and maps the result — <c>(await dispatcher.SendAsync(cmd, ct)).ToActionResult(this)</c>.</summary>
    public static async Task<ActionResult<T>> ToActionResultAsync<T>(
        this Task<Result<T>> pending, ControllerBase controller, string? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(pending);
        var result = await pending.ConfigureAwait(false);
        return result.ToActionResult(controller, correlationId);
    }

    /// <summary>Awaits a void dispatch and maps the result to 204 No Content or Problem Details.</summary>
    public static async Task<IActionResult> ToActionResultAsync(
        this Task<Result<Unit>> pending, ControllerBase controller, string? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(pending);
        var result = await pending.ConfigureAwait(false);
        return result.ToActionResult(controller, correlationId);
    }

    private static ObjectResult ToProblemActionResult(
        IReadOnlyList<Error> errors, ControllerBase controller, string? correlationId)
    {
        var options = controller.HttpContext.GetKotoHttpErrorOptions();

        Microsoft.AspNetCore.Mvc.ProblemDetails problem;
        if (errors.Count == 1)
        {
            var error = errors[0];
            problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Detail = error.Message,
                Status = options.StatusCodeFor(error),
            };
            problem.Extensions["errorCode"] = error.Code;
            if (error.Field is not null)
                problem.Extensions["field"] = error.Field;
        }
        else
        {
            problem = new ValidationProblemDetails(KotoProblemDetails.GroupByField(errors))
            {
                Status = StatusCodes.Status400BadRequest,
            };
            problem.Extensions["errorCodes"] = errors.Select(e => e.Code).ToArray();
        }

        if (correlationId is not null)
            problem.Extensions["correlationId"] = correlationId;

        return new ObjectResult(problem)
        {
            StatusCode = problem.Status,
            ContentTypes = { "application/problem+json" },
        };
    }
}
