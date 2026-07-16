using FraudEvaluation.Application.Commands;
using FraudEvaluation.Application.Common;
using FraudEvaluation.Application.Interfaces;
using FraudEvaluation.Domain.Entities;
using FraudEvaluation.Domain.Repositories;
using MediatR;
using System.Text.Json;

namespace FraudEvaluation.Application.Handlers
{
    public class SubmitFraudEvaluationHandler(ICacheService cache, ITransactionRepository repo, IMessagePublisher publisher) : IRequestHandler<SubmitFraudEvaluationCommand, Result<SubmitFraudEvaluationResult>>
    {
        private readonly ICacheService _cache = cache;
        private readonly ITransactionRepository _repo = repo;
        private readonly IMessagePublisher _publisher = publisher;
        private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);

        public async Task<Result<SubmitFraudEvaluationResult>> Handle(SubmitFraudEvaluationCommand request, CancellationToken cancellationToken)
        {
            // Validate request using a centralized validator
            var validation = Validators.SubmitFraudEvaluationValidator.Validate(request);
            if (!validation.IsSuccess)
            {
                return Result.Fail<SubmitFraudEvaluationResult>(validation.Error ?? "Validation failed", validation.Code);
            }

            var currencyVo = validation.Value!;

            // Check cache for idempotency key
            var cached = await _cache.GetAsync(request.IdempotencyKey);
            if (!string.IsNullOrEmpty(cached))
            {
                if (Guid.TryParse(cached, out var existingId))
                {
                    return Result.Ok(new SubmitFraudEvaluationResult(existingId, true));
                }
            }
            // If not in cache, check repository for an existing transaction with the same idempotency key
            var existingTxn = await _repo.GetByIdempotencyKeyAsync(request.IdempotencyKey);
            if (existingTxn != null)
            {
                // populate cache to speed up future checks
                await _cache.SetAsync(request.IdempotencyKey, existingTxn.Id.ToString(), CacheTtl);
                return Result.Ok(new SubmitFraudEvaluationResult(existingTxn.Id, true));
            }

            // Create transaction entity
            var entity = new TransactionEntity
            {
                Id = Guid.NewGuid(),
                IdempotencyKey = request.IdempotencyKey,
                Ip = request.Ip,
                TaxId = request.TaxId,
                Amount = request.Amount,
                Currency = currencyVo,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                ValidationStatus = null,
                TransactionStatus = TransactionStatus.Processing
            };

            // Save to DB
            await _repo.SaveAsync(entity);

            // Set cache
            await _cache.SetAsync(request.IdempotencyKey, entity.Id.ToString(), CacheTtl);

            // Publish event
            var payload = JsonSerializer.Serialize(new { transactionId = entity.Id, entity.TaxId, entity.Amount, currency = entity.Currency.Code });
            await _publisher.PublishAsync("fraud.evaluation.request", payload);

            return Result.Ok(new SubmitFraudEvaluationResult(entity.Id, false));
        }
    }
}
