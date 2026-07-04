using System;

namespace MoodStabilizer
{
    public class User
    {
        public int UserId { get; set; }
        public int? AdminId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string? FaceEmbedding { get; set; }
        public string? FacePhotoPath { get; set; }
    }
}