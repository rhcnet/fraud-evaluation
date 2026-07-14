using System.Threading;
using System.Threading.Tasks;
using MediatR;
using FraudEvaluation.Application.Queries;
using FraudEvaluation.Domain.Repositories;

namespace FraudEvaluation.Application.Handlers
{
    public class GetTransactionStatusHandler : IRequestHandler<GetTransactionStatusQuery, GetTransactionStatusResult?>
    {
        private readonly ITransactionRepository _repo;

        public GetTransactionStatusHandler(ITransactionRepository repo)
        {
            _repo = repo;
        }

        public async Task<GetTransactionStatusResult?> Handle(GetTransactionStatusQuery request, CancellationToken cancellationToken)
        {
            var entity = await _repo.GetByIdAsync(request.TransactionId);
            if (entity == null)
            {
                return null;
            }

            return new GetTransactionStatusResult(entity.Id, entity.ValidationStatus, entity.TransactionStatus);
        }
    }
}
