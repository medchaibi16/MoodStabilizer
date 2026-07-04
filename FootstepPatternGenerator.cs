using System;
using System.Collections.Generic;
using System.Linq;

namespace MoodStabilizer
{
    /// <summary>
    /// Generates realistic footstep patterns based on emotional state,
    /// room layout, and time of day. Data integrates with EmotionRecognizer output.
    /// </summary>
    public class FootstepPatternGenerator
    {
        // Room zones from user.json
        private readonly Dictionary<string, RoomZone> _zones = new()
        {
            {
                "home_office", new RoomZone
                {
                    Name = "home_office",
                    Position = new Vector2(0, 0),
                    Dimensions = new Vector2(2.5f, 4.5f),
                    Description = "Desk area, workspace"
                }
            },
            {
                "living_area", new RoomZone
                {
                    Name = "living_area",
                    Position = new Vector2(2.5f, 0),
                    Dimensions = new Vector2(3, 4.5f),
                    Description = "Sofa, relaxation area"
                }
            },
            {
                "entry", new RoomZone
                {
                    Name = "entry",
                    Position = new Vector2(5.5f, 2),
                    Dimensions = new Vector2(0.5f, 1),
                    Description = "Door entrance"
                }
            }
        };

        private Vector2 _currentPosition;
        private Random _random;
        private Dictionary<string, int> _timeInZone; // Track how long user stays in each zone

        public FootstepPatternGenerator()
        {
            _currentPosition = GetZoneCenter("home_office"); // Start at desk
            _random = new Random();
            _timeInZone = new Dictionary<string, int> { { "home_office", 0 }, { "living_area", 0 }, { "entry", 0 } };
        }

        /// <summary>
        /// Generate next footstep position based on emotion and time context
        /// Returns the position (X, Y) in the room where user is located
        /// </summary>
        public FootstepData GenerateNextStep(
            string emotion,
            DateTime currentTime,
            int secondsElapsedInSession)
        {
            var movementProfile = GetEmotionMovementProfile(emotion);
            var currentZone = GetCurrentZone(_currentPosition);

            // Decide: should user move or stay still?
            bool shouldMove = DecideToMove(
                emotion,
                movementProfile,
                currentZone,
                secondsElapsedInSession);

            if (shouldMove)
            {
                var targetZone = SelectTargetZone(emotion, currentZone, currentTime);
                _currentPosition = GeneratePathToZone(_currentPosition, targetZone, movementProfile);
                _timeInZone[currentZone] = 0; // Reset timer when leaving zone

                return new FootstepData
                {
                    Position = _currentPosition,
                    Zone = GetCurrentZone(_currentPosition),
                    Timestamp = currentTime,
                    IsMoving = true,
                    Movement_Speed = movementProfile.MovementSpeed,
                    Emotion = emotion
                };
            }
            else
            {
                // User stays in current zone but may shift position slightly
                _currentPosition += new Vector2(
                    (float)(_random.NextDouble() - 0.5) * 0.1f,
                    (float)(_random.NextDouble() - 0.5) * 0.1f);

                ClampPositionToZone(currentZone);
                _timeInZone[currentZone]++;

                return new FootstepData
                {
                    Position = _currentPosition,
                    Zone = currentZone,
                    Timestamp = currentTime,
                    IsMoving = false,
                    Movement_Speed = 0,
                    Emotion = emotion
                };
            }
        }

        /// <summary>
        /// Get emotional movement profile
        /// </summary>
        private EmotionMovementProfile GetEmotionMovementProfile(string emotion)
        {
            return emotion.ToLower() switch
            {
                "sadness" => new EmotionMovementProfile
                {
                    MovementSpeed = 0.3f,
                    IdlePercentage = 0.85f,
                    PacingIntensity = 0.1f,
                    PreferredZones = new[] { "living_area" },
                    ZoneTransitionFrequency = 0.05f // Rarely moves
                },

                "anger" => new EmotionMovementProfile
                {
                    MovementSpeed = 0.8f,
                    IdlePercentage = 0.15f,
                    PacingIntensity = 0.9f, // High pacing
                    PreferredZones = new[] { "home_office", "entry" },
                    ZoneTransitionFrequency = 0.6f // Frequently moves back and forth
                },

                "frustration" => new EmotionMovementProfile
                {
                    MovementSpeed = 0.6f,
                    IdlePercentage = 0.5f,
                    PacingIntensity = 0.7f,
                    PreferredZones = new[] { "home_office" },
                    ZoneTransitionFrequency = 0.3f,
                    PacingZone = "home_office" // Tends to pace in office
                },

                "excited" => new EmotionMovementProfile
                {
                    MovementSpeed = 0.7f,
                    IdlePercentage = 0.2f,
                    PacingIntensity = 0.3f,
                    PreferredZones = new[] { "home_office", "living_area" },
                    ZoneTransitionFrequency = 0.5f // Explores room
                },

                "happiness" => new EmotionMovementProfile
                {
                    MovementSpeed = 0.5f,
                    IdlePercentage = 0.6f,
                    PacingIntensity = 0.1f,
                    PreferredZones = new[] { "living_area", "home_office" },
                    ZoneTransitionFrequency = 0.25f // Leisurely movement
                },

                _ => new EmotionMovementProfile // neutral or default
                {
                    MovementSpeed = 0.4f,
                    IdlePercentage = 0.7f,
                    PacingIntensity = 0.2f,
                    PreferredZones = new[] { "home_office", "living_area" },
                    ZoneTransitionFrequency = 0.2f
                }
            };
        }

        /// <summary>
        /// Decide if user should move based on emotion, time in zone, and randomness
        /// </summary>
        private bool DecideToMove(
            string emotion,
            EmotionMovementProfile profile,
            string currentZone,
            int sessionSeconds)
        {
            float idleThreshold = profile.IdlePercentage;
            float randomValue = (float)_random.NextDouble();

            // If user has been idle in zone too long, force movement
            if (_timeInZone[currentZone] > 120) // 2 minutes
                return true;

            return randomValue > idleThreshold;
        }

        /// <summary>
        /// Select target zone based on emotion and time of day
        /// </summary>
        private string SelectTargetZone(string emotion, string currentZone, DateTime time)
        {
            var profile = GetEmotionMovementProfile(emotion);

            // Special handling for pacing emotions (anger, frustration)
            if (emotion.ToLower() == "anger" && currentZone == "home_office")
                return "entry"; // Angry users pace toward door

            if (emotion.ToLower() == "anger" && currentZone == "entry")
                return "home_office"; // Back to office

            if (emotion.ToLower() == "frustration")
            {
                // Frustration: oscillate office → kitchen (water break)
                return currentZone == "home_office" ? "living_area" : "home_office";
            }

            if (emotion.ToLower() == "sadness")
                return "living_area"; // Stay in comfort zone

            if (emotion.ToLower() == "happiness")
                return currentZone == "home_office" ? "living_area" : "home_office"; // Leisurely

            // Default: use preferred zones
            var validZones = profile.PreferredZones
                .Where(z => z != currentZone)
                .ToList();

            return validZones.Any()
                ? validZones[_random.Next(validZones.Count)]
                : "home_office";
        }

        /// <summary>
        /// Generate smooth interpolated path to target zone
        /// Simulates user walking from current position to target zone
        /// </summary>
        private Vector2 GeneratePathToZone(
            Vector2 from,
            string targetZoneName,
            EmotionMovementProfile profile)
        {
            var targetZone = _zones[targetZoneName];
            var targetPosition = GetRandomPointInZone(targetZone);

            // Interpolate partway toward target (simulate 1 step/5-second window)
            float stepSize = profile.MovementSpeed * 0.2f; // Adjust step size
            var direction = (targetPosition - from).Normalize();
            var nextPosition = from + direction * stepSize;

            // Clamp to room boundaries
            nextPosition.X = Math.Max(0, Math.Min(5.5f, nextPosition.X));
            nextPosition.Y = Math.Max(0, Math.Min(4.5f, nextPosition.Y));

            return nextPosition;
        }

        /// <summary>
        /// Get the zone name where user currently is
        /// </summary>
        private string GetCurrentZone(Vector2 position)
        {
            foreach (var zone in _zones.Values)
            {
                if (position.X >= zone.Position.X && position.X <= zone.Position.X + zone.Dimensions.X &&
                    position.Y >= zone.Position.Y && position.Y <= zone.Position.Y + zone.Dimensions.Y)
                {
                    return zone.Name;
                }
            }

            return "home_office"; // Default fallback
        }

        /// <summary>
        /// Get center point of a zone
        /// </summary>
        private Vector2 GetZoneCenter(string zoneName)
        {
            var zone = _zones[zoneName];
            return zone.Position + (zone.Dimensions / 2);
        }

        /// <summary>
        /// Get random point within a zone
        /// </summary>
        private Vector2 GetRandomPointInZone(RoomZone zone)
        {
            float x = zone.Position.X + (float)_random.NextDouble() * zone.Dimensions.X;
            float y = zone.Position.Y + (float)_random.NextDouble() * zone.Dimensions.Y;
            return new Vector2(x, y);
        }

        /// <summary>
        /// Clamp position to stay within zone boundaries
        /// </summary>
        private void ClampPositionToZone(string zoneName)
        {
            var zone = _zones[zoneName];
            _currentPosition.X = Math.Max(zone.Position.X, Math.Min(zone.Position.X + zone.Dimensions.X, _currentPosition.X));
            _currentPosition.Y = Math.Max(zone.Position.Y, Math.Min(zone.Position.Y + zone.Dimensions.Y, _currentPosition.Y));
        }

        /// <summary>
        /// Get current position
        /// </summary>
        public Vector2 GetCurrentPosition() => _currentPosition;

        /// <summary>
        /// Get current zone
        /// </summary>
        public string GetCurrentZoneName() => GetCurrentZone(_currentPosition);

        /// <summary>
        /// Check if user is pacing (rapid back-and-forth in same zone)
        /// Useful signal for decision engine
        /// </summary>
        public bool IsPacing(List<FootstepData> recentSteps, int windowSize = 10)
        {
            if (recentSteps.Count < windowSize)
                return false;

            var lastSteps = recentSteps.TakeLast(windowSize).ToList();
            var zones = lastSteps.Select(s => s.Zone).ToList();

            // Pacing = rapid zone alternation between 2 zones
            var uniqueZones = zones.Distinct().Count();
            var zoneChanges = zones.Zip(zones.Skip(1), (a, b) => a != b ? 1 : 0).Sum();

            return uniqueZones == 2 && zoneChanges >= 5;
        }

        /// <summary>
        /// Reset generator (e.g., at session start)
        /// </summary>
        public void Reset()
        {
            _currentPosition = GetZoneCenter("home_office");
            _timeInZone = new Dictionary<string, int> { { "home_office", 0 }, { "living_area", 0 }, { "entry", 0 } };
        }
    }

    /// <summary>
    /// Simple 2D vector for positions
    /// </summary>
    public struct Vector2
    {
        public float X { get; set; }
        public float Y { get; set; }

        public Vector2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public static Vector2 operator +(Vector2 a, Vector2 b) => new(a.X + b.X, a.Y + b.Y);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new(a.X - b.X, a.Y - b.Y);
        public static Vector2 operator *(Vector2 a, float scalar) => new(a.X * scalar, a.Y * scalar);
        public static Vector2 operator /(Vector2 a, float scalar) => new(a.X / scalar, a.Y / scalar);

        public float Length() => (float)Math.Sqrt(X * X + Y * Y);

        public Vector2 Normalize()
        {
            float len = Length();
            return len > 0 ? this / len : new Vector2(0, 0);
        }
    }

    /// <summary>
    /// Room zone definition
    /// </summary>
    public class RoomZone
    {
        public string Name { get; set; }
        public Vector2 Position { get; set; }
        public Vector2 Dimensions { get; set; }
        public string Description { get; set; }
    }

    /// <summary>
    /// Emotional movement profile
    /// </summary>
    public class EmotionMovementProfile
    {
        public float MovementSpeed { get; set; } // 0.0-1.0
        public float IdlePercentage { get; set; } // How often user stays still (0.0-1.0)
        public float PacingIntensity { get; set; } // 0.0-1.0, how much they pace
        public string[] PreferredZones { get; set; }
        public float ZoneTransitionFrequency { get; set; } // Probability of moving to different zone
        public string PacingZone { get; set; } // Zone where pacing typically happens
    }

    /// <summary>
    /// Single footstep data point (one 5-second window)
    /// </summary>
    public class FootstepData
    {
        public Vector2 Position { get; set; }
        public string Zone { get; set; }
        public DateTime Timestamp { get; set; }
        public bool IsMoving { get; set; }
        public float Movement_Speed { get; set; }
        public string Emotion { get; set; }

        public override string ToString()
            => $"[{Emotion}] Zone: {Zone}, Pos: ({Position.X:F2}, {Position.Y:F2}), Moving: {IsMoving}, Speed: {Movement_Speed:F2}";
    }
}