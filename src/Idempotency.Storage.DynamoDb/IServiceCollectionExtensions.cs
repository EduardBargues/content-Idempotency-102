using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using Idempotency.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Idempotency.Storage.DynamoDb
{
    public static class Extensions
    {
        public static IServiceCollection AddDynamoAsIdempotencyStorage(this IServiceCollection services, string tableName)
        {
            services.AddTransient<IIdempotentStorage>
                (servProv => new DynamoDbStorage(new AmazonDynamoDBClient(), tableName));
            return services;
        }
    }
}