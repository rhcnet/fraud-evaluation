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
            var sql = "SELECT * FROM transactions WHERE idempotency_key = @Key LIMIT 1";
            return await conn.QueryFirstOrDefaultAsync<TransactionEntity>(sql, new { Key = idempotencyKey });
        }

        public async Task<TransactionEntity?> GetByIdAsync(Guid id)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            var sql = "SELECT * FROM transactions WHERE id = @Id LIMIT 1";
            return await conn.QueryFirstOrDefaultAsync<TransactionEntity>(sql, new { Id = id });
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

            await conn.ExecuteAsync(sql, entity);
        }
    }
}
