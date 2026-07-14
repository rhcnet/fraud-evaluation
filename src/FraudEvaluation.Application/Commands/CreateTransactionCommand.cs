using MediatR;

namespace FraudEvaluation.Application.Commands
{
    public record CreateTransactionCommand(string TaxId, decimal Amount) : IRequest<CreateTransactionResult>;
}
