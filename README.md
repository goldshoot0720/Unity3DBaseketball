# 喵喵街頭籃球

台北夕陽球場上的一對一街頭籃球。Windows、WebGL、Android 安裝檔放在 [Releases](https://github.com/goldshoot0720/Unity3DBaseketball/releases)。

## 操作

| 動作 | 按鍵 |
|---|---|
| 賽制 | `C` 切換單場／連續比賽 |
| 難度 | `N` 切換簡單／普通／困難 |
| 選角 | `A` / `D`、方向鍵，或畫面按鈕 |
| 開始 | Enter |
| 移動 | WASD / 方向鍵 |
| 衝刺 | Shift |
| 投籃 | 按住空白鍵開投籃條，掃到綠區放開 |
| 抄截 | 靠近持球者後按 `E` 或空白鍵 |
| 視角 | `1` `2` `3` 或 `V` |
| 暫停 | Esc |

每場 3 分 30 秒，先達到 21 分者立即獲勝；時間到由得分較高者獲勝，平手加賽 30 秒。兩分球與三分球依距離判定。

## 賽制

- **單場比賽** — 你和對手各選一位，一場定勝負。
- **連續比賽** — 八位角色抽籤排進單淘汰賽：八強 → 四強 → 冠軍賽或季軍賽。你最多打 3 場，你沒上場的那幾場由電腦打完，冠亞季軍一定會產生。八強輸掉就止步八強，賽程仍會跑完。

## 難度

難度決定你的命中率，也決定電腦對手多兇。

| 難度 | 兩分球 | 三分球 | 抄截成功 | 蓋火鍋成功 |
|---|---|---|---|---|
| 簡單 | 93% | 33% | 53% | 73% |
| 普通 | 83% | 23% | 33% | 53% |
| 困難 | 73% | 13% | 13% | 33% |

兩分與三分是「在綠區放開、且沒人干擾」時的命中率；放得太早或太晚會按投籃條品質往下打折，被貼身干擾再打八折左右。抄截與蓋火鍋的機率是在「手已經碰到人或球」之後才擲的——伸手沒搆到就直接落空，搆到了再看這個機率，失手一樣要吃收手的硬直。

難度同時調整電腦的反應速度、移動速度、出手時機，以及它自己的命中率、抄截與火鍋成功率——越難的設定，對手越強。

## 球員

喵白白、喵布布、咕咕嘎嘎、牙妹、魚妹、鋒兄、塗董、深索娘。單場比賽你選一位、對手選一位；連續比賽你只選自己，其餘七位由抽籤決定。

## 建置

Unity 6000.6.0f1、URP。編輯器選單：

- `Mia Court / Build Windows game` → `Builds/Windows/MiaBasketball.exe`
- `Mia Court / Build WebGL` → `Builds/WebGL`
- `Mia Court / Build Android APK` → `Builds/Android/MiaBasketball.apk`

WebGL 為 Gzip，內建解壓縮，可用任何靜態網站伺服器或本機 `python -m http.server` 開啟。網頁版用專案自己的樣板（`Assets/WebGLTemplates/MiaCourt`）：畫面填滿整個瀏覽器視窗，載入完自動取得鍵盤焦點；直接雙擊 `index.html` 會顯示該怎麼開啟，而不是一片空白。Android 為 ARM64 IL2CPP，使用 Unity 偵錯金鑰簽署，可側載。

## 驗證

規則與幾何的純函式檢查：

```
Unity -batchmode -quit -nographics -projectPath . -executeMethod MiaCourt.Editor.MiaCourtSetup.ValidateRules
```

實際進 Play mode 的執行期檢查（動作骨架、投籃條時限、命中計分、難度機率、賽程），結束後以離開碼回報，逐項結果寫在 `Documentation/Validation/runtime.txt`：

```
Unity -batchmode -nographics -projectPath . -executeMethod MiaCourt.Editor.MiaCourtSetup.RunSmokeTest -miaSmokeTest
```

`-nographics` 沒有可以結束的影格，所以截圖會跳過、只跑檢查；要連截圖一起產出就拿掉 `-nographics`。

投籃條會在 0.8 秒內掃完全長後折返，左右快速來回；在綠區放開可提高命中品質。出手時限依出手點決定：三分線外 3 秒、三分線內 1.5 秒，時限一到就倉促出手。持球時有運球與雙手收球動作；防守鍵會依對方持球或投籃狀態觸發抄截或跳起封蓋。
