# op リファレンス

Runtime の op は `AiCommandDispatcher` が実装し、メールボックス（`req-*.json`）と Unity 公式 CLI（`unity command ai_*`）の両方から同じ意味で呼べる。
**メールボックス経路は非同期**で、操作後の落ち着き待ち・撮影のファイル生成待ち・シナリオの完了待ちを済ませてから応答する。CLI 経路は同期で、要求した時点の結果を返す。

Play 停止中も使う [Editor メールボックス](#editor-メールボックス) は `EditorControlMailbox` が処理する独立した入口。

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
| `settled` | 非同期経路で落ち着き待ちを済ませたか。待機だけの `agent.act` はアンカー成立時に true |
| `ready` / `waitedMs` | 非同期 `agent.act` のアンカーと操作対象の準備が成立したか、両方の待機に費やした合計ミリ秒 |
| `elapsedMs` | 要求受理から応答までの実時間 |
| `width` / `height` / `blank` | 撮影の画像サイズと白紙判定（輝度の標準偏差 3.0 未満） |
| `view` | 今回フォーカスを適用した `game` / `simulator`。未指定・適用不能時は空文字列 |
| `expectOk` / `expectFailures` | `expect` の判定結果と未達の理由 |
| `status` / `verdict` / `failedSteps` / `warningCount` | シナリオ実行の状態と合否 |

## 一覧

| op | 引数 | 説明 |
|---|---|---|
| `ping` | – | `playMode=<bool> scene=<name> frame=<n>` |
| `ops` | – | op 名の一覧 |
| `agent.begin` | `goal`（必須）, `options` | セッション開始。`goal` は `{"freePlay":true,"maxSteps":5000,"maxSeconds":14400}` か `{"goal":[{"kind":"textVisible","value":"…"}],"maxSteps":…}`。期待値 0 件で freePlay でもない目標は拒否。`options`: `{"stuckRepeatLimit":40,"inputMode":"gamepad","settleFrames":1}` |
| `agent.observe` | `diffOnly`, `scope`（`visible` 既定 / `all`）, `capture`（撮影名）, `directory`, `view`（`""` 既定 / `game` / `simulator`） | 観測テキスト。`capture` を付けると同じフレームで撮影し `path/width/height/blank` を埋める。`view` の経路別動作は下記 |
| `agent.act` | `action` または `steps[]`（各手に `waitForText` / `waitForObject` / `waitForFocus` / `waitForScene`、`timeoutSeconds`(30)）, `expect[]`, `settleSeconds`(0.35), `settleTimeoutSeconds`(10), `readyTimeoutSeconds`(5) | 各手: アンカー待ち → 対象の準備待ち → 実行 → 落ち着き待ち → 観測。待機だけも可。待機失敗・`status` が `running` 以外・`expect` 未達で打ち切り |
| `agent.find` | `label`, `kind`（Button/Text/Toggle/Input/Selectable）, `scope` | ラベル部分一致で要素検索。1 行 1 件、末尾に推奨の `submit:"…"` |
| `agent.goal` | – | 目標達成状態 |
| `agent.end` | – | セッション終了（`session.json` / `actions.jsonl` を確定） |
| `agent.export` | `name` | セッションの手順を回帰シナリオ `scenario.json` に書き出す。`expect` 付きの手はそのまま、未達だった手は `comment` 付き |
| `scenario.run` | `path`（プロジェクト相対 or 絶対）, `name`, `scenarioTimeoutSeconds`(900) | シナリオ実行。非同期経路は完了まで待って `verdict` を返す |
| `scenario.status` | – | 直前のシナリオの状態 |
| `capture` | `name`（必須。英数字・`_`・`-`）, `directory`（既定 `DebugOutput/captures`）, `view`（`""` 既定 / `game` / `simulator`） | 画面を PNG に。`view` で Game View / Device Simulator を指定できる |
| `snapshot` | `compact`(true), `save` | UI スナップショット（`all` 相当。ツール用） |
| `scene.dump` | `depth`(3), `maxNodes`(200), `filter`（名前の部分一致、任意）, `save`(false) | シーン階層のコンパクトテキスト。`save` で全階層 JSON を `DebugOutput/scene/` に保存し `path` を返す |
| `console` | `count`(40), `level`（`all` / `error`） | Unity コンソールの末尾。Error/Exception はスタックトレース先頭 3 行付き |

## Editor メールボックス

`[InitializeOnLoad]` の `EditorControlMailbox` が `EditorApplication.update` で処理する。
場所は `<Unity プロジェクト>/DebugOutput/editor-mailbox/`。Play 停止中・Pause 中も利用できる。
Runtime 用の `agent-mailbox` / `AiCommandDispatcher` とは要求・応答を分ける。

要求 `req-<id>.json`:

```json
{"op":"menu","arg":"Window/General/Console"}
```

`arg` は普通の文字列。menu 以外は省略または空文字列にする。
`editor_ctl.py menu 'Window/General/Console'` のように、メニューパスを第2引数で渡す。

応答 `res-<id>.json` は **`ok`（bool）と `message`（string）の2項目のみ**。
`status` の状態値も `message` に格納する:

```json
{"ok":true,"message":"isPlaying=True isPaused=False isCompiling=False focusedWindow=UnityEditor.GameView"}
```

| op | arg | 応答の意味 |
|---|---|---|
| `status` | なし | `isPlaying` / `isPaused` / `isCompiling` / `focusedWindow`。bool は `True` / `False`、focusedWindow は前面 EditorWindow の完全型名（なければ空文字列） |
| `play` | なし | Play 開始の要求受理。開始完了は `status` で確認 |
| `stop` | なし | Play 停止の要求受理。停止完了は `status` で確認 |
| `pause` | なし | Pause の要求受理。反映は `status` で確認 |
| `unpause` | なし | `EditorApplication.isPaused = false` で Pause を解除 |
| `focus_game_view` | なし | T1 の `PlayModeViewFocus` で Game View をフォーカス。適用不能なら `ok:false` |
| `simulator_view` | なし | 同じ `PlayModeViewFocus` で Device Simulator をフォーカス。適用不能なら `ok:false` |
| `menu` | メニューパス（必須） | `EditorApplication.ExecuteMenuItem` の成否。失敗なら `ok:false` |

`play` / `stop` / `pause` は受理応答を公開してから、次の Editor 更新で状態変更を要求する。
応答を受けた直後は以前の状態の場合がある。呼び出し側が `status` を繰り返して確認する。
`isCompiling=True` の間は `status` 以外を `ok:false` で拒否する。
未知 op、不正 JSON、op / arg が文字列でない要求、空のメニューパスも `ok:false` と理由を返す。

ファイル公開は既存メールボックスと同じく、同一ディレクトリの `.tmp` を閉じてから rename する。
正式要求を名前順に1件ずつ処理し、応答を公開できた要求だけ削除する。
I/O 失敗時は応答を保持して書き込みを再試行し、同じ要求を実行し直さない。
ポーリング間隔は定数 0.05 秒。起動時の走査後はファイル通知で走査を予約し、
必要な間隔につき `Directory.GetFiles` は最大1回。待機中は通知フラグだけを確認し、空走査の確保を避ける。

## 撮影・観測対象（`view`）

`capture` と `agent.observe` が受ける。`"game"` は Game View、`"simulator"` は Device Simulator。
未指定・`""` は従来どおり前面の PlayModeView を使う。不正値は `ArgumentException` とし、
ディスパッチャが `ok:false` と `error` に変換する。

メールボックスでは `TryFocus` → `Screen.width/height` の安定待ち → 観測・撮影の順。
寸法が 2 フレーム連続して変化しなくなるまで、最大 5 フレーム待つ。上限に達した場合も従来処理へ進む。
待つのは観測の前だけで、`agent.observe` の観測から撮影要求の間には yield を挟まない。

```sh
python3 Tools/ai_client.py capture '{"name":"a","view":"simulator"}'
python3 Tools/ai_client.py agent.observe '{"capture":"b","view":"game"}'
```

対象の型・ウィンドウが利用できない場合、バッチモード、Handler 未登録時は、フォーカス不能を理由に失敗させず、
従来の対象で処理する。応答は `view:""`、`message` に適用できなかった理由を含む。
成功時の `view` は今回適用した値であり、`view` 未指定時に現在の前面ウィンドウを推定して返すものではない。

**同期 CLI は `--view` の適用成功時、フォーカスだけを行う。** その呼び出しでは撮影・観測を行わず、
`view` と次回呼び出しの案内を返す（`path` / `text` は空、`width=height=0`）。
Editor のフレーム反映後、次の呼び出しで `--view` を省略して撮影・観測する。
フォーカス不能時はメールボックスと同じく従来処理へ進む。フォーカス不要なら最初から `--view` を省略する。

```sh
unity command ai_capture --name a --view simulator
# Editor のフレーム反映後に、同じコマンドを view なしで呼ぶ。
unity command ai_capture --name a
```

`ai_agent_observe` も `--view` を受け、適用後は次の `ai_agent_observe` を `--view` なしで呼ぶ。
同 CLI の既存引数は `--diffOnly` のみで、`capture` / `directory` / `scope` は公開していない。
CLI で PNG が必要なら次の `ai_capture` を使う。観測と撮影を一要求にまとめる場合はメールボックスの `agent.observe` を使う。
CLI の撮影は従来どおり PNG の生成完了を待たず、`width=height=0` / `blank=false` を返す。

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
| `waitForText` / `waitForObject` / `waitForFocus` / `waitForScene` | `"waitForObject":"InventoryPanel"` | 行動前に待つ条件。複数指定はすべて成立するまで待つ |
| `timeoutSeconds` | `30`（既定） | `waitFor*` の実時間上限。有限の正数を指定する |

`click` / `tap` はターゲット名を受け、対象の **RectTransform 中心へ** ポインタ・タッチ入力を送る。
`OnPointerClick` だけで反応する UI は `submit` ではなくこれを使う。
例: `agent.act {"action":{"click":"InventoryPanel/ItemCard0"}}`、タッチなら `{"action":{"tap":"InventoryPanel/ItemCard0"}}`。

### 行動前の待機

メールボックスの `agent.act` は `UiScenarioStepReader.CreateAnchor` と
`UiInputLocator.IsAnchorSatisfied` を共用する。`waitForObject` はアクティブな対象の存在・遮蔽なし・操作可能、
`waitForText` は文字の可視性、`waitForFocus` はフォーカス、`waitForScene` はシーンのロードを待つ。
`timeoutSeconds` はシナリオと同じ既定 30 秒で、timeScale に依存しない。
アンカー成立後に、従来の対象の自動準備待ち（`readyTimeoutSeconds`、既定 5 秒）を行う。

```sh
python3 Tools/ai_client.py agent.act '{"action":{"submit":"StartButton","waitForObject":"GameScreen","timeoutSeconds":30}}'
python3 Tools/ai_client.py agent.act '{"action":{"waitForObject":"GameScreen"}}'
```

待機だけの要求は成立時に `ok:true, ready:true, settled:true` を返す。入力後の落ち着き待ちは行わない。
アンカーのタイムアウトは `ok:false, ready:false` と `message` に理由を返し、入力と後続ステップを送らない。
`actions.jsonl` に待ち条件と `timeoutSeconds` を残し、実行した手は `agent.export` で同名フィールドへ写す。
タイムアウトで拒否した要求は履歴へ残すが、再生するステップには加えない。

同期 CLI はフレームを進められないため、アンカーを一回評価する。未成立なら入力を送らず
`ok:false, message` でメールボックス利用を案内する。成立済みなら従来の即時実行へ進む。

## シーン階層（`scene.dump`）

```sh
python3 Tools/ai_client.py scene.dump '{"depth":3,"maxNodes":200,"filter":"Panel","save":true}'
unity command ai_scene_dump --depth 3 --maxNodes 200 --filter Panel --save true
```

`SceneHierarchyDumper` のロード済み全シーンを対象とし、非 UI・非アクティブなオブジェクトも含める。
`text` は `scene=<シーン名>` に続けて、深さごとに半角空白 2 個を付けた階層を返す:

```text
scene=Home
Canvas activeInHierarchy=true
  InventoryPanel activeInHierarchy=false
    Content activeInHierarchy=false
```

ルートの深さは 0。`depth` は 0 以上、`maxNodes` は 1 以上。
`filter` は GameObject **名**の大文字・小文字を区別する部分一致で、パスでは判定しない。
フィルタで親を省いても元の深さと祖先のアクティブ状態を維持する。
件数は深さとフィルタに一致した表示ノードを全シーンで通算し、超過時は末尾に `... maxNodes=<上限>` を付ける。
シーン見出しは件数に含めず、一致するノードがなければ `text` は空文字列。

`save:true` は `DebugOutput/scene/hierarchy-<日時>.json` の絶対パスを `path` に返す。
深さ・件数・フィルタはテキストにだけ適用し、JSON は既存の `SceneHierarchyDump` 形式の全階層を保存する。
テキストの `activeInHierarchy` は既存 JSON の `activeSelf` と `parentIndex` から算出する。
`save:false` の `path` は空。共通ディスパッチャ自体は PlayMode 外でも実行できる。

## 事後条件（`expect`）の語彙

`{"kind": "...", "value": "...", "target": "...", "scope": "...", "key": "...", "op": "..."}` の配列。シナリオの `expect` と同じ。

| kind | 判定 |
|---|---|
| `textVisible` / `textAbsent` | `value` の文字が画面に見える／見えない |
| `exists` / `absent` / `interactable` / `disabled` | `target` の要素が存在／不在／操作可能／無効 |
| `objectExists` / `objectAbsent` | `target` のアクティブな GameObject が存在／不在。非 UI も対象。`FindTarget` と同じ名前・パス断片・`label:` 指定を一回評価する。待機はしない |
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
