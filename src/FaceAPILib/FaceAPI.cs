using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.CognitiveServices.Vision.Face;
using Microsoft.Azure.CognitiveServices.Vision.Face.Models;

namespace FaceAPILib
{
    public class FaceAPI
    {
        private string SUBSCRIPTION_KEY = Environment.GetEnvironmentVariable("FACE_SUBSCRIPTION_KEY");
        private string ENDPOINT = Environment.GetEnvironmentVariable("FACE_ENDPOINT");

        AzureBlobStorage blobhelper = new AzureBlobStorage();
        private string IMAGE_BASE_URL;
        public IFaceClient faceAPIClient;

        public FaceAPI(string imageName)
        {
            IMAGE_BASE_URL = blobhelper.GetImageUrl(imageName);
            faceAPIClient = blobhelper.AuthenticateToStorageService(ENDPOINT, SUBSCRIPTION_KEY);
        }

        const string RECOGNITION_MODEL2 = RecognitionModel.Recognition02;
        const string ECOGNITION_MODEL1 = RecognitionModel.Recognition01;

        public int GetAge()
        {
            return DetectFaceExtract(faceAPIClient, IMAGE_BASE_URL, RECOGNITION_MODEL2)
                .GetAwaiter()
                .GetResult();
        }

        public static async Task<int> DetectFaceExtract(IFaceClient client, string url, string recognitionModel)
        {
            Console.WriteLine("========DETECT FACES========");
            Console.WriteLine();

            // Create a list of images
            List<string> imageFileNames = new List<string>
                    {
                        "detection1.jpg",    // single female with glasses
                        // "detection2.jpg", // (optional: single man)
                        // "detection3.jpg", // (optional: single male construction worker)
                        // "detection4.jpg", // (optional: 3 people at cafe, 1 is blurred)
                        "detection5.jpg",    // family, woman child man
                        "detection6.jpg"     // elderly couple, male female
                    };

            foreach (var imageFileName in imageFileNames)
            {
                IList<DetectedFace> detectedFaces;

                // Detect faces with all attributes from image url.
                detectedFaces = await client.Face.DetectWithUrlAsync($"{url}{imageFileName}",
                        returnFaceAttributes: new List<FaceAttributeType> { FaceAttributeType.Accessories, FaceAttributeType.Age},
                        recognitionModel: recognitionModel);
                Console.WriteLine($"{detectedFaces.Count} face(s) detected from image `{imageFileName}`.");
                return (int)detectedFaces[0].FaceAttributes.Age;
            }

            return 0;
        }
    }
}