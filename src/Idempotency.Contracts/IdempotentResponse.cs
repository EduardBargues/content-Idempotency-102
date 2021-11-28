namespace Idempotency.Contracts
{
    public class IdempotentResponse
    {
        public IdempotentResponse(string idemKey, string ownerId, int statusCode = 0, string body = null)
        {
            IdempotencyKey = idemKey;
            OwnerId = ownerId;
            StatusCode = statusCode;
            Body = body;
        }

        public string IdempotencyKey { get; }
        public string OwnerId { get; }
        public int StatusCode { get; }
        public string Body { get; }
        public bool Finished => StatusCode != 0;

    }
}