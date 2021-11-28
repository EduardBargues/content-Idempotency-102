using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using System.IO;
using Idempotency.Contracts;
using System.Diagnostics;

namespace Idempotency.WebApi
{
    public class IdempotencyFilter : Attribute, IAsyncResourceFilter
    {
        private readonly ILogger<IdempotencyFilter> _logger;
        private readonly IIdempotentStorage _db;
        private readonly IdempotencyConfiguration _conf;

        public IdempotencyFilter(IIdempotentStorage db, IOptions<IdempotencyConfiguration> conf, ILogger<IdempotencyFilter> logger)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _conf = conf.Value ?? throw new ArgumentNullException(nameof(conf));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
        {
            var request = context.HttpContext.Request;
            var hasIdemKey = request.Headers != null
                && request.Headers.ContainsKey(_conf.IdempotencyKeyHeaderName);
            if (!hasIdemKey)
                SetBadRequestResult(_conf.IdempotencyKeyHeaderName, context);

            var idemKey = request.Headers[_conf.IdempotencyKeyHeaderName];
            var ownerId = context.HttpContext.TraceIdentifier;
            (var response, var ownershipTime) = await GetKeyOwnership(idemKey, ownerId, _conf.TimeToLiveMaster);

            if (response.Finished)
                SetCacheResult(idemKey, response, ownershipTime, context);
            if (response.OwnerId != ownerId)
                SetConflictResult(idemKey, response, ownershipTime, context);

            (var statusCode, var body) = await LetApiManageRequest(context, next);

            TimeSpan cachingTime = TimeSpan.Zero;
            if (_conf.StatusCodesToCacheResponseFrom.Contains(statusCode))
            {
                var ttl = request.Headers.ContainsKey(_conf.TimeToLiveHeaderName)
                    ? TimeSpan.FromSeconds(int.Parse(request.Headers[_conf.TimeToLiveHeaderName]))
                    : _conf.DefaultTimeToLive;
                cachingTime = await CacheResponse(idemKey, statusCode, body, ttl);
            }

            AddResponseHeaders(idemKey, ownershipTime, cachingTime, context);
        }
        private async Task<(int statusCode, string body)> LetApiManageRequest(ResourceExecutingContext context, ResourceExecutionDelegate next)
        {
            var originalBody = context.HttpContext.Response.Body;
            using (var memStream = new MemoryStream())
            {
                context.HttpContext.Response.Body = memStream;

                var executedContext = await next();

                memStream.Position = 0;
                string responseBody = new StreamReader(memStream).ReadToEnd();

                memStream.Position = 0;
                await memStream.CopyToAsync(originalBody);
                var statusCode = executedContext.HttpContext.Response.StatusCode;
                return (statusCode, responseBody);
            }
        }
        private async Task<TimeSpan> CacheResponse(string idemKey, int statusCode, string body, TimeSpan ttl)
        {
            Stopwatch watch = new Stopwatch();
            watch.Start();
            await _db.CacheResponse(idemKey, statusCode, body, ttl);
            watch.Stop();

            return watch.Elapsed;
        }
        private async Task<(IdempotentResponse, TimeSpan)> GetKeyOwnership(string idemKey, string ownerId, TimeSpan timeToLiveMaster)
        {
            Stopwatch watch = new Stopwatch();
            watch.Start();
            var response = await _db.GetKeyOwnership(idemKey, ownerId, timeToLiveMaster);
            watch.Stop();

            return (response, watch.Elapsed);
        }

        private void AddResponseHeaders(string idemKey,
                                        TimeSpan getOwnershipElapsedTime,
                                        TimeSpan cacheElapsedTime,
                                        ResourceExecutingContext context)
        {
            var httpResponse = context.HttpContext.Response;
            httpResponse.Headers.Add(_conf.MarkerHeaderName, IdempotencyConfiguration.ResponseFromImplementation);
            httpResponse.Headers.Add(_conf.IdempotencyKeyHeaderName, idemKey);
            httpResponse.Headers.Add(_conf.GetOwnershipElapsedTimeHeaderName, $"{getOwnershipElapsedTime.TotalMilliseconds}");
            if (cacheElapsedTime != TimeSpan.Zero)
                httpResponse.Headers.Add(_conf.CacheResponseElapsedTimeHeaderName, $"{cacheElapsedTime.TotalMilliseconds}");
        }

        private void SetConflictResult(string idemKey, IdempotentResponse response, TimeSpan getOwnershipElapsedTime, ResourceExecutingContext context)
        {
            context.Result = new ConflictObjectResult($"Request with idempotency-key: {idemKey} is in progress.");
            // return new APIGatewayProxyResponse()
            // {
            //     StatusCode = 409,
            //     Headers = new Dictionary<string, string>() {
            //         { _conf.IdempotencyKeyHeaderName, idemKey },
            //         { _conf.GetOwnershipElapsedTimeHeaderName, $"{getOwnershipElapsedTime.TotalMilliseconds}" },
            //         },
            //     Body = JsonConvert.SerializeObject(new { message = $"Request with idempotency-key: {idemKey} is in progress" }),
            // };
        }
        private void SetCacheResult(string idemKey, IdempotentResponse response, TimeSpan getOwnershipElapsedTime, ResourceExecutingContext context)
        {
            context.Result = new ObjectResult(response.Body)
            {
                StatusCode = response.StatusCode,
                // Headers
            };

            // var proxyResponse = new APIGatewayProxyResponse();
            // proxyResponse.Headers = new Dictionary<string, string>();
            // proxyResponse.Headers.Add(_conf.MarkerHeaderName, IdempotencyConfiguration.ResponseFromCache);
            // proxyResponse.Headers.Add(_conf.IdempotencyKeyHeaderName, idemKey);
            // proxyResponse.Headers.Add(_conf.GetOwnershipElapsedTimeHeaderName, $"{getOwnershipElapsedTime.TotalMilliseconds}");

            // return proxyResponse;
        }
        private void SetBadRequestResult(string headerName, ResourceExecutingContext context)
        {
            var message = $"Missing idempotency key. Unable to find header {headerName} nor configuration.";
            context.Result = new BadRequestObjectResult(message);
        }

    }
}