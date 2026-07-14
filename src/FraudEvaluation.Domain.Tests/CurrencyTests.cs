using Xunit;
using FraudEvaluation.Domain.ValueObjects;

namespace FraudEvaluation.Domain.Tests
{
    public class CurrencyTests
    {
        [Theory]
        [InlineData("BRL")]
        [InlineData("brl")]
        [InlineData("USD")]
        [InlineData("EUR")]
        public void TryCreate_ValidCodes_ReturnsTrue(string code)
        {
            var ok = Currency.TryCreate(code, out var currency);
            Assert.True(ok);
            Assert.NotNull(currency);
            Assert.Equal(code.ToUpperInvariant(), currency!.Code);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("ABC")]
        public void TryCreate_InvalidCodes_ReturnsFalse(string code)
        {
            var ok = Currency.TryCreate(code, out var currency);
            Assert.False(ok);
            Assert.Null(currency);
        }
    }
}
