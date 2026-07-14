using FraudEvaluation.Application.Commands;
using FraudEvaluation.Application.Common;
using FraudEvaluation.Domain.ValueObjects;

namespace FraudEvaluation.Application.Validators
{
    public class SubmitFraudEvaluationValidator
    {
        public static Result<Currency> Validate(SubmitFraudEvaluationCommand cmd)
        {
            if (cmd == null) return Result.Fail<Currency>("Request is null", ErrorCode.ValidationFailed);

            if (string.IsNullOrWhiteSpace(cmd.TaxId) || !cmd.TaxId.All(char.IsDigit))
            {
                return Result.Fail<Currency>("TaxId must contain only digits.", ErrorCode.ValidationFailed);
            }

            if (cmd.Amount <= 0)
            {
                return Result.Fail<Currency>("Amount must be greater than zero.", ErrorCode.ValidationFailed);
            }

            // Validate idempotency key
            if (!Guid.TryParse(cmd.IdempotencyKey, out _))
            {
                return Result.Fail<Currency>("Idempotency-Key must be a valid GUID.", ErrorCode.InvalidId);
            }

            // Validate currency using TryCreate to avoid exceptions
            if (!Currency.TryCreate(cmd.Currency, out var currencyVo))
            {
                return Result.Fail<Currency>("Currency must be one of: BRL, USD, EUR.", ErrorCode.ValidationFailed);
            }

            return Result.Ok(currencyVo!);
        }
    }
}
