using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using Microsoft.AspNetCore.Mvc;
namespace AWS_S3_Bucked_and_File_CRUD.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class BucketsController : ControllerBase
    {
        private readonly IAmazonS3 _S3Bucket;
        private readonly string _defaultRegion;

        public BucketsController(IAmazonS3 S3Bucket, IConfiguration config)
        {
            _S3Bucket = S3Bucket;
            _defaultRegion = config["AWS:Region"] ?? "ap-south-1";  // From your appsettings.json
        }

        [HttpPost]
        public async Task<IActionResult> CreateBucketAsync(string bucketName)
        {
            var bucketExists = await AmazonS3Util.DoesS3BucketExistV2Async(_S3Bucket, bucketName);
            if (bucketExists)
                return BadRequest($"Bucket {bucketName} already exists.");

            // Add explicit region to PutBucketRequest (bucket names are global, but needs location constraint)
            var putBucketRequest = new PutBucketRequest
            {
                BucketName = bucketName,
                BucketRegion = _defaultRegion  // Use your configured region
            };

            var result = await _S3Bucket.PutBucketAsync(putBucketRequest);

            if (result.HttpStatusCode != System.Net.HttpStatusCode.OK)
                return BadRequest("Something went wrong. Bucket creation failed.");

            return Created("buckets", $"Bucket {bucketName} created in {_defaultRegion}.");
        }

        [HttpGet]
        public async Task<IActionResult> GetAllBuckets()
        {
            var result = await _S3Bucket.ListBucketsAsync();
            if (result.Buckets == null || !result.Buckets.Any())
                return NotFound("No Buckets found");

            var allBuckets = result.Buckets.Select(b => b.BucketName);

            return Ok(allBuckets);
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteBucket(string BucketName)
        {
            // Fix: Await the async method properly
            var result = await _S3Bucket.DeleteBucketAsync(BucketName);

            if (result.HttpStatusCode != System.Net.HttpStatusCode.OK)
                return BadRequest($"Bucket: {BucketName} deletion failed.");

            return Ok($"Bucket {BucketName} deleted successfully.");
        }
    }
}
