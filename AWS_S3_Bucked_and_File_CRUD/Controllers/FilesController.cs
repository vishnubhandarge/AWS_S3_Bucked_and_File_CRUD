using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using AWS_S3_Bucked_and_File_CRUD.Models;
using Microsoft.AspNetCore.Mvc;
namespace AWS_S3_Bucked_and_File_CRUD
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class FilesController : ControllerBase
    {
        private readonly IAmazonS3 _BucketFile;
        private readonly string _defaultRegion;
        private readonly int _presignExpiryMinutes;

        public FilesController(IAmazonS3 BucketFile, IConfiguration config)
        {
            _BucketFile = BucketFile;
            _defaultRegion = config["AWS:Region"] ?? "ap-south-1";
            _presignExpiryMinutes = config.GetValue<int>("AWS:PresignExpiryMinutes", 10);  // Default 10 min
        }

        [HttpPost]
        public async Task<IActionResult> UploadBucketFileAsyns(IFormFile file, string bucketName, string? prefix)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file provided.");

            var IsBucketExist = await AmazonS3Util.DoesS3BucketExistV2Async(_BucketFile, bucketName);
            if (!IsBucketExist)
                return NotFound($"Bucket {bucketName} not found.");

            var key = string.IsNullOrEmpty(prefix) ? file.FileName : $"{prefix.TrimEnd('/')}/{file.FileName}";
            var request = new PutObjectRequest
            {
                BucketName = bucketName,
                Key = key,
                InputStream = file.OpenReadStream(),
            };
            request.Metadata.Add("Content-Type", file.ContentType ?? "application/octet-stream");

            var result = await _BucketFile.PutObjectAsync(request);

            if (result.HttpStatusCode != System.Net.HttpStatusCode.OK)
                return BadRequest("Upload failed.");

            return Ok($"File {key} uploaded successfully.");
        }

        [HttpGet]
        public async Task<IActionResult> GetBucketFilesAsyns(string bucketName, string prefix)
        {
            var IsBucketExist = await AmazonS3Util.DoesS3BucketExistV2Async(_BucketFile, bucketName);
            if (!IsBucketExist)
                return NotFound($"Bucket {bucketName} not found.");

            var request = new ListObjectsV2Request
            {
                BucketName = bucketName,
                Prefix = prefix
            };
            var result = await _BucketFile.ListObjectsV2Async(request);

            if (result.S3Objects == null || !result.S3Objects.Any())
                return NotFound("No files found.");

            var files = result.S3Objects.Select(s =>
            {
                var urlRequest = new GetPreSignedUrlRequest
                {
                    BucketName = bucketName,
                    Key = s.Key,
                    Expires = DateTime.UtcNow.AddMinutes(_presignExpiryMinutes)
                };
                return new S3ObjectDTO
                {
                    Name = s.Key,
                    PresignedURL = _BucketFile.GetPreSignedURL(urlRequest)
                };
            });

            return Ok(files);
        }

        [HttpGet]
        public async Task<IActionResult> GetBucketFileByKeyAsync(string bucketName, string key)
        {
            var bucketExists = await AmazonS3Util.DoesS3BucketExistV2Async(_BucketFile, bucketName);
            if (!bucketExists)
                return NotFound($"Bucket {bucketName} does not exist.");

            try
            {
                var s3Object = await _BucketFile.GetObjectAsync(bucketName, key);
                return File(s3Object.ResponseStream, s3Object.Headers.ContentType ?? "application/octet-stream");
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return NotFound($"File {key} not found.");
            }
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteFileAsync(string bucketName, string key)
        {
            var bucketExists = await AmazonS3Util.DoesS3BucketExistV2Async(_BucketFile, bucketName);
            if (!bucketExists)
                return NotFound($"Bucket {bucketName} does not exist");

            await _BucketFile.DeleteObjectAsync(bucketName, key);
            return NoContent();
        }
    }
}
