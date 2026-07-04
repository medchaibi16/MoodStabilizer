using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.VisualBasic.Devices;
using OpenCvSharp;
using System;
using System.IO;
using System.Linq;
using NAudio.Wave;

namespace MoodStabilizer
{
    public class EmotionRecognizer : IDisposable
    {
        private string _videoPath;
        private InferenceSession? _audioSession;
        private InferenceSession? _faceSession;

        private readonly string MODELS_FOLDER;

        private const string AUDIO_MODEL_NAME = "emotion_model.onnx";
        private const string FACE_MODEL_NAME = "model.onnx";

        // Emotion class labels (from your training)
        private readonly string[] EMOTION_LABELS = { "anger", "frustration", "excited", "neutral", "sadness", "happiness" };

        // Alpha weighting: 0.7 = 70% audio, 30% face
        private const float ALPHA = 0.7f;

        // Audio config (from your training)
        private const int AUDIO_SAMPLE_RATE = 16000;
        private const int AUDIO_SAMPLES_PER_5SEC = 80000;  // 5 seconds @ 16kHz
        private const int AUDIO_SAMPLES_PADDED = 160000;   // Pad to 10 seconds (what model was trained on)

        // Video config (from your training)
        private const int VIDEO_FRAMES_PER_CLIP = 16;
        private const int FRAME_WIDTH = 224;
        private const int FRAME_HEIGHT = 224;

        // ImageNet normalization (from your training)
        private readonly float[] IMAGENET_MEAN = { 0.485f, 0.456f, 0.406f };
        private readonly float[] IMAGENET_STD = { 0.229f, 0.224f, 0.225f };

        public EmotionRecognizer(string videoPath)
        {
            _videoPath = videoPath;
            MODELS_FOLDER = GetModelsFolder();
            
            Console.WriteLine("\n" + new string('=', 60));
            Console.WriteLine("Emotion Recognition System - Initialization");
            Console.WriteLine(new string('=', 60));
        }

        /// <summary>
        /// Get the models folder path relative to the          mbly location
        /// </summary>
        private static string GetModelsFolder()
        {
            string assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string assemblyDir = Path.GetDirectoryName(assemblyPath) ?? AppDomain.CurrentDomain.BaseDirectory;
            string projectRoot = Path.GetFullPath(Path.Combine(assemblyDir, "..", "..", ".."));
            return Path.Combine(projectRoot, "models");
        }

        /// <summary>
        /// Load both ONNX models from the models folder
        /// </summary>
        public bool LoadModels()
        {
            try
            {
                Console.WriteLine("\n[1/2] Loading Audio Model...");
                string audioModelPath = Path.Combine(MODELS_FOLDER, AUDIO_MODEL_NAME);

                if (!File.Exists(audioModelPath))
                {
                    Console.WriteLine($"✗ Audio model not found: {audioModelPath}");
                    return false;
                }

                _audioSession = new InferenceSession(audioModelPath);
                Console.WriteLine($"✓ Audio model loaded: {audioModelPath}");
                PrintModelInfo(_audioSession, "Audio Model");

                Console.WriteLine("\n[2/2] Loading Face Model...");
                string faceModelPath = Path.Combine(MODELS_FOLDER, FACE_MODEL_NAME);

                if (!File.Exists(faceModelPath))
                {
                    Console.WriteLine($"✗ Face model not found: {faceModelPath}");
                    return false;
                }

                _faceSession = new InferenceSession(faceModelPath);
                Console.WriteLine($"✓ Face model loaded: {faceModelPath}");
                PrintModelInfo(_faceSession, "Face Model");

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Error loading models: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Validate video file exists and is readable
        /// </summary>
        public bool ValidateVideo()
        {
            Console.WriteLine($"\n[Loading Video]");
            Console.WriteLine($"Path: {_videoPath}");

            if (!File.Exists(_videoPath))
            {
                Console.WriteLine($"✗ Video file not found");
                return false;
            }

            try
            {
                using (var cap = new VideoCapture(_videoPath))
                {
                    if (!cap.IsOpened())
                    {
                        Console.WriteLine($"✗ Cannot open video file");
                        return false;
                    }

                    int frameCount = (int)cap.Get(VideoCaptureProperties.FrameCount);
                    double fps = cap.Get(VideoCaptureProperties.Fps);
                    int width = (int)cap.Get(VideoCaptureProperties.FrameWidth);
                    int height = (int)cap.Get(VideoCaptureProperties.FrameHeight);
                    double duration = frameCount / fps;

                    Console.WriteLine($"✓ Video loaded successfully");
                    Console.WriteLine($"  Frames: {frameCount}");
                    Console.WriteLine($"  FPS: {fps}");
                    Console.WriteLine($"  Resolution: {width}x{height}");
                    Console.WriteLine($"  Duration: {duration:F2} seconds");

                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Error reading video: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Extract 16 frames uniformly from a 5-second window
        /// </summary>
        private float[][]? ExtractFrames(VideoCapture cap, int startFrameIndex)
        {
            float[][] frames = new float[VIDEO_FRAMES_PER_CLIP][];

            try
            {
                int framesPerStep = 1;  // Start simple: take every frame if we have exactly 16 frames in 5 seconds

                for (int i = 0; i < VIDEO_FRAMES_PER_CLIP; i++)
                {
                    int frameIndex = startFrameIndex + (i * framesPerStep);
                    cap.Set(VideoCaptureProperties.PosFrames, frameIndex);

                    Mat frame = new Mat();
                    if (!cap.Read(frame))
                    {
                        Console.WriteLine($"  ⚠ Could not read frame at index {frameIndex}");
                        return null;
                    }

                    // Resize to 224x224
                    Mat resized = new Mat();
                    Cv2.Resize(frame, resized, new OpenCvSharp.Size(FRAME_WIDTH, FRAME_HEIGHT));

                    // Convert BGR to RGB
                    Mat rgb = new Mat();
                    Cv2.CvtColor(resized, rgb, ColorConversionCodes.BGR2RGB);

                    // Normalize to [0, 1]
                    rgb.ConvertTo(rgb, MatType.CV_32F, 1.0 / 255.0);

                    // Extract pixel data and normalize with ImageNet stats
                    float[][] normalized = NormalizeFrame(rgb);
                    
                    // Flatten the 2D array [3][224*224] into 1D [3*224*224]
                    frames[i] = new float[3 * FRAME_WIDTH * FRAME_HEIGHT];
                    int idx = 0;
                    for (int c = 0; c < 3; c++)
                    {
                        for (int p = 0; p < FRAME_WIDTH * FRAME_HEIGHT; p++)
                        {
                            frames[i][idx++] = normalized[c][p];
                        }
                    }

                    frame.Dispose();
                    resized.Dispose();
                    rgb.Dispose();
                }

                return frames;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Error extracting frames: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Normalize frame using ImageNet mean/std
        /// Returns [3][224*224] for C, H, W format
        /// </summary>
        private float[][] NormalizeFrame(Mat frame)
        {
            float[][] normalized = new float[3][];
            for (int c = 0; c < 3; c++)
                normalized[c] = new float[FRAME_WIDTH * FRAME_HEIGHT];

            byte[] data = new byte[frame.Total() * frame.Channels()];
            System.Runtime.InteropServices.Marshal.Copy(frame.Data, data, 0, data.Length);

            int idx = 0;
            for (int h = 0; h < FRAME_HEIGHT; h++)
            {
                for (int w = 0; w < FRAME_WIDTH; w++)
                {
                    // OpenCV reads as BGR when normalized
                    float r = data[idx + 2] / 255.0f;  // R channel
                    float g = data[idx + 1] / 255.0f;  // G channel
                    float b = data[idx + 0] / 255.0f;  // B channel

                    // Apply ImageNet normalization
                    normalized[0][h * FRAME_WIDTH + w] = (r - IMAGENET_MEAN[0]) / IMAGENET_STD[0];
                    normalized[1][h * FRAME_WIDTH + w] = (g - IMAGENET_MEAN[1]) / IMAGENET_STD[1];
                    normalized[2][h * FRAME_WIDTH + w] = (b - IMAGENET_MEAN[2]) / IMAGENET_STD[2];

                    idx += 3;
                }
            }

            return normalized;
        }

        /// <summary>
        /// Print ONNX model input/output information
        /// </summary>
        private void PrintModelInfo(InferenceSession session, string modelName)
        {
            Console.WriteLine($"\n  {modelName} Info:");
            Console.WriteLine($"  Inputs:");
            foreach (var input in session.InputMetadata)
            {
                Console.WriteLine($"    - {input.Key}: [{string.Join(", ", input.Value.Dimensions)}]");
            }
            Console.WriteLine($"  Outputs:");
            foreach (var output in session.OutputMetadata)
            {
                Console.WriteLine($"    - {output.Key}: [{string.Join(", ", output.Value.Dimensions)}]");
            }
        }

        /// <summary>
        /// Process 5-second window: extract frames + run face model
        /// </summary>
        public float[]? ProcessVideoWindow(VideoCapture cap, int startFrameIndex)
        {
            try
            {
                Console.WriteLine($"\n  Extracting 16 frames starting from frame {startFrameIndex}...");
                float[][]? frames = ExtractFrames(cap, startFrameIndex);

                if (frames == null)
                    return null;

                Console.WriteLine($"  ✓ Frames extracted");

                // Create input tensor: [1, 16, 3, 224, 224]
                // frames is [16][3*224*224] - need to reshape to [1, 16, 3, 224*224]
                float[,,,] input = new float[1, VIDEO_FRAMES_PER_CLIP, 3, FRAME_WIDTH * FRAME_HEIGHT];

                for (int t = 0; t < VIDEO_FRAMES_PER_CLIP; t++)
                {
                    // frames[t] is float[3*224*224], need to split into 3 channels
                    for (int c = 0; c < 3; c++)
                    {
                        for (int p = 0; p < FRAME_WIDTH * FRAME_HEIGHT; p++)
                        {
                            input[0, t, c, p] = frames[t][c * FRAME_WIDTH * FRAME_HEIGHT + p];
                        }
                    }
                }

                Console.WriteLine($"  Running face model inference...");

                var inputTensor = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor("pixel_values",
                        new DenseTensor<float>(Flatten4D(input), new int[] { 1, 16, 3, 224, 224 }))
                };

                var results = _faceSession?.Run(inputTensor);
                var logits = (results?[0].Value as DenseTensor<float>)?.ToArray();

                Console.WriteLine($"  ✓ Face inference complete");

                return logits;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Error processing video window: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Flatten 4D array to 1D for tensor
        /// </summary>
        private float[] Flatten4D(float[,,,] arr)
        {
            int total = arr.Length;
            float[] result = new float[total];
            int idx = 0;
            for (int i = 0; i < arr.GetLength(0); i++)
                for (int j = 0; j < arr.GetLength(1); j++)
                    for (int k = 0; k < arr.GetLength(2); k++)
                        for (int l = 0; l < arr.GetLength(3); l++)
                            result[idx++] = arr[i, j, k, l];
            return result;
        }

        public void Dispose()
        {
            _audioSession?.Dispose();
            _faceSession?.Dispose();
            _cachedAudioReader?.Dispose();
        }
        private string? _cachedAudioPath;
        private WaveFileReader? _cachedAudioReader;

        /// <summary>
        /// Extract audio chunk and prepare for inference (from your training pipeline)
        /// </summary>
        public float[]? ProcessAudioWindow(string videoPath, int windowIndex)
        {
            try
            {
                Console.WriteLine($"\n  Extracting audio for window {windowIndex}...");

                // Extract audio only once and cache it
                if (_cachedAudioReader == null || _cachedAudioPath != videoPath)
                {
                    _cachedAudioPath = videoPath;
                    
                    // Use FFmpeg to extract audio from video
                    string tempAudioPath = Path.GetTempFileName() + ".wav";

                    // Extract audio using FFmpeg command
                    var startInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "ffmpeg",
                        Arguments = $"-i \"{videoPath}\" -acodec pcm_s16le -ar 16000 -ac 1 \"{tempAudioPath}\" -y",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    using (var process = System.Diagnostics.Process.Start(startInfo))
                    {
                        process?.WaitForExit();
                    }

                    if (!File.Exists(tempAudioPath))
                    {
                        Console.WriteLine($"  ✗ Failed to extract audio");
                        return null;
                    }

                    _cachedAudioReader = new WaveFileReader(tempAudioPath);
                }

                // Now read audio samples for this window
                using (var soundBuffer = new NAudio.Wave.RawSourceWaveStream(_cachedAudioReader, _cachedAudioReader.WaveFormat))
                {
                    int sampleRate = _cachedAudioReader.WaveFormat.SampleRate;

                    if (sampleRate != AUDIO_SAMPLE_RATE)
                    {
                        Console.WriteLine($"  ✗ Audio sample rate mismatch: {sampleRate} vs {AUDIO_SAMPLE_RATE}");
                        return null;
                    }

                    // Calculate samples for this 5-second window
                    int samplesPerWindow = (int)(AUDIO_SAMPLE_RATE * 5); // 80000 samples for 5 seconds
                    int bytesPerSample = _cachedAudioReader.WaveFormat.BitsPerSample / 8; // Usually 2 bytes for 16-bit
                    int startBytePosition = windowIndex * samplesPerWindow * bytesPerSample;

                    // Read as bytes first
                    byte[] audioBytes = new byte[samplesPerWindow * bytesPerSample];
                    _cachedAudioReader.Position = startBytePosition;

                    int bytesRead = _cachedAudioReader.Read(audioBytes, 0, audioBytes.Length);
                    Console.WriteLine($"  ✓ Audio bytes read: {bytesRead}");

                    // Convert bytes to float samples
                    float[] audioSamples = new float[AUDIO_SAMPLES_PADDED]; // Pad to 160000
                    
                    // Convert 16-bit PCM bytes to float [-1, 1]
                    for (int i = 0; i < bytesRead; i += 2)
                    {
                        short sample = BitConverter.ToInt16(audioBytes, i);
                        audioSamples[i / 2] = sample / 32768f; // Normalize to [-1, 1]
                    }

                    return audioSamples;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Error processing audio window: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Run audio model inference (emotion_model.onnx expects input_values)
        /// </summary>
        public float[]? InferenceAudio(float[] audioSamples)
        {
            try
            {
                Console.WriteLine($"  Running audio model inference...");

                var inputTensor = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input",
                new DenseTensor<float>(audioSamples, new int[] { 1, AUDIO_SAMPLES_PADDED }))
        };

                var results = _audioSession?.Run(inputTensor);
                var logits = (results?[0].Value as DenseTensor<float>)?.ToArray();

                Console.WriteLine($"  ✓ Audio inference complete");

                return logits;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Error in audio inference: {ex.Message}");
                return null;
            }
        }

        private float _lastConfidence = 0f;
        private string _lastAudioEmotion = "";
        private string _lastFaceEmotion = "";

        public float GetLastConfidence()
        {
            return _lastConfidence;
        }

        public string GetLastAudioEmotion()
        {
            return _lastAudioEmotion;
        }

        public string GetLastFaceEmotion()
        {
            return _lastFaceEmotion;
        }

        /// <summary>
        /// Fuse audio and face predictions with alpha weighting
        /// </summary>
        public string FusePredictions(float[]? audioLogits, float[]? faceLogits)
        {
            if (audioLogits == null || faceLogits == null)
                return "ERROR";

            // Ensure both have the same number of emotion classes
            if (audioLogits.Length != faceLogits.Length)
            {
                Console.WriteLine($"⚠ Warning: Audio logits length ({audioLogits.Length}) != Face logits length ({faceLogits.Length})");
                Console.WriteLine($"  Audio model may need reshaping. Using only first {Math.Min(audioLogits.Length, faceLogits.Length)} values.");

                // Trim to the smaller length (face model's 6 classes)
                int numClasses = Math.Min(audioLogits.Length, faceLogits.Length);
                System.Array.Resize(ref audioLogits, numClasses);
                System.Array.Resize(ref faceLogits, numClasses);
            }

            // Convert logits to probabilities using softmax
            float[] audioProbs = Softmax(audioLogits);
            float[] faceProbs = Softmax(faceLogits);

            // Get individual predictions before fusion
            int audioPredictedClass = audioProbs.ToList().IndexOf(audioProbs.Max());
            int facePredictedClass = faceProbs.ToList().IndexOf(faceProbs.Max());

            _lastAudioEmotion = EMOTION_LABELS[audioPredictedClass];
            _lastFaceEmotion = EMOTION_LABELS[facePredictedClass];

            // Fuse: p_fused = 0.7 * p_audio + 0.3 * p_face
            float[] fusedProbs = new float[audioProbs.Length];
            for (int i = 0; i < audioProbs.Length; i++)
            {
                fusedProbs[i] = (ALPHA * audioProbs[i]) + ((1 - ALPHA) * faceProbs[i]);
            }

            // Get argmax
            int predictedClass = fusedProbs.ToList().IndexOf(fusedProbs.Max());
            string emotion = EMOTION_LABELS[predictedClass];
            _lastConfidence = fusedProbs[predictedClass];

            // Print probabilities
            Console.WriteLine($"\n  📊 PREDICTIONS:");
            Console.WriteLine($"     Audio probs:  {string.Join(", ", audioProbs.Select(p => p.ToString("F3")))}");
            Console.WriteLine($"     Face probs:   {string.Join(", ", faceProbs.Select(p => p.ToString("F3")))}");
            Console.WriteLine($"     Fused probs:  {string.Join(", ", fusedProbs.Select(p => p.ToString("F3")))}");
            Console.WriteLine($"\n  🎯 PREDICTED EMOTION: {emotion.ToUpper()} (confidence: {fusedProbs[predictedClass]:F3})");
            Console.WriteLine($"     Audio voted: {_lastAudioEmotion.ToUpper()} | Face voted: {_lastFaceEmotion.ToUpper()}");

            return emotion;
        }

        /// <summary>
        /// Softmax function to convert logits to probabilities
        /// </summary>
        private float[] Softmax(float[] logits)
        {
            float max = logits.Max();
            float[] exp = logits.Select(x => (float)Math.Exp(x - max)).ToArray();
            float sum = exp.Sum();
            return exp.Select(x => x / sum).ToArray();
        }
    }
}
