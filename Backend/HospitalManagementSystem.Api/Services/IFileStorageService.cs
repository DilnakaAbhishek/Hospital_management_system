using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace HospitalManagementSystem.Api.Services
{
    public interface IFileStorageService
    {
        /// <summary>
        /// Uploads a stream to storage and returns the accessible URL.
        /// </summary>
        Task<string> UploadAsync(
            Stream stream,
            string fileName,
            string contentType,
            string folder = "medical-records",
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Uploads raw byte array to storage and returns the accessible URL.
        /// </summary>
        Task<string> UploadBytesAsync(
            byte[] bytes,
            string fileName,
            string contentType,
            string folder = "medical-records",
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Downloads raw byte array of a file from storage given its public or relative URL or object key.
        /// </summary>
        Task<byte[]?> DownloadBytesAsync(string fileUrl, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a file from storage given its public or relative URL.
        /// </summary>
        Task<bool> DeleteAsync(string fileUrl, CancellationToken cancellationToken = default);
    }
}
