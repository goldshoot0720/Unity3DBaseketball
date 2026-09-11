# 喵喵街頭籃球

台北夕陽球場上的一對一街頭籃球。Windows、WebGL 安裝檔放在 [Releases](https://github.com/goldshoot0720/Unity3DBaseketball/releases)。

## 操作

| 動作 | 按鍵 |
|---|---|
| 選角 | `A` / `D`、方向鍵，或畫面按鈕 |
| 開始 | Enter |
| 移動 | WASD / 方向鍵 |
| 衝刺 | Shift |
| 投籃 | 按住空白鍵蓄力，放開出手 |
| 抄截 | 靠近持球者後按 `E` 或空白鍵 |
| 視角 | `1` `2` `3` 或 `V` |
| 暫停 | Esc |

每場 3 分鐘，平手加賽 30 秒。兩分球與三分球依距離判定。

## 球員

喵白白、喵布布、咕咕嘎嘎、牙妹、魚妹、鋒兄、小塗、鋒市、鋒總、塗董。你選一位，電腦用下一位。

## 建置

Unity 6000.6.0f1、URP。編輯器選單：

- `Mia Court / Build Windows game` → `Builds/Windows/MiaBasketball.exe`
- `Mia Court / Build WebGL` → `Builds/WebGL`
- `Mia Court / Build Android APK` → `Builds/Android/MiaBasketball.apk`

WebGL 為 Gzip，內建解壓縮，可用任何靜態網站伺服器或本機 `python -m http.server` 開啟（不要直接雙擊 `index.html`）。Android 為 ARM64 IL2CPP，使用 Unity 偵錯金鑰簽署，可側載。
