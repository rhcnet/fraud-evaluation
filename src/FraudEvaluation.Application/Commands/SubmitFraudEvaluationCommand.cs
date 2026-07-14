using MediatR;

namespace FraudEvaluation.Application.Commands
{
    public record SubmitFraudEvaluationCommand(string IdempotencyKey, string Ip, string TaxId, decimal Amount, string Currency) : IRequest<SubmitFraudEvaluationResult>;
}
