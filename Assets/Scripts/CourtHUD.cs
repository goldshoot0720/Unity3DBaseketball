using System.Collections.Generic;
using UnityEngine;

namespace MiaCourt
{
    /// <summary>Keyboard and mouse controls over the existing sunset court.</summary>
    public sealed class CourtHUD : MonoBehaviour
    {
        public MiaBasketballGame game;
        readonly List<Rect> buttons = new List<Rect>();
        readonly Color paper = new Color(.96f, .94f, .85f);
        readonly Color ink = new Color(.045f, .09f, .085f, .96f);
        GUIStyle label, button;
        Font font;
        float scale = 1;
        float width, height;

        public bool PointerOverButton()
        {
            Vector2 point = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y) / scale;
            foreach (Rect rect in buttons) if (rect.Contains(point)) return true;
            return false;
        }

        void OnGUI()
        {
            if (game == null || game.players == null) return;
            if (label == null)
            {
                font = Resources.Load<Font>("NotoSansTC-Regular");
                if (font == null)
                    font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft JhengHei", "Noto Sans CJK TC", "Arial" }, 24);
                label = new GUIStyle(GUI.skin.label) { font = font, wordWrap = true };
                button = new GUIStyle(GUI.skin.button) { font = font, fontSize = 20, alignment = TextAnchor.MiddleCenter };
                button.normal.background = button.hover.background = button.active.background = Texture2D.whiteTexture;
                button.normal.textColor = button.hover.textColor = button.active.textColor = ink;
            }
            scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            width = Screen.width / scale;
            height = Screen.height / scale;
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
            buttons.Clear();
            if (game.State == MatchState.Home) Home();
            else
            {
                Scoreboard();
                if (game.State == MatchState.Paused) Pause();
                else if (game.State == MatchState.Result) Result();
                else Play();
            }
            GUI.matrix = previous;
        }

        void Home()
        {
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Tab)
                Event.current.Use();
            Fill(new Rect(24, 20, 430, height - 40), ink);
            Text(new Rect(48, 28, 390, 70), "喵喵\n街頭籃球", 34, paper, FontStyle.Bold);
            Text(new Rect(48, 102, 380, 28),
                game.cupMode ? "台北夕陽球場 · 八強單淘汰，最多 3 場" : "台北夕陽球場 · 一場定勝負", 18, paper);
            int previousSize = button.fontSize;
            button.fontSize = 18;
            if (Button(new Rect(48, 132, 175, 42), "單場比賽", !game.cupMode)) game.SetCupMode(false);
            if (Button(new Rect(231, 132, 175, 42), "連續比賽", game.cupMode)) game.SetCupMode(true);
            for (int level = 0; level < 3; level++)
            {
                var pick = (CourtDifficulty)level;
                if (Button(new Rect(48 + level * 121, 178, 116, 38), DifficultyTuning.LabelFor(pick), game.difficulty == pick))
                    game.SetDifficulty(pick);
            }
            DifficultyTuning odds = game.Tuning;
            Text(new Rect(48, 222, 380, 22),
                "你的命中率 兩分 " + Mathf.RoundToInt(odds.playerTwo * 100) + "% · 三分 " +
                Mathf.RoundToInt(odds.playerThree * 100) + "%   N 切換", 16, paper);
            bool pickingCpu = game.pickingOpponent && !game.cupMode;
            if (Button(new Rect(48, 248, game.cupMode ? 358 : 175, 42), "你  " + MiaCourtAssets.NameFor(game.selectedCharacter), !pickingCpu))
                game.pickingOpponent = false;
            if (!game.cupMode && Button(new Rect(231, 248, 175, 42), "對手  " + MiaCourtAssets.NameFor(game.selectedOpponent),
                    pickingCpu, CourtBuilder.Coral))
                game.pickingOpponent = true;
            Text(new Rect(48, 294, 380, 22),
                game.cupMode ? "點角色選你 · 七位對手由抽籤決定 · C 切換賽制"
                    : pickingCpu ? "點角色當對手 · Tab 改選你" : "點角色當你 · Tab 改選對手 · C 切換賽制",
                16, pickingCpu ? CourtBuilder.Coral : CourtBuilder.Mint);
            const int cols = 3;
            const float bw = 114;
            const float bh = 36;
            const float gap = 8;
            float gridY = 320;
            for (int i = 0; i < MiaCourtAssets.CharacterCount; i++)
            {
                int col = i % cols;
                int row = i / cols;
                var rect = new Rect(48 + col * (bw + gap), gridY + row * (bh + gap), bw, bh);
                bool you = i == game.selectedCharacter;
                bool cpu = !game.cupMode && i == game.selectedOpponent;
                if (Button(rect, MiaCourtAssets.NameFor(i), you || cpu, you ? CourtBuilder.Mint : CourtBuilder.Coral))
                    game.SelectRosterSlot(i);
            }
            button.fontSize = previousSize;
            int rows = (MiaCourtAssets.CharacterCount + cols - 1) / cols;
            float after = gridY + rows * (bh + gap) + 6;
            if (Button(new Rect(48, after, 358, 48), game.cupMode ? "開始連續比賽   Enter" : "開始比賽   Enter", true))
                game.StartSelected();
            Text(new Rect(48, after + 56, 358, 24), "3分30秒 · 先達21分勝 · 平手加賽", 17, paper);
            Text(new Rect(48, after + 82, 358, 76), "WASD 移動 · Shift 衝刺 · V 視角\n按住空白鍵開投籃條，掃到綠區放開\n三分線外 3 秒、線內 1.5 秒 · E 抄截", 16, paper);
            SoundButton();
        }

        void Scoreboard()
        {
            float x = (width - 654) / 2;
            Fill(new Rect(x, 24, 654, 100), ink);
            Text(new Rect(x + 22, 38, 175, 28), game.players[0].displayName + "  你", 18, CourtBuilder.Mint);
            Text(new Rect(x + 22, 66, 175, 46), game.scores[0].ToString(), 34, paper, FontStyle.Bold);
            Text(new Rect(x + 450, 38, 184, 28), game.players[1].displayName + "  電腦", 18, CourtBuilder.Coral);
            Text(new Rect(x + 450, 66, 175, 46), game.scores[1].ToString(), 34, paper, FontStyle.Bold);
            int seconds = Mathf.CeilToInt(game.remaining);
            Text(new Rect(x + 225, 34, 204, 44), (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00"),
                32, paper, FontStyle.Bold, TextAnchor.MiddleCenter);
            Text(new Rect(x + 210, 80, 234, 28),
                (game.overtime ? "加賽" : game.InCup ? game.CupRoundName : "一對一") + " · " + game.DifficultyName,
                17, paper, FontStyle.Normal, TextAnchor.MiddleCenter);
            if (Button(new Rect(width - 156, 26, 124, 44), "暫停 Esc")) game.TogglePause();
        }

        void Play()
        {
            bool stealReady = game.StealInRange;
            Fill(new Rect(32, height - 103, 520, 73), ink);
            Text(new Rect(48, height - 93, 488, 50), PlayHint(stealReady), 17, stealReady ? CourtBuilder.Mint : paper);
            Text(new Rect(width - 252, height - 92, 220, 32), "進攻時間  " + Mathf.CeilToInt(game.shotClock), 22, paper);
            Fill(new Rect(width - 250, height - 48, 216, 8), ink);
            Fill(new Rect(width - 250, height - 48, 216 * game.players[0].stamina, 8), CourtBuilder.Mint);
            if (game.messageTime > 0)
            {
                Fill(new Rect((width - 580) / 2, 142, 580, 52), ink);
                Text(new Rect((width - 560) / 2, 148, 560, 40), game.message, 24, game.messageColor,
                    FontStyle.Bold, TextAnchor.MiddleCenter);
            }
            else if (stealReady)
            {
                Fill(new Rect((width - 580) / 2, 142, 580, 52), ink);
                Text(new Rect((width - 560) / 2, 148, 560, 40), "按 E 或空白鍵抄截", 24, CourtBuilder.Mint,
                    FontStyle.Bold, TextAnchor.MiddleCenter);
            }
            if (game.countdown > 0)
                Text(new Rect((width - 160) / 2, height / 2 - 80, 160, 150), Mathf.CeilToInt(game.countdown).ToString(),
                    80, paper, FontStyle.Bold, TextAnchor.MiddleCenter);
            if (game.charging)
            {
                float x = (width - 300) / 2;
                float left = game.ChargeSecondsRemaining;
                Fill(new Rect(x - 16, height - 164, 332, 119), ink);
                Text(new Rect(x, height - 158, 300, 26), game.ChargeWindow > BasketballRules.CloseShotWindow ? "三分線外 · 3 秒內要出手" : "三分線內 · 1.5 秒內要出手",
                    17, paper, FontStyle.Normal, TextAnchor.MiddleCenter);
                Text(new Rect(x, height - 130, 300, 28), "綠區放開 · 剩 " + left.ToString("0.0") + " 秒",
                    19, left < .5f ? CourtBuilder.Coral : paper, FontStyle.Bold, TextAnchor.MiddleCenter);
                Fill(new Rect(x, height - 92, 300, 14), new Color(.32f, .39f, .35f));
                Fill(new Rect(x + 188, height - 96, 32, 22), CourtBuilder.Mint);
                Fill(new Rect(x + game.charge * 296, height - 100, 4, 30), Color.white);
            }
        }

        void Pause()
        {
            float x = (width - 360) / 2;
            Fill(new Rect(x, 200, 360, 370), ink);
            Text(new Rect(x + 24, 222, 312, 56), "比賽暫停", 32, paper, FontStyle.Bold);
            if (Button(new Rect(x + 24, 290, 312, 52), "繼續比賽   Esc", true)) game.TogglePause();
            if (Button(new Rect(x + 24, 358, 312, 52), "回到主畫面")) game.ShowHome();
            Text(new Rect(x + 24, 428, 312, 118), "WASD 移動 · Shift 衝刺\n空白鍵開投籃條，綠區放開出手\n三分線外 3 秒、線內 1.5 秒\n防守時按 E 或空白鍵抄截", 17, paper);
            SoundButton();
        }

        string PlayHint(bool stealReady)
        {
            if (game.holder == 0)
                return "WASD 移動 · Shift 衝刺 · 空白鍵開投籃條\n投籃條掃到綠區放開出手 · V 切換視角";
            if (stealReady)
                return "按 E 或空白鍵抄截！\nWASD 移動 · Shift 衝刺 · V 切換視角";
            if (game.holder < 0)
                return "WASD 移動 · 靠近球撿起 · E 撥球\n空白鍵也可撥球 · V 切換視角";
            return "WASD 移動 · Shift 衝刺 · E 抄截\n靠近持球者再按 E 或空白鍵 · V 切換視角";
        }

        void Result()
        {
            if (game.InCup) { CupResult(); return; }
            float x = (width - 440) / 2;
            Fill(new Rect(x, 215, 440, 355), ink);
            Text(new Rect(x + 28, 239, 384, 58), game.scores[0] > game.scores[1] ? "你贏了！" : "下場再挑戰", 36, paper, FontStyle.Bold);
            Text(new Rect(x + 28, 316, 384, 64), "命中 " + game.baskets[0] + " / " + game.attempts[0] + " 球\n最高得分  " + game.BestScore, 21, paper);
            if (Button(new Rect(x + 28, 410, 384, 52), "再打一場   Enter", true)) game.ContinueAfterResult();
            if (Button(new Rect(x + 28, 484, 384, 48), "回到主畫面")) game.ShowHome();
        }

        void CupResult()
        {
            CupBracket cup = game.cup;
            const float w = 560;
            float x = (width - w) / 2;
            bool over = cup.round == CupRound.Over;
            Fill(new Rect(x, 120, w, 440), ink);
            string place = CupBracket.PlaceName(cup.PlayerPlace);
            Text(new Rect(x + 28, 142, w - 56, 54),
                over ? (cup.PlayerPlace <= 3 ? place + "！" : place) : cup.LastRoundName + (cup.PlayerAdvanced ? " · 晉級" : " · 淘汰"),
                34, cup.PlayerAdvanced || cup.PlayerPlace == 1 ? CourtBuilder.Mint : paper, FontStyle.Bold);
            float y = 204;
            if (cup.LastPlayerTie != null)
            {
                Text(new Rect(x + 28, y, w - 56, 28), TieLine(cup.LastPlayerTie), 20, CourtBuilder.Mint, FontStyle.Bold);
                y += 32;
            }
            Text(new Rect(x + 28, y, w - 56, 22), "同輪其他場次", 15, paper);
            y += 24;
            foreach (CupTie tie in cup.LastRoundTies)
            {
                if (tie == null || tie == cup.LastPlayerTie) continue;
                Text(new Rect(x + 28, y, w - 56, 24), TieLine(tie), 16, paper);
                y += 24;
            }
            y += 10;
            if (over)
                Text(new Rect(x + 28, y, w - 56, 52),
                    "冠軍 " + MiaCourtAssets.NameFor(cup.champion) + " · 亞軍 " + MiaCourtAssets.NameFor(cup.runnerUp) +
                    "\n季軍 " + MiaCourtAssets.NameFor(cup.third) + " · 命中 " + game.baskets[0] + " / " + game.attempts[0] + " 球",
                    18, paper);
            else
            {
                CupTie next = cup.PlayerTie();
                Text(new Rect(x + 28, y, w - 56, 28),
                    "下一場 " + cup.NextMatchName() + " · 對手 " + MiaCourtAssets.NameFor(next.Other(cup.player)), 19, paper);
            }
            if (Button(new Rect(x + 28, 430, w - 56, 52), over ? "再抽一次籤   Enter" : "打下一場   Enter", true))
                game.ContinueAfterResult();
            if (Button(new Rect(x + 28, 494, w - 56, 48), "回到主畫面")) game.ShowHome();
        }

        /// <summary>One line of the bracket, with the player's side called out.</summary>
        string TieLine(CupTie tie)
        {
            string left = MiaCourtAssets.NameFor(tie.left);
            string right = MiaCourtAssets.NameFor(tie.right);
            if (game.cup != null && tie.left == game.cup.player) left = "你 · " + left;
            else if (game.cup != null && tie.right == game.cup.player) right += " · 你";
            return left + "   " + tie.leftScore + " － " + tie.rightScore + "   " + right;
        }

        void SoundButton()
        {
            if (Button(new Rect(width - 176, height - 76, 144, 46), game.sound.Muted ? "音效：關  M" : "音效：開  M"))
                game.sound.ToggleMute();
        }

        bool Button(Rect rect, string text, bool primary = false, Color accent = default)
        {
            buttons.Add(rect);
            Color before = GUI.backgroundColor;
            GUI.backgroundColor = primary ? (accent.a > 0 ? accent : CourtBuilder.Mint) : paper;
            bool clicked = GUI.Button(rect, text, button);
            GUI.backgroundColor = before;
            return clicked;
        }

        void Text(Rect rect, string text, int size, Color color, FontStyle weight = FontStyle.Normal, TextAnchor alignment = TextAnchor.UpperLeft)
        {
            label.fontSize = size;
            label.fontStyle = weight;
            label.alignment = alignment;
            label.normal.textColor = color;
            GUI.Label(rect, text, label);
        }

        static void Fill(Rect rect, Color color)
        {
            Color before = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = before;
        }

        void OnDestroy() { if (font != null) Destroy(font); }
    }
}
