using System.Collections.Generic;
using UnityEngine;

namespace MiaCourt
{
    /// <summary>
    /// Keyboard and mouse controls over the sunset court. Everything is laid out on a 1280x720
    /// canvas and scaled to the window. Each screen draws itself in full rather than sharing
    /// layout code: repeating a few rectangles is cheaper than a layout that nobody can read.
    /// </summary>
    public sealed class CourtHUD : MonoBehaviour
    {
        public MiaBasketballGame game;
        readonly List<Rect> buttons = new List<Rect>();

        // Palette. Paper on ink, with mint for you and coral for the computer throughout.
        readonly Color paper = new Color(.96f, .94f, .85f);
        readonly Color muted = new Color(.66f, .72f, .68f);
        readonly Color ink = new Color(.045f, .09f, .085f, .94f);
        readonly Color inkDeep = new Color(.02f, .05f, .045f, .97f);
        readonly Color track = new Color(.20f, .27f, .25f);
        readonly Color amber = new Color(1f, .78f, .30f);
        readonly Color danger = new Color(1f, .36f, .30f);
        Color Mint => CourtBuilder.Mint;
        Color Coral => CourtBuilder.Coral;

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
                label = new GUIStyle(GUI.skin.label) { font = font, wordWrap = true, clipping = TextClipping.Overflow };
                button = new GUIStyle(GUI.skin.button) { font = font, fontSize = 20, alignment = TextAnchor.MiddleCenter };
                button.normal.background = button.hover.background = button.active.background = Texture2D.whiteTexture;
                button.normal.textColor = button.hover.textColor = button.active.textColor = new Color(.045f, .09f, .085f);
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

        // ------------------------------------------------------------------ home

        void Home()
        {
            // Tab switches between picking you and picking the opponent; keep it from moving
            // IMGUI keyboard focus between buttons at the same time.
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Tab)
                Event.current.Use();

            const float px = 40, py = 24, pw = 530, ph = 672;
            const float x = px + 32, w = pw - 64;
            Fill(new Rect(px, py, pw, ph), ink);
            Text(new Rect(x, py + 18, w, 52), "喵喵街頭籃球", 38, paper, FontStyle.Bold);
            Text(new Rect(x, py + 74, w, 26), "台北夕陽球場 · 一對一街頭籃球", 17, muted);

            int previousSize = button.fontSize;

            // Mode
            SectionLabel(new Rect(x, py + 114, w, 20), "賽制", "C 切換");
            button.fontSize = 19;
            float half = (w - 10) / 2;
            if (Button(new Rect(x, py + 138, half, 46), "單場比賽", !game.cupMode)) game.SetCupMode(false);
            if (Button(new Rect(x + half + 10, py + 138, half, 46), "連續比賽", game.cupMode)) game.SetCupMode(true);
            Text(new Rect(x, py + 190, w, 24),
                game.cupMode ? "八強抽籤單淘汰 · 八強 → 四強 → 冠軍賽／季軍賽 · 最多 3 場"
                             : "你挑對手，一場定勝負",
                15, paper);

            // Difficulty
            SectionLabel(new Rect(x, py + 226, w, 20), "難度", "N 切換");
            button.fontSize = 18;
            float third = (w - 20) / 3;
            for (int level = 0; level < 3; level++)
            {
                var pick = (CourtDifficulty)level;
                Color accent = level == 0 ? Mint : level == 1 ? amber : danger;
                if (Button(new Rect(x + level * (third + 10), py + 250, third, 44), DifficultyTuning.LabelFor(pick),
                        game.difficulty == pick, accent))
                    game.SetDifficulty(pick);
            }
            DifficultyTuning odds = game.Tuning;
            float col = (w - 16) / 2;
            StatBar(new Rect(x, py + 304, col, 24), "兩分命中", odds.playerTwo, Mint);
            StatBar(new Rect(x + col + 16, py + 304, col, 24), "三分命中", odds.playerThree, Mint);
            StatBar(new Rect(x, py + 332, col, 24), "抄截成功", odds.playerSteal, amber);
            StatBar(new Rect(x + col + 16, py + 332, col, 24), "火鍋成功", odds.playerBlock, amber);

            // Characters
            bool pickingCpu = game.pickingOpponent && !game.cupMode;
            SectionLabel(new Rect(x, py + 372, w, 20), "角色", game.cupMode ? "" : "Tab 切換你／對手");
            button.fontSize = 18;
            if (game.cupMode)
            {
                Button(new Rect(x, py + 396, w, 44), "你  ·  " + MiaCourtAssets.NameFor(game.selectedCharacter), true);
            }
            else
            {
                if (Button(new Rect(x, py + 396, half, 44), "你  ·  " + MiaCourtAssets.NameFor(game.selectedCharacter), !pickingCpu))
                    game.pickingOpponent = false;
                if (Button(new Rect(x + half + 10, py + 396, half, 44), "對手  ·  " + MiaCourtAssets.NameFor(game.selectedOpponent),
                        pickingCpu, Coral))
                    game.pickingOpponent = true;
            }
            string pickHint = game.cupMode ? "點角色選你，其餘七位抽籤決定對手"
                : pickingCpu ? "現在點角色 → 設為對手" : "現在點角色 → 設為你";
            Text(new Rect(x, py + 444, w, 22), pickHint, 15, pickingCpu ? Coral : Mint);

            const int cols = 4;
            const float gap = 6;
            float bw = (w - gap * (cols - 1)) / cols;
            const float bh = 40;
            button.fontSize = 17;
            for (int i = 0; i < MiaCourtAssets.CharacterCount; i++)
            {
                var rect = new Rect(x + (i % cols) * (bw + gap), py + 472 + (i / cols) * (bh + 8), bw, bh);
                bool you = i == game.selectedCharacter;
                bool cpu = !game.cupMode && i == game.selectedOpponent;
                if (Button(rect, MiaCourtAssets.NameFor(i), you || cpu, you ? Mint : Coral))
                    game.SelectRosterSlot(i);
            }

            button.fontSize = 22;
            if (Button(new Rect(x, py + 574, w, 54), game.cupMode ? "開始連續比賽    Enter" : "開始比賽    Enter", true))
                game.StartSelected();
            button.fontSize = previousSize;
            Text(new Rect(x, py + 636, w, 24), "3 分 30 秒 · 先到 21 分獲勝 · 時間到比分高者勝 · 平手加賽 30 秒", 14, muted);

            ControlsCard();
            CornerButtons();
        }

        /// <summary>The full control list, kept off the setup panel so the choices stay readable.</summary>
        void ControlsCard()
        {
            float cw = 360, ch = 170;
            float cx = width - cw - 32, cy = height - ch - 86;
            Fill(new Rect(cx, cy, cw, ch), ink);
            Text(new Rect(cx + 20, cy + 12, cw - 40, 22), "操作", 15, muted, FontStyle.Bold);
            KeyRow(cx + 20, cy + 38, "WASD", "移動　　Shift 衝刺");
            KeyRow(cx + 20, cy + 64, "空白鍵", "按住開投籃條，掃到綠區放開");
            KeyRow(cx + 20, cy + 90, "E", "抄截／跳起蓋火鍋");
            KeyRow(cx + 20, cy + 116, "V", "切換視角　　Esc 暫停");
            Text(new Rect(cx + 20, cy + 142, cw - 40, 20), "三分線外 3 秒內出手 · 線內 1.5 秒", 14, amber);
        }

        void KeyRow(float x, float y, string key, string meaning)
        {
            Fill(new Rect(x, y + 1, 64, 22), track);
            Text(new Rect(x, y, 64, 24), key, 14, paper, FontStyle.Bold, TextAnchor.MiddleCenter);
            Text(new Rect(x + 76, y, 260, 24), meaning, 15, paper, FontStyle.Normal, TextAnchor.MiddleLeft);
        }

        // ------------------------------------------------------------------ scoreboard

        void Scoreboard()
        {
            const float sw = 700;
            float x = (width - sw) / 2;
            Fill(new Rect(x, 20, sw, 104), ink);

            // Possession: a lit dot beside whoever has the ball.
            bool youHave = game.holder == 0, cpuHas = game.holder == 1;
            Fill(new Rect(x + 20, 38, 10, 10), youHave ? Mint : track);
            Text(new Rect(x + 38, 30, 210, 26), game.players[0].displayName + "  你", 18, Mint, FontStyle.Bold);
            Text(new Rect(x + 38, 58, 160, 58), game.scores[0].ToString(), 44, paper, FontStyle.Bold);

            Fill(new Rect(x + sw - 30, 38, 10, 10), cpuHas ? Coral : track);
            Text(new Rect(x + sw - 250, 30, 212, 26), "電腦  " + game.players[1].displayName, 18, Coral, FontStyle.Bold, TextAnchor.UpperRight);
            Text(new Rect(x + sw - 200, 58, 162, 58), game.scores[1].ToString(), 44, paper, FontStyle.Bold, TextAnchor.UpperRight);

            // First to 21: how close each side is, as a thin bar under the score.
            float race = 130;
            Fill(new Rect(x + 38, 114, race, 4), track);
            Fill(new Rect(x + 38, 114, race * Mathf.Clamp01(game.scores[0] / (float)BasketballRules.WinningScore), 4), Mint);
            Fill(new Rect(x + sw - 38 - race, 114, race, 4), track);
            float cpuRace = race * Mathf.Clamp01(game.scores[1] / (float)BasketballRules.WinningScore);
            Fill(new Rect(x + sw - 38 - cpuRace, 114, cpuRace, 4), Coral);

            int seconds = Mathf.CeilToInt(game.remaining);
            Color clock = game.overtime ? amber : seconds <= 10 ? danger : paper;
            Text(new Rect(x + 250, 26, 200, 50), (seconds / 60).ToString("0") + ":" + (seconds % 60).ToString("00"),
                38, clock, FontStyle.Bold, TextAnchor.MiddleCenter);
            string chip = (game.overtime ? "加賽" : game.InCup ? game.CupRoundName : "單場") + "  ·  " + game.DifficultyName
                + "  ·  搶 " + BasketballRules.WinningScore;
            Text(new Rect(x + 210, 78, 280, 24), chip, 15, muted, FontStyle.Normal, TextAnchor.MiddleCenter);

            int previousSize = button.fontSize;
            button.fontSize = 17;
            if (game.State == MatchState.Playing && Button(new Rect(width - 148, 24, 116, 42), "暫停  Esc")) game.TogglePause();
            button.fontSize = previousSize;
        }

        // ------------------------------------------------------------------ play

        void Play()
        {
            bool stealReady = game.StealInRange;

            // Bottom left: what the keys do right now. It steps aside while the meter is up.
            if (!game.charging)
            {
                Fill(new Rect(32, height - 112, 470, 80), ink);
                Text(new Rect(52, height - 102, 60, 22), SituationLabel(), 14, SituationColour(), FontStyle.Bold);
                Text(new Rect(52, height - 80, 440, 48), PlayHint(), 16, paper);
            }

            StatusCard();

            // Centre banner: the latest message, or the steal prompt when nothing else is up.
            if (game.messageTime > 0)
                Banner(game.message, game.messageColor, Mathf.Clamp01(game.messageTime / .25f));
            else if (stealReady)
                Banner("靠近了！按 E 抄截", Mint, 1);

            if (game.countdown > 0)
            {
                int n = Mathf.CeilToInt(game.countdown);
                float pulse = 1f - (game.countdown - Mathf.Floor(game.countdown));
                Text(new Rect((width - 300) / 2, height / 2 - 110, 300, 170), n.ToString(),
                    Mathf.RoundToInt(96 + pulse * 30), paper, FontStyle.Bold, TextAnchor.MiddleCenter);
            }

            if (game.charging) ShotMeter();
            ScorePopup();
        }

        string SituationLabel()
        {
            if (game.holder == 0) return "進攻";
            if (game.holder == 1) return "防守";
            return "搶球";
        }

        Color SituationColour()
        {
            if (game.holder == 0) return Mint;
            if (game.holder == 1) return Coral;
            return amber;
        }

        string PlayHint()
        {
            if (game.holder == 0)
                return game.charging ? "看投籃條，掃到綠區就放開空白鍵" : "按住空白鍵開投籃條 · WASD 移動 · Shift 衝刺";
            if (game.holder == 1)
                return game.StealInRange ? "現在按 E 抄截！失手會有短暫硬直" : "貼近持球者再按 E 抄截 · 對方出手時按 E 跳起封蓋";
            return "跑到球旁邊自動撿起 · 球在空中時按 E 封蓋";
        }

        /// <summary>Shot clock and stamina, bottom right.</summary>
        void StatusCard()
        {
            const float cw = 260, ch = 112;
            float cx = width - cw - 32, cy = height - ch - 32;
            Fill(new Rect(cx, cy, cw, ch), ink);

            float shot = game.shotClock;
            bool urgent = game.holder >= 0 && shot <= 5f;
            Text(new Rect(cx + 18, cy + 10, 120, 22), "進攻時間", 14, muted);
            Text(new Rect(cx + cw - 110, cy + 2, 92, 40), game.holder >= 0 ? Mathf.CeilToInt(shot).ToString() : "—",
                32, urgent ? danger : paper, FontStyle.Bold, TextAnchor.UpperRight);
            Fill(new Rect(cx + 18, cy + 44, cw - 36, 6), track);
            Fill(new Rect(cx + 18, cy + 44, (cw - 36) * Mathf.Clamp01(shot / BasketballRules.PossessionSeconds), 6),
                urgent ? danger : paper);

            float stamina = game.players[0].stamina;
            Text(new Rect(cx + 18, cy + 62, 120, 22), "體力", 14, muted);
            Text(new Rect(cx + cw - 110, cy + 60, 92, 24), Mathf.RoundToInt(stamina * 100) + "%", 16,
                stamina < .2f ? danger : paper, FontStyle.Bold, TextAnchor.UpperRight);
            Fill(new Rect(cx + 18, cy + 88, cw - 36, 8), track);
            Fill(new Rect(cx + 18, cy + 88, (cw - 36) * stamina, 8), stamina < .2f ? danger : Mint);
        }

        void Banner(string text, Color colour, float alpha)
        {
            const float bw = 620, bh = 56;
            float bx = (width - bw) / 2, by = 142;
            Color back = ink; back.a *= alpha;
            Fill(new Rect(bx, by, bw, bh), back);
            Color stripe = colour; stripe.a = alpha;
            Fill(new Rect(bx, by, 6, bh), stripe);
            Color face = colour; face.a = alpha;
            Text(new Rect(bx + 16, by + 6, bw - 32, 44), text, 26, face, FontStyle.Bold, TextAnchor.MiddleCenter);
        }

        /// <summary>
        /// The meter the shot hangs on. It shows the time left as a number and a draining bar,
        /// the good and perfect bands on the track, which way the needle is travelling, and
        /// says "放開" out loud while a release would land in the good band.
        /// </summary>
        void ShotMeter()
        {
            const float mw = 560, mh = 170;
            float mx = (width - mw) / 2, my = height - mh - 24;
            Fill(new Rect(mx, my, mw, mh), inkDeep);

            bool longShot = game.ChargeWindow > BasketballRules.CloseShotWindow;
            float left = game.ChargeSecondsRemaining;
            float window = Mathf.Max(.01f, game.ChargeWindow);
            Color timeColour = left < .5f ? danger : left < 1f ? amber : paper;

            Text(new Rect(mx + 24, my + 12, 300, 24), longShot ? "三分球 · 3 秒內出手" : "兩分球 · 1.5 秒內出手", 16,
                longShot ? amber : muted, FontStyle.Bold);
            Text(new Rect(mx + 24, my + 36, 240, 44), left.ToString("0.0") + " 秒", 34, timeColour, FontStyle.Bold);

            float quality = game.ChargeQuality;
            if (quality >= .90f)
                Text(new Rect(mx + mw - 304, my + 30, 280, 50), "完美！放開", 34, Mint, FontStyle.Bold, TextAnchor.MiddleRight);
            else if (quality >= .67f)
                Text(new Rect(mx + mw - 304, my + 30, 280, 50), "放開！", 34, Mint, FontStyle.Bold, TextAnchor.MiddleRight);
            else
                Text(new Rect(mx + mw - 304, my + 38, 280, 36), "等綠區…", 22, muted, FontStyle.Normal, TextAnchor.MiddleRight);

            // Time left, draining right to left.
            Fill(new Rect(mx + 24, my + 86, mw - 48, 4), track);
            Fill(new Rect(mx + 24, my + 86, (mw - 48) * Mathf.Clamp01(left / window), 4), timeColour);

            // The meter itself.
            float tx = mx + 24, ty = my + 104, tw = mw - 48, th = 34;
            Fill(new Rect(tx, ty, tw, th), track);
            float good = BasketballRules.GoodReleaseBand, perfect = BasketballRules.PerfectReleaseBand;
            float centre = BasketballRules.PerfectRelease;
            Color goodBand = Mint; goodBand.a = .35f;
            Fill(new Rect(tx + tw * (centre - good), ty, tw * good * 2, th), goodBand);
            Fill(new Rect(tx + tw * (centre - perfect), ty, tw * perfect * 2, th), Mint);

            // Needle, with a short fading tail on the side it came from.
            float nx = tx + tw * Mathf.Clamp01(game.charge);
            bool rising = game.ChargeRising;
            for (int i = 1; i <= 4; i++)
            {
                float offset = i * 7f * (rising ? -1 : 1);
                Color tail = Color.white; tail.a = .32f - i * .07f;
                Fill(new Rect(nx + offset - 2, ty + 4, 4, th - 8), tail);
            }
            Color needle = quality >= .90f ? Mint : quality >= .67f ? Color.white : new Color(1f, 1f, 1f, .85f);
            Fill(new Rect(nx - 3, ty - 8, 6, th + 16), needle);

            Text(new Rect(tx, ty + th + 2, 80, 20), "太早", 13, muted);
            Text(new Rect(tx + tw * centre - 40, ty + th + 2, 80, 20), "完美", 13, Mint, FontStyle.Bold, TextAnchor.UpperCenter);
            Text(new Rect(tx + tw - 80, ty + th + 2, 80, 20), "太晚", 13, muted, FontStyle.Normal, TextAnchor.UpperRight);
        }

        /// <summary>A "+2" or "+3" that rises off the hoop the ball went through, then fades.</summary>
        void ScorePopup()
        {
            if (game.basketFlash <= 0 || game.courtCamera == null) return;
            Vector3 hoop = BasketballRules.HoopFor(game.basketFlashTeam) + Vector3.up * .7f;
            Vector3 screen = game.courtCamera.WorldToScreenPoint(hoop);
            if (screen.z <= 0) return;
            float life = 1f - game.basketFlash / 1.6f;
            float gx = Mathf.Clamp(screen.x / scale, 90, width - 90);
            float gy = Mathf.Clamp((Screen.height - screen.y) / scale - life * 80f, 150, height - 150);
            Color colour = game.basketFlashTeam == 0 ? Mint : Coral;
            colour.a = Mathf.Clamp01(game.basketFlash / .5f);
            Color shadow = new Color(0, 0, 0, colour.a * .6f);
            string text = "+" + game.basketFlashPoints;
            int size = game.basketFlashPoints == 3 ? 64 : 54;
            Text(new Rect(gx - 98, gy - 48, 200, 90), text, size, shadow, FontStyle.Bold, TextAnchor.MiddleCenter);
            Text(new Rect(gx - 100, gy - 50, 200, 90), text, size, colour, FontStyle.Bold, TextAnchor.MiddleCenter);
        }

        // ------------------------------------------------------------------ pause

        void Pause()
        {
            Fill(new Rect(0, 0, width, height), new Color(0, 0, 0, .35f));
            const float pw = 420, ph = 400;
            float x = (width - pw) / 2, y = (height - ph) / 2 + 30;
            Fill(new Rect(x, y, pw, ph), inkDeep);
            Text(new Rect(x + 32, y + 24, pw - 64, 48), "比賽暫停", 34, paper, FontStyle.Bold);
            Text(new Rect(x + 32, y + 76, pw - 64, 24),
                (game.InCup ? game.CupRoundName : "單場比賽") + " · " + game.DifficultyName + " · " +
                game.scores[0] + " : " + game.scores[1], 16, muted);
            int previousSize = button.fontSize;
            button.fontSize = 21;
            if (Button(new Rect(x + 32, y + 120, pw - 64, 54), "繼續比賽    Esc", true)) game.TogglePause();
            button.fontSize = 18;
            if (Button(new Rect(x + 32, y + 186, pw - 64, 46), game.sound.Muted ? "音效：關    M" : "音效：開    M"))
                game.sound.ToggleMute();
            if (Button(new Rect(x + 32, y + 242, pw - 64, 46), game.InCup ? "放棄這次連續比賽，回主畫面" : "回到主畫面"))
                game.ShowHome();
            button.fontSize = previousSize;
            Text(new Rect(x + 32, y + 304, pw - 64, 80),
                "空白鍵：按住開投籃條，綠區放開\nE：抄截／蓋火鍋　V：切換視角\n三分線外 3 秒、線內 1.5 秒內出手", 15, paper);
        }

        // ------------------------------------------------------------------ result

        void Result()
        {
            if (game.InCup) { CupResult(); return; }
            Fill(new Rect(0, 0, width, height), new Color(0, 0, 0, .30f));
            const float pw = 520, ph = 470;
            float x = (width - pw) / 2, y = 150;
            Fill(new Rect(x, y, pw, ph), inkDeep);
            bool won = game.scores[0] > game.scores[1];
            Fill(new Rect(x, y, pw, 6), won ? Mint : Coral);
            Text(new Rect(x + 32, y + 26, pw - 64, 56), won ? "你贏了！" : "輸了，再來一場", 40, won ? Mint : paper, FontStyle.Bold);
            Text(new Rect(x + 32, y + 84, pw - 64, 24),
                game.players[0].displayName + " 對 " + game.players[1].displayName + " · " + game.DifficultyName +
                (game.overtime ? " · 加賽" : ""), 16, muted);
            Text(new Rect(x + 32, y + 112, pw - 64, 70), game.scores[0] + "  :  " + game.scores[1], 54, paper, FontStyle.Bold);

            MatchStats(x + 32, y + 196, pw - 64);

            int previousSize = button.fontSize;
            button.fontSize = 21;
            if (Button(new Rect(x + 32, y + 330, pw - 64, 54), "再打一場    Enter", true)) game.ContinueAfterResult();
            button.fontSize = 18;
            if (Button(new Rect(x + 32, y + 396, pw - 64, 46), "回到主畫面")) game.ShowHome();
            button.fontSize = previousSize;
        }

        /// <summary>Four numbers that say how the match went, plus the best score on record.</summary>
        void MatchStats(float x, float y, float w)
        {
            float cell = (w - 30) / 4;
            int made = game.baskets[0], tried = game.attempts[0];
            string rate = tried > 0 ? Mathf.RoundToInt(100f * made / tried) + "%" : "—";
            StatCell(new Rect(x, y, cell, 72), "命中", made + "/" + tried);
            StatCell(new Rect(x + (cell + 10), y, cell, 72), "命中率", rate);
            StatCell(new Rect(x + (cell + 10) * 2, y, cell, 72), "抄截", game.steals.ToString());
            StatCell(new Rect(x + (cell + 10) * 3, y, cell, 72), "火鍋", game.blocks.ToString());
            Text(new Rect(x, y + 84, w, 24), "最高得分紀錄  " + game.BestScore, 15, muted);
        }

        void StatCell(Rect rect, string name, string value)
        {
            Fill(rect, track);
            Text(new Rect(rect.x, rect.y + 6, rect.width, 22), name, 14, muted, FontStyle.Normal, TextAnchor.UpperCenter);
            Text(new Rect(rect.x, rect.y + 28, rect.width, 38), value, 26, paper, FontStyle.Bold, TextAnchor.UpperCenter);
        }

        // ------------------------------------------------------------------ cup

        void CupResult()
        {
            CupBracket cup = game.cup;
            Fill(new Rect(0, 0, width, height), new Color(0, 0, 0, .35f));
            const float pw = 900, ph = 610;
            float px = (width - pw) / 2, py = 70;
            Fill(new Rect(px, py, pw, ph), inkDeep);
            bool over = cup.round == CupRound.Over;
            bool good = over ? cup.PlayerPlace <= 3 : cup.PlayerAdvanced;
            Fill(new Rect(px, py, pw, 6), good ? Mint : Coral);

            string headline = over
                ? (cup.PlayerPlace == 1 ? "冠軍！" : cup.PlayerPlace <= 3 ? CupBracket.PlaceName(cup.PlayerPlace) + "！" : CupBracket.PlaceName(cup.PlayerPlace))
                : cup.LastRoundName + (cup.PlayerAdvanced ? " · 晉級！" : " · 淘汰");
            Text(new Rect(px + 32, py + 20, pw - 64, 50), headline, 36, good ? Mint : paper, FontStyle.Bold);
            if (cup.LastPlayerTie != null)
            {
                CupTie t = cup.LastPlayerTie;
                int mine = t.ScoreFor(cup.player), theirs = t.ScoreFor(t.Other(cup.player));
                Text(new Rect(px + 32, py + 70, pw - 64, 26),
                    "你 " + mine + " : " + theirs + " " + MiaCourtAssets.NameFor(t.Other(cup.player)) + "  ·  " + game.DifficultyName,
                    17, muted);
            }

            DrawBracket(px + 32, py + 112, cup);

            int previousSize = button.fontSize;
            float by = py + ph - 76;
            if (over)
            {
                button.fontSize = 20;
                if (Button(new Rect(px + 32, by, 400, 52), "再抽一次籤    Enter", true)) game.ContinueAfterResult();
            }
            else
            {
                CupTie next = cup.PlayerTie();
                button.fontSize = 20;
                string nextLabel = "打" + cup.NextMatchName() + "：對 " + MiaCourtAssets.NameFor(next.Other(cup.player)) + "    Enter";
                if (Button(new Rect(px + 32, by, 480, 52), nextLabel, true)) game.ContinueAfterResult();
            }
            button.fontSize = 18;
            if (Button(new Rect(px + pw - 32 - 240, by, 240, 52), "回到主畫面")) game.ShowHome();
            button.fontSize = previousSize;
        }

        /// <summary>
        /// The whole draw: eight names, then the four who went through, the two finalists and the
        /// champion, each centred on the pair it came from. The third-place match sits under it.
        /// </summary>
        void DrawBracket(float x, float y, CupBracket cup)
        {
            const float slotW = 186, slotH = 30, slotGap = 6, pairGap = 14, colGap = 26;
            float[] y0 = new float[8];
            for (int i = 0; i < 8; i++) y0[i] = y + 24 + i * (slotH + slotGap) + (i / 2) * pairGap;
            float[] y1 = new float[4];
            for (int j = 0; j < 4; j++) y1[j] = (y0[j * 2] + y0[j * 2 + 1]) / 2;
            float[] y2 = new float[2];
            for (int k = 0; k < 2; k++) y2[k] = (y1[k * 2] + y1[k * 2 + 1]) / 2;
            float y3 = (y2[0] + y2[1]) / 2;

            float c0 = x, c1 = x + slotW + colGap, c2 = c1 + slotW + colGap, c3 = c2 + slotW + colGap;
            Text(new Rect(c0, y, slotW, 20), "八強", 14, muted, FontStyle.Bold);
            Text(new Rect(c1, y, slotW, 20), "四強", 14, muted, FontStyle.Bold);
            Text(new Rect(c2, y, slotW, 20), "冠軍賽", 14, muted, FontStyle.Bold);
            Text(new Rect(c3, y, slotW, 20), "冠軍", 14, muted, FontStyle.Bold);

            // Quarter-finals.
            for (int i = 0; i < 8; i++)
            {
                CupTie tie = cup.quarters[i / 2];
                int who = i % 2 == 0 ? tie.left : tie.right;
                Slot(c0, y0[i], slotW, slotH, who, tie.played ? tie.ScoreFor(who) : -1, tie.played && tie.Winner == who, cup.player);
                Connector(c0 + slotW, y0[i] + slotH / 2, c1, y1[i / 2] + slotH / 2, tie.played && tie.Winner == who, who == cup.player);
            }
            // Semi-finals.
            for (int j = 0; j < 4; j++)
            {
                CupTie tie = cup.semis[j / 2];
                if (tie == null) { Slot(c1, y1[j], slotW, slotH, -1, -1, false, cup.player); continue; }
                int who = j % 2 == 0 ? tie.left : tie.right;
                Slot(c1, y1[j], slotW, slotH, who, tie.played ? tie.ScoreFor(who) : -1, tie.played && tie.Winner == who, cup.player);
                Connector(c1 + slotW, y1[j] + slotH / 2, c2, y2[j / 2] + slotH / 2, tie.played && tie.Winner == who, who == cup.player);
            }
            // Final.
            for (int k = 0; k < 2; k++)
            {
                if (cup.final == null) { Slot(c2, y2[k], slotW, slotH, -1, -1, false, cup.player); continue; }
                int who = k == 0 ? cup.final.left : cup.final.right;
                bool won = cup.final.played && cup.final.Winner == who;
                Slot(c2, y2[k], slotW, slotH, who, cup.final.played ? cup.final.ScoreFor(who) : -1, won, cup.player);
                Connector(c2 + slotW, y2[k] + slotH / 2, c3, y3 + slotH / 2, won, who == cup.player);
            }
            // Champion.
            Slot(c3, y3, slotW, slotH, cup.champion, -1, cup.champion >= 0, cup.player);
            if (cup.champion >= 0)
                Text(new Rect(c3, y3 + slotH + 6, slotW, 22), "亞軍 " + MiaCourtAssets.NameFor(cup.runnerUp), 14, muted);

            // Third-place match.
            float by = y0[7] + slotH + 22;
            Text(new Rect(c0, by, 120, 22), "季軍賽", 14, muted, FontStyle.Bold);
            if (cup.bronze == null)
                Text(new Rect(c0 + 80, by, 500, 22), "四強輸的兩位爭第三名", 14, muted);
            else
            {
                Slot(c0 + 80, by - 4, slotW, slotH, cup.bronze.left, cup.bronze.played ? cup.bronze.leftScore : -1,
                    cup.bronze.played && cup.bronze.Winner == cup.bronze.left, cup.player);
                Text(new Rect(c0 + 80 + slotW, by, 34, 22), "對", 14, muted, FontStyle.Normal, TextAnchor.UpperCenter);
                Slot(c0 + 114 + slotW, by - 4, slotW, slotH, cup.bronze.right, cup.bronze.played ? cup.bronze.rightScore : -1,
                    cup.bronze.played && cup.bronze.Winner == cup.bronze.right, cup.player);
            }
        }

        /// <summary>One name in the bracket. The player is always mint; winners are lit, losers dimmed.</summary>
        void Slot(float x, float y, float w, float h, int roster, int score, bool winner, int player)
        {
            bool you = roster >= 0 && roster == player;
            Fill(new Rect(x, y, w, h), winner ? new Color(.16f, .25f, .23f) : track);
            if (you) Fill(new Rect(x, y, 4, h), Mint);
            if (roster < 0)
            {
                Text(new Rect(x + 12, y + 3, w - 24, h - 4), "—", 15, muted);
                return;
            }
            string name = MiaCourtAssets.NameFor(roster) + (you ? "（你）" : "");
            Color face = you ? Mint : winner ? paper : muted;
            Text(new Rect(x + 12, y + 3, w - 56, h - 4), name, 15, face, winner || you ? FontStyle.Bold : FontStyle.Normal);
            if (score >= 0)
                Text(new Rect(x + w - 48, y + 3, 38, h - 4), score.ToString(), 15, face, FontStyle.Bold, TextAnchor.UpperRight);
        }

        /// <summary>An elbow line from one slot to the next round. Lit when that side went through.</summary>
        void Connector(float fromX, float fromY, float toX, float toY, bool advanced, bool player)
        {
            Color line = advanced ? (player ? Mint : new Color(.55f, .62f, .58f)) : new Color(.24f, .31f, .29f);
            float midX = (fromX + toX) / 2;
            Fill(new Rect(fromX, fromY - 1, midX - fromX, 2), line);
            Fill(new Rect(midX - 1, Mathf.Min(fromY, toY) - 1, 2, Mathf.Abs(toY - fromY) + 2), line);
            Fill(new Rect(midX, toY - 1, toX - midX, 2), line);
        }

        // ------------------------------------------------------------------ shared pieces

        void SectionLabel(Rect rect, string title, string key)
        {
            Text(new Rect(rect.x, rect.y, 120, rect.height), title, 14, muted, FontStyle.Bold);
            if (!string.IsNullOrEmpty(key))
                Text(new Rect(rect.x + rect.width - 200, rect.y, 200, rect.height), key, 13, muted, FontStyle.Normal, TextAnchor.UpperRight);
        }

        void StatBar(Rect rect, string name, float value, Color fill)
        {
            Text(new Rect(rect.x, rect.y + 1, 72, rect.height), name, 14, muted);
            float bx = rect.x + 72, bw = rect.width - 72 - 46;
            Fill(new Rect(bx, rect.y + 9, bw, 7), track);
            Fill(new Rect(bx, rect.y + 9, bw * Mathf.Clamp01(value), 7), fill);
            Text(new Rect(rect.x + rect.width - 44, rect.y + 1, 44, rect.height), Mathf.RoundToInt(value * 100) + "%", 15, paper,
                FontStyle.Bold, TextAnchor.UpperRight);
        }

        void CornerButtons()
        {
            int previousSize = button.fontSize;
            button.fontSize = 16;
            if (Button(new Rect(width - 392, height - 72, 176, 44), game.sound.Muted ? "音效：關  M" : "音效：開  M"))
                game.sound.ToggleMute();
            if (Button(new Rect(width - 208, height - 72, 176, 44), Screen.fullScreen ? "離開全螢幕  F11" : "全螢幕  F11"))
                Screen.fullScreen = !Screen.fullScreen;
            button.fontSize = previousSize;
        }

        bool Button(Rect rect, string text, bool primary = false, Color accent = default)
        {
            buttons.Add(rect);
            Color before = GUI.backgroundColor;
            GUI.backgroundColor = primary ? (accent.a > 0 ? accent : Mint) : paper;
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
