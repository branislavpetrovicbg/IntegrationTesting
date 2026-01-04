using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Testcontainers.LocalStack;
using Xunit;
using IntegrationTesting.API;

namespace IntegrationTesting.IntegrationTests
{
    public class LocalStackS3Fixture : IAsyncLifetime
    {
        private readonly LocalStackContainer _container;
        private IAmazonS3 _s3Client;
        public string ConnectionString { get; private set; }
        public string LocalStackUrl { get; private set; }
        public string BucketName { get; } = "test-bucket";
        public string TestFileName { get; } = "test-file.txt";
        public string TestFileContent { get; } = "Hello from S3!";

        public LocalStackS3Fixture()
        {
            _container = new LocalStackBuilder()
                .Build();
        }

        async Task IAsyncLifetime.InitializeAsync()
        {
            Environment.SetEnvironmentVariable("TESTCONTAINERS_RYUK_DISABLED", "true");
            
            try
            {
                await _container.StartAsync();
            }
            catch (Exception)
            {
                Environment.SetEnvironmentVariable("TESTCONTAINERS_RYUK_DISABLED", "false");
                throw;
            }

            // Get the LocalStack endpoint for localhost (test setup)
            var endpoint = _container.GetConnectionString();
            ConnectionString = endpoint;
            
            // For API configuration, use the container's internal IP/port that other containers can reach
            // LocalStack exposes port 4566, and we'll connect via the container IP
            var containerIp = _container.IpAddress;
            LocalStackUrl = $"http://{containerIp}:4566";

            // Create S3 client pointing to LocalStack on localhost
            var s3Config = new Amazon.S3.AmazonS3Config
            {
                ServiceURL = endpoint,
                UseHttp = true,
                ForcePathStyle = true
            };

            var credentials = new Amazon.Runtime.BasicAWSCredentials("test", "test");
            _s3Client = new Amazon.S3.AmazonS3Client(credentials, s3Config);

            // Create bucket and upload test file
            await _s3Client.PutBucketAsync(new PutBucketRequest { BucketName = BucketName });

            var putRequest = new PutObjectRequest
            {
                BucketName = BucketName,
                Key = TestFileName,
                ContentBody = TestFileContent
            };

            await _s3Client.PutObjectAsync(putRequest);
        }

        async Task IAsyncLifetime.DisposeAsync()
        {
            if (_s3Client != null)
            {
                _s3Client.Dispose();
            }

            if (_container != null)
            {
                await _container.StopAsync();
            }

            Environment.SetEnvironmentVariable("TESTCONTAINERS_RYUK_DISABLED", "false");
        }
    }

    [Collection("LocalStack S3 Collection")]
    public class S3FileControllerIntegrationTests : IClassFixture<LocalStackS3Fixture>
    {
        private readonly LocalStackS3Fixture _fixture;

        public S3FileControllerIntegrationTests(LocalStackS3Fixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task GetFile_WithValidBucketAndKey_ReturnsFileContent()
        {
            // Arrange
            var factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureAppConfiguration((context, config) =>
                    {
                        var inMemorySettings = new Dictionary<string, string>
                        {
                            { "AWS:S3ServiceUrl", _fixture.LocalStackUrl },
                            { "AWS:AccessKeyId", "test" },
                            { "AWS:SecretAccessKey", "test" },
                            { "AWS:Region", "us-east-1" }
                        };

                        config.AddInMemoryCollection(inMemorySettings);
                    });
                });

            using var client = factory.CreateClient();

            // Act
            var response = await client.GetAsync($"/api/s3file/{_fixture.BucketName}/{_fixture.TestFileName}");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var content = await response.Content.ReadAsStringAsync();
            Assert.Contains(_fixture.TestFileContent, content);
            Assert.Contains(_fixture.BucketName, content);
            Assert.Contains(_fixture.TestFileName, content);
        }

        [Fact]
        public async Task GetFile_WithNonExistentKey_Returns404()
        {
            // Arrange
            var factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureAppConfiguration((context, config) =>
                    {
                        var inMemorySettings = new Dictionary<string, string>
                        {
                            { "AWS:S3ServiceUrl", _fixture.LocalStackUrl },
                            { "AWS:AccessKeyId", "test" },
                            { "AWS:SecretAccessKey", "test" },
                            { "AWS:Region", "us-east-1" }
                        };

                        config.AddInMemoryCollection(inMemorySettings);
                    });
                });

            using var client = factory.CreateClient();

            // Act
            var response = await client.GetAsync($"/api/s3file/{_fixture.BucketName}/nonexistent-file.txt");

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task GetFile_WithNonExistentBucket_Returns404()
        {
            // Arrange
            var factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureAppConfiguration((context, config) =>
                    {
                        var inMemorySettings = new Dictionary<string, string>
                        {
                            { "AWS:S3ServiceUrl", _fixture.LocalStackUrl },
                            { "AWS:AccessKeyId", "test" },
                            { "AWS:SecretAccessKey", "test" },
                            { "AWS:Region", "us-east-1" }
                        };

                        config.AddInMemoryCollection(inMemorySettings);
                    });
                });

            using var client = factory.CreateClient();

            // Act
            var response = await client.GetAsync($"/api/s3file/nonexistent-bucket/some-file.txt");

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }
}
