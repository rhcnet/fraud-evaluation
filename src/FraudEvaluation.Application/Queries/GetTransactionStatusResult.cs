using System;

namespace FraudEvaluation.Application.Queries
{
    public record GetTransactionStatusResult(Guid TransactionId, string? ValidationStatus, string TransactionStatus);
}
