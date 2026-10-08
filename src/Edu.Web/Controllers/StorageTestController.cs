using Amazon.S3;
using Amazon.S3.Model;
using Edu.Infrastructure.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Edu.Web.Controllers
{
    public class StorageTestController : Controller
    {
        private readonly IAmazonS3 _s3;
        private readonly DigitalOceanSpacesOptions _options;
        private readonly ILogger<StorageTestController> _logger;

        public StorageTestController(
            IAmazonS3 s3,
            IOptions<DigitalOceanSpacesOptions> options,
            ILogger<StorageTestController> logger)
        {
            _s3 = s3;
            _options = options.Value;
            _logger = logger;
        }

        [HttpGet("/storage-test")]
        public async Task<IActionResult> Test()
        {
            var results = new List<string>();

            // 1. Test bucket access
            try
            {
                var headResponse = await _s3.HeadBucketAsync(
                    new HeadBucketRequest
                    {
                        BucketName = _options.Container
                    });

                results.Add(
                    $"PASS - HeadBucket: {headResponse.HttpStatusCode}");
            }
            catch (AmazonS3Exception ex)
            {
                _logger.LogError(
                    ex,
                    "HeadBucket test failed. Bucket={Bucket}, Status={Status}, ErrorCode={ErrorCode}",
                    _options.Container,
                    ex.StatusCode,
                    ex.ErrorCode);

                results.Add(
                    $"FAIL - HeadBucket: Status={ex.StatusCode}, ErrorCode={ex.ErrorCode}, Message={ex.Message}");

                return Content(string.Join(Environment.NewLine, results));
            }

            // 2. Test upload
            var testKey = $"diagnostics/test-{Guid.NewGuid():N}.txt";

            try
            {
                await using var stream = new MemoryStream(
                    System.Text.Encoding.UTF8.GetBytes(
                        "DigitalOcean Spaces connection test."));

                var putRequest = new PutObjectRequest
                {
                    BucketName = _options.Container,
                    Key = testKey,
                    InputStream = stream,
                    ContentType = "text/plain",
                    CannedACL = S3CannedACL.Private
                };

                var putResponse = await _s3.PutObjectAsync(putRequest);

                results.Add(
                    $"PASS - PutObject: {putResponse.HttpStatusCode}");
            }
            catch (AmazonS3Exception ex)
            {
                _logger.LogError(
                    ex,
                    "PutObject test failed. Bucket={Bucket}, Key={Key}, Status={Status}, ErrorCode={ErrorCode}",
                    _options.Container,
                    testKey,
                    ex.StatusCode,
                    ex.ErrorCode);

                results.Add(
                    $"FAIL - PutObject: Status={ex.StatusCode}, ErrorCode={ex.ErrorCode}, Message={ex.Message}");

                return Content(string.Join(Environment.NewLine, results));
            }

            // 3. Test delete
            try
            {
                var deleteResponse = await _s3.DeleteObjectAsync(
                    new DeleteObjectRequest
                    {
                        BucketName = _options.Container,
                        Key = testKey
                    });

                results.Add(
                    $"PASS - DeleteObject: {deleteResponse.HttpStatusCode}");
            }
            catch (AmazonS3Exception ex)
            {
                _logger.LogError(
                    ex,
                    "DeleteObject test failed. Bucket={Bucket}, Key={Key}, Status={Status}, ErrorCode={ErrorCode}",
                    _options.Container,
                    testKey,
                    ex.StatusCode,
                    ex.ErrorCode);

                results.Add(
                    $"FAIL - DeleteObject: Status={ex.StatusCode}, ErrorCode={ex.ErrorCode}, Message={ex.Message}");
            }

            return Content(string.Join(Environment.NewLine, results));
        }
    }
}
