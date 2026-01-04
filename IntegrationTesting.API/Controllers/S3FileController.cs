using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace IntegrationTesting.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class S3FileController : ControllerBase
    {
        private readonly IAmazonS3 _s3Client;
        private readonly ILogger<S3FileController> _logger;

        public S3FileController(IAmazonS3 s3Client, ILogger<S3FileController> logger)
        {
            _s3Client = s3Client;
            _logger = logger;
        }

        /// <summary>
        /// Get file content and metadata from S3
        /// </summary>
        /// <param name="bucket">S3 bucket name</param>
        /// <param name="key">S3 object key (file path)</param>
        /// <returns>File content and metadata</returns>
        [HttpGet("{bucket}/{key}")]
        public async Task<IActionResult> GetFile(string bucket, string key)
        {
            try
            {
                _logger.LogInformation($"Retrieving file from S3: bucket={bucket}, key={key}");

                var request = new GetObjectRequest
                {
                    BucketName = bucket,
                    Key = key
                };

                var response = await _s3Client.GetObjectAsync(request);

                using (var reader = new StreamReader(response.ResponseStream))
                {
                    var content = await reader.ReadToEndAsync();

                    var result = new
                    {
                        bucket = bucket,
                        key = key,
                        contentType = response.Headers.ContentType,
                        contentLength = response.ContentLength,
                        lastModified = response.LastModified,
                        content = content
                    };

                    return Ok(result);
                }
            }
            catch (AmazonS3Exception ex)
            {
                // Handle typical S3 not-found cases regardless of provider error code
                if (ex.StatusCode == HttpStatusCode.NotFound || ex.ErrorCode == "NoSuchKey")
                {
                    _logger.LogWarning($"File not found in S3: bucket={bucket}, key={key}");
                    return NotFound(new { message = $"File not found: {key}" });
                }

                if (ex.ErrorCode == "NoSuchBucket")
                {
                    _logger.LogWarning($"Bucket not found in S3: bucket={bucket}");
                    return NotFound(new { message = $"Bucket not found: {bucket}" });
                }

                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error retrieving file from S3: {ex.Message}");
                return StatusCode(500, new { message = "Error retrieving file from S3", error = ex.Message });
            }
        }
    }
}
