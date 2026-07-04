using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using OpenCvSharp;

namespace MoodStabilizer
{
    public class ExperimentSimulator
    {
        private readonly EmotionRecognizer _emotionRecognizer;
        private readonly FootstepPatternGenerator _footstepGenerator;
        private readonly DoorSlamDetector _doorSlamDetector;
        private readonly EmotionalContextAggregator _contextAggregator;
        private readonly SmartHomeDecisionEngine _decisionEngine;

        private string _assetsFolder;
        private List<string> _videoClips;
        private List<string> _doorAudioFiles;
        private List<ExperimentFrame> _experimentFrames;
        private Random _random;

        public ExperimentSimulator(
            EmotionRecognizer emotionRecognizer,
            FootstepPatternGenerator footstepGenerator,
            DoorSlamDetector doorSlamDetector,
            UserProfile userProfile,
            string assetsFolder)
        {
            _emotionRecognizer = emotionRecognizer;
            _footstepGenerator = footstepGenerator;
            _doorSlamDetector = doorSlamDetector;
            _contextAggregator = new EmotionalContextAggregator(
                emotionRecognizer,
                footstepGenerator,
                doorSlamDetector);
            _decisionEngine = new SmartHomeDecisionEngine(userProfile);

            _assetsFolder = assetsFolder;
            _videoClips = new List<string>();
            _doorAudioFiles = new List<string>();
            _experimentFrames = new List<ExperimentFrame>();
            _random = new Random(42);

            LoadAvailableAssets();
        }

        private void LoadAvailableAssets()
        {
            Console.WriteLine($"\n[ExperimentSimulator] Scanning for video assets in DivX folder...\n");

            // Scan DivX folder dynamically for ALL .avi files
            string divXPath = @"C:\Users\mohamed chaibi\Downloads\pfe\ActuallApp\MoodStabilizer\MoodStabilizer\assets\DivX";
            
            _videoClips.Clear();

            if (!Directory.Exists(divXPath))
            {
                Console.WriteLine($"❌ DivX folder not found: {divXPath}\n");
                return;
            }

            // Get ALL .avi files, sort alphabetically, and FILTER OUT macOS system files (starting with ._)
            var allVideoFiles = Directory.GetFiles(divXPath, "*.avi")
                .Where(f => !Path.GetFileName(f).StartsWith("._"))  // ✅ FILTER OUT macOS metadata files
                .OrderBy(f => Path.GetFileName(f))
                .ToList();

            if (allVideoFiles.Count == 0)
            {
                Console.WriteLine($"❌ No .avi files found in: {divXPath}\n");
                return;
            }

            _videoClips = allVideoFiles;

            Console.WriteLine($"✓ Found {_videoClips.Count} video files in DivX folder:\n");
            for (int i = 0; i < _videoClips.Count; i++)
            {
                Console.WriteLine($"  [{i}] {Path.GetFileName(_videoClips[i])}");
            }
            Console.WriteLine();

            if (_videoClips.Count > 0)
            {
                Console.WriteLine($"\n✓ Loaded {_videoClips.Count} video files\n");
            }
            else
            {
                Console.WriteLine("\n❌ No video files found!\n");
            }

            // Find door audio files
            string[] possibleAudioPaths = new[]
            {
                Path.Combine(_assetsFolder, "audio"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "audio"),
            };

            foreach (var audioBasePath in possibleAudioPaths)
            {
                if (Directory.Exists(audioBasePath))
                {
                    var audioFiles = Directory.GetFiles(audioBasePath, "*.*", SearchOption.AllDirectories)
                        .Where(f => new[] { ".wav", ".mp3", ".flac" }.Contains(Path.GetExtension(f).ToLower()))
                        .ToList();

                    _doorAudioFiles = audioFiles;
                    if (audioFiles.Count > 0)
                    {
                        Console.WriteLine($"✓ Found {audioFiles.Count} door audio files\n");
                    }
                    break;
                }
            }
        }

        public void RunRealExperiment(TimeSpan maxDuration)
        {
            Console.WriteLine("\n" + new string('=', 100));
            Console.WriteLine("🎬 REAL EXPERIMENT - PROCESSING VIDEO FILES");
            Console.WriteLine($"Max Duration: {maxDuration.TotalSeconds} seconds");
            Console.WriteLine(new string('=', 100) + "\n");

            if (_videoClips.Count == 0)
            {
                Console.WriteLine("❌ No video clips found! Please add video files to the assets folder.\n");
                return;
            }

            _footstepGenerator.Reset();
            _experimentFrames.Clear();

            DateTime experimentStartTime = DateTime.Now;
            int frameIndex = 0;

            // Process each video file
            foreach (var videoPath in _videoClips)
            {
                Console.WriteLine($"\n{'=',100}");
                Console.WriteLine($"📹 Processing: {Path.GetFileName(videoPath)}");
                Console.WriteLine($"{'=',100}\n");

                ProcessVideoFile(videoPath, ref frameIndex, experimentStartTime, maxDuration);

                if (_experimentFrames.Count > 0)
                {
                    TimeSpan elapsed = DateTime.Now - experimentStartTime;
                    if (elapsed > maxDuration)
                    {
                        Console.WriteLine($"\n⏱ Max duration reached. Stopping processing.\n");
                        break;
                    }
                }
            }

            Console.WriteLine($"\n[✓] Experiment complete: {_experimentFrames.Count} frames processed\n");
            DisplaySummary();
            ExportResultsToJson(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "experiment_results.json"));
        }

        private void ProcessVideoFile(string videoPath, ref int frameIndex, DateTime experimentStartTime, TimeSpan maxDuration)
        {
            try
            {
                using (var cap = new VideoCapture(videoPath))
                {
                    if (!cap.IsOpened())
                    {
                        Console.WriteLine($"❌ Cannot open video: {videoPath}\n");
                        return;
                    }

                    int totalFrames = (int)cap.Get(VideoCaptureProperties.FrameCount);
                    double fps = cap.Get(VideoCaptureProperties.Fps);
                    double videoDurationSecs = totalFrames / fps;

                    Console.WriteLine($"Total Frames: {totalFrames}");
                    Console.WriteLine($"FPS: {fps}");
                    Console.WriteLine($"Duration: {videoDurationSecs:F2} seconds\n");

                    // Process every 5 seconds worth of frames (extracting from video to analyze)
                    int framesPerWindow = (int)(fps * 5); // 5-second windows
                    int windowIndex = 0;

                    for (int startFrame = 0; startFrame < totalFrames; startFrame += framesPerWindow)
                    {
                        // Check max duration
                        TimeSpan elapsed = DateTime.Now - experimentStartTime;
                        if (elapsed > maxDuration)
                            break;

                        // Extract audio window and get logits
                        float[]? audioLogits = _emotionRecognizer.ProcessAudioWindow(videoPath, windowIndex);

                        // Seek to frame and extract video window
                        cap.Set(VideoCaptureProperties.PosFrames, startFrame);
                        float[]? videoLogits = _emotionRecognizer.ProcessVideoWindow(cap, startFrame);

                        // Fuse predictions
                        string detectedEmotion = "neutral";
                        float confidence = 0.5f;

                        if (audioLogits != null && videoLogits != null)
                        {
                            detectedEmotion = _emotionRecognizer.FusePredictions(audioLogits, videoLogits);
                            confidence = _emotionRecognizer.GetLastConfidence();
                        }

                        // Create footstep data (simulated based on emotion)
                        FootstepData footstepData = _footstepGenerator.GenerateNextStep(
                            detectedEmotion,
                            experimentStartTime.AddSeconds((DateTime.Now - experimentStartTime).TotalSeconds),
                            (int)(windowIndex * 5)
                        );

                        // Create experiment frame
                        var frame = new ExperimentFrame
                        {
                            FrameIndex = frameIndex++,
                            SimulatedTime = experimentStartTime.AddSeconds((DateTime.Now - experimentStartTime).TotalSeconds),
                            FootstepData = footstepData,
                            ProcessedVideoClip = Path.GetFileName(videoPath),
                            EmotionalContext = new EmotionalContext
                            {
                                DetectedEmotion = detectedEmotion,
                                EmotionConfidence = confidence,
                                AudioVote = _emotionRecognizer.GetLastAudioEmotion(),
                                FaceVote = _emotionRecognizer.GetLastFaceEmotion(),
                                FootstepContext = footstepData,
                                FinalConfidenceScore = confidence,
                                BehavioralValidation = confidence > 0.7f ? "good_match" : "moderate_match",
                                Timestamp = experimentStartTime.AddSeconds((DateTime.Now - experimentStartTime).TotalSeconds),
                                AnomalyDetected = confidence < 0.5f
                            },
                            RecordedAt = DateTime.Now
                        };

                        _experimentFrames.Add(frame);

                        Console.WriteLine($"[Window {windowIndex}] Frame {frameIndex - 1}: {detectedEmotion.ToUpper()} " +
                            $"(Confidence: {confidence:F3}, Audio: {_emotionRecognizer.GetLastAudioEmotion()}, Face: {_emotionRecognizer.GetLastFaceEmotion()})");

                        windowIndex++;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error processing video: {ex.Message}\n");
            }
        }

        /// <summary>
        /// Generate emotional clips for ALL videos in the DivX folder
        /// Extracts emotions every 5 seconds with confidence scores
        /// </summary>
        public void GenerateEmotionalClipsForAllVideos()
        {
            Console.WriteLine("\n" + new string('=', 100));
            Console.WriteLine("🎬 GENERATING EMOTIONAL CLIPS - ALL VIDEOS");
            Console.WriteLine(new string('=', 100) + "\n");

            if (_videoClips.Count == 0)
            {
                Console.WriteLine("❌ No video clips found! Please add video files to the assets folder.\n");
                return;
            }

            int totalVideosProcessed = 0;
            int totalClipsGenerated = 0;

            // Process EACH video file
            foreach (var videoPath in _videoClips)
            {
                Console.WriteLine($"\n{'=',100}");
                Console.WriteLine($"📹 Processing: {Path.GetFileName(videoPath)} [{totalVideosProcessed + 1}/{_videoClips.Count}]");
                Console.WriteLine($"{'=',100}\n");

                try
                {
                    List<EmotionalClip> emotionalClips = ProcessSingleVideoForEmotionalClips(videoPath);
                    
                    if (emotionalClips.Count > 0)
                    {
                        // Export clips for this video
                        string videoNameWithoutExt = Path.GetFileNameWithoutExtension(videoPath);
                        string outputFileName = $"emotional_clips_{videoNameWithoutExt}.json";
                        string outputPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, outputFileName);
                        
                        ExportEmotionalClipsToJson(emotionalClips, outputPath);
                        totalClipsGenerated += emotionalClips.Count;
                        totalVideosProcessed++;

                        DisplayEmotionalClipsSummary(emotionalClips);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error processing video {Path.GetFileName(videoPath)}: {ex.Message}\n");
                }
            }

            Console.WriteLine($"\n{'=',100}");
            Console.WriteLine("✅ BATCH PROCESSING COMPLETE");
            Console.WriteLine($"{'=',100}");
            Console.WriteLine($"📊 Summary:");
            Console.WriteLine($"   Total videos processed: {totalVideosProcessed}/{_videoClips.Count}");
            Console.WriteLine($"   Total clips generated: {totalClipsGenerated}");
            Console.WriteLine($"{'=',100}\n");
        }

        /// <summary>
        /// Process a single video file and return emotional clips
        /// </summary>
        private List<EmotionalClip> ProcessSingleVideoForEmotionalClips(string videoPath)
        {
            List<EmotionalClip> emotionalClips = new();

            try
            {
                using (var cap = new VideoCapture(videoPath))
                {
                    if (!cap.IsOpened())
                    {
                        Console.WriteLine($"❌ Cannot open video: {videoPath}\n");
                        return emotionalClips;
                    }

                    int totalFrames = (int)cap.Get(VideoCaptureProperties.FrameCount);
                    double fps = cap.Get(VideoCaptureProperties.Fps);
                    double videoDurationSecs = totalFrames / fps;

                    Console.WriteLine($"Total Frames: {totalFrames}");
                    Console.WriteLine($"FPS: {fps}");
                    Console.WriteLine($"Duration: {videoDurationSecs:F2} seconds\n");
                    Console.WriteLine("Extracting emotions every 5 seconds...\n");

                    // Process every 5 seconds worth of frames
                    int framesPerWindow = (int)(fps * 5); // 5-second windows
                    int windowIndex = 0;
                    int clipIndex = 0;

                    for (int startFrame = 0; startFrame < totalFrames; startFrame += framesPerWindow)
                    {
                        // ✅ Extract audio samples AND run inference
                        float[]? audioSamples = _emotionRecognizer.ProcessAudioWindow(videoPath, windowIndex);
                        float[]? audioLogits = null;
                        if (audioSamples != null)
                        {
                            audioLogits = _emotionRecognizer.InferenceAudio(audioSamples);
                        }

                        // Seek to frame and extract video window
                        cap.Set(VideoCaptureProperties.PosFrames, startFrame);
                        float[]? videoLogits = _emotionRecognizer.ProcessVideoWindow(cap, startFrame);

                        // Fuse predictions
                        string detectedEmotion = "neutral";
                        float confidence = 0.0f;

                        if (audioLogits != null && videoLogits != null)
                        {
                            detectedEmotion = _emotionRecognizer.FusePredictions(audioLogits, videoLogits);
                            confidence = _emotionRecognizer.GetLastConfidence();
                        }

                        // Create emotional clip record
                        var clip = new EmotionalClip
                        {
                            ClipIndex = clipIndex++,
                            VideoName = Path.GetFileName(videoPath),
                            TimeInSeconds = windowIndex * 5,
                            Emotion = detectedEmotion,
                            Confidence = confidence,
                            AudioVote = _emotionRecognizer.GetLastAudioEmotion(),
                            FaceVote = _emotionRecognizer.GetLastFaceEmotion(),
                            Timestamp = DateTime.Now
                        };

                        emotionalClips.Add(clip);

                        Console.WriteLine($"[{windowIndex * 5}s] Clip {clipIndex}: {detectedEmotion.ToUpper()} " +
                            $"(Confidence: {confidence:F3}, Audio: {_emotionRecognizer.GetLastAudioEmotion()}, Face: {_emotionRecognizer.GetLastFaceEmotion()})");

                        windowIndex++;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error processing video: {ex.Message}\n");
            }

            return emotionalClips;
        }

        /// <summary>
            /// Generate all_emotional_clips.json from first video file only
        /// Extracts emotions every 5 seconds with confidence scores
        /// </summary>
        public void GenerateEmotionalClipsForFirstVideo()
        {
            Console.WriteLine("\n" + new string('=', 100));
            Console.WriteLine("🎬 GENERATING EMOTIONAL CLIPS - FIRST VIDEO ONLY");
            Console.WriteLine(new string('=', 100) + "\n");

            if (_videoClips.Count == 0)
            {
                Console.WriteLine("❌ No video clips found! Please add video files to the assets folder.\n");
                return;
            }

            // Get FIRST video file only
            string firstVideoPath = _videoClips[0];
            Console.WriteLine($"📹 Processing first video: {Path.GetFileName(firstVideoPath)}\n");

            _experimentFrames.Clear();
            List<EmotionalClip> emotionalClips = new();

            try
            {
                using (var cap = new VideoCapture(firstVideoPath))
                {
                    if (!cap.IsOpened())
                    {
                        Console.WriteLine($"❌ Cannot open video: {firstVideoPath}\n");
                        return;
                    }

                    int totalFrames = (int)cap.Get(VideoCaptureProperties.FrameCount);
                    double fps = cap.Get(VideoCaptureProperties.Fps);
                    double videoDurationSecs = totalFrames / fps;

                    Console.WriteLine($"Total Frames: {totalFrames}");
                    Console.WriteLine($"FPS: {fps}");
                    Console.WriteLine($"Duration: {videoDurationSecs:F2} seconds\n");
                    Console.WriteLine("Extracting emotions every 5 seconds...\n");

                    // Process every 5 seconds worth of frames
                    int framesPerWindow = (int)(fps * 5); // 5-second windows
                    int windowIndex = 0;
                    int clipIndex = 0;

                    for (int startFrame = 0; startFrame < totalFrames; startFrame += framesPerWindow)
                    {
                        // ✅ FIXED: Extract audio samples AND run inference
                        float[]? audioSamples = _emotionRecognizer.ProcessAudioWindow(firstVideoPath, windowIndex);
                        float[]? audioLogits = null;
                        if (audioSamples != null)
                        {
                            audioLogits = _emotionRecognizer.InferenceAudio(audioSamples);
                        }

                        // Seek to frame and extract video window
                        cap.Set(VideoCaptureProperties.PosFrames, startFrame);
                        float[]? videoLogits = _emotionRecognizer.ProcessVideoWindow(cap, startFrame);

                        // Fuse predictions
                        string detectedEmotion = "neutral";
                        float confidence = 0.0f;

                        if (audioLogits != null && videoLogits != null)
                        {
                            detectedEmotion = _emotionRecognizer.FusePredictions(audioLogits, videoLogits);
                            confidence = _emotionRecognizer.GetLastConfidence();
                        }

                        // Create emotional clip record
                        var clip = new EmotionalClip
                        {
                            ClipIndex = clipIndex++,
                            VideoName = Path.GetFileName(firstVideoPath),
                            TimeInSeconds = windowIndex * 5,
                            Emotion = detectedEmotion,
                            Confidence = confidence,
                            AudioVote = _emotionRecognizer.GetLastAudioEmotion(),
                            FaceVote = _emotionRecognizer.GetLastFaceEmotion(),
                            Timestamp = DateTime.Now
                        };

                        emotionalClips.Add(clip);

                        Console.WriteLine($"[{windowIndex * 5}s] Clip {clipIndex}: {detectedEmotion.ToUpper()} " +
                            $"(Confidence: {confidence:F3}, Audio: {_emotionRecognizer.GetLastAudioEmotion()}, Face: {_emotionRecognizer.GetLastFaceEmotion()})");

                        windowIndex++;
                    }
                }

                // Export to JSON
                ExportEmotionalClipsToJson(emotionalClips, 
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "all_emotional_clips.json"));

                Console.WriteLine($"\n✅ Generated {emotionalClips.Count} emotional clips\n");
                DisplayEmotionalClipsSummary(emotionalClips);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error processing video: {ex.Message}\n");
            }
        }

        /// <summary>
        /// Export emotional clips to JSON file
        /// </summary>
        private void ExportEmotionalClipsToJson(List<EmotionalClip> clips, string outputFilePath)
        {
            try
            {
                var exportData = new
                {
                    metadata = new
                    {
                        total_clips = clips.Count,
                        video_file = clips.FirstOrDefault()?.VideoName ?? "unknown",
                        total_duration_seconds = (clips.Count - 1) * 5,
                        generated_at = DateTime.Now
                    },
                    emotional_clips = clips.Select(c => new
                    {
                        clip_index = c.ClipIndex,
                        video_name = c.VideoName,
                        time_in_seconds = c.TimeInSeconds,
                        emotion = c.Emotion,
                        confidence = c.Confidence,
                        audio_vote = c.AudioVote,
                        face_vote = c.FaceVote,
                        timestamp = c.Timestamp
                    }).ToList()
                };

                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(exportData, options);
                File.WriteAllText(outputFilePath, json);

                Console.WriteLine($"✓ Emotional clips exported to: {outputFilePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Error exporting emotional clips: {ex.Message}");
            }
        }

        /// <summary>
        /// Display summary of emotional clips
        /// </summary>
        private void DisplayEmotionalClipsSummary(List<EmotionalClip> clips)
        {
            Console.WriteLine(new string('=', 100));
            Console.WriteLine("📊 EMOTIONAL CLIPS SUMMARY");
            Console.WriteLine(new string('=', 100));

            var emotionDistribution = clips
                .GroupBy(c => c.Emotion)
                .Select(g => new { emotion = g.Key, count = g.Count() })
                .OrderByDescending(x => x.count)
                .ToList();

            var averageConfidence = clips.Average(c => c.Confidence);

            Console.WriteLine($"\n📈 Clip Statistics:");
            Console.WriteLine($"   Total Clips: {clips.Count}");
            Console.WriteLine($"   Duration: {(clips.Count - 1) * 5} seconds");
            Console.WriteLine($"   Average Confidence: {averageConfidence:F3}");

            Console.WriteLine($"\n😊 Emotion Distribution:");
            foreach (var item in emotionDistribution)
            {
                int barLength = (int)(item.count * 40 / clips.Count);
                string bar = new string('█', barLength);
                Console.WriteLine($"   {item.emotion.PadRight(12)} | {bar} {item.count}");
            }

            Console.WriteLine($"\n" + new string('=', 100) + "\n");
        }

        public void DisplaySummary()
        {
            Console.WriteLine("\n" + new string('=', 100));
            Console.WriteLine("📊 EXPERIMENT SUMMARY");
            Console.WriteLine(new string('=', 100));

            int totalFrames = _experimentFrames.Count;
            int movingFrames = _experimentFrames.Count(f => f.FootstepData.IsMoving);

            var emotionDistribution = _experimentFrames
                .GroupBy(f => f.EmotionalContext.DetectedEmotion)
                .Select(g => new { emotion = g.Key, count = g.Count() })
                .OrderByDescending(x => x.count)
                .ToList();

            var averageConfidence = _experimentFrames.Average(f => f.EmotionalContext.FinalConfidenceScore);
            var anomalousFrames = _experimentFrames.Count(f => f.EmotionalContext.AnomalyDetected);

            Console.WriteLine($"\n📈 Frame Statistics:");
            Console.WriteLine($"   Total Frames: {totalFrames}");
            Console.WriteLine($"   Duration: {totalFrames * 5} seconds ({totalFrames * 5 / 60:F1} minutes)");
            Console.WriteLine($"   Movement Frames: {movingFrames} ({(float)movingFrames / totalFrames * 100:F1}%)");
            Console.WriteLine($"   Average Confidence: {averageConfidence:F3}");
            Console.WriteLine($"   Anomalous Frames: {anomalousFrames}");

            Console.WriteLine($"\n😊 Emotion Distribution:");
            foreach (var item in emotionDistribution)
            {
                int barLength = (int)(item.count * 40 / totalFrames);
                string bar = new string('█', barLength);
                Console.WriteLine($"   {item.emotion.PadRight(12)} | {bar} {item.count}");
            }

            Console.WriteLine($"\n" + new string('=', 100));
        }

        /// <summary>
        /// Export experiment results to JSON file (for RunRealExperiment)
        /// </summary>
        public void ExportResultsToJson(string outputFilePath)
        {
            try
            {
                var exportData = new
                {
                    experiment_metadata = new
                    {
                        total_frames = _experimentFrames.Count,
                        video_files_processed = _videoClips.Count,
                        start_time = _experimentFrames.FirstOrDefault()?.SimulatedTime,
                        end_time = _experimentFrames.LastOrDefault()?.SimulatedTime,
                        total_duration_seconds = _experimentFrames.Count * 5
                    },
                    frames = _experimentFrames.Select(f => new
                    {
                        frame_index = f.FrameIndex,
                        timestamp = f.SimulatedTime,
                        video_file = f.ProcessedVideoClip,
                        footstep = new
                        {
                            zone = f.FootstepData.Zone,
                            position_x = f.FootstepData.Position.X,
                            position_y = f.FootstepData.Position.Y,
                            is_moving = f.FootstepData.IsMoving,
                            speed = f.FootstepData.Movement_Speed
                        },
                        emotion_detection = new
                        {
                            detected_emotion = f.EmotionalContext.DetectedEmotion,
                            confidence = f.EmotionalContext.FinalConfidenceScore,
                            audio_vote = f.EmotionalContext.AudioVote,
                            face_vote = f.EmotionalContext.FaceVote,
                            anomaly_detected = f.EmotionalContext.AnomalyDetected
                        }
                    }).ToList()
                };

                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(exportData, options);
                File.WriteAllText(outputFilePath, json);

                Console.WriteLine($"\n✓ Results exported to: {outputFilePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Error exporting results: {ex.Message}");
            }
        }
    }

    public class ExperimentFrame
    {
        public int FrameIndex { get; set; }
        public DateTime SimulatedTime { get; set; }
        public FootstepData? FootstepData { get; set; }
        public string? ProcessedVideoClip { get; set; }
        public EmotionalContext EmotionalContext { get; set; }
        public DateTime RecordedAt { get; set; }

        public override string ToString()
            => $"[Frame {FrameIndex}] {SimulatedTime:HH:mm:ss} | " +
               $"Emotion: {EmotionalContext?.DetectedEmotion} | " +
               $"Confidence: {EmotionalContext?.FinalConfidenceScore:F3}";
    }
}