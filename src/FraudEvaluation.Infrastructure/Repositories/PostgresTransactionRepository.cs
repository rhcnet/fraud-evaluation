using System;
using System.Threading.Tasks;
using Dapper;
using Npgsql;
using FraudEvaluation.Domain.Entities;
using FraudEvaluation.Domain.Repositories;

namespace FraudEvaluation.Infrastructure.Repositories
{
    public class PostgresTransactionRepository : ITransactionRepository
    {
        private readonly string _connectionString;

        public PostgresTransactionRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<TransactionEntity?> GetByIdempotencyKeyAsync(string idempotencyKey)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            var sql = "SELECT id, idempotency_key, ip, tax_id, amount, currency, created_at, updated_at, validation_status, transaction_status FROM transactions WHERE idempotency_key = @Key LIMIT 1";
            var row = await conn.QueryFirstOrDefaultAsync(sql, new { Key = idempotencyKey });
            if (row == null) return null;

            string? vsStr = row.validation_status;
            ValidationStatus? vs = null;
            if (!string.IsNullOrEmpty(vsStr) && Enum.TryParse<ValidationStatus>(vsStr, true, out var parsedVs))
            {
                vs = parsedVs;
            }

            string? tsStr = row.transaction_status;
            FraudEvaluation.Domain.Entities.TransactionStatus ts = FraudEvaluation.Domain.Entities.TransactionStatus.Processing;
            if (!string.IsNullOrEmpty(tsStr) && Enum.TryParse<FraudEvaluation.Domain.Entities.TransactionStatus>(tsStr, true, out var parsedTs))
            {
                ts = parsedTs;
            }

            // Map currency string to Currency value object
            FraudEvaluation.Domain.ValueObjects.Currency currencyVo;
            try
            {
                currencyVo = FraudEvaluation.Domain.ValueObjects.Currency.Create((string)row.currency);
            }
            catch
            {
                currencyVo = FraudEvaluation.Domain.ValueObjects.Currency.Create("BRL");
            }

            return new TransactionEntity
            {
                Id = (Guid)row.id,
                IdempotencyKey = (string)row.idempotency_key,
                Ip = (string)row.ip,
                TaxId = (string)row.tax_id,
                Amount = (decimal)row.amount,
                Currency = currencyVo,
                CreatedAt = (DateTime)row.created_at,
                UpdatedAt = (DateTime)row.updated_at,
                ValidationStatus = vs,
                TransactionStatus = ts
            };
        }

        public async Task<TransactionEntity?> GetByIdAsync(Guid id)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            var sql = "SELECT id, idempotency_key, ip, tax_id, amount, currency, created_at, updated_at, validation_status, transaction_status FROM transactions WHERE id = @Id LIMIT 1";
            var row = await conn.QueryFirstOrDefaultAsync(sql, new { Id = id });
            if (row == null) return null;

            string? vsStr = row.validation_status;
            ValidationStatus? vs = null;
            if (!string.IsNullOrEmpty(vsStr) && Enum.TryParse<ValidationStatus>(vsStr, true, out var parsedVs))
            {
                vs = parsedVs;
            }

            string? tsStr = row.transaction_status;
            FraudEvaluation.Domain.Entities.TransactionStatus ts = FraudEvaluation.Domain.Entities.TransactionStatus.Processing;
            if (!string.IsNullOrEmpty(tsStr) && Enum.TryParse<FraudEvaluation.Domain.Entities.TransactionStatus>(tsStr, true, out var parsedTs))
            {
                ts = parsedTs;
            }

            // Map currency string to Currency value object
            FraudEvaluation.Domain.ValueObjects.Currency currencyVo;
            try
            {
                currencyVo = FraudEvaluation.Domain.ValueObjects.Currency.Create((string)row.currency);
            }
            catch
            {
                currencyVo = FraudEvaluation.Domain.ValueObjects.Currency.Create("BRL");
            }

            return new TransactionEntity
            {
                Id = (Guid)row.id,
                IdempotencyKey = (string)row.idempotency_key,
                Ip = (string)row.ip,
                TaxId = (string)row.tax_id,
                Amount = (decimal)row.amount,
                Currency = currencyVo,
                CreatedAt = (DateTime)row.created_at,
                UpdatedAt = (DateTime)row.updated_at,
                ValidationStatus = vs,
                TransactionStatus = ts
            };
        }

        public async Task SaveAsync(TransactionEntity entity)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            // Ensure table exists (simple migration)
            var create = @"CREATE TABLE IF NOT EXISTS transactions (
    id uuid PRIMARY KEY,
    idempotency_key varchar(200) UNIQUE,
    ip varchar(100),
    tax_id varchar(100),
    amount numeric,
    currency varchar(10),
    created_at timestamp,
    updated_at timestamp,
    validation_status varchar(100),
    transaction_status varchar(100)
);";
            await conn.ExecuteAsync(create);

            var sql = @"INSERT INTO transactions (id, idempotency_key, ip, tax_id, amount, currency, created_at, updated_at, validation_status, transaction_status)
VALUES (@Id, @IdempotencyKey, @Ip, @TaxId, @Amount, @Currency, @CreatedAt, @UpdatedAt, @ValidationStatus, @TransactionStatus)";

            var param = new
            {
                Id = entity.Id,
                IdempotencyKey = entity.IdempotencyKey,
                Ip = entity.Ip,
                TaxId = entity.TaxId,
                Amount = entity.Amount,
                Currency = entity.Currency.Code,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt,
                ValidationStatus = entity.ValidationStatus?.ToString(),
                TransactionStatus = entity.TransactionStatus.ToString()
            };

            await conn.ExecuteAsync(sql, param);
        }
    }
}
