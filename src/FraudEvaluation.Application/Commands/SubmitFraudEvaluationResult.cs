using System;

namespace FraudEvaluation.Application.Commands
{
    public record SubmitFraudEvaluationResult(Guid TransactionId, bool AlreadyExists);
}
