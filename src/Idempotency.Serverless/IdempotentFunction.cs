
using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Idempotency.Contracts;

namespace Idempotency.Serverless
{
    public abstract class IdempotentFunction
    {
        public async Task<APIGatewayProxyResponse> IdempotentHandler(APIGatewayProxyRequest request,
                                                                     ILambdaContext context,
                                                                     IFunction function,
                                                                     IIdempotentStorage db,
                                                                     IdempotencyConfiguration conf)
        {
            var hasIdemKey = request.Headers != null
                && request.Headers.ContainsKey(conf.IdempotencyKeyHeaderName);
            if (!hasIdemKey)
                return GetBadRequestResponse(conf.IdempotencyKeyHeaderName);

            var idemKey = request.Headers[conf.IdempotencyKeyHeaderName];
            var ownerId = request.RequestContext.RequestId;
            (var response, var ownershipTime) = await GetKeyOwnership(db, idemKey, ownerId, conf.TimeToLiveMaster);

            if (response.Finished)
                return GetCacheResponse(idemKey, response, conf, ownershipTime);
            if (response.OwnerId != ownerId)
                return GetConflictResponse(idemKey, conf, ownershipTime);

            var functionResponse = await function.FunctionHandler(request, context);

            TimeSpan cachingTime = TimeSpan.Zero;
            if (conf.StatusCodesToCacheResponseFrom.Contains(functionResponse.StatusCode))
            {
                var ttl = request.Headers.ContainsKey(conf.TimeToLiveHeaderName)
                ? TimeSpan.FromSeconds(int.Parse(request.Headers[conf.TimeToLiveHeaderName]))
                : conf.DefaultTimeToLive;
                cachingTime = await CacheResponse(db, idemKey, functionResponse, ttl);
            }

            AddResponseHeaders(functionResponse, idemKey, conf, ownershipTime, cachingTime);
            return functionResponse;
        }

        private async Task<TimeSpan> CacheResponse(IIdempotentStorage db, string idemKey, APIGatewayProxyResponse functionResponse, TimeSpan ttl)
        {
            Stopwatch watch = new Stopwatch();
            watch.Start();
            await db.CacheResponse(idemKey, functionResponse.StatusCode, functionResponse.Body, ttl);
            watch.Stop();

            return watch.Elapsed;
        }

        private async Task<(IdempotentResponse, TimeSpan)> GetKeyOwnership(IIdempotentStorage db, string idemKey, string ownerId, TimeSpan timeToLiveMaster)
        {
            Stopwatch watch = new Stopwatch();
            watch.Start();
            var response = await db.GetKeyOwnership(idemKey, ownerId, timeToLiveMaster);
            watch.Stop();

            return (response, watch.Elapsed);
        }

        private void AddResponseHeaders(APIGatewayProxyResponse functionResponse,
                                                           string idemKey,
                                                           IdempotencyConfiguration conf,
                                                           TimeSpan getOwnershipElapsedTime,
                                                           TimeSpan cacheElapsedTime)
        {
            if (functionResponse.Headers == null)
                functionResponse.Headers = new Dictionary<string, string>();
            functionResponse.Headers.Add(conf.MarkerHeaderName, IdempotencyConfiguration.ResponseFromImplementation);
            functionResponse.Headers.Add(conf.IdempotencyKeyHeaderName, idemKey);
            functionResponse.Headers.Add(conf.GetOwnershipElapsedTimeHeaderName, $"{getOwnershipElapsedTime.TotalMilliseconds}");
            if (cacheElapsedTime != TimeSpan.Zero)
                functionResponse.Headers.Add(conf.CacheResponseElapsedTimeHeaderName, $"{cacheElapsedTime.TotalMilliseconds}");
        }

        private APIGatewayProxyResponse GetConflictResponse(string idemKey, IdempotencyConfiguration conf, TimeSpan getOwnershipElapsedTime)
        {
            return new APIGatewayProxyResponse()
            {
                StatusCode = 409,
                Headers = new Dictionary<string, string>() {
                    { conf.IdempotencyKeyHeaderName, idemKey },
                    { conf.GetOwnershipElapsedTimeHeaderName, $"{getOwnershipElapsedTime.TotalMilliseconds}" },
                    },
                Body = JsonConvert.SerializeObject(new { message = $"Request with idempotency-key: {idemKey} is in progress" }),
            };
        }
        private APIGatewayProxyResponse GetCacheResponse(string idemKey, IdempotentResponse response, IdempotencyConfiguration conf, TimeSpan getOwnershipElapsedTime)
        {
            var proxyResponse = new APIGatewayProxyResponse();
            proxyResponse.StatusCode = response.StatusCode;
            proxyResponse.Body = response.Body;
            proxyResponse.Headers = new Dictionary<string, string>();
            proxyResponse.Headers.Add(conf.MarkerHeaderName, IdempotencyConfiguration.ResponseFromCache);
            proxyResponse.Headers.Add(conf.IdempotencyKeyHeaderName, idemKey);
            proxyResponse.Headers.Add(conf.GetOwnershipElapsedTimeHeaderName, $"{getOwnershipElapsedTime.TotalMilliseconds}");

            return proxyResponse;
        }
        private APIGatewayProxyResponse GetBadRequestResponse(string headerName)
        {
            var message = $"Missing idempotency key. Unable to find header {headerName} nor configuration.";
            return new APIGatewayProxyResponse()
            {
                StatusCode = 400,
                Body = JsonConvert.SerializeObject(new { message })
            };
        }
    }
}
