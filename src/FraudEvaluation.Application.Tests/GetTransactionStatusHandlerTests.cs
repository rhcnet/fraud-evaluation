using System;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Xunit;
using FraudEvaluation.Application.Handlers;
using FraudEvaluation.Application.Queries;
using FraudEvaluation.Application.Common;
using FraudEvaluation.Domain.Repositories;
using FraudEvaluation.Domain.Entities;

namespace FraudEvaluation.Application.Tests
{
    public class GetTransactionStatusHandlerTests
    {
        [Fact]
        public async Task Handle_InvalidGuid_ReturnsInvalidId()
        {
            var repoMock = new Mock<ITransactionRepository>();
            var handler = new GetTransactionStatusHandler(repoMock.Object);

            var result = await handler.Handle(new GetTransactionStatusQuery("not-a-guid"), CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorCode.InvalidId, result.Code);
        }

        [Fact]
        public async Task Handle_TransactionNotFound_ReturnsNotFound()
        {
            var repoMock = new Mock<ITransactionRepository>();
            repoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((TransactionEntity?)null);

            var handler = new GetTransactionStatusHandler(repoMock.Object);
            var id = Guid.NewGuid().ToString();

            var result = await handler.Handle(new GetTransactionStatusQuery(id), CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorCode.NotFound, result.Code);
        }

        [Fact]
        public async Task Handle_TransactionFound_ReturnsStatuses()
        {
            var guid = Guid.NewGuid();
            var entity = new TransactionEntity
            {
                Id = guid,
                IdempotencyKey = "key",
                Ip = "127.0.0.1",
                TaxId = "123",
                Amount = 10m,
                Currency = FraudEvaluation.Domain.ValueObjects.Currency.Create("BRL"),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                ValidationStatus = ValidationStatus.Approved,
                TransactionStatus = TransactionStatus.Processed
            };

            var repoMock = new Mock<ITransactionRepository>();
            repoMock.Setup(r => r.GetByIdAsync(guid)).ReturnsAsync(entity);

            var handler = new GetTransactionStatusHandler(repoMock.Object);

            var result = await handler.Handle(new GetTransactionStatusQuery(guid.ToString()), CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Value);
            Assert.Equal(guid, result.Value!.TransactionId);
            Assert.Equal(ValidationStatus.Approved, result.Value.ValidationStatus);
            Assert.Equal(TransactionStatus.Processed, result.Value.TransactionStatus);
        }
    }
}
