using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Lambda.Serverless;
using Idempotency.Contracts;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using Service;
using Xunit;

namespace Idempotency.Serverless.Tests
{
    public class Main
    {
        [Fact]
        public async Task RequestCanTakeOwnership_SuccessFromImplementation_SuccessfulResponse()
        {
            // ARRANGE
            var idempotencyKey = "idempotencyKey";
            var ownerId = "ownerId";
            var dummyConf = IdempotencyConfiguration.Default;
            var mock = new Mock<ILogger<Implementation>>(); // actual function
            var function = new ProcessTransactionFunction(new Implementation(mock.Object));
            var dbMock = new Mock<IIdempotentStorage>(); // idempotency allows to take ownership of the execution
            dbMock
                .Setup(m => m.GetKeyOwnership(idempotencyKey, ownerId, It.IsAny<TimeSpan>()))
                .ReturnsAsync(new IdempotentResponse(idempotencyKey, ownerId));
            var idempotentFunction = new ProcessTransactionIdempotentFunction(function, dbMock.Object, dummyConf);
            var request = new APIGatewayProxyRequest() // request
            {
                Headers = new Dictionary<string, string>() { },
                Body = JsonConvert.SerializeObject(new Transaction()),
                RequestContext = new APIGatewayProxyRequest.ProxyRequestContext()
            };
            request.Headers.Add(dummyConf.IdempotencyKeyHeaderName, idempotencyKey);
            request.Headers.Add(ProcessTransactionFunction.DELAY_HEADER_NAME, $"{1}");
            request.RequestContext.RequestId = ownerId;
            var contextMock = new Mock<ILambdaContext>(); // Lambda context

            // ACT
            var response = await idempotentFunction.FunctionHandler(request, contextMock.Object);

            // ASSERT
            Assert.Equal(201, response.StatusCode);

            Assert.Equal(IdempotencyConfiguration.ResponseFromImplementation,
                         response.Headers[dummyConf.MarkerHeaderName]);
            Assert.Contains(dummyConf.GetOwnershipElapsedTimeHeaderName, response.Headers);
            Assert.Contains(dummyConf.CacheResponseElapsedTimeHeaderName, response.Headers);

            var body = JsonConvert.DeserializeObject<TransactionCreatedResponse>(response.Body);
            Assert.NotNull(body.TransactionId);
            Assert.NotEqual(Guid.Empty.ToString(), body.TransactionId);
        }

        [Fact]
        public async Task MissingIdempotencyKey_BadRequestResponse()
        {
            // ARRANGE
            var dummyConf = IdempotencyConfiguration.Default;
            var implementationMock = new Mock<IImplementation>();
            var function = new ProcessTransactionFunction(implementationMock.Object);
            var dbMock = new Mock<IIdempotentStorage>();
            var idempotentFunction = new ProcessTransactionIdempotentFunction(function, dbMock.Object, dummyConf);
            var request = new APIGatewayProxyRequest() // request
            {
                Headers = new Dictionary<string, string>(),
                Body = JsonConvert.SerializeObject(new Transaction())
            };
            var contextMock = new Mock<ILambdaContext>(); // Lambda context

            // ACT
            var response = await idempotentFunction.FunctionHandler(request, contextMock.Object);

            // ASSERT
            Assert.Equal(400, response.StatusCode);
        }

        [Fact]
        public async Task RequestCanNotTakeOwnership_ConflictResponse()
        {
            // ARRANGE
            var idempotencyKey = "idempotencyKey";
            var ownerId = "ownerId";
            var dummyConf = IdempotencyConfiguration.Default;
            var mock = new Mock<ILogger<Implementation>>(); // actual function
            var function = new ProcessTransactionFunction(new Implementation(mock.Object));
            var dbMock = new Mock<IIdempotentStorage>(); // idempotency does NOT allow to take ownership of the execution
            dbMock
                .Setup(m => m.GetKeyOwnership(idempotencyKey, ownerId, It.IsAny<TimeSpan>()))
                .ReturnsAsync(new IdempotentResponse(idempotencyKey, $"{ownerId}-different"));
            var idempotentFunction = new ProcessTransactionIdempotentFunction(function, dbMock.Object, dummyConf);
            var request = new APIGatewayProxyRequest() // request
            {
                Headers = new Dictionary<string, string>(),
                Body = JsonConvert.SerializeObject(new Transaction()),
                RequestContext = new APIGatewayProxyRequest.ProxyRequestContext()
            };
            request.Headers.Add(dummyConf.IdempotencyKeyHeaderName, idempotencyKey);
            request.Headers.Add(ProcessTransactionFunction.DELAY_HEADER_NAME, $"{1}");
            request.RequestContext.RequestId = ownerId;
            var contextMock = new Mock<ILambdaContext>(); // Lambda context

            // ACT
            var response = await idempotentFunction.FunctionHandler(request, contextMock.Object);

            // ASSERT
            Assert.Equal(409, response.StatusCode);
            Assert.Contains(dummyConf.GetOwnershipElapsedTimeHeaderName, response.Headers);
        }

        [Fact]
        public async Task RequestCanTakeOwnership_StatusCodeIsNotInTheList_ResponseIsNotCache()
        {
            // ARRANGE
            var statusCode = 404;
            var idempotencyKey = "idempotencyKey";
            var ownerId = "ownerId";
            var dummyConf = IdempotencyConfiguration.Default;
            var function = new Mock<IFunction>(); // actual function
            function
                .Setup(m => m.FunctionHandler(It.IsAny<APIGatewayProxyRequest>(), It.IsAny<ILambdaContext>()))
                .ReturnsAsync(new APIGatewayProxyResponse() { StatusCode = statusCode });
            var dbMock = new Mock<IIdempotentStorage>(); // idempotency allows to take ownership of the execution
            dbMock
                .Setup(m => m.GetKeyOwnership(idempotencyKey, ownerId, It.IsAny<TimeSpan>()))
                .ReturnsAsync(new IdempotentResponse(idempotencyKey, ownerId));
            var idempotentFunction = new ProcessTransactionIdempotentFunction(function.Object, dbMock.Object, dummyConf);
            var request = new APIGatewayProxyRequest() // request
            {
                Headers = new Dictionary<string, string>(),
                Body = JsonConvert.SerializeObject(new Transaction()),
                RequestContext = new APIGatewayProxyRequest.ProxyRequestContext()
            };
            request.Headers.Add(dummyConf.IdempotencyKeyHeaderName, idempotencyKey);
            request.Headers.Add(ProcessTransactionFunction.DELAY_HEADER_NAME, $"{1}");
            request.RequestContext.RequestId = ownerId;
            var contextMock = new Mock<ILambdaContext>(); // Lambda context

            // ACT
            var response = await idempotentFunction.FunctionHandler(request, contextMock.Object);

            // ASSERT
            Assert.Equal(statusCode, response.StatusCode);
            Assert.Contains(dummyConf.GetOwnershipElapsedTimeHeaderName, response.Headers);
            Assert.DoesNotContain(dummyConf.CacheResponseElapsedTimeHeaderName, response.Headers);
            dbMock.Verify(m => m.CacheResponse(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<TimeSpan>()),
                          Times.Never);
        }

        [Fact]
        public async Task KeyAlreadyProcessed_SuccessfulResponse()
        {
            // ARRANGE
            var transactionId = "transactionId";
            var idempotencyKey = "idempotencyKey";
            var ownerId = "ownerId";
            var dummyConf = IdempotencyConfiguration.Default;
            var mock = new Mock<ILogger<Implementation>>(); // actual function
            var function = new ProcessTransactionFunction(new Implementation(mock.Object));
            var dbMock = new Mock<IIdempotentStorage>(); // key already processed
            dbMock
                .Setup(m => m.GetKeyOwnership(idempotencyKey, ownerId, It.IsAny<TimeSpan>()))
                .ReturnsAsync(new IdempotentResponse(idempotencyKey, $"{ownerId}-different", 201, JsonConvert.SerializeObject(new { transactionId })));
            var idempotentFunction = new ProcessTransactionIdempotentFunction(function, dbMock.Object, dummyConf);
            var request = new APIGatewayProxyRequest() // request
            {
                Headers = new Dictionary<string, string>(),
                Body = JsonConvert.SerializeObject(new Transaction()),
                RequestContext = new APIGatewayProxyRequest.ProxyRequestContext()
            };
            request.Headers.Add(dummyConf.IdempotencyKeyHeaderName, idempotencyKey);
            request.RequestContext.RequestId = ownerId;
            var contextMock = new Mock<ILambdaContext>(); // Lambda context

            // ACT
            var response = await idempotentFunction.FunctionHandler(request, contextMock.Object);

            // ASSERT
            Assert.Equal(201, response.StatusCode);

            Assert.Equal(IdempotencyConfiguration.ResponseFromCache,
                         response.Headers[dummyConf.MarkerHeaderName]);
            Assert.Contains(dummyConf.GetOwnershipElapsedTimeHeaderName, response.Headers);

            var body = JsonConvert.DeserializeObject<TransactionCreatedResponse>(response.Body);
            Assert.Equal(transactionId, body.TransactionId);
        }
    }
}
