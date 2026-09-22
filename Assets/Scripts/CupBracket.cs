using UnityEngine;

namespace MiaCourt
{
    /// <summary>Where the cup currently stands. The player is in exactly one tie per round.</summary>
    public enum CupRound { Quarter, Semi, Medal, Over }

    /// <summary>One match in the bracket, whether the player is in it or the computer settled it.</summary>
    public sealed class CupTie
    {
        public string label = "";
        public int left, right;
        public int leftScore, rightScore;
        public bool played;
        // Matches cannot end level: a tie on the clock goes to overtime, and a simulated
        // result never produces equal scores, so the higher score is always the winner.
        public int Winner => leftScore >= rightScore ? left : right;
        public int Loser => leftScore >= rightScore ? right : left;
        public int WinnerScore => Mathf.Max(leftScore, rightScore);
        public int LoserScore => Mathf.Min(leftScore, rightScore);
        public bool Has(int roster) => left == roster || right == roster;
        public int Other(int roster) => left == roster ? right : left;
        public int ScoreFor(int roster) => left == roster ? leftScore : rightScore;
    }

    /// <summary>
    /// The eight-character knockout: four quarter-finals, two semi-finals, then the final and
    /// the third-place match played side by side. The player plays three matches at most, and
    /// the computer settles every tie they are not in so the cup always crowns a champion.
    /// </summary>
    public sealed class CupBracket
    {
        /// <summary>Quarter-final, semi-final, then either the final or the third-place match.</summary>
        public const int PlayerMatches = 3;
        public const int Entrants = 8;

        public readonly CupTie[] quarters = new CupTie[4];
        public readonly CupTie[] semis = new CupTie[2];
        public CupTie bronze, final;
        public CupRound round = CupRound.Quarter;
        public int player;
        public int champion = -1, runnerUp = -1, third = -1;
        /// <summary>1, 2 or 3 for a medal, 4 for the fourth place, 5 for a quarter-final exit.</summary>
        public int PlayerPlace { get; private set; }
        public CupTie LastPlayerTie { get; private set; }
        public string LastRoundName { get; private set; } = "";
        public CupTie[] LastRoundTies { get; private set; } = new CupTie[0];
        public bool PlayerAdvanced => LastPlayerTie != null && LastPlayerTie.Winner == player;

        public void Begin(int playerRoster)
        {
            player = playerRoster;
            int count = Mathf.Max(Entrants, MiaCourtAssets.CharacterCount);
            int[] seeds = new int[count];
            for (int i = 0; i < count; i++) seeds[i] = i % MiaCourtAssets.CharacterCount;
            for (int i = count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                int swap = seeds[i]; seeds[i] = seeds[j]; seeds[j] = swap;
            }
            // Only the first eight seeds enter, so the player is pulled into the draw if the
            // shuffle left them out of it.
            int playerSeed = System.Array.IndexOf(seeds, player);
            if (playerSeed >= Entrants)
            {
                int slot = Random.Range(0, Entrants);
                seeds[playerSeed] = seeds[slot];
                seeds[slot] = player;
            }
            for (int i = 0; i < quarters.Length; i++)
                quarters[i] = new CupTie { label = "八強 " + (i + 1), left = seeds[i * 2], right = seeds[i * 2 + 1] };
            semis[0] = semis[1] = null;
            bronze = final = null;
            round = CupRound.Quarter;
            champion = runnerUp = third = -1;
            PlayerPlace = 0;
            LastPlayerTie = null;
            LastRoundName = "";
            LastRoundTies = new CupTie[0];
        }

        public CupTie[] CurrentTies()
        {
            if (round == CupRound.Quarter) return quarters;
            if (round == CupRound.Semi) return semis;
            if (round == CupRound.Medal) return new[] { final, bronze };
            return new CupTie[0];
        }

        public CupTie PlayerTie()
        {
            foreach (CupTie tie in CurrentTies())
                if (tie != null && tie.Has(player)) return tie;
            return null;
        }

        /// <summary>What the player is about to walk out for.</summary>
        public string NextMatchName()
        {
            if (round == CupRound.Quarter) return "八強賽";
            if (round == CupRound.Semi) return "四強賽";
            if (round == CupRound.Medal) return PlayerTie() == final ? "冠軍賽" : "季軍賽";
            return "";
        }

        /// <summary>Files the match the player just played, settles the rest of the round, advances.</summary>
        public void RecordPlayerResult(int playerScore, int opponentScore)
        {
            CupTie tie = PlayerTie();
            if (tie == null || tie.played) return;
            bool playerIsLeft = tie.left == player;
            tie.leftScore = playerIsLeft ? playerScore : opponentScore;
            tie.rightScore = playerIsLeft ? opponentScore : playerScore;
            tie.played = true;
            LastRoundName = NextMatchName();
            LastPlayerTie = tie;
            LastRoundTies = CurrentTies();
            foreach (CupTie other in LastRoundTies) Simulate(other);
            Advance();
        }

        void Advance()
        {
            if (round == CupRound.Quarter)
            {
                semis[0] = new CupTie { label = "四強 1", left = quarters[0].Winner, right = quarters[1].Winner };
                semis[1] = new CupTie { label = "四強 2", left = quarters[2].Winner, right = quarters[3].Winner };
                round = CupRound.Semi;
                // A quarter-final loss ends the player's cup, but the bracket is still played out.
                if (PlayerTie() == null) PlayOut(5);
                return;
            }
            if (round == CupRound.Semi)
            {
                OpenMedalRound();
                round = CupRound.Medal;
                return;
            }
            Crown();
        }

        void OpenMedalRound()
        {
            final = new CupTie { label = "冠軍賽", left = semis[0].Winner, right = semis[1].Winner };
            bronze = new CupTie { label = "季軍賽", left = semis[0].Loser, right = semis[1].Loser };
        }

        /// <summary>The player is out, so the computer finishes the cup in one go.</summary>
        void PlayOut(int place)
        {
            foreach (CupTie tie in semis) Simulate(tie);
            OpenMedalRound();
            Simulate(final);
            Simulate(bronze);
            PlayerPlace = place;
            Crown();
        }

        void Crown()
        {
            champion = final.Winner;
            runnerUp = final.Loser;
            third = bronze.Winner;
            round = CupRound.Over;
            if (PlayerPlace == 0)
                PlayerPlace = player == champion ? 1 : player == runnerUp ? 2 : player == third ? 3 : 4;
        }

        /// <summary>A computer tie still has to read like a real 21-point game.</summary>
        static void Simulate(CupTie tie)
        {
            if (tie == null || tie.played) return;
            int winning = Random.value < .65f ? BasketballRules.WinningScore
                : Random.Range(13, BasketballRules.WinningScore);
            int losing = Random.Range(Mathf.Max(5, winning - 12), winning - 1);
            bool leftWins = Random.value < .5f;
            tie.leftScore = leftWins ? winning : losing;
            tie.rightScore = leftWins ? losing : winning;
            tie.played = true;
        }

        public static string PlaceName(int place)
        {
            if (place == 1) return "冠軍";
            if (place == 2) return "亞軍";
            if (place == 3) return "季軍";
            if (place == 4) return "第四名";
            return "止步八強";
        }
    }
}
