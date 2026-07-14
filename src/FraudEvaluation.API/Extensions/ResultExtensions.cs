using FraudEvaluation.Application.Common;

namespace FraudEvaluation.API.Extensions
{
    public static class ResultExtensions
    {
        public static IResult ToIResult<T>(this Result<T> result, Func<T, IResult>? onSuccess = null)
        {
            if (!result.IsSuccess)
            {
                return result.Code switch
                {
                    ErrorCode.InvalidId => Results.BadRequest(new { code = result.Code.ToString(), message = result.Error ?? "Invalid id" }),
                    ErrorCode.NotFound => Results.NotFound(),
                    ErrorCode.ValidationFailed => Results.UnprocessableEntity(new { code = result.Code.ToString(), message = result.Error }),
                    _ => Results.Problem(detail: result.Error ?? "An internal error occurred", statusCode: 500),
                };
            }

            if (onSuccess != null)
            {
                return onSuccess(result.Value!);
            }

            return Results.Ok(result.Value);
        }
    }
}
