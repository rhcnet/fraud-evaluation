using System;
using FraudEvaluation.Domain.Entities;

namespace FraudEvaluation.Application.Queries
{
    public record GetTransactionStatusResult(Guid TransactionId, ValidationStatus? ValidationStatus, FraudEvaluation.Domain.Entities.TransactionStatus TransactionStatus);
}
