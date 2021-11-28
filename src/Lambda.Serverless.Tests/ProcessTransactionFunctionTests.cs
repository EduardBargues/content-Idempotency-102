using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Amazon.Lambda.APIGatewayEvents;
using Lambda.Serverless;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using Service;
using Xunit;

namespace Lambda.Serverless.Tests
{
    public partial class ProcessTransactionFunctionTests
    {
        [Fact]
        public async Task ServiceFailsToCreateTransaction_FunctionReturnsFailure()
        {
            // ARRANGE
            var mock = new Mock<ILogger<Implementation>>();
            var function = new ProcessTransactionFunction(new Implementation(mock.Object));
            var transaction = new Transaction();
            var request = new APIGatewayProxyRequest()
            {
                Body = JsonConvert.SerializeObject(transaction),
                Headers = new Dictionary<string, string>()
            };
            request.Headers.Add(ProcessTransactionFunction.DELAY_HEADER_NAME, $"{1}");
            request.Headers.Add(ProcessTransactionFunction.SUCCESS_HEADER_NAME, $"{false}");

            // ACT
            var response = await function.FunctionHandler(request, null);

            // ASSERT
            Assert.Equal((int)HttpStatusCode.UnprocessableEntity, response.StatusCode);
        }

        [Fact]
        public async Task ServiceCreatesTransaction_FunctionReturnsSuccess()
        {
            // ARRANGE
            var mock = new Mock<ILogger<Implementation>>();
            var function = new ProcessTransactionFunction(new Implementation(mock.Object));
            var transaction = new Transaction();
            var request = new APIGatewayProxyRequest()
            {
                Body = JsonConvert.SerializeObject(transaction),
                Headers = new Dictionary<string, string>()
            };
            request.Headers.Add(ProcessTransactionFunction.DELAY_HEADER_NAME, $"{1}");
            request.Headers.Add(ProcessTransactionFunction.SUCCESS_HEADER_NAME, $"{true}");

            // ACT
            var response = await function.FunctionHandler(request, null);

            // ASSERT
            Assert.Equal((int)HttpStatusCode.Created, response.StatusCode);

            var body = JsonConvert.DeserializeObject<TransactionCreatedResponse>(response.Body);
            Assert.NotNull(body.TransactionId);
            Assert.NotEqual(Guid.Empty.ToString(), body.TransactionId);
        }
    }
}
