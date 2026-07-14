using FraudEvaluation.Application.Commands;
using FraudEvaluation.Application.Interfaces;
using FraudEvaluation.Domain.Entities;
using FraudEvaluation.Domain.Repositories;
using MediatR;
using System.Text.Json;

namespace FraudEvaluation.Application.Handlers
{
    public class SubmitFraudEvaluationHandler(ICacheService cache, ITransactionRepository repo, IMessagePublisher publisher) : IRequestHandler<SubmitFraudEvaluationCommand, FraudEvaluation.Application.Common.Result<SubmitFraudEvaluationResult>>
    {
        private readonly ICacheService _cache = cache;
        private readonly ITransactionRepository _repo = repo;
        private readonly IMessagePublisher _publisher = publisher;
        private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);

        public async Task<FraudEvaluation.Application.Common.Result<SubmitFraudEvaluationResult>> Handle(SubmitFraudEvaluationCommand request, CancellationToken cancellationToken)
        {
            // Validate business parameters (moved from endpoint)
            if (string.IsNullOrWhiteSpace(request.TaxId) || !request.TaxId.All(char.IsDigit))
            {
                return FraudEvaluation.Application.Common.Result.Fail<SubmitFraudEvaluationResult>("TaxId must contain only digits.", FraudEvaluation.Application.Common.ErrorCode.ValidationFailed);
            }

            if (request.Amount <= 0)
            {
                return FraudEvaluation.Application.Common.Result.Fail<SubmitFraudEvaluationResult>("Amount must be greater than zero.", FraudEvaluation.Application.Common.ErrorCode.ValidationFailed);
            }

            // Validate currency using domain ValueObject
            FraudEvaluation.Domain.ValueObjects.Currency currencyVo;
            try
            {
                currencyVo = FraudEvaluation.Domain.ValueObjects.Currency.Create(request.Currency);
            }
            catch (FraudEvaluation.Domain.DomainException dex)
            {
                return FraudEvaluation.Application.Common.Result.Fail<SubmitFraudEvaluationResult>(dex.Message, FraudEvaluation.Application.Common.ErrorCode.ValidationFailed);
            }

            // Validate idempotency key format
            if (!Guid.TryParse(request.IdempotencyKey, out _))
            {
                return FraudEvaluation.Application.Common.Result.Fail<SubmitFraudEvaluationResult>("Idempotency-Key must be a valid GUID.", FraudEvaluation.Application.Common.ErrorCode.InvalidId);
            }

            // Check cache for idempotency key
            var cached = await _cache.GetAsync(request.IdempotencyKey);
            if (!string.IsNullOrEmpty(cached))
            {
                if (Guid.TryParse(cached, out var existingId))
                {
                    return FraudEvaluation.Application.Common.Result.Ok(new SubmitFraudEvaluationResult(existingId, true));
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

            return FraudEvaluation.Application.Common.Result.Ok(new SubmitFraudEvaluationResult(entity.Id, false));
        }
    }
}
