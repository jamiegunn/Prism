using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Prism.Common.Results;

/// <summary>
/// Extension methods for converting <see cref="Result{T}"/> to HTTP responses.
/// </summary>
public static class ResultExtensions
{
    /// <summary>
    /// Converts a <see cref="Result{T}"/> to an <see cref="IResult"/> suitable for Minimal API responses.
    /// Maps success to 200 OK and errors to the appropriate HTTP status code.
    /// </summary>
    /// <typeparam name="T">The type of the success value.</typeparam>
    /// <param name="result">The result to convert.</param>
    /// <returns>An HTTP result with the appropriate status code and body.</returns>
    public static IResult ToHttpResult<T>(this Result<T> result)
    {
        if (result.IsSuccess)
        {
            return TypedResults.Ok(result.Value);
        }

        return result.Error.ToHttpResult();
    }

    /// <summary>
    /// Converts a non-generic <see cref="Result"/> to an <see cref="IResult"/>.
    /// Maps success to 204 No Content and errors to the appropriate HTTP status code.
    /// </summary>
    /// <param name="result">The result to convert.</param>
    /// <returns>An HTTP result with the appropriate status code.</returns>
    public static IResult ToHttpResult(this Result result)
    {
        if (result.IsSuccess)
        {
            return TypedResults.NoContent();
        }

        return result.Error.ToHttpResult();
    }

    /// <summary>
    /// Converts an <see cref="Error"/> to an <see cref="IResult"/> with the appropriate HTTP status code
    /// and a ProblemDetails body.
    /// </summary>
    /// <param name="error">The error to convert.</param>
    /// <returns>An HTTP result with ProblemDetails.</returns>
    public static IResult ToHttpResult(this Error error)
    {
        Microsoft.AspNetCore.Mvc.ProblemDetails problemDetails = error.ToProblemDetails();

        return error.Type switch
        {
            ErrorType.Validation => TypedResults.Problem(problemDetails),
            ErrorType.NotFound => TypedResults.Problem(problemDetails),
            ErrorType.Conflict => TypedResults.Problem(problemDetails),
            ErrorType.Unavailable => TypedResults.Problem(problemDetails),
            _ => TypedResults.Problem(problemDetails)
        };
    }

    /// <summary>
    /// Converts an <see cref="Error"/> to a <see cref="Microsoft.AspNetCore.Mvc.ProblemDetails"/> instance
    /// with the appropriate status code and detail message.
    /// </summary>
    /// <param name="error">The error to convert.</param>
    /// <returns>A ProblemDetails instance representing the error.</returns>
    public static Microsoft.AspNetCore.Mvc.ProblemDetails ToProblemDetails(this Error error)
    {
        int statusCode = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Unavailable => StatusCodes.Status503ServiceUnavailable,
            ErrorType.Internal => StatusCodes.Status500InternalServerError,
            _ => StatusCodes.Status500InternalServerError
        };

        return new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Status = statusCode,
            Title = error.Code,
            Detail = error.Message,
            Type = $"https://httpstatuses.com/{statusCode}"
        };
    }
}
