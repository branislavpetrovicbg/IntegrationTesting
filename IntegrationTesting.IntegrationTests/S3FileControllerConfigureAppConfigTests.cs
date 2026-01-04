using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;
using IntegrationTesting.API;

namespace IntegrationTesting.IntegrationTests
{
    // Demonstrates overriding configuration for the test host using ConfigureAppConfiguration
    public class S3FileControllerConfigureAppConfigTests : IAsyncLifetime
    {
        private WireMockServer _server;

        public Task InitializeAsync()
        {
            _server = WireMockServer.Start();

            // simple stubs
            _server.Given(Request.Create().WithPath("/test-bucket/test-file.txt").UsingGet())
                .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "text/plain").WithBody("Configured via ConfigureAppConfiguration"));

            _server.Given(Request.Create().WithPath("/test-bucket/nonexistent-file.txt").UsingGet())
                .RespondWith(Response.Create().WithStatusCode(404));

            _server.Given(Request.Create().WithPath("/nonexistent-bucket/*").UsingGet())
                .RespondWith(Response.Create().WithStatusCode(404));

            return Task.CompletedTask;
        }

        public Task DisposeAsync()
        {
            _server?.Stop();
            _server?.Dispose();
            return Task.CompletedTask;
        }

        [Fact]
        public async Task GetFile_UsingConfigureAppConfiguration_OverridesS3Url()
        {
            var s3Url = _server.Urls[0].TrimEnd('/');

            // Ensure process env var doesn't point to a running LocalStack instance.
            // Set it to the WireMock URL so the test host picks up the stub reliably.
            ////Environment.SetEnvironmentVariable("AWS__S3ServiceUrl", s3Url);

            var factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureAppConfiguration((context, config) =>
                    {
                        var inMemorySettings = new Dictionary<string, string>
                        {
                            { "AWS:S3ServiceUrl", s3Url },
                            { "AWS:AccessKeyId", "test" },
                            { "AWS:SecretAccessKey", "test" },
                            { "AWS:Region", "us-east-1" }
                        };

                        config.AddInMemoryCollection(inMemorySettings);
                    });
                });

            using var client = factory.CreateClient();

            var response = await client.GetAsync($"/api/s3file/test-bucket/test-file.txt");
            var content = await response.Content.ReadAsStringAsync();
            // Clear the environment override to avoid leaking into other tests
            ////Environment.SetEnvironmentVariable("AWS__S3ServiceUrl", null);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Configured via ConfigureAppConfiguration", content);
        }
    }
}
