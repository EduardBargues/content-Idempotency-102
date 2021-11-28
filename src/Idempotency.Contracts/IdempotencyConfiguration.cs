using System;
using System.Collections.Generic;
using Amazon.Lambda.APIGatewayEvents;

namespace Idempotency.Contracts
{
    public class IdempotencyConfiguration
    {
        public string IdempotencyKeyHeaderName { get; set; }
        public string TimeToLiveHeaderName { get; set; }
        public string MarkerHeaderName { get; set; }
        public string GetOwnershipElapsedTimeHeaderName { get; set; }
        public string CacheResponseElapsedTimeHeaderName { get; set; }
        public TimeSpan DefaultTimeToLive { get; set; }
        public TimeSpan TimeToLiveMaster { get; set; }
        public HashSet<int> StatusCodesToCacheResponseFrom { get; set; }

        public static string ResponseFromCache = "response-from-cache";
        public static string ResponseFromImplementation = "response-from-implementation";

        public IdempotencyConfiguration(string idemKeyHeaderName,
                                        string ttlHeaderName,
                                        string markerHeaderName,
                                        string getOwnershipElapsedTimeHeaderName,
                                        string cacheResponseElapsedTimeHeaderName,
                                        TimeSpan ttl,
                                        TimeSpan ttlMaster,
                                        IEnumerable<int> statusCodesToCache)
        {
            IdempotencyKeyHeaderName = idemKeyHeaderName;
            TimeToLiveHeaderName = ttlHeaderName;
            MarkerHeaderName = markerHeaderName;
            GetOwnershipElapsedTimeHeaderName = getOwnershipElapsedTimeHeaderName;
            CacheResponseElapsedTimeHeaderName = cacheResponseElapsedTimeHeaderName;
            DefaultTimeToLive = ttl;
            TimeToLiveMaster = ttlMaster;
            StatusCodesToCacheResponseFrom = new HashSet<int>(statusCodesToCache);
        }

        public static IdempotencyConfiguration Default => new IdempotencyConfiguration(
            "x-idem-key",
            "x-idem-ttl",
            "x-idem-marker",
            "x-idem-diagnostics-get-ownership",
            "x-idem-diagnostics-cache-response",
            TimeSpan.FromSeconds(60),
            TimeSpan.FromSeconds(15),
            new[] { 201 }
            );
    }
}