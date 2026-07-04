using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using OpenCvSharp;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace MoodStabilizer
{
    public class FaceAuthenticator
    {
        private CascadeClassifier _faceCascade;
        private InferenceSession _arcFaceSession;

        public FaceAuthenticator()
        {
            // Dynamically find the absolute path to the Models folder
            string assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string assemblyDir = Path.GetDirectoryName(assemblyPath) ?? AppDomain.CurrentDomain.BaseDirectory;
            string projectRoot = Path.GetFullPath(Path.Combine(assemblyDir, "..", "..", ".."));
            string modelsFolder = Path.Combine(projectRoot, "models");

            string haarPath = Path.Combine(modelsFolder, "haarcascade_frontalface_default.xml");
            string arcFacePath = Path.Combine(modelsFolder, "arcface.onnx");

            if (!File.Exists(haarPath))
                throw new FileNotFoundException($"Haar Cascade file not found at: {haarPath}");

            if (!File.Exists(arcFacePath))
                throw new FileNotFoundException($"ArcFace model not found at: {arcFacePath}");

            // 1. Load the Haar Cascade for finding the face box
            _faceCascade = new CascadeClassifier(haarPath);

            // 2. Load the Embedding model (e.g. ArcFace) to generate the signature
            _arcFaceSession = new InferenceSession(arcFacePath);
        }

        public float[]? GetFaceEmbeddingFromCamera()
        {
            using var capture = new VideoCapture(0); // 0 = Default webcam
            using var frame = new Mat();

            capture.Read(frame);
            if (frame.Empty()) return null;

            // Step 1: Detect Face
            using var grayFrame = new Mat();
            Cv2.CvtColor(frame, grayFrame, ColorConversionCodes.BGR2GRAY);

            Rect[] faces = _faceCascade.DetectMultiScale(
                grayFrame,
                scaleFactor: 1.1,
                minNeighbors: 5,
                minSize: new OpenCvSharp.Size(30, 30));

            if (faces.Length == 0) return null;

            // Step 2: Draw the box just for visual feedback (The "Cool Box")
            Rect largestFace = faces.OrderByDescending(f => f.Width * f.Height).First();
            Cv2.Rectangle(frame, largestFace, Scalar.Green, 2);

            // Step 3: Crop the face and generate embedding
            using Mat croppedFace = new Mat(frame, largestFace);
            return GenerateEmbedding(croppedFace);
        }

        private float[] GenerateEmbedding(Mat faceImage)
        {
            // 1. ArcFace expects 112x112 images
            using var resized = new Mat();
            Cv2.Resize(faceImage, resized, new OpenCvSharp.Size(112, 112));

            // 2. Convert to RGB
            using var rgb = new Mat();
            Cv2.CvtColor(resized, rgb, ColorConversionCodes.BGR2RGB);

            // 3. Normalize image from [0, 255] to [-1, 1] (Standard for ArcFace)
            rgb.ConvertTo(rgb, MatType.CV_32FC3, 1.0f / 127.5f, -1.0f);

            // 4. Extract pixels into a flat array for the tensor 
            // Model expects shape [1, 112, 112, 3] (Channels Last)
            float[] inputData = new float[112 * 112 * 3];

            unsafe
            {
                float* ptr = (float*)rgb.DataPointer;
                int idx = 0;

                for (int h = 0; h < 112; h++)
                {
                    for (int w = 0; w < 112; w++)
                    {
                        for (int c = 0; c < 3; c++)
                        {
                            // OpenCV inherently stores data interleaved (RGB RGB) which matches Channels Last perfectly!
                            inputData[idx++] = ptr[(h * 112 + w) * 3 + c];
                        }
                    }
                }
            }

            // 5. Create tensor and run inference with correct dimensions [1, 112, 112, 3]
            var inputTensor = new DenseTensor<float>(inputData, new[] { 1, 112, 112, 3 });
            
            // Automatically grab the correct input name from the loaded ONNX model metadata
            string inputName = _arcFaceSession.InputMetadata.Keys.First();
            
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(inputName, inputTensor)
            };

            using var results = _arcFaceSession.Run(inputs);
            var embedding = results.First().AsTensor<float>().ToArray();

            return embedding;
        }
    }
}