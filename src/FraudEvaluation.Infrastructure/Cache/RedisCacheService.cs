using System;
using System.Threading.Tasks;
using StackExchange.Redis;
using FraudEvaluation.Application.Interfaces;

namespace FraudEvaluation.Infrastructure.Cache
{
    public class RedisCacheService : ICacheService
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly IDatabase _db;

        public RedisCacheService(string configuration)
        {
            _redis = ConnectionMultiplexer.Connect(configuration);
            _db = _redis.GetDatabase();
        }

        // Internal constructor for unit testing to inject mocks
        public RedisCacheService(IConnectionMultiplexer redis)
        {
            _redis = redis ?? throw new ArgumentNullException(nameof(redis));
            _db = _redis.GetDatabase();
        }

        public async Task<string?> GetAsync(string key)
        {
            var value = await _db.StringGetAsync(key).ConfigureAwait(false);
            return value.HasValue ? value.ToString() : null;
        }

        public async Task SetAsync(string key, string value, TimeSpan? expiry = null)
        {
            await _db.StringSetAsync(key, value).ConfigureAwait(false);
            if (expiry.HasValue)
            {
                await _db.KeyExpireAsync(key, expiry.Value).ConfigureAwait(false);
            }
        }
    }
}
