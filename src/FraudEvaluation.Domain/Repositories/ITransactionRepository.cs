using System;
using System.Threading.Tasks;
using FraudEvaluation.Domain.Entities;

namespace FraudEvaluation.Domain.Repositories
{
    public interface ITransactionRepository
    {
        Task<TransactionEntity?> GetByIdempotencyKeyAsync(string idempotencyKey);
        Task<TransactionEntity?> GetByIdAsync(Guid id);
        Task SaveAsync(TransactionEntity entity);
        Task UpdateAsync(TransactionEntity entity);
    }
}
