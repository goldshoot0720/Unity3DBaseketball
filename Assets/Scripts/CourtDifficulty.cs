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
        /// <summary>The player's odds that a swipe lands and that a contest gets a hand on the ball.</summary>
        public float playerSteal, playerBlock;
        /// <summary>The computer's own odds, which move the other way: a hard cup is a hard rival.</summary>
        public float rivalTwo, rivalThree, rivalSteal, rivalBlock;
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

        /// <summary>Odds a swipe that reaches the ball carrier actually takes the ball.</summary>
        public float StealOdds(bool human) => human ? playerSteal : rivalSteal;

        /// <summary>Odds a contest that reaches the shot actually gets a hand on it.</summary>
        public float BlockOdds(bool human) => human ? playerBlock : rivalBlock;

        public static DifficultyTuning For(CourtDifficulty level)
        {
            if (level == CourtDifficulty.Easy)
                return new DifficultyTuning
                {
                    label = "簡單", playerTwo = .93f, playerThree = .33f, playerSteal = .53f, playerBlock = .73f,
                    rivalTwo = .55f, rivalThree = .15f, rivalSteal = .15f, rivalBlock = .25f,
                    thinkInterval = .34f, speed = .80f, stealChance = .14f, patience = 4.6f, gather = 4.0f,
                    aimLow = .34f, aimHigh = .96f
                };
            if (level == CourtDifficulty.Hard)
                return new DifficultyTuning
                {
                    label = "困難", playerTwo = .73f, playerThree = .13f, playerSteal = .13f, playerBlock = .33f,
                    rivalTwo = .80f, rivalThree = .30f, rivalSteal = .50f, rivalBlock = .65f,
                    thinkInterval = .12f, speed = 1.08f, stealChance = .62f, patience = 2.8f, gather = 2.4f,
                    aimLow = .58f, aimHigh = .78f
                };
            return new DifficultyTuning
            {
                label = "普通", playerTwo = .83f, playerThree = .23f, playerSteal = .33f, playerBlock = .53f,
                rivalTwo = .68f, rivalThree = .22f, rivalSteal = .30f, rivalBlock = .45f,
                thinkInterval = .23f, speed = .92f, stealChance = .28f, patience = 3.8f, gather = 3.4f,
                aimLow = .48f, aimHigh = .88f
            };
        }

        public static string LabelFor(CourtDifficulty level) => For(level).label;
    }
}
