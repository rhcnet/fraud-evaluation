using System;
using Xunit;
using FraudEvaluation.Application.Validators;
using FraudEvaluation.Application.Commands;
using FraudEvaluation.Application.Common;

namespace FraudEvaluation.Application.Tests
{
    public class SubmitFraudEvaluationValidatorTests
    {
        [Fact]
        public void Validate_NullCommand_ReturnsValidationFailed()
        {
            var result = SubmitFraudEvaluationValidator.Validate(null);
            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorCode.ValidationFailed, result.Code);
        }

        [Fact]
        public void Validate_InvalidTaxId_ReturnsValidationFailed()
        {
            var cmd = new SubmitFraudEvaluationCommand(Guid.NewGuid().ToString(), "127.0.0.1", "abc123", 10m, "BRL");
            var result = SubmitFraudEvaluationValidator.Validate(cmd);
            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorCode.ValidationFailed, result.Code);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Validate_InvalidAmount_ReturnsValidationFailed(decimal amount)
        {
            var cmd = new SubmitFraudEvaluationCommand(Guid.NewGuid().ToString(), "127.0.0.1", "123456", amount, "BRL");
            var result = SubmitFraudEvaluationValidator.Validate(cmd);
            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorCode.ValidationFailed, result.Code);
        }

        [Fact]
        public void Validate_InvalidIdempotencyKey_ReturnsInvalidId()
        {
            var cmd = new SubmitFraudEvaluationCommand("not-a-guid", "127.0.0.1", "123456", 10m, "BRL");
            var result = SubmitFraudEvaluationValidator.Validate(cmd);
            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorCode.InvalidId, result.Code);
        }

        [Fact]
        public void Validate_InvalidCurrency_ReturnsValidationFailed()
        {
            var cmd = new SubmitFraudEvaluationCommand(Guid.NewGuid().ToString(), "127.0.0.1", "123456", 10m, "ABC");
            var result = SubmitFraudEvaluationValidator.Validate(cmd);
            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorCode.ValidationFailed, result.Code);
        }

        [Fact]
        public void Validate_ValidCommand_ReturnsCurrency()
        {
            var idempotency = Guid.NewGuid().ToString();
            var cmd = new SubmitFraudEvaluationCommand(idempotency, "127.0.0.1", "123456", 10m, "brl");
            var result = SubmitFraudEvaluationValidator.Validate(cmd);
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Value);
            Assert.Equal("BRL", result.Value!.Code);
        }
    }
}
