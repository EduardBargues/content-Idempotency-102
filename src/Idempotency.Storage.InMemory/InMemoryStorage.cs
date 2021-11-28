using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Idempotency.Contracts;
using Nito.AsyncEx;

namespace Idempotency.Storage.InMemory
{
    public class InMemoryStorage : IIdempotentStorage
    {
        private readonly Dictionary<string, InMemoryEntity> _db;
        private readonly AsyncLock _mutex;

        internal InMemoryStorage()
        {
            _db = new Dictionary<string, InMemoryEntity>();
            _mutex = new AsyncLock();
        }
        public static IIdempotentStorage CreateInstance() => new InMemoryStorage();

        public async Task<IdempotentResponse> GetKeyOwnership(string idemKey, string ownerId, TimeSpan timeToLive)
        {
            using (await _mutex.LockAsync())
            {
                if (_db.ContainsKey(idemKey))
                    return ToResponse(_db[idemKey]);
                _db.Add(idemKey, new InMemoryEntity(idemKey, ownerId));
                SetMasterTimeToLive(idemKey, timeToLive);
                return ToResponse(_db[idemKey]);
            }
        }
        public async Task CacheResponse(string idemKey, int statusCode, string body, TimeSpan timeToLive)
        {
            using (await _mutex.LockAsync())
            {
                var entity = _db[idemKey];
                entity.StatusCode = statusCode;
                entity.Body = body;
                SetDeprecationTimeToLive(idemKey, timeToLive);
            }
        }

        private void SetDeprecationTimeToLive(string idemKey, TimeSpan ttl)
        {
            Task.Run(async () =>
            {
                await Task.Delay(ttl);
                using (await _mutex.LockAsync())
                    if (_db.ContainsKey(idemKey))
                        _db.Remove(idemKey);
            });
        }
        private void SetMasterTimeToLive(string idemKey, TimeSpan ttl)
        {
            Task.Run(async () =>
            {
                await Task.Delay(ttl);
                using (await _mutex.LockAsync())
                    _db.Remove(idemKey);
            });
        }
        private IdempotentResponse ToResponse(InMemoryEntity entity)
            => new IdempotentResponse(entity.IdempotencyKey, entity.OwnerId, entity.StatusCode, entity.Body);

        private class InMemoryEntity
        {
            public string IdempotencyKey { get; }
            public string OwnerId { get; }
            public int StatusCode { get; set; }
            public string Body { get; set; }

            public InMemoryEntity(string idemKey, string ownerId)
            {
                IdempotencyKey = idemKey;
                OwnerId = ownerId;
            }
        }
    }
}