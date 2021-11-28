using System.Collections.Generic;
using System.Net;

using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;
using Service;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Microsoft.Extensions.DependencyInjection;
using Idempotency;
using Idempotency.Contracts;
using Microsoft.Extensions.Logging;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace Lambda.Serverless
{
    public class ProcessTransactionFunction : IFunction
    {
        public const string DELAY_HEADER_NAME = "x-idem-for-testing-delay";
        public const string SUCCESS_HEADER_NAME = "x-idem-for-testing-success";
        private const int DEFAULT_DELAY_IN_MILLISECONDS = 500;
        private const bool DEFAULT_SUCCESS = true;

        private readonly IServiceCollection _container;

        public ProcessTransactionFunction()
        {
            _container = new ServiceCollection();
            _container.AddTransient<IImplementation, Implementation>();
            _container.AddLogging(builder => builder.AddLambdaLogger());
        }

        public ProcessTransactionFunction(IImplementation service)
        {
            _container = new ServiceCollection();
            _container.AddTransient<IImplementation>(provider => service);
            _container.AddLogging();
        }

        public async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
        {
            using (var provider = _container.BuildServiceProvider())
            using (var scope = provider.CreateScope())
            {
                var logger = scope.ServiceProvider.GetService<ILogger<ProcessTransactionFunction>>();
                logger.LogInformation($"processing transaction");

                var transaction = JsonConvert.DeserializeObject<Transaction>(request.Body);
                var delayInMilliseconds = request.Headers != null
                                       && request.Headers.ContainsKey(DELAY_HEADER_NAME)
                                       && int.TryParse(request.Headers[DELAY_HEADER_NAME], out int delay)
                    ? delay
                    : DEFAULT_DELAY_IN_MILLISECONDS;
                var succeed = request.Headers != null
                           && request.Headers.ContainsKey(SUCCESS_HEADER_NAME)
                           && bool.TryParse(request.Headers[SUCCESS_HEADER_NAME], out bool success)
                    ? success
                    : DEFAULT_SUCCESS;
                var service = scope.ServiceProvider.GetService<IImplementation>();
                var transactionId = await service.ProcessAsync(transaction, delayInMilliseconds, succeed);

                var created = !string.IsNullOrWhiteSpace(transactionId);
                var response = new APIGatewayProxyResponse
                {
                    StatusCode = (int)(created ? HttpStatusCode.Created : HttpStatusCode.UnprocessableEntity),
                    Body = created ? JsonConvert.SerializeObject(new { transactionId }) : null,
                    Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
                };

                return response;
            }
        }
    }
}
