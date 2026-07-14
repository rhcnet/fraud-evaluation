using FraudEvaluation.Application.Commands;
using FraudEvaluation.Application.Interfaces;
using FraudEvaluation.Domain.Entities;
using FraudEvaluation.Domain.Repositories;
using MediatR;
using System.Text.Json;

namespace FraudEvaluation.Application.Handlers
{
    public class SubmitFraudEvaluationHandler(ICacheService cache, ITransactionRepository repo, IMessagePublisher publisher) : IRequestHandler<SubmitFraudEvaluationCommand, SubmitFraudEvaluationResult>
    {
        private readonly ICacheService _cache = cache;
        private readonly ITransactionRepository _repo = repo;
        private readonly IMessagePublisher _publisher = publisher;
        private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);

        public async Task<SubmitFraudEvaluationResult> Handle(SubmitFraudEvaluationCommand request, CancellationToken cancellationToken)
        {
            // Validate business parameters (moved from endpoint)
            if (string.IsNullOrWhiteSpace(request.TaxId) || !request.TaxId.All(char.IsDigit))
            {
                throw new FraudEvaluation.Application.Common.ValidationException("TaxId must contain only digits.");
            }

            if (request.Amount <= 0)
            {
                throw new FraudEvaluation.Application.Common.ValidationException("Amount must be greater than zero.");
            }

            // Validate currency using domain ValueObject
            FraudEvaluation.Domain.ValueObjects.Currency currencyVo;
            try
            {
                currencyVo = FraudEvaluation.Domain.ValueObjects.Currency.Create(request.Currency);
            }
            catch (FraudEvaluation.Domain.DomainException dex)
            {
                throw new FraudEvaluation.Application.Common.ValidationException(dex.Message);
            }

            // Validate idempotency key format
            if (!Guid.TryParse(request.IdempotencyKey, out _))
            {
                throw new FraudEvaluation.Application.Common.ValidationException("Idempotency-Key must be a valid GUID.");
            }

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

            return new SubmitFraudEvaluationResult(entity.Id, false);
        }
    }
}
