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
            Check(MiaCourtAssets.CharacterCount==10 && MiaCourtAssets.NameFor(3)=="牙妹" && MiaCourtAssets.NameFor(9)=="塗董","ten playable characters");
            Check(game.players[0].displayName=="喵白白" && game.players[1].displayName=="喵布布","GLB character mapping");
            yield return Capture("01-home.png");
            game.SelectCharacter(3);
            Check(game.players[0].displayName=="牙妹" && game.players[1].displayName=="魚妹","selecting 牙妹");
            game.SelectCharacter(9);
            Check(game.players[0].displayName=="塗董" && game.players[1].displayName=="喵白白","selecting 塗董 wraps opponent");
            game.SelectCharacter(0);
            Check(game.players[0].displayName=="喵白白" && game.players[1].displayName=="喵布布","default pairing restored");
            game.StartMatch();
            game.countdown=0;
            Check(game.State==MatchState.Playing && game.scores[0]==0 && game.remaining==180,"new match reset");
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
            int scoreBefore=game.scores[0];
            game.ReleaseShot(0,.68f);
            yield return new WaitForSeconds(.4f);
            yield return Capture("06-shot.png");
            float wait=0;
            while (game.scores[0]==scoreBefore && wait<4f) { wait+=Time.deltaTime; yield return null; }
            Check(game.scores[0]==scoreBefore+2,"real physics perfect two-point shot scores");
            int firstScore=game.scores[0];
            yield return new WaitForSeconds(.25f);
            Check(game.scores[0]==firstScore,"one basket counted only once");
            yield return new WaitForSeconds(1.6f);
            Check(game.holder==1,"scoring transfers possession to opponent");
            game.GiveBall(0);
            game.players[0].transform.position=new Vector3(2.8f,0,-1.3f);
            game.players[1].transform.position=new Vector3(-7,0,3);
            scoreBefore=game.scores[0];
            game.ReleaseShot(0,.68f);
            wait=0;
            while (game.scores[0]==scoreBefore && wait<4f) { wait+=Time.deltaTime; yield return null; }
            Check(game.scores[0]==scoreBefore+3,"real physics perfect three-point shot scores");
            yield return new WaitForSeconds(1.7f);
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
            Application.logMessageReceived -= OnLog;
            File.WriteAllText(Path.Combine(output,"runtime.txt"),string.Join("\n",checks)+"\nRuntime errors: "+errors.Count+"\n"+string.Join("\n",errors));
            bool pass=checks.TrueForAll(x=>x.StartsWith("PASS")) && errors.Count==0;
            File.WriteAllText(Path.Combine(output,"runtime-result.txt"),pass?"PASS":"FAIL");
            Debug.Log("MIA_SMOKE_"+(pass?"PASS":"FAIL"));
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying=false;
            #else
            Application.Quit(pass?0:1);
            #endif
        }

        void Check(bool condition,string name) => checks.Add((condition?"PASS ":"FAIL ")+name);
        void OnLog(string message,string trace,LogType type)
        {
            if (type==LogType.Exception || type==LogType.Error || type==LogType.Assert) errors.Add(message+"\n"+trace);
        }
        IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(output,name));
            yield return null;
        }
    }
}
