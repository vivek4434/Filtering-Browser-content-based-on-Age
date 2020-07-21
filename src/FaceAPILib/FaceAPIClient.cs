using Microsoft.Azure.CognitiveServices.Vision.Face;
using System;
using System.Collections.Generic;
using System.Text;

namespace FaceAPILib
{
    public abstract class FaceAPIClient
    {
        public abstract IFaceClient AuthenticateToStorageService(string endpoint, string key);

        public abstract string GetImageUrl(string imageName);

        public abstract bool SaveImage(CaptureImage image);
    }
}
