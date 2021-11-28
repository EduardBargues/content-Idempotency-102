using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Idempotency.WebApi;
using Idempotency.Storage.DynamoDb;

namespace Lambda.WebApi
{
    public class Startup
    {
        private const string IDEM_TABLE_NAME_ENV_VAR = "IDEMPOTENCY_TABLE_NAME";

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();
            services.AddIdempotency("x-idem-key", // header to retrieve key
                                    60, // seconds to cache the key response
                                    15, // seconds to wait the owner before deprecating key
                                    "x-idem-ttl", // your client can specify the time to live for the keys in this header
                                    "x-idem-marker", // for debugging purposes you can receive in this header where the response is coming from (cache or implementation)
                                    "x-idem-diagnostics-get-ownership", // for performance diagnostics, you can receive the time it takes to own the key in this header
                                    "x-idem-diagnostics-cache-response", // for performance diagnostics, you can receive the time it takes to cache the response in this header
                                    200, 201 // status code that idempotency will cache the response for
                                    );
            var storageTable = Environment.GetEnvironmentVariable(IDEM_TABLE_NAME_ENV_VAR);
            services.AddDynamoAsIdempotencyStorage(storageTable);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
