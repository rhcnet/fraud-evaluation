using MediatR;
using FraudEvaluation.Application.Common;

namespace FraudEvaluation.Application.Commands
{
    public record SubmitFraudEvaluationCommand(string IdempotencyKey, string Ip, string TaxId, decimal Amount, string Currency) : IRequest<Result<SubmitFraudEvaluationResult>>;
}
