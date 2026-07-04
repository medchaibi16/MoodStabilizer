using System;

namespace MoodStabilizer
{
    /// <summary>
    /// Single emotional clip (5-second window)
    /// </summary>
    public class EmotionalClip
    {
        public int ClipIndex { get; set; }
        public string VideoName { get; set; }
        public int TimeInSeconds { get; set; }
        public string Emotion { get; set; }
        public float Confidence { get; set; }
        public string AudioVote { get; set; }
        public string FaceVote { get; set; }
        public DateTime Timestamp { get; set; }

        public override string ToString()
            => $"[{TimeInSeconds}s] {Emotion} (confidence: {Confidence:F3})";
    }
}