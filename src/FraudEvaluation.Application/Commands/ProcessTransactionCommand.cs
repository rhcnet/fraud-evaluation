using MediatR;
using FraudEvaluation.Domain.ValueObjects;
using FraudEvaluation.Application.Common;

namespace FraudEvaluation.Application.Commands;

// Command sent when a transaction from the message queue should be processed
public record ProcessTransactionCommand(Guid TransacationId, string TaxId, decimal Amount, Currency Currency) : IRequest<Result<string>>;
