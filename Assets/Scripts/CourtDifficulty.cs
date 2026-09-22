using UnityEngine;

namespace MiaCourt
{
    public enum CourtDifficulty { Easy, Normal, Hard }

    /// <summary>
    /// Everything a difficulty setting changes. The two shooting percentages are the headline:
    /// they are the odds a clean, unguarded release goes in, which a late or contested one drags
    /// down from there. The rest is how hard the computer opponent plays.
    /// </summary>
    public struct DifficultyTuning
    {
        public string label;
        /// <summary>The player's odds from two and from three, as set by the brief.</summary>
        public float playerTwo, playerThree;
        /// <summary>The computer's own odds, which move the other way: a hard cup is a hard rival.</summary>
        public float rivalTwo, rivalThree;
        /// <summary>Seconds between the opponent's decisions.</summary>
        public float thinkInterval;
        public float speed;
        /// <summary>Odds the opponent commits to a swipe once it is close enough to try.</summary>
        public float stealChance;
        /// <summary>Seconds the opponent holds the ball before it shoots, and before it gathers.</summary>
        public float patience, gather;
        /// <summary>The slice of the meter the opponent releases from.</summary>
        public float aimLow, aimHigh;

        public float Odds(bool human, int points) =>
            human ? (points == 3 ? playerThree : playerTwo) : (points == 3 ? rivalThree : rivalTwo);

        public static DifficultyTuning For(CourtDifficulty level)
        {
            if (level == CourtDifficulty.Easy)
                return new DifficultyTuning
                {
                    label = "簡單", playerTwo = .93f, playerThree = .33f, rivalTwo = .55f, rivalThree = .15f,
                    thinkInterval = .34f, speed = .80f, stealChance = .14f, patience = 4.6f, gather = 4.0f,
                    aimLow = .34f, aimHigh = .96f
                };
            if (level == CourtDifficulty.Hard)
                return new DifficultyTuning
                {
                    label = "困難", playerTwo = .73f, playerThree = .13f, rivalTwo = .80f, rivalThree = .30f,
                    thinkInterval = .12f, speed = 1.08f, stealChance = .62f, patience = 2.8f, gather = 2.4f,
                    aimLow = .58f, aimHigh = .78f
                };
            return new DifficultyTuning
            {
                label = "普通", playerTwo = .83f, playerThree = .23f, rivalTwo = .68f, rivalThree = .22f,
                thinkInterval = .23f, speed = .92f, stealChance = .28f, patience = 3.8f, gather = 3.4f,
                aimLow = .48f, aimHigh = .88f
            };
        }

        public static string LabelFor(CourtDifficulty level) => For(level).label;
    }
}
