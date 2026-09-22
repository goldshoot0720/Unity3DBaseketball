using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MiaCourt
{
    /// <summary>Opt-in integration checks and real rendered captures. Inactive for normal play.</summary>
    public sealed class MiaSmokeTest : MonoBehaviour
    {
        readonly List<string> checks = new List<string>();
        readonly List<string> errors = new List<string>();
        readonly List<string> skippedCaptures = new List<string>();
        readonly List<string> editorNoise = new List<string>();
        MiaBasketballGame game;
        string output;
        bool smoke;

        IEnumerator Start()
        {
            string[] args = Environment.GetCommandLineArgs();
            smoke = Array.IndexOf(args,"-miaSmokeTest") >= 0;
            int outputIndex = Array.IndexOf(args,"-miaOutput");
            output = outputIndex>=0 && outputIndex+1<args.Length ? args[outputIndex+1] : Path.GetFullPath("Documentation/Validation");
            #if UNITY_EDITOR
            if (UnityEditor.SessionState.GetBool("MiaCourt.Smoke",false))
            {
                smoke = true;
                UnityEditor.SessionState.SetBool("MiaCourt.Smoke",false);
            }
            #endif
            if (!smoke) { enabled=false; yield break; }
            Directory.CreateDirectory(output);
            Application.logMessageReceived += OnLog;
            game = GetComponent<MiaBasketballGame>();
            yield return new WaitForSeconds(1.5f);
            Check(game.State==MatchState.Home,"home opens");
            Check(MiaCourtAssets.CharacterCount==8 && MiaCourtAssets.NameFor(3)=="牙妹" && MiaCourtAssets.NameFor(7)=="深索娘","eight playable characters");
            Check(game.players[0].displayName=="喵白白" && game.players[1].displayName=="喵布布","rigged character mapping");
            yield return Capture("01-home.png");
            game.SelectCharacter(3);
            Check(game.players[0].displayName=="牙妹" && game.players[1].displayName=="喵布布","player pick keeps opponent");
            game.SelectOpponent(7);
            Check(game.players[0].displayName=="牙妹" && game.players[1].displayName=="深索娘","opponent pick is independent");
            game.SelectOpponent(3);
            Check(game.players[0].displayName=="深索娘" && game.players[1].displayName=="牙妹","picking the other slot swaps");
            game.SelectCharacter(0);
            game.SelectOpponent(1);
            Check(game.players[0].displayName=="喵白白" && game.players[1].displayName=="喵布布","default pairing restored");
            // Verify actual imported skeletons, not just that an action flag changes.
            for (int roster = 0; roster < MiaCourtAssets.CharacterCount; roster++)
            {
                game.SelectCharacter(roster);
                CatPlayer actor = game.players[0];
                actor.ResetMotion();
                actor.Animate(0, true, false);
                Check(actor.HasArmRig, "arm bones wired: " + roster);
                Vector3 highBall = actor.Dribble(0, false);
                Vector3 highHand = actor.RightPalm;
                actor.Animate(.32f, true, false);
                Vector3 lowBall = actor.Dribble(.32f, false);
                Check(highBall.y - lowBall.y > .8f && Vector3.Distance(highHand, actor.RightPalm) > .10f,
                    "ball and arm dribble together: " + roster);
                actor.Shoot();
                actor.Animate(.06f, false, false);
                Check(actor.RightPalm.y > highHand.y + .2f, "shooting arm raised: " + roster);
                actor.ResetMotion();
                actor.Defend(false, actor.transform.position + Vector3.forward);
                actor.Animate(.05f, false, false);
                Vector3 swipe = actor.RightPalm;
                actor.Animate(.2f, false, false);
                Check(Vector3.Distance(swipe, actor.RightPalm) > .1f && actor.jump == 0, "grounded steal sweep: " + roster);
                actor.ResetMotion();
                actor.Defend(true, actor.transform.position + Vector3.forward);
                actor.Animate(.24f, false, false);
                Check(actor.jump > .1f && actor.RightPalm.y > highHand.y + .3f && actor.LeftPalm.y > highHand.y + .3f,
                    "block jumps with both hands up: " + roster);
                actor.ResetMotion();
            }
            game.SelectCharacter(0);
            game.StartMatch();
            game.countdown=0;
            Check(game.State==MatchState.Playing && game.scores[0]==0 && game.remaining==210,"new match reset");
            game.GiveBall(0);
            game.players[0].transform.position = new Vector3(-3.4f, 0, -.5f);
            game.BeginCharge();
            Check(game.charging && game.ChargeWindow == BasketballRules.LongShotWindow, "beyond the arc aims for three seconds");
            float sweep = BasketballRules.ChargeSweepSeconds;
            game.AdvanceCharge(sweep * .5f);
            float rising = game.charge;
            game.AdvanceCharge(sweep * .5f);
            float peak = game.charge;
            game.AdvanceCharge(sweep * .5f);
            Check(rising > .45f && rising < .55f && peak > .99f && game.charge < .55f, "shot meter sweeps across and back");
            game.AdvanceCharge(BasketballRules.LongShotWindow - sweep * 1.5f - .01f);
            Check(game.charging && game.holder == 0, "long range charge holds until three seconds");
            game.AdvanceCharge(.02f);
            Check(!game.charging && game.holder == -1 && game.ShotInFlight, "three second limit releases real shot");
            game.GiveBall(0);
            game.players[0].transform.position = new Vector3(6.2f, 0, -1f);
            game.BeginCharge();
            Check(game.charging && game.ChargeWindow == BasketballRules.CloseShotWindow, "inside the arc aims for one and a half seconds");
            game.AdvanceCharge(BasketballRules.CloseShotWindow - .01f);
            Check(game.charging && game.holder == 0, "close range charge holds until 1.5 seconds");
            game.AdvanceCharge(.02f);
            Check(!game.charging && game.holder == -1 && game.ShotInFlight, "1.5 second limit releases real shot");
            game.StartMatch();
            game.countdown = 0;

            yield return new WaitForSeconds(1.5f);
            yield return Capture("02-court-center.png");
            game.cameraRig.SetView(0);
            yield return new WaitForSeconds(.5f);
            yield return Capture("03-court-left.png");
            Check(game.cameraRig.view==0,"left reference camera");
            game.cameraRig.SetView(2);
            yield return new WaitForSeconds(.5f);
            yield return Capture("04-court-right.png");
            game.cameraRig.SetView(1);
            game.TogglePause();
            float remaining = game.remaining;
            yield return new WaitForSecondsRealtime(.35f);
            Check(game.State==MatchState.Paused && Mathf.Approximately(remaining,game.remaining),"pause freezes clock");
            yield return Capture("05-pause.png");
            game.TogglePause();
            game.GiveBall(0);
            game.players[0].transform.position=new Vector3(6.2f,0,-1);
            game.players[1].transform.position=new Vector3(-7,0,3);
            game.ReleaseShot(0,.68f);
            yield return new WaitForSeconds(.4f);
            yield return Capture("06-shot.png");
            yield return new WaitForSeconds(3f);
            // Whether a shot drops is a roll against the difficulty odds, so what is checked is
            // that a make happens and is worth the right number of points, not that any single
            // release is automatic. Easy keeps the hunt for a three short.
            game.ShowHome();
            game.SetDifficulty(CourtDifficulty.Easy);
            game.StartMatch();
            game.countdown=0;
            yield return ScoreFrom(new Vector3(6.2f,0,-1),2,14,"real physics two-point shot scores two");
            yield return ScoreFrom(new Vector3(2.8f,0,-1.3f),3,24,"real physics three-point shot scores three");
            Check(game.holder==1,"scoring transfers possession to opponent");
            yield return new WaitForSeconds(1.7f);
            game.ShowHome();
            foreach (CourtDifficulty level in new[]{CourtDifficulty.Easy,CourtDifficulty.Normal,CourtDifficulty.Hard})
            {
                game.SetDifficulty(level);
                DifficultyTuning odds = game.Tuning;
                float two = level==CourtDifficulty.Easy?.93f:level==CourtDifficulty.Normal?.83f:.73f;
                float three = level==CourtDifficulty.Easy?.33f:level==CourtDifficulty.Normal?.23f:.13f;
                float steal = level==CourtDifficulty.Easy?.53f:level==CourtDifficulty.Normal?.33f:.13f;
                float block = level==CourtDifficulty.Easy?.73f:level==CourtDifficulty.Normal?.53f:.33f;
                Check(Mathf.Approximately(odds.Odds(true,2),two) && Mathf.Approximately(odds.Odds(true,3),three),
                    "shooting odds at " + odds.label);
                Check(Mathf.Approximately(odds.StealOdds(true),steal) && Mathf.Approximately(odds.BlockOdds(true),block),
                    "steal and block odds at " + odds.label);
                // A harder setting is a harder rival, not just a harder shot.
                Check(odds.Odds(false,2)>0 && odds.StealOdds(false)>0 && odds.BlockOdds(false)>0,
                    "the computer has its own odds at " + odds.label);
            }
            game.SetDifficulty(CourtDifficulty.Normal);
            game.StartMatch();
            game.countdown=0;
            for (int winner = 0; winner < 2; winner++)
            {
                game.StartMatch();
                game.countdown = 0;
                game.scores[winner] = winner == 0 ? 19 : 20;
                game.RegisterBasket(winner, winner == 0 ? 2 : 3);
                MatchState afterBasket = game.State;
                // The winning basket keeps its celebration; the result screen follows the pause.
                Check(afterBasket == MatchState.Playing && game.WinningScoreReached && game.remaining > 0,
                    "the winning basket still gets its moment: " + winner
                    + " [state=" + afterBasket + " scores=" + game.scores[0] + "/" + game.scores[1] + "]");
                game.FinishMatch();
                Check(game.State == MatchState.Result && game.remaining > 0 && game.scores[winner] >= 21,
                    "reaching or exceeding 21 ends the match before the clock: " + winner
                    + " [state=" + game.State + " remaining=" + game.remaining
                    + " scores=" + game.scores[0] + "/" + game.scores[1] + "]");
            }
            game.StartMatch();
            game.countdown = 0;
            game.scores[0]=game.scores[1]=5;
            game.remaining=0;
            game.FinishMatch();
            Check(game.overtime && game.remaining==30 && game.State==MatchState.Playing,"tie starts overtime");
            game.scores[0]=9;game.scores[1]=5;game.remaining=0;
            game.FinishMatch();
            Check(game.State==MatchState.Result,"winner receives result screen");
            yield return Capture("07-result.png");
            game.StartMatch();
            Check(game.scores[0]==0 && game.scores[1]==0 && !game.overtime,"replay resets scores and overtime");
            game.ShowHome();
            Check(game.State==MatchState.Home && Time.timeScale==1,"return to character selection");
            // The cup: three matches at most, a real opponent every round, and a champion either
            // way. Results are filed straight through FinishMatch rather than played out.
            game.SelectCharacter(0);
            game.SetCupMode(true);
            game.StartCup();
            Check(game.InCup && game.cup.round==CupRound.Quarter && game.cup.player==0 &&
                game.players[1].displayName!=game.players[0].displayName,"cup opens with a drawn quarter final");
            int cupMatches=0;
            while (game.InCup && !game.CupFinished && cupMatches<CupBracket.PlayerMatches+2)
            {
                game.countdown=0;
                game.scores[0]=BasketballRules.WinningScore;
                game.scores[1]=7;
                game.remaining=0;
                game.FinishMatch();
                cupMatches++;
                if (!game.CupFinished) game.ContinueAfterResult();
            }
            Check(cupMatches==CupBracket.PlayerMatches,"winning every tie takes exactly three matches");
            Check(game.cup.PlayerPlace==1 && game.cup.champion==0 && game.cup.runnerUp>=0 && game.cup.third>=0,
                "sweeping the cup crowns you champion");
            game.ShowHome();
            Check(!game.InCup,"leaving the cup clears the bracket");
            game.StartCup();
            game.countdown=0;
            game.scores[0]=6;
            game.scores[1]=BasketballRules.WinningScore;
            game.remaining=0;
            game.FinishMatch();
            Check(game.CupFinished && game.cup.PlayerPlace==5 && game.cup.champion>=0 && game.cup.champion!=0,
                "a quarter final loss ends your cup but still crowns a champion");
            game.ShowHome();
            game.SetCupMode(false);
            Application.logMessageReceived -= OnLog;
            string captures = skippedCaptures.Count == 0 ? ""
                : "\nCaptures skipped (no graphics device): "+string.Join(", ",skippedCaptures);
            string noise = editorNoise.Count == 0 ? ""
                : "\nEditor-only exceptions ignored: "+string.Join(" · ",editorNoise);
            File.WriteAllText(Path.Combine(output,"runtime.txt"),string.Join("\n",checks)+captures+noise+
                "\nRuntime errors: "+errors.Count+"\n"+string.Join("\n",errors));
            bool pass=checks.TrueForAll(x=>x.StartsWith("PASS")) && errors.Count==0;
            File.WriteAllText(Path.Combine(output,"runtime-result.txt"),pass?"PASS":"FAIL");
            Debug.Log("MIA_SMOKE_"+(pass?"PASS":"FAIL"));
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying=false;
            // A batch run has nobody to read the editor window, so it reports through the exit code.
            if (Application.isBatchMode) UnityEditor.EditorApplication.Exit(pass?0:1);
            #else
            Application.Quit(pass?0:1);
            #endif
        }

        /// <summary>Shoots from a spot until one drops, then checks what the make was worth.</summary>
        IEnumerator ScoreFrom(Vector3 spot,int points,int tries,string name)
        {
            int made = 0;
            for (int attempt = 0; attempt < tries && made == 0; attempt++)
            {
                game.GiveBall(0);
                game.players[0].transform.position = spot;
                game.players[1].transform.position = new Vector3(-7,0,3);
                int before = game.scores[0];
                game.ReleaseShot(0,.68f);
                float wait = 0;
                while (game.scores[0]==before && wait<3f) { wait += Time.deltaTime; yield return null; }
                made = game.scores[0]-before;
                if (made > 0)
                {
                    int settled = game.scores[0];
                    yield return new WaitForSeconds(.25f);
                    Check(game.scores[0]==settled,"one basket counted only once: "+points);
                    yield return new WaitForSeconds(1.45f);
                }
            }
            Check(made==points,name+" [made="+made+"]");
        }

        void Check(bool condition,string name) => checks.Add((condition?"PASS ":"FAIL ")+name);
        void OnLog(string message,string trace,LogType type)
        {
            if (type!=LogType.Exception && type!=LogType.Error && type!=LogType.Assert) return;
            // A cold project copy makes the editor's own search indexer throw on startup, which
            // says nothing about the game. Anything the game is anywhere near still fails the run;
            // only editor internals with no project frame in the stack are set aside.
            bool editorOnly = !string.IsNullOrEmpty(trace) && trace.Contains("UnityEditor.") && !trace.Contains("MiaCourt");
            if (editorOnly) editorNoise.Add(message.Split('\n')[0]);
            else errors.Add(message+"\n"+trace);
        }
        static bool CanRender => SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null;

        /// <summary>
        /// Screenshots need a frame to end. A -nographics run never ends one, so
        /// WaitForEndOfFrame would never return and the whole test would hang there; the checks
        /// matter more than the captures, so a headless run simply skips them.
        /// </summary>
        IEnumerator Capture(string name)
        {
            if (!CanRender) { skippedCaptures.Add(name); yield break; }
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(output,name));
            yield return null;
        }
    }
}
