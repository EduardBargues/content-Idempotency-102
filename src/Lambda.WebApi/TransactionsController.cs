using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Idempotency.WebApi;

namespace Lambda.WebApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class TransactionsController : ControllerBase
    {
        private readonly ILogger<TransactionsController> _logger;

        public TransactionsController(ILogger<TransactionsController> logger)
        {
            _logger = logger;
        }

        [HttpPost]
        [ServiceFilter(typeof(IdempotencyFilter))]
        public Task<IActionResult> Post()
        {
            return Task.FromResult<IActionResult>(Ok(new { message = "this is a message from controller" }));
        }
    }
}
