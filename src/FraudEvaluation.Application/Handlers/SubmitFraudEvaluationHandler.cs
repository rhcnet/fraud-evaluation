using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using FraudEvaluation.Application.Commands;
using FraudEvaluation.Application.Interfaces;
using FraudEvaluation.Domain.Entities;
using FraudEvaluation.Domain.Repositories;

namespace FraudEvaluation.Application.Handlers
{
    public class SubmitFraudEvaluationHandler : IRequestHandler<SubmitFraudEvaluationCommand, SubmitFraudEvaluationResult>
    {
        private readonly ICacheService _cache;
        private readonly ITransactionRepository _repo;
        private readonly IMessagePublisher _publisher;
        private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);

        public SubmitFraudEvaluationHandler(ICacheService cache, ITransactionRepository repo, IMessagePublisher publisher)
        {
            _cache = cache;
            _repo = repo;
            _publisher = publisher;
        }

        public async Task<SubmitFraudEvaluationResult> Handle(SubmitFraudEvaluationCommand request, CancellationToken cancellationToken)
        {
            // Check cache for idempotency key
            var cached = await _cache.GetAsync(request.IdempotencyKey);
            if (!string.IsNullOrEmpty(cached))
            {
                if (Guid.TryParse(cached, out var existingId))
                {
                    return new SubmitFraudEvaluationResult(existingId, true);
                }
            }

            // Create transaction entity
            var entity = new TransactionEntity
            {
                Id = Guid.NewGuid(),
                IdempotencyKey = request.IdempotencyKey,
                Ip = request.Ip,
                TaxId = request.TaxId,
                Amount = request.Amount,
                Currency = request.Currency,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                ValidationStatus = null,
                TransactionStatus = FraudEvaluation.Domain.Entities.TransactionStatus.Processing
            };

            // Save to DB
            await _repo.SaveAsync(entity);

            // Set cache
            await _cache.SetAsync(request.IdempotencyKey, entity.Id.ToString(), CacheTtl);

            // Publish event
            var payload = JsonSerializer.Serialize(new { transactionId = entity.Id, entity.TaxId, entity.Amount, entity.Currency });
            await _publisher.PublishAsync("fraud.evaluation.request", payload);

            return new SubmitFraudEvaluationResult(entity.Id, false);
        }
    }
}
