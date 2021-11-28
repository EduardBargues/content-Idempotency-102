using System;
using System.Threading.Tasks;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Idempotency.Contracts;
using Idempotency.Serverless;
using Idempotency.Storage.DynamoDb;

namespace Lambda.Serverless
{
    public class ProcessTransactionIdempotentFunction : IdempotentFunction
    {
        private const string IDEM_TABLE_NAME_ENV_VAR = "IDEMPOTENCY_TABLE_NAME";
        private readonly IFunction _function;
        private readonly IIdempotentStorage _db;
        private readonly IdempotencyConfiguration _conf;

        public ProcessTransactionIdempotentFunction() : this(
            new ProcessTransactionFunction(),
            DynamoDbStorage.CreateInstance(Environment.GetEnvironmentVariable(IDEM_TABLE_NAME_ENV_VAR)),
            IdempotencyConfiguration.Default)
        { }

        public ProcessTransactionIdempotentFunction(
            IFunction function,
            IIdempotentStorage db,
            IdempotencyConfiguration conf)
        {
            _function = function ?? throw new ArgumentNullException(nameof(function));
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _conf = conf ?? throw new ArgumentNullException(nameof(conf));
        }

        public Task<APIGatewayProxyResponse> FunctionHandler(
            APIGatewayProxyRequest request,
            ILambdaContext context) => IdempotentHandler(request, context, _function, _db, _conf);
    }
}
