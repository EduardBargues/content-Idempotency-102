using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using Idempotency.Contracts;

namespace Idempotency.Storage.DynamoDb
{
    public class DynamoDbStorage : IIdempotentStorage
    {
        private readonly string _tableName;
        private const string IDEMPOTENCY_KEY_FIELD_NAME = "IdempotencyKey";
        private const string OWNER_ID_FIELD_NAME = "OwnerId";
        private const string STATUS_CODE_FIELD_NAME = "StatusCode";
        private const string BODY_FIELD_NAME = "Body";
        private const string TTL_FIELD_NAME = "TTL";

        private readonly IAmazonDynamoDB _client;

        public DynamoDbStorage(IAmazonDynamoDB client, string tableName)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _tableName = tableName ?? throw new ArgumentNullException(nameof(tableName));
        }
        public static IIdempotentStorage CreateInstance(string tableName) => new DynamoDbStorage(new AmazonDynamoDBClient(), tableName);

        public async Task<IdempotentResponse> GetKeyOwnership(string idemKey, string ownerId, TimeSpan timeToLive)
        {
            var item = GetPartitionKey(idemKey);
            var epochTtlInSeconds = ToEpochInSeconds(timeToLive);
            item.Add(TTL_FIELD_NAME, new AttributeValue() { N = $"{epochTtlInSeconds}" });
            item.Add(OWNER_ID_FIELD_NAME, new AttributeValue(ownerId));
            var request = new PutItemRequest(_tableName, item);
            request.ConditionExpression = $"attribute_not_exists({IDEMPOTENCY_KEY_FIELD_NAME})";
            try
            {
                await _client.PutItemAsync(request);
                return new IdempotentResponse(idemKey, ownerId);
            }
            catch (ConditionalCheckFailedException)
            {
                var partitionKey = GetPartitionKey(idemKey);
                var getResponse = await _client.GetItemAsync(new GetItemRequest(_tableName, partitionKey));

                var result = new IdempotentResponse(idemKey,
                    getResponse.Item[OWNER_ID_FIELD_NAME].S,
                    getResponse.Item.TryGetValue(STATUS_CODE_FIELD_NAME, out AttributeValue statusCodeValue)
                        ? int.Parse(statusCodeValue.N) : 0,
                    getResponse.Item.TryGetValue(BODY_FIELD_NAME, out AttributeValue bodyValue)
                        ? bodyValue.S : null);
                return result;
            }
        }

        public async Task CacheResponse(string idemKey, int statusCode, string body, TimeSpan timeToLive)
        {
            var request = new UpdateItemRequest();
            request.TableName = _tableName;
            request.Key = GetPartitionKey(idemKey);
            request.AttributeUpdates = new Dictionary<string, AttributeValueUpdate>();
            request.UpdateExpression = $"SET {STATUS_CODE_FIELD_NAME}=:sc, {BODY_FIELD_NAME}=:b";
            request.ExpressionAttributeValues = new Dictionary<string, AttributeValue>();
            request.ExpressionAttributeValues.Add(":sc", new AttributeValue() { N = $"{statusCode}" });
            request.ExpressionAttributeValues.Add(":b", new AttributeValue(body));
            await _client.UpdateItemAsync(request);
        }

        private long ToEpochInSeconds(TimeSpan timeToLive)
            => (long)(DateTime.UtcNow.Add(timeToLive)
                    - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;

        private Dictionary<string, AttributeValue> GetPartitionKey(string idemKey)
            => new Dictionary<string, AttributeValue>() { { IDEMPOTENCY_KEY_FIELD_NAME, new AttributeValue(idemKey) } };
    }
}