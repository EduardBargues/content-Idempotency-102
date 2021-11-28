
using Microsoft.Extensions.DependencyInjection;
using Idempotency.Contracts;
using System;
using System.Collections.Generic;

namespace Idempotency.WebApi
{
    public static class IServiceCollectionExtensions
    {
        public static IServiceCollection AddIdempotency(this IServiceCollection services,
                                                        string idempotencyKeyHeaderName,
                                                        long defaultTimeToLiveSeconds,
                                                        long masterTimeToLiveSeconds,
                                                        string timeToLiveHeaderName,
                                                        string markerHeaderName,
                                                        string getOwnershipElapsedTimeHeaderName,
                                                        string cacheResponseElapsedTimeHeaderName,
                                                        params int[] statusCodesToCache
                                                        )
        {
            return services.Configure<IdempotencyConfiguration>(conf =>
            {
                conf.IdempotencyKeyHeaderName = idempotencyKeyHeaderName;
                conf.DefaultTimeToLive = TimeSpan.FromSeconds(defaultTimeToLiveSeconds);
                conf.TimeToLiveHeaderName = timeToLiveHeaderName;
                conf.StatusCodesToCacheResponseFrom = new HashSet<int>(statusCodesToCache);
                conf.MarkerHeaderName = markerHeaderName;
                conf.GetOwnershipElapsedTimeHeaderName = getOwnershipElapsedTimeHeaderName;
                conf.CacheResponseElapsedTimeHeaderName = cacheResponseElapsedTimeHeaderName;
                conf.TimeToLiveMaster = TimeSpan.FromSeconds(masterTimeToLiveSeconds);
            });
        }
    }
}