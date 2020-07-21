using Microsoft.Azure.CognitiveServices.Vision.Face;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Auth;
using Microsoft.Azure.Storage.Blob;

namespace FaceAPILib
{
    public class AzureBlobStorage : FaceAPIClient
    {
        public const string BlobStorageAccountName = "storage_account_name";

        public const string BlobStorageAccountKey = "storage-_account_key";

        public const string BlobName = "blob_name";

        public override IFaceClient AuthenticateToStorageService(string endpoint, string key)
        {
            return new FaceClient(new ApiKeyServiceClientCredentials(key)) { Endpoint = endpoint };
        }

        public override string GetImageUrl(string image)
        {
            var storageAccount = new CloudStorageAccount(new StorageCredentials(BlobStorageAccountName, BlobStorageAccountKey), true);
            return storageAccount
                .CreateCloudBlobClient()
                .GetContainerReference(BlobName)
                .GetBlockBlobReference(image)
                .Uri
                .AbsoluteUri;
        }

        public override string SaveImage(CaptureImage Image)
        {
            //TODO
            throw new System.NotImplementedException();
        }
    }
}
