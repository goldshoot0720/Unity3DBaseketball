using UnityEngine;

namespace MiaCourt
{
    /// <summary>Geometry and timing shared by gameplay, previews and validation.</summary>
    public static class BasketballRules
    {
        public const float HalfLength = 12.5f;
        public const float HalfWidth = 6.4f;
        public const float HoopX = 10.9f;
        public const float HoopHeight = 3.05f;
        public const float RimRadius = .62f;
        public const float BallRadius = .20f;
        public const float ThreePointDistance = 6.6f;
        public const int WinningScore = 21;
        public const float MatchSeconds = 210f;
        public const float PossessionSeconds = 18f;
        public const float Gravity = 14f;
        /// <summary>Seconds the shot meter needs to travel the bar once, before it turns around.</summary>
        public const float ChargeSweepSeconds = .8f;
        public const float LongShotWindow = 3f;
        public const float CloseShotWindow = 1.5f;

        public static Vector3 HoopFor(int attackingTeam) => new Vector3(attackingTeam == 0 ? HoopX : -HoopX, HoopHeight, 0);

        public static int ShotValue(Vector3 origin, Vector3 hoop)
        {
            origin.y = hoop.y = 0;
            return Vector3.Distance(origin, hoop) >= ThreePointDistance ? 3 : 2;
        }

        public static Vector3 LaunchVelocity(Vector3 start, Vector3 end, float apex)
        {
            apex = Mathf.Max(apex, Mathf.Max(start.y, end.y) + .1f);
            float upTime = Mathf.Sqrt(2f * (apex - start.y) / Gravity);
            float downTime = Mathf.Sqrt(2f * (apex - end.y) / Gravity);
            Vector3 velocity = (end - start) / (upTime + downTime);
            velocity.y = Gravity * upTime;
            return velocity;
        }

        public static bool CrossedHoop(Vector3 previous, Vector3 current, Vector3 hoop)
        {
            if (previous.y <= hoop.y || current.y > hoop.y || current.y >= previous.y) return false;
            float fraction = (previous.y - hoop.y) / (previous.y - current.y);
            Vector3 crossing = Vector3.Lerp(previous, current, fraction);
            crossing.y = hoop.y;
            return (crossing - hoop).sqrMagnitude < Mathf.Pow(RimRadius - BallRadius - .025f, 2);
        }

        public static float ReleaseQuality(float charge) => Mathf.Clamp01(1f - Mathf.Abs(charge - .68f) * 2.7f);

        /// <summary>A long shot earns a longer aim, so the meter offers more passes over the green zone.</summary>
        public static float ShotWindow(Vector3 origin, Vector3 hoop) =>
            ShotValue(origin, hoop) == 3 ? LongShotWindow : CloseShotWindow;

        public static float SweepCharge(float age) => Mathf.PingPong(age / ChargeSweepSeconds, 1f);

        /// <summary>True while the meter is travelling left to right, so the HUD can show which way it is going.</summary>
        public static bool SweepRising(float age) => Mathf.Repeat(age / ChargeSweepSeconds, 2f) < 1f;

        /// <summary>Half the width, in meter units, of the band that still counts as a good release.</summary>
        public const float GoodReleaseBand = .122f;
        /// <summary>Half the width of the band that counts as perfect.</summary>
        public const float PerfectReleaseBand = .037f;
        /// <summary>The centre of the green zone.</summary>
        public const float PerfectRelease = .68f;

        public static Vector3 ClampToCourt(Vector3 position)
        {
            position.x = Mathf.Clamp(position.x, -HalfLength + .55f, HalfLength - .55f);
            position.z = Mathf.Clamp(position.z, -HalfWidth + .55f, HalfWidth - .55f);
            position.y = 0;
            return position;
        }
    }
}
