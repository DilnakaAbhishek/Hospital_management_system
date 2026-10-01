using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HospitalManagementSystem.Api.Services
{
    public class CloudflareR2StorageService : IFileStorageService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<CloudflareR2StorageService> _logger;

        private readonly string _accountId;
        private readonly string _bucketName;
        private readonly string _accessKeyId;
        private readonly string _secretAccessKey;
        private readonly string _publicDomain;
        private readonly bool _isConfigured;

        public CloudflareR2StorageService(
            IConfiguration configuration,
            ILogger<CloudflareR2StorageService> logger)
        {
            _configuration = configuration;
            _logger = logger;

            var section = _configuration.GetSection("CloudflareR2");
            _accountId = (section["AccountId"] ?? string.Empty).Trim();
            _bucketName = (section["BucketName"] ?? "hospital-medical-records").Trim();
            _accessKeyId = (section["AccessKeyId"] ?? string.Empty).Trim();
            _secretAccessKey = (section["SecretAccessKey"] ?? string.Empty).Trim();
            _publicDomain = (section["PublicDomain"] ?? string.Empty).Trim().TrimEnd('/');

            _isConfigured = !string.IsNullOrWhiteSpace(_accountId) &&
                            !string.IsNullOrWhiteSpace(_accessKeyId) &&
                            !string.IsNullOrWhiteSpace(_secretAccessKey) &&
                            !_accessKeyId.Contains("YOUR_", StringComparison.OrdinalIgnoreCase);

            if (_isConfigured)
            {
                _logger.LogInformation("Cloudflare R2 Storage initialized for bucket '{BucketName}'.", _bucketName);
            }
            else
            {
                _logger.LogWarning("Cloudflare R2 Storage credentials are not fully configured in appsettings.json.");
            }
        }

        private IAmazonS3 CreateS3Client()
        {
            if (!_isConfigured)
            {
                throw new InvalidOperationException("Cloudflare R2 is not configured. Please provide AccountId, AccessKeyId, and SecretAccessKey in appsettings.json.");
            }

            var credentials = new BasicAWSCredentials(_accessKeyId, _secretAccessKey);
            var config = new AmazonS3Config
            {
                ServiceURL = $"https://{_accountId}.r2.cloudflarestorage.com",
                ForcePathStyle = true
            };

            return new AmazonS3Client(credentials, config);
        }

        public async Task<string> UploadAsync(
            Stream stream,
            string fileName,
            string contentType,
            string folder = "medical-records",
            CancellationToken cancellationToken = default)
        {
            var safeOriginalName = Path.GetFileName(fileName);
            if (string.IsNullOrWhiteSpace(safeOriginalName))
            {
                safeOriginalName = "attachment.bin";
            }

            var cleanFolder = string.IsNullOrWhiteSpace(folder) ? "medical-records" : folder.Trim('/');
            var uniqueFileName = $"{Guid.NewGuid():N}_{safeOriginalName}";
            var objectKey = $"{cleanFolder}/{uniqueFileName}";

            using var client = CreateS3Client();

            Stream uploadStream = stream;
            MemoryStream? memStream = null;
            if (!stream.CanSeek)
            {
                memStream = new MemoryStream();
                await stream.CopyToAsync(memStream, cancellationToken);
                memStream.Position = 0;
                uploadStream = memStream;
            }
            else if (stream.Position != 0)
            {
                stream.Position = 0;
            }

            var putRequest = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = objectKey,
                InputStream = uploadStream,
                ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
                DisablePayloadSigning = true
            };

            await client.PutObjectAsync(putRequest, cancellationToken);
            memStream?.Dispose();

            _logger.LogInformation("Successfully uploaded {ObjectKey} to Cloudflare R2 bucket {BucketName}", objectKey, _bucketName);

            // Construct accessible CDN URL
            if (!string.IsNullOrWhiteSpace(_publicDomain))
            {
                return $"{_publicDomain}/{objectKey}";
            }

            return $"https://{_bucketName}.{_accountId}.r2.cloudflarestorage.com/{objectKey}";
        }

        public async Task<string> UploadBytesAsync(
            byte[] bytes,
            string fileName,
            string contentType,
            string folder = "medical-records",
            CancellationToken cancellationToken = default)
        {
            using var ms = new MemoryStream(bytes);
            return await UploadAsync(ms, fileName, contentType, folder, cancellationToken);
        }

        public async Task<byte[]?> DownloadBytesAsync(string fileUrl, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(fileUrl)) return null;

            // Handle data URI if stored directly
            if (fileUrl.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                var commaIndex = fileUrl.IndexOf(',');
                if (commaIndex > 0)
                {
                    try
                    {
                        return Convert.FromBase64String(fileUrl[(commaIndex + 1)..]);
                    }
                    catch
                    {
                        return null;
                    }
                }
            }

            if (_isConfigured)
            {
                try
                {
                    string objectKey = fileUrl;
                    if (Uri.TryCreate(fileUrl, UriKind.Absolute, out var uri))
                    {
                        objectKey = uri.AbsolutePath.TrimStart('/');
                    }

                    using var client = CreateS3Client();
                    var getRequest = new GetObjectRequest
                    {
                        BucketName = _bucketName,
                        Key = objectKey
                    };

                    using var response = await client.GetObjectAsync(getRequest, cancellationToken);
                    using var ms = new MemoryStream();
                    await response.ResponseStream.CopyToAsync(ms, cancellationToken);
                    return ms.ToArray();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to download file from Cloudflare R2 bucket: {FileUrl}, attempting HTTP fallback", fileUrl);
                }
            }

            // Fallback 1: Download from public HTTP/HTTPS URL
            if (fileUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                fileUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                    return await httpClient.GetByteArrayAsync(fileUrl, cancellationToken);
                }
                catch (Exception httpEx)
                {
                    _logger.LogWarning(httpEx, "Failed to download file via HTTP fallback from {FileUrl}", fileUrl);
                }
            }

            // Fallback 2: Check local disk storage (e.g. wwwroot or relative path)
            try
            {
                var cleanRel = fileUrl.TrimStart('/');
                var candidatePaths = new[]
                {
                    Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", cleanRel),
                    Path.Combine(AppContext.BaseDirectory, "wwwroot", cleanRel),
                    Path.Combine(Directory.GetCurrentDirectory(), cleanRel)
                };
                foreach (var path in candidatePaths)
                {
                    if (File.Exists(path))
                    {
                        return await File.ReadAllBytesAsync(path, cancellationToken);
                    }
                }
            }
            catch { }

            return null;
        }

        public async Task<bool> DeleteAsync(string fileUrl, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(fileUrl) || !_isConfigured) return false;

            try
            {
                string objectKey = fileUrl;
                if (Uri.TryCreate(fileUrl, UriKind.Absolute, out var uri))
                {
                    objectKey = uri.AbsolutePath.TrimStart('/');
                }

                using var client = CreateS3Client();
                var deleteRequest = new DeleteObjectRequest
                {
                    BucketName = _bucketName,
                    Key = objectKey
                };

                await client.DeleteObjectAsync(deleteRequest, cancellationToken);
                _logger.LogInformation("Deleted object {ObjectKey} from Cloudflare R2 bucket {BucketName}", objectKey, _bucketName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete file from Cloudflare R2: {FileUrl}", fileUrl);
                return false;
            }
        }
    }
}
