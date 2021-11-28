
using System;
using System.Threading.Tasks;

namespace Idempotency.Contracts
{
    public interface IIdempotentStorage
    {
        Task<IdempotentResponse> GetKeyOwnership(string idemKey, string ownerId, TimeSpan timeToLive);
        Task CacheResponse(string idemKey, int statusCode, string body, TimeSpan timeToLive);
    }
}