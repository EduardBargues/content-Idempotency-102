using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Idempotency.Contracts;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using Service;
using Xunit;

namespace Idempotency.WebApi.Tests
{
    public class Main
    {
    }
}
