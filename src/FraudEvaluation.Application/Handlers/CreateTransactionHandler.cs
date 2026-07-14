using FraudEvaluation.Application.Commands;
using MediatR;

namespace FraudEvaluation.Application.Handlers
{
    public class CreateTransactionHandler : IRequestHandler<CreateTransactionCommand, CreateTransactionResult>
    {
        public Task<CreateTransactionResult> Handle(CreateTransactionCommand request, CancellationToken cancellationToken)
        {
            // In a real app, persist to database or call other services here.
            var id = System.Guid.NewGuid();
            var result = new CreateTransactionResult(id, request.TaxId, request.Amount);
            return Task.FromResult(result);
        }
    }
}
