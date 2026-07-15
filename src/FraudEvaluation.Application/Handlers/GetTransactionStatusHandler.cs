using FraudEvaluation.Application.Common;
using FraudEvaluation.Application.Queries;
using FraudEvaluation.Domain.Repositories;
using MediatR;

namespace FraudEvaluation.Application.Handlers
{
    public class GetTransactionStatusHandler : IRequestHandler<GetTransactionStatusQuery, Result<GetTransactionStatusResult>>
    {
        private readonly ITransactionRepository _repo;

        public GetTransactionStatusHandler(ITransactionRepository repo) => _repo = repo;

        public async Task<Result<GetTransactionStatusResult>> Handle(GetTransactionStatusQuery request, CancellationToken cancellationToken)
        {
            if (!Guid.TryParse(request.TransactionId, out var guid))
            {
                return Result.Fail<GetTransactionStatusResult>("Invalid transaction id format.", ErrorCode.InvalidId);
            }

            var entity = await _repo.GetByIdAsync(guid);
            if (entity == null)
            {
                return Result.NotFound<GetTransactionStatusResult>("Transaction not found");
            }

            var result = new GetTransactionStatusResult(entity.Id, entity.ValidationStatus, entity.TransactionStatus);
            return Result.Ok(result);
        }
    }
}
