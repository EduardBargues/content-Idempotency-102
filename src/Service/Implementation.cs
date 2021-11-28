using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Service
{
    public interface IImplementation
    {
        Task<string> ProcessAsync(Transaction transaction, int delayInMilliseconds, bool succeed);
    }

    public class Implementation : IImplementation
    {
        private readonly ILogger<Implementation> _logger;

        public Implementation(ILogger<Implementation> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        public async Task<string> ProcessAsync(Transaction transaction, int delayInMilliseconds, bool succeed)
        {
            await Task.Delay(delayInMilliseconds);
            return succeed ? Guid.NewGuid().ToString() : null;
        }
    }

    public class Transaction
    {
        public decimal Amount { get; set; }
        public string OriginId { get; set; }
        public string DestinationId { get; set; }
    }
}
