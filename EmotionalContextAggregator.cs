using System;
using System.Collections.Generic;
using System.Linq;

namespace MoodStabilizer
{
    /// <summary>
    /// Aggregates information from multiple sources (video, audio, movement, door events)
    /// to determine the current emotional state of the user
    /// </summary>
    public class EmotionalContextAggregator
    {
        private readonly EmotionRecognizer _emotionRecognizer;
        private readonly FootstepPatternGenerator _footstepGenerator;
        private readonly DoorSlamDetector _doorSlamDetector;

        public EmotionalContextAggregator(
            EmotionRecognizer emotionRecognizer,
            FootstepPatternGenerator footstepGenerator,
            DoorSlamDetector doorSlamDetector)
        {
            _emotionRecognizer = emotionRecognizer;
            _footstepGenerator = footstepGenerator;
            _doorSlamDetector = doorSlamDetector;
        }

        /// <summary>
        /// Aggregate all emotional signals into a unified emotional context
        /// </summary>
        public EmotionalContext AggregateContext(
            string detectedEmotion,
            float emotionConfidence,
            string audioVote,
            string faceVote,
            FootstepData footstepData,
            DoorSlamEvent doorSlamEvent)
        {
            // Multi-signal fusion logic
            float validationFactor = CalculateBehavioralValidationFactor(detectedEmotion, footstepData, doorSlamEvent);
            float finalConfidence = emotionConfidence * validationFactor;
            bool anomalyDetected = DetectAnomaly(detectedEmotion, footstepData, doorSlamEvent);

            string validation = validationFactor switch
            {
                >= 0.9f => "excellent_match",
                >= 0.7f => "good_match",
                >= 0.5f => "moderate_match",
                _ => "poor_match"
            };

            return new EmotionalContext
            {
                DetectedEmotion = detectedEmotion,
                EmotionConfidence = emotionConfidence,
                AudioVote = audioVote,
                FaceVote = faceVote,
                FootstepContext = footstepData,
                DoorSlamContext = doorSlamEvent,
                FinalConfidenceScore = Math.Min(finalConfidence, 1.0f),
                BehavioralValidation = validation,
                Timestamp = DateTime.Now,
                AnomalyDetected = anomalyDetected
            };
        }

        /// <summary>
        /// Validate if emotion matches behavioral patterns
        /// </summary>
        public float CalculateBehavioralValidationFactor(
            string emotion,
            FootstepData footsteps,
            DoorSlamEvent doorSlam)
        {
            float factor = 1.0f;

            if (footsteps == null)
                return 0.5f;

            // Emotion-behavior matching rules
            switch (emotion.ToLower())
            {
                case "anger":
                    // Angry people move fast and often pace
                    if (footsteps.IsMoving && footsteps.Movement_Speed > 0.6f)
                        factor += 0.2f;
                    if (doorSlam != null && doorSlam.SlamIntensity > 0.7f)
                        factor += 0.1f;
                    if (footsteps.Zone == "entry")
                        factor += 0.1f;
                    break;

                case "sadness":
                    // Sad people stay still, in comfortable zones
                    if (!footsteps.IsMoving)
                        factor += 0.2f;
                    if (footsteps.Zone == "living_area")
                        factor += 0.15f;
                    if (doorSlam != null && doorSlam.SlamIntensity < 0.3f)
                        factor += 0.05f;
                    break;

                case "happiness":
                    // Happy people move leisurely, explore zones
                    if (footsteps.IsMoving && footsteps.Movement_Speed > 0.3f && footsteps.Movement_Speed < 0.7f)
                        factor += 0.15f;
                    if (doorSlam == null)
                        factor += 0.1f;
                    break;

                case "excited":
                    // Excited people move fast and frequently
                    if (footsteps.IsMoving && footsteps.Movement_Speed > 0.5f)
                        factor += 0.2f;
                    if (doorSlam != null && doorSlam.SlamIntensity > 0.4f)
                        factor += 0.1f;
                    break;

                case "neutral":
                    // Neutral = moderate behavior
                    if (footsteps.Movement_Speed >= 0.3f && footsteps.Movement_Speed <= 0.7f)
                        factor += 0.15f;
                    break;
            }

            // Penalize contradictions
            if (emotion.ToLower() == "sadness" && footsteps.Movement_Speed > 0.8f)
                factor -= 0.3f;

            if (emotion.ToLower() == "happiness" && doorSlam != null && doorSlam.SlamIntensity > 0.85f)
                factor -= 0.2f;

            return Math.Max(0.1f, Math.Min(factor, 1.0f));
        }

        /// <summary>
        /// Detect when emotion contradicts behavior
        /// </summary>
        private bool DetectAnomaly(string emotion, FootstepData footsteps, DoorSlamEvent doorSlam)
        {
            if (footsteps == null)
                return false;

            // Happiness with aggressive door slam
            if (emotion.ToLower() == "happiness" && doorSlam != null && doorSlam.SlamIntensity > 0.85f)
                return true;

            // Neutral with extreme pacing
            if (emotion.ToLower() == "neutral" && footsteps.Movement_Speed > 0.85f)
                return true;

            // Sadness with rapid exploration
            if (emotion.ToLower() == "sadness" && footsteps.Movement_Speed > 0.75f)
                return true;

            return false;
        }
    }

    /// <summary>
    /// Complete emotional context combining all available signals
    /// </summary>
    public class EmotionalContext
    {
        public string DetectedEmotion { get; set; }
        public float EmotionConfidence { get; set; }
        public string AudioVote { get; set; }
        public string FaceVote { get; set; }

        public FootstepData FootstepContext { get; set; }
        public DoorSlamEvent DoorSlamContext { get; set; }

        public float FinalConfidenceScore { get; set; }
        public string BehavioralValidation { get; set; }
        public DateTime Timestamp { get; set; }
        public bool AnomalyDetected { get; set; }

        public override string ToString()
            => $"[Emotion] {DetectedEmotion} ({EmotionConfidence:F2}) | " +
               $"Final: {FinalConfidenceScore:F2} | Anomaly: {AnomalyDetected} | " +
               $"Zone: {FootstepContext?.Zone}";
    }
}