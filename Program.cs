using OpenCvSharp;
using System;
using System.IO;
using System.Windows.Forms;
using static MoodStabilizer.SmartHomeDecisionEngine;

namespace MoodStabilizer
{
    public static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Console.WriteLine("🎯 MOOD STABILIZER - SMART HOME EMOTION RECOGNITION SYSTEM\n");

            // Initialize shared components once
            var emotionRecognizer = new EmotionRecognizer("");
            if (!emotionRecognizer.LoadModels())
            {
                Console.WriteLine("Failed to load models!");
                return;
            }

            // SPECIFIC VIDEO PATH FOR TESTING
            string videoPath = @"C:\Users\mohamed chaibi\Downloads\pfe\ActuallApp\MoodStabilizer\MoodStabilizer\assets\DivX\Ses01F_impro01.avi";

            Console.WriteLine($"\n📹 Processing Video: {Path.GetFileName(videoPath)}");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");

            // Process the single video
            ProcessSingleVideo(videoPath, emotionRecognizer);

            Console.WriteLine("\n✨ Video processing complete. Press any key to exit...");
            Console.ReadKey();
        }

        static void ProcessSingleVideo(string videoPath, EmotionRecognizer recognizer)
        {
            try
            {
                // Create a temporary recognizer with the specific video path
                var videoRecognizer = new EmotionRecognizer(videoPath);
                if (!videoRecognizer.LoadModels())
                {
                    Console.WriteLine("Failed to load models for video processing!");
                    return;
                }

                // Validate video
                if (!videoRecognizer.ValidateVideo())
                {
                    Console.WriteLine("Video validation failed!");
                    return;
                }

                using (var cap = new VideoCapture(videoPath))
                {
                    if (!cap.IsOpened())
                    {
                        Console.WriteLine("Cannot open video capture!");
                        return;
                    }

                    int totalFrames = (int)cap.Get(VideoCaptureProperties.FrameCount);
                    double fps = cap.Get(VideoCaptureProperties.Fps);
                    double durationSeconds = totalFrames / fps;

                    Console.WriteLine($"\n🎬 Video Analysis Started");
                    Console.WriteLine($"   Total Duration: {durationSeconds:F2} seconds");
                    Console.WriteLine($"   Processing in 5-second windows...\n");

                    // Process every 5-second window
                    int windowDurationFrames = (int)(5 * fps); // 5 seconds worth of frames
                    int numWindows = Math.Max(1, totalFrames / windowDurationFrames);

                    for (int window = 0; window < numWindows; window++)
                    {
                        int startFrame = window * windowDurationFrames;
                        double startTime = startFrame / fps;

                        Console.WriteLine($"┌─────────────────────────────────────────");
                        Console.WriteLine($"│ Window {window + 1}/{numWindows} (Start time: {startTime:F2}s)");
                        Console.WriteLine($"├─────────────────────────────────────────");

                        // Process video (face) for this window
                        float[]? faceLogits = videoRecognizer.ProcessVideoWindow(cap, startFrame);

                        // Process audio for this window
                        float[]? audioSamples = videoRecognizer.ProcessAudioWindow(videoPath, window);
                        float[]? audioLogits = null;

                        if (audioSamples != null)
                        {
                            audioLogits = videoRecognizer.InferenceAudio(audioSamples);
                        }

                        // Fuse predictions if both modalities are available
                        if (faceLogits != null && audioLogits != null)
                        {
                            string emotion = videoRecognizer.FusePredictions(audioLogits, faceLogits);
                            Console.WriteLine($"└─ ✅ Result: {emotion.ToUpper()} (Confidence: {videoRecognizer.GetLastConfidence():F2%})");
                        }
                        else if (faceLogits != null)
                        {
                            // Only face available
                            var faceProbs = Softmax(faceLogits);
                            int predictedClass = Array.IndexOf(faceProbs, faceProbs.Max());
                            string[] emotionLabels = { "anger", "frustration", "excited", "neutral", "sadness", "happiness" };
                            Console.WriteLine($"└─ ✅ Result (Face only): {emotionLabels[predictedClass].ToUpper()}");
                        }
                        else
                        {
                            Console.WriteLine($"└─ ❌ Failed to process this window");
                        }

                        Console.WriteLine();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing video: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        static float[] Softmax(float[] logits)
        {
            float max = logits.Max();
            float[] exp = logits.Select(x => (float)Math.Exp(x - max)).ToArray();
            float sum = exp.Sum();
            return exp.Select(x => x / sum).ToArray();
        }
    }
}