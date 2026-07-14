using System;
using MediatR;

namespace FraudEvaluation.Application.Queries
{
    public record GetTransactionStatusQuery(Guid TransactionId) : IRequest<GetTransactionStatusResult?>;
}
