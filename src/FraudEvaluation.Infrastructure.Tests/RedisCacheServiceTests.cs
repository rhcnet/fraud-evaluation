using System;
using System.Threading.Tasks;
using Moq;
using StackExchange.Redis;
using Xunit;
using FraudEvaluation.Infrastructure.Cache;

namespace FraudEvaluation.Infrastructure.Tests
{
    public class RedisCacheServiceTests
    {
        [Fact]
        public async Task GetAsync_ReturnsValue_WhenKeyExists()
        {
            // Arrange
            var key = "test-key";
            var expected = "cached-value";

            var mockDb = new Mock<IDatabase>();
            mockDb.Setup(d => d.StringGetAsync(
                    It.Is<RedisKey>(k => k == key),
                    It.IsAny<CommandFlags>()))
                .ReturnsAsync((RedisValue)expected);

            var mockConn = new Mock<IConnectionMultiplexer>();
            mockConn.Setup(c => c.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                .Returns(mockDb.Object);

            var service = new RedisCacheService(mockConn.Object);

            // Act
            var actual = await service.GetAsync(key);

            // Assert
            Assert.Equal(expected, actual);
        }
    }
}
