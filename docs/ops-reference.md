# op リファレンス

すべての op は `AiCommandDispatcher` が実装し、メールボックス（`req-*.json`）と Unity 公式 CLI（`unity command ai_*`）の両方から同じ意味で呼べる。
**メールボックス経路は非同期**で、操作後の落ち着き待ち・撮影のファイル生成待ち・シナリオの完了待ちを済ませてから応答する。CLI 経路は同期で、要求した時点の結果を返す。

## 要求・応答の形

要求（`req-<id>.json`）:
```json
{ "op": "agent.act", "args": "{\"action\":{\"submit\":\"NewGameButton\"}}" }
```
`args` は JSON 文字列。`ai_client.py` は第 2 引数の JSON をそのまま詰める。

応答（`res-<id>.json`）の共通フィールド:

| フィールド | 意味 |
|---|---|
| `ok` | 処理できたか。`false` のとき `error` か `message` に理由 |
| `op` | 要求した op |
| `session` | エージェントセッション ID（`agent.*`） |
| `message` | 人向けの補足（`セッションを開始しました。` 等） |
| `text` | 本文（観測テキスト・検索結果・ログ） |
| `path` | 成果物の絶対パス（撮影 PNG・scenario.json・結果 JSON） |
| `settled` | 非同期経路で落ち着き待ちを済ませたか |
| `ready` / `waitedMs` | `submit`/`click`/`tap` の対象が押せるまで待って押せたか、待った実時間 |
| `elapsedMs` | 要求受理から応答までの実時間 |
| `width` / `height` / `blank` | 撮影の画像サイズと白紙判定（輝度の標準偏差 3.0 未満） |
| `expectOk` / `expectFailures` | `expect` の判定結果と未達の理由 |
| `status` / `verdict` / `failedSteps` / `warningCount` | シナリオ実行の状態と合否 |

## 一覧

| op | 引数 | 説明 |
|---|---|---|
| `ping` | – | `playMode=<bool> scene=<name> frame=<n>` |
| `ops` | – | op 名の一覧 |
| `agent.begin` | `goal`（必須）, `options` | セッション開始。`goal` は `{"freePlay":true,"maxSteps":5000,"maxSeconds":14400}` か `{"goal":[{"kind":"textVisible","value":"…"}],"maxSteps":…}`。期待値 0 件で freePlay でもない目標は拒否。`options`: `{"stuckRepeatLimit":40,"inputMode":"gamepad","settleFrames":1}` |
| `agent.observe` | `diffOnly`, `scope`（`visible` 既定 / `all`）, `capture`（撮影名）, `directory` | 観測テキスト。`capture` を付けると同じフレームで撮影し `path/width/height/blank` を埋める |
| `agent.act` | `action` または `steps[]`, `expect[]`, `settleSeconds`(0.35), `settleTimeoutSeconds`(10), `readyTimeoutSeconds`(5) | 1 手または複数手。各手: 準備待ち → 実行 → 落ち着き待ち → 観測。`steps` は `status` が `running` 以外か `expect` 未達で打ち切り |
| `agent.find` | `label`, `kind`（Button/Text/Toggle/Input/Selectable）, `scope` | ラベル部分一致で要素検索。1 行 1 件、末尾に推奨の `submit:"…"` |
| `agent.goal` | – | 目標達成状態 |
| `agent.end` | – | セッション終了（`session.json` / `actions.jsonl` を確定） |
| `agent.export` | `name` | セッションの手順を回帰シナリオ `scenario.json` に書き出す。`expect` 付きの手はそのまま、未達だった手は `comment` 付き |
| `scenario.run` | `path`（プロジェクト相対 or 絶対）, `name`, `scenarioTimeoutSeconds`(900) | シナリオ実行。非同期経路は完了まで待って `verdict` を返す |
| `scenario.status` | – | 直前のシナリオの状態 |
| `capture` | `name`（必須。英数字・`_`・`-`）, `directory`（既定 `DebugOutput/captures`） | Game View を PNG に |
| `snapshot` | `compact`(true), `save` | UI スナップショット（`all` 相当。ツール用） |
| `console` | `count`(40), `level`（`all` / `error`） | Unity コンソールの末尾。Error/Exception はスタックトレース先頭 3 行付き |

## 行動（`action`）の語彙

| キー | 例 | 意味 |
|---|---|---|
| `submit` | `"NewGameButton"` / `"label:剛 攻撃のルーン"` / `"Panel/Row0"` | UI の決定。名前・パス断片・ラベル部分一致 |
| `press` | `south` `east` `north` `west` `start` `select` `leftShoulder` `rightShoulder` | パッド単打（`east` が B/戻る） |
| `hold` + `seconds` | | 長押し |
| `move` | `up` `down` `left` `right` | 十字キー（フォーカス移動） |
| `stick` + `x` `y` `seconds` | `left` / `right` | スティック |
| `key` | `Enter` `Escape` `Space` `ArrowUp` … | キーボード |
| `text` | | TMP 入力欄へ文字列 |
| `click` / `tap` / `pointerMove` / `scroll` + `button` / `amount` | 要素名か `x` `y` | ポインタ・タッチ |
| `drag` / `swipe` + `from` `to`（要素名）or `fromX/fromY/toX/toY` + `seconds` | | ドラッグ・スワイプ |
| `pinch` + `center` `fromDistance` `toDistance` | | ピンチ |
| `scrollTo` | `"MarketRuneListRow8"` | 祖先 ScrollRect の表示範囲へ入れる（フォーカスは動かさない） |
| `reason` | | 行動理由（`actions.jsonl` に残る） |

## 事後条件（`expect`）の語彙

`{"kind": "...", "value": "...", "target": "...", "scope": "...", "key": "...", "op": "..."}` の配列。シナリオの `expect` と同じ。

| kind | 判定 |
|---|---|
| `textVisible` / `textAbsent` | `value` の文字が画面に見える／見えない |
| `exists` / `absent` / `interactable` / `disabled` | `target` の要素が存在／不在／操作可能／無効 |
| `focused` | `target` にフォーカスがある |
| `sceneIs` | アクティブシーン名が `value` |
| `gameState` | `game:` の `key` が `op`（eq/ne/contains/lt/le/gt/ge）で `value` を満たす |
| `changed` | 直前との差分に `target` が含まれる |
| `noException` | 操作中に例外フォレンジックが増えていない |
| `auditClean` | レイアウト監査が 0 件 |
| `noDroppedFrames` / `frameMsP95Below` / `gcAllocBelow` / `noGcCollection` | 録画・性能計測の条件 |

## 観測テキストの読み方

```
scene=Home focus=DollRow/DollButton0(アリア)
[Text] AssetsBar/GoldValue 「120G」
[Button] WorkshopTabBarView/TabButton0 「編成」 !disabled
[Button] DollRow/DollButton0 「[F] アリア 戦士 Lv2」 *focused
[Button] Content/MarketRuneListRow6 「明 呪詛のルーン」 blocked:Panel [clipped]
game: gold=120 run.floor=2 battle.active=false
agent: busy=inputBlocked
agent: settleFrames=1

actions:
 - submit/click/tap target=Canvas/…/DollButton0 label=[F] アリア 戦士 Lv2
 - scrollTo=<target>
 - press=south/east/north/west/start/select/leftShoulder/rightShoulder
 - move=up/down/left/right
```

- `*focused`: 今のフォーカス。`!disabled`: 押せない。`blocked:X`: X に遮られている。`[clipped]`: マスクの外（`scope:"all"` のときだけ表示）
- `game:` はゲームが `IGameStateProvider` で登録した値
- `agent: busy=…` が出ている観測は遷移・演出の途中
- `actions:` は今すぐ押せる候補。同名行がある場合は `→ submit:"label:…"` の推奨指定が付く
- 差分観測（`diffOnly:true`）は `diff:` に追加・削除・変更だけを出す
