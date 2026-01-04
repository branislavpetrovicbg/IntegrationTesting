using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;
using Microsoft.Extensions.Configuration;
using IntegrationTesting.API;

namespace IntegrationTesting.IntegrationTests
{
    public class S3FileControllerWireMockTests : IAsyncLifetime
    {
        private WireMockServer _server;

        public Task InitializeAsync()
        {
            // Start WireMock on a random available port
            _server = WireMockServer.Start();

            // Stub: existing file
            _server.Given(Request.Create()
                    .WithPath("/test-bucket/test-file.txt")
                    .UsingGet())
                .RespondWith(Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "text/plain")
                    .WithBody("This is the stubbed file content from WireMock."));

            // Stub: nonexistent key -> 404
            _server.Given(Request.Create()
                    .WithPath("/test-bucket/nonexistent-file.txt")
                    .UsingGet())
                .RespondWith(Response.Create()
                    .WithStatusCode(404));

            // Stub: any request to nonexistent-bucket -> 404
            _server.Given(Request.Create()
                    .WithPath("/nonexistent-bucket/*")
                    .UsingGet())
                .RespondWith(Response.Create()
                    .WithStatusCode(404));

            return Task.CompletedTask;
        }

        public Task DisposeAsync()
        {
            _server?.Stop();
            _server?.Dispose();
            return Task.CompletedTask;
        }

        [Fact]
        public async Task GetFile_ReturnsFileContent_FromWireMock()
        {
            // Arrange
            var s3Url = _server.Urls[0].TrimEnd('/');

            Environment.SetEnvironmentVariable("AWS__S3ServiceUrl", s3Url);
            Environment.SetEnvironmentVariable("AWS__AccessKeyId", "test");
            Environment.SetEnvironmentVariable("AWS__SecretAccessKey", "test");
            Environment.SetEnvironmentVariable("AWS__Region", "us-east-1");

            var factory = new WebApplicationFactory<Program>();

            using var client = factory.CreateClient();

            // Act
            var response = await client.GetAsync($"/api/s3file/test-bucket/test-file.txt");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var content = await response.Content.ReadAsStringAsync();
            Assert.Contains("This is the stubbed file content from WireMock.", content);
        }

        [Fact]
        public async Task GetFile_NonexistentKey_Returns404()
        {
            var s3Url = _server.Urls[0].TrimEnd('/');

            Environment.SetEnvironmentVariable("AWS__S3ServiceUrl", s3Url);
            Environment.SetEnvironmentVariable("AWS__AccessKeyId", "test");
            Environment.SetEnvironmentVariable("AWS__SecretAccessKey", "test");
            Environment.SetEnvironmentVariable("AWS__Region", "us-east-1");

            var factory = new WebApplicationFactory<Program>();

            using var client = factory.CreateClient();

            var response = await client.GetAsync($"/api/s3file/test-bucket/nonexistent-file.txt");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task GetFile_NonexistentBucket_Returns404()
        {
            var s3Url = _server.Urls[0].TrimEnd('/');

            Environment.SetEnvironmentVariable("AWS__S3ServiceUrl", s3Url);
            Environment.SetEnvironmentVariable("AWS__AccessKeyId", "test");
            Environment.SetEnvironmentVariable("AWS__SecretAccessKey", "test");
            Environment.SetEnvironmentVariable("AWS__Region", "us-east-1");

            var factory = new WebApplicationFactory<Program>();

            using var client = factory.CreateClient();

            var response = await client.GetAsync($"/api/s3file/nonexistent-bucket/some-file.txt");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }
}
