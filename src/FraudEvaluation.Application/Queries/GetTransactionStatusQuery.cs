using System;
using System;
using MediatR;
using FraudEvaluation.Application.Common;

namespace FraudEvaluation.Application.Queries
{
    // Accept raw string id so handler can validate and return Result with ErrorCode
    public record GetTransactionStatusQuery(string TransactionId) : IRequest<Result<GetTransactionStatusResult>>;
}
