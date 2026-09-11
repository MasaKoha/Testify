# 12 AI ゲートウェイ（PR1）

## 目的

CLI と Unity 内蔵メールボックスの実行先を `AiCommandDispatcher` に統一する。
`AgentSessionCommands` から既存セッション API を呼び、`AgentSession` 本体と
`AiSessionState.Enter/Exit` は変更しない。外部 Python 中継プロセスは不要になる。

この PR は `Assets/UniLab.AI/` の外を変更しないため、設計書・roadmap 追記・EditMode テストも
この階層内に置く。テストアセンブリ名は `UniLab.AI.Tests.EditMode`。

## T9: 実機向け HTTP 入口（2026-09-11）

`Runtime/Gateway/Http/AiHttpServer` を `AfterSceneLoad` で起動する。
`UniTestifySettings.asset` の `httpEnabled=false` / `httpPort=7910` / `httpToken=""` /
`httpAllowLan=false` を読み、`DebugOutputPath.DirectoryPath/http.enabled` の同名キーで部分上書きする。
空のトークンは起動ごとに生成し、ポート 0 は空きポートを割り当てる。
待受成功後に `http.port.json` へ `{"port":<実ポート>,"token":"<実効トークン>"}` を公開する。
この接続情報は起動前と正常停止時に削除し、設定アセットは変更しない。

`HttpListener` の prefix は `http://+:<port>/`。`POST /op` の本文は既存メールボックスと同じ
`AiCommandRequest`（`op` / JSON 文字列の `args`）であり、計画表の `AiMailboxRequest` は実在しない。
応答は既存の `AiCommandResponse` 全体。新規 op・引数・共通応答フィールド・Pipeline の追加はない。
ワーカーの `GetContextAsync` → 接続元・Bearer 認証 → 本文の復元 → キュー公開の順で受け付ける。
`Update` は一件ずつ `AiCommandDispatcher.ExecuteAsync` のコルーチンを開始し、
ワーカーは完了通知までコンテキストを保持してから応答する。停止でコルーチンとソケットを解放する。

既定は `IPAddress.IsLoopback` 以外を 403、Bearer 不一致を 401 とする。
`httpAllowLan:true` でも同一サブネットだけを許可する。IPv4 は実インターフェースのマスクで照合し、
IPv6 のプレフィックスを取得できない Unity 同梱 Mono では IPv6 LAN を拒否する（Wi-Fi は IPv4 を使用）。
不正本文 400、未知パス 404、POST 以外 405 も共通の `ok:false` / `error` で返す。
ディスパッチャが受けた op は HTTP 200 で応答し、成否は既存の `ok` を使う。

`ai_client.py` は `--transport file|http`（既定 file）と `--url http://HOST:PORT` を受ける。
HTTP のトークンは環境変数 `TESTIFY_HTTP_TOKEN` から取得する。
HTTP 応答 JSON の `text` を分離し、従来と同じメタデータ一行＋観測本文を標準出力へ出す。
`agent.observe` は既存どおり `agent.begin` の後に使う。
設定・エラー・接続元判定の詳細は [HTTP リファレンス](../ops-reference.md#http-入口)、
Android の Internet Access Require、`adb forward` / `iproxy`、iOS LAN プライバシーの
OS 仕様との差異は [接続手順](../getting-started.md#6-実機へ-http-で一手ずつ接続する) に記録する。

## 操作一覧

`AiCommandRequest` は `op` と `args` を持つ。`args` は JSON オブジェクトを格納した**文字列**で、
省略・空文字列は `{}` として扱う。Python クライアントはこの二重の JSON 化を引き受ける。

| op | args | 結果 |
|---|---|---|
| `ping` | なし | `playMode=<bool> scene=<name> frame=<n>` |
| `ops` | なし | 登録済み op を改行区切りで返す |
| `agent.begin` | `goal` 必須、`options` 任意 | freePlay:true は期待値 0 件を許可。それ以外は拒否 |
| `agent.observe` | `diffOnly=false`、`scope="visible"`、`capture`・`directory` 任意、`view=""`（`game` / `simulator`） | 現在の観測。撮影指定時は画像情報も返す。view の同期経路はフォーカス適用のみ |
| `agent.find` | `label`、`kind` 任意、`scope="visible"` | 観測を検索し、一件一行で推奨 target spec を返す |
| `agent.act` | `action` または空でない `steps` 配列、各手の `waitFor*` と `timeoutSeconds=30`、`expect` 任意 | 各手を順に実行し、待機失敗・expect 未達・status が running 以外なら打ち切る |
| `agent.goal` | なし | 既存の目標判定 |
| `agent.end` | なし | 既存のセッション終了 |
| `agent.export` | `name` | 成功または自由行動セッションのシナリオ保存 |
| `capture` | 英数字・`_`・`-` だけの `name` 必須、`directory` 任意、`view=""`（`game` / `simulator`） | PNG の絶対パス。既定は `DebugOutput/captures`。view の同期経路はフォーカス適用のみ |
| `snapshot` | `compact=true`、CLI 互換の `save=false` | 圧縮テキストまたはスナップショット JSON を text に格納 |
| `scene.dump` | `depth=3`、`maxNodes=200`、名前部分一致の `filter` 任意、`save=false` | 階層コンパクトテキストと、任意の全階層 JSON の絶対パス |
| `console` | `count=40`、`level="all"` | 直近 500 行のリングから対象レベルの末尾 N 行 |

`AiCommandResponse` は `ok/op/session/message/text/path/width/height/view/blank/settled/ready/expectOk/expectFailures/waitedMs/elapsedMs/error` を持つ。
既存の `AgentCommandResult` の `ok/session/message/text/path` は同名・同型を保つ。
変換はディスパッチャの一箇所に集約する。未知 op は `error:"unknown op"`、不正 JSON は
`ok:false` と例外メッセージを返す。`agent.*` は Play 外で `message:"playMode が必要です"`。

## 同期経路と落ち着き待ち

CLI は `Execute` を使う。単発 act は従来どおり即時の観測を返し、`settled=false`。
同期 capture は要求と予定パスを返すだけで、撮影完了を保証しない。view 指定の適用成功時は後述のとおりフォーカスだけを行う。

メールボックスは `ExecuteAsync` を使う。各 act の直前から `sceneLoaded` を購読し、
入力後に最低一度フレームを進める。`AgentSessionDriver.IsBusy` と列挙可能な未ロードシーンを監視し、
それらがなくなってから実時間 `settleSeconds`（既定 0.35 秒）を待つ。
シーン到着イベントが来たら静止時間を計り直す。各手の全待機には
`settleTimeoutSeconds`（既定 10 秒）の上限があり、`Time.realtimeSinceStartup` で計測する。
`Time.timeScale=0` でも待機上限は進む。

待機完了後に Observe を取り直し、行動結果の status / message 行と最新の観測を返す。
成功した待機は `settled=true`。上限超過は `ok:false, settled:false, error:"settle timeout"` とし、
その時点の観測を返して後続手を送らない。目標判定・手数・拒否・記録は既存セッションに任せる。

非同期 capture は同名の前回画像を削除して撮影を要求し、ファイルの生成を実時間最大 3 秒待つ。
生成確認時は `settled=true`、未生成なら `error:"capture timeout"`。

## メールボックスのプロトコル

既定ディレクトリは Editor では `<Unity プロジェクト>/DebugOutput/agent-mailbox`、
実機 Development Build では `<Application.persistentDataPath>/DebugOutput/agent-mailbox`。

1. クライアントは一意な ID（Python は UUID）で `req-<id>.json.tmp` を書いて閉じる。
2. 同じディレクトリ内で `req-<id>.json` に rename して公開する。
3. サーバーは `Update` から既定 0.05 秒ごとに `req-*.json` を名前順に走査する。
4. 同時に一件だけ実行する。応答が書き終わるまでは次の要求を実行しない。
5. `res-<id>.json.tmp` を書き、`File.Move` で `res-<id>.json` を公開する。
6. 応答公開後に要求を削除する。応答はクライアントが読むため残す。

`.tmp` は列挙対象外で、読み取りヘルパに直接渡しても拒否する。
応答書き込み失敗時は結果を保持して書き込みだけを再試行し、操作を再実行しない。
応答公開後・要求削除前に停止した場合、次の走査は既存応答を認識して要求だけを削除する。
起動時に更新日時が 1 時間より古い `res-*.json` を削除する。

Stop はコルーチンとシーン購読を解放し、処理中の要求へ `ok:false, error:"server stopped"` を
公開してからサーバーを破棄する。セッション自体は終了しない。
ファイル書き込みが失敗した場合、明示 Stop は例外を返しサーバーを維持する。
Unity 終了中の I/O 失敗やプロセスクラッシュでは応答を保証できない。

## 起動方法

### T2 Editor 操作メールボックス（2026-09-11 追加）

Runtime のメールボックスとは別に、`EditorControlMailbox` を `[InitializeOnLoad]` で常駐させる。
`EditorApplication.update` で `<Unity プロジェクト>/DebugOutput/editor-mailbox/req-*.json` を処理する。
`EditorControlRequest` / `EditorControlResponse` は `[Serializable]` と `JsonUtility` を使い、
要求は `{"op":"…","arg":"…"}`、応答は `{"ok":true,"message":"…"}` に限定する。
JSON 解釈・検証・応答生成は Editor API 呼び出しから分離する。

op は `status` / `play` / `stop` / `pause` / `unpause` / `focus_game_view` / `simulator_view` / `menu`。
`menu` は arg のメニューパスを `EditorApplication.ExecuteMenuItem` へ渡す。
フォーカスは T1 の `PlayModeViewFocus.TryFocus` を共用し、適用不能なら `ok:false`。
`status` の message は `isPlaying=True isPaused=False isCompiling=False focusedWindow=UnityEditor.GameView`
の形式。focusedWindow は完全型名、前面ウィンドウがなければ空文字列とする。

`play` / `stop` / `pause` は応答と要求削除を先に確定し、次の Editor 更新で状態変更する。
成功は要求受理であり、呼び出し側は `status` で反映を確認する。
`unpause` は `EditorApplication.isPaused = false` を実行し、解除の応答を返す。
`isCompiling=true` の間は `status` 以外を拒否する。
未知 op、不正 JSON、文字列でない op / arg、menu のパス欠落も `ok:false` と理由を返す。

要求・応答は同一ディレクトリの `.tmp` → rename で公開し、I/O 部品は `AiMailboxFiles` を共用する。
ポーリングは定数 0.05 秒。初期走査後は `FileSystemWatcher` の通知で走査を予約し、
必要な間隔につき `Directory.GetFiles` を最大1回呼ぶ。通知のない待機中は走査・要求用の確保をしない。
ファイル通知のスレッドでは Editor API を呼ばず、ドメインリロード前と終了時に監視を解放する。

`Tools/editor_ctl.py` は `ai_client.py` と同じ UUID・原子的公開・応答待ち・終了コードの流儀を使う。
`--mailbox` 省略時はカレントから親へ `DebugOutput/editor-mailbox` を探索し、初回は Unity プロジェクト構造でも解決する。
`--timeout` は既定 60 秒。Runtime 用環境変数・`.enabled` は使わない。
詳細は [op リファレンス](../ops-reference.md#editor-メールボックス) を参照。

### Runtime メールボックス

すべて `AiMailboxServer.Start(directory)` に集約する。

- **マーカー**: 既定メールボックスに `.enabled` を作り、Play を開始する。
  `RuntimeInitializeOnLoadMethod(AfterSceneLoad)` が自動起動する。
- **Editor**: Play 中に `UniLab/AI/Mailbox/Start` / `Stop`。
- **CLI**: `ai_mailbox --start` / `--stop` / `--status`。
  開始時は `--directory <dir>` も指定できる。

状態は `AiMailboxServer.IsRunning` / `Directory` / `HandledCount`。
サーバーは Resources の設定アセットが保持する型付き Prefab から生成し、シーンをまたいで存続する。
Stop は `.enabled` を削除しないため、次の Play では再び自動起動する。

## クライアント

```sh
python3 Assets/UniLab.AI/Tools/ai_client.py ping
python3 Assets/UniLab.AI/Tools/ai_client.py agent.begin '{"goal":{"freePlay":true,"maxSteps":5000,"maxSeconds":14400}}'
python3 Assets/UniLab.AI/Tools/ai_client.py agent.act '{"action":{"submit":"NewGameButton"}}'
python3 Assets/UniLab.AI/Tools/ai_client.py agent.act '{"steps":[{"press":"east"},{"submit":"TabButton1"}],"settleSeconds":0.35}'
python3 Assets/UniLab.AI/Tools/ai_client.py capture '{"name":"03_workshop"}'
```

パスは `--mailbox DIR` → `UNILAB_AI_MAILBOX` → カレントから親へ既存ディレクトリを探索、の順。
初回だけは `Assets` と `ProjectSettings` のある親を見つけて既定ディレクトリを作る。
`.enabled` は自動作成する。待ち時間は `--timeout`（既定 60 秒）。
応答の text 以外を先頭の一行 JSON、text を続く本文に表示する。成功の終了コードは 0、失敗は 1。
タイムアウト後も要求を残すため、その要求が後から実行される可能性がある。再送は別要求になる。

## CLI との使い分けと互換性

即時操作・既存スクリプトには CLI、フレームをまたぐ操作や localhost に届かない環境にはメールボックスを使う。
追加 CLI は `ai_capture` / `ai_mailbox` / `ai_ops`。
既存 agent CLI のコマンド名と引数名を保ち、応答 JSON には既存キーを残す。

`ai_snapshot` の `compact` / `save` 引数と保存動作は保持するが、今回の応答統一指定に従い、
従来の生テキスト／オブジェクトを `AiCommandResponse` の JSON 文字列へ変更する。
このコマンドの旧戻り値を直接読むクライアントは `text` を読む必要がある。
Play 外の agent CLI も従来の生文字列から、同じ文言を含む応答 JSON になる。

## 既知の制約

- Runtime は Editor / Development Build に限定する。Pipeline は `UNILAB_AI_PIPELINE` が必要。
- メールボックスの起動と agent 操作には PlayMode が必要。Play 中にマーカーを置くだけでは起動せず、
  メニューまたは CLI で開始するか、マーカーを置いた状態で次の Play を開始する。
- 独自ディレクトリの `.enabled` は自動探索しない。メニューは既定パス、CLI / API は任意パスを扱う。
- 同時処理は一要求。CLI とメールボックスを同じセッションへ同時に送る排他制御は PR1 の対象外。
- Unity には任意の `LoadSceneAsync` の開始を全体で通知するイベントがない。
  シーン一覧に未登場の AsyncOperation や `allowSceneActivation=false` は検出できない。
  ロード完了イベントは静止待ちを延長するが、未公開のロードを完全に待つにはゲーム側の通知が別途必要。
- 落ち着き待ちはアニメーションの完了そのものを検出しない。長い遷移には settleSeconds を調整する。
- 非同期処理後の目標達成をセッションが確定するタイミングは既存実装のまま。
- 撮影完了判定はファイルの存在であり、画像デコード・最終書き込み完了までは保証しない。
- ファイルログ読み取りは末尾 N 行だけを保持するが、ファイルは先頭から走査する。
- クラッシュをまたぐ exactly-once は保証しない。実行後・応答公開前のクラッシュで要求が残り得る。
- Unity の起動・コンパイル・EditMode テストは依頼者が実施する。


## 準備待ち

メールボックスの `agent.act` は `submit` / `click` / `tap` の対象について、
`UiReadiness.IsSubmittable` で存在・遮蔽なし・操作可能を確認してから既存の `Act` を呼ぶ。
ランナーも同じヘルパを使う。T3 で対話操作にもシナリオと同じアンカー判定を追加した（末尾参照）。
`readyTimeoutSeconds` は実時間で既定 5 秒。0 は即時判定、負値・NaN・無限大は拒否する。
上限到達時も `Act` を呼び、submit の対象なし・遮蔽・操作不可などの既存メッセージを保持する。
click / tap の対象解決失敗時は従来の座標フォールバックも保持する。
準備待ちがタイムアウトした手では落ち着き待ちと観測更新を省き、追加フィールド以外は Act の応答をそのまま返す。steps もそこで打ち切る。
`steps` は各手について準備待ち → 実行 → 落ち着き待ちの順に処理する。
同期 CLI は対象の自動準備待ちをせず即時実行する。T3 の明示アンカーは未成立なら入力を拒否する。

## 観測の可視フィルタ（offscreen / clipped / scope）

要素矩形は画面座標の `[x, y, width, height]`。`ComputeVisibleRatio` は交差面積を
要素面積で割った 0〜1 を返し、要素面積が 0 の場合は 0 とする。

- `offscreen`: 画面矩形との交差が要素面積の 10% 未満。
- `clipped`: 最寄りの有効な祖先 `RectMask2D` または Image 付き `Mask` の矩形との交差が 50% 未満。祖先マスクがなければ false。
- `agent.observe` の `scope:"visible"`（既定）は両方を除外する。`scope:"all"` は画面外も含め、clipped 行の末尾に ` [clipped]` を付ける。不正な scope は `ok:false`。
- 通常の `UiSnapshot.ToCompactText` は offscreen を除き、clipped を注記付きで残す。`snapshot` op は全要素を返し、保存 JSON も全要素を保持する。
- `actions:` は scope に関係なく clipped / offscreen を除外する。
- `UiSnapshot.Compare` は clipped / offscreen の変化を changed に記録する。visible の差分観測では表示範囲に入った要素を added、外れた要素を removed として返す。

祖先探索は観測時だけ実施する。Selectable の表示ラベルは 80 文字、Text は 120 文字。
`label:` 推奨表記の長さは変更しない。

## 応答フィールド（ready / waitedMs / elapsedMs）

| フィールド | 型 | 意味 |
|---|---|---|
| `ready` | bool | 非同期経路で最終行動の対象が押下準備条件を満たしたか。タイムアウト・待機対象外・同期経路は false。入力ハンドラーでの成功とは別の判定 |
| `waitedMs` | int | 最終行動の準備待ち実時間（ミリ秒）。タイムアウト時も計測。待機対象外・同期経路は 0 |
| `elapsedMs` | int | ディスパッチャの実行開始から応答完成までの実時間（ミリ秒）。準備待ちと落ち着き待ちを含み、同期・非同期・失敗応答すべてで計測 |

`steps` の ready / waitedMs は既存の応答と同じく最後に実行した手の値。
elapsedMs は要求全体の値。ミリ秒未満は切り捨てるため即時応答は 0 になり得る。
メールボックスのキュー待ちや応答ファイル公開の時間は含まない。

## PR4: 観測品質とクライアント操作

### フリープレイ

`agent.begin {"goal":{"freePlay":true,"maxSteps":5000,"maxSeconds":14400}}` で、
期待値を持たないプレイテストを開始する。`freePlay` の既定は false で、通常目標では従来どおり
期待値が 1 件以上必要。検証は `AgentGoalValidator` に分離し、`Begin` では Play 判定より先に実施する。

自由行動では目標は常に未達で、レポートの `goalReached` も false。目標判定・反復検出による
自動終了を行わず、既存の手数・時間予算を行動時に確認する。禁止語の拒否と明示終了は維持する。
観測の `goalFailures:` 節は出さず、`agent.export` は目標未達の拒否をスキップし、
最後のステップに目標の `expect` やシーン待ちを追加しない。

### 観測＋撮影の同一フレーム

`agent.observe` に任意の `capture`（英数字・`_`・`-` の撮影名）を追加する。
スナップショット取得から `ScreenCapture.CaptureScreenshot` の発行まで yield を挟まず、
別要求によるフレームのずれを解消する。Unity の撮影自体はそのフレームの描画終了時であり、
フレーム内で後続処理が UI を更新した場合まで状態を凍結するものではない。
`text` は観測テキストを保持し、撮影先は `path` に返す。`directory` は単独撮影と同じ保存先指定。

単独の `capture` op も残し、両経路で `AiCaptureSupport` の撮影発行と完了待ちを共有する。
非同期版は最大 3 秒待って PNG を読み取り、応答に `int width` / `int height` / `bool blank` を設定する。
読み取りは `File.ReadAllBytes` → `Texture2D` → `ImageConversion.LoadImage` → `GetPixels32`。
RGB を 0〜255 の輝度（係数 0.2126 / 0.7152 / 0.0722）に変換し、母標準偏差が 3.0 未満なら
`blank=true`。白色に限定せず、ほぼ単色の画像を判定する。Texture は Play 中なら Destroy、
それ以外は DestroyImmediate で必ず破棄する。撮影時だけの処理で毎フレーム解析はしない。
同期 CLI は生成を待たず、`width=height=0` / `blank=false` のまま返す。

### T1: 撮影対象ウィンドウの明示指定

`AiCommandArguments.view` は `""`（既定）/ `"game"` / `"simulator"`。`capture` と `agent.observe` で検証し、
不正値は `ArgumentException` をディスパッチャで `ok:false` / `error` に変換する。
`Runtime/Gateway/Execution/AiPlayModeViewFocus` の `internal static Func<string, bool> FocusHandler` を
`Editor/Gateway/PlayModeViewFocus` が `[InitializeOnLoadMethod]` で登録する。
Runtime に `InternalsVisibleTo("UniTestify.Editor")` を追加して登録を許可する。

Editor 側は全ロード済みアセンブリから `UnityEditor.GameView` または
`UnityEditor.DeviceSimulation.SimulatorWindow` を型名で探し、`EditorWindow.GetWindow(type).Focus()` を呼ぶ。
型が無い、ウィンドウを取得・生成できない、バッチモードのいずれも `false`。
`TryFocus(view)` は view が空または Handler 未登録なら `false`。
適用不能時は要求を失敗させず既存処理へ進み、応答 `view` を空、`message` に理由を追記する。

メールボックスは既存の `AiMailboxServer` → `AiCommandDispatcher.ExecuteAsync` で要求を処理する。
共通の非同期経路で `TryFocus` → `Screen.width/height` の安定待ち → 従来の観測・撮影を実行する。
安定判定は 2 フレーム連続の寸法一致、待機上限は定数 5 フレーム。上限到達時も処理を進める。
yield は観測の前に置き、観測と撮影要求の間では yield しない。

同期 `Execute` / Pipeline CLI はフレームをまたげない。`--view` の適用成功時はフォーカスだけを行い、
`ok:true` / `view` / 次回呼び出しの案内を返す。`path` / `text` は空で撮影・観測は発行しない。
Editor のフレーム反映後、次の呼び出しで view を省略して撮影・観測する。
適用不能時と view 未指定時は従来処理を使う。`agent.observe` の PlayMode 必須条件は維持する。
`ai_capture` / `ai_agent_observe` に `--view` を追加するが、後者の既存引数は `diffOnly` のみのため、
撮影は次回の `ai_capture` で行う。観測と撮影を一要求にする場合はメールボックスを使う。

応答に `string view` を追加する。値は今回実際にフォーカスを適用した `game` / `simulator`、
未指定・適用不能時は空。PNG の実寸は既存の非同期応答 `width` / `height` を読む。
観測本文とセッション成果物 JSON の形式は変更しない。

### console のリングバッファ

ファイルログ参照を廃止し、`Application.logMessageReceived` で収集した 500 行のリングを正とする。
`[{type}] {condition}` を格納し、Error / Exception / Assert にはスタック先頭 3 行を続ける。
各行のレベルを保持するため、容量超過で本文が落ちてもスタック行のエラー分類は維持される。
超過した最古行は Dequeue する。

`console {"level":"all","count":40}` が既定。`level:"error"` は Error / Exception / Assert と
そのスタック行だけに絞り、`count` は絞り込み後の末尾行数。無効な level・負の count は拒否する。
生成・購読は SubsystemRegistration のみで実行し、前回購読を解除して Play 再開時にリセットする。
静的コンストラクタでは生成せず、Play 開始前の未購読状態は空文字を返す。

### Text の遮蔽判定

Text の矩形中心にレイキャストし、GraphicRaycaster の結果から観測用 OverlayMarker 配下を除いた
最前面 Graphic を調べる。その Graphic が Text 自身・祖先・子孫のいずれでもなければ
`blockedBy` に遮蔽元の名前を格納する。Text 自身の `raycastTarget=false` でも判定できる。
これは中心点とレイキャスト対象 Graphic に基づく判定であり、文字の全ピクセルの遮蔽率ではない。

`visible` は遮蔽された Text を除外する。`all` は残して `blocked:<name>` を出力する。
Selectable は既存の遮蔽判定を維持し、visible でも押せない理由を表示する。


## PR5: 検索・事後条件・スクロールと省電力化

### agent.find

`agent.find {"label":"開始","kind":"Button","scope":"visible"}` は `UiSnapshot.Capture()` の
結果に `UiObservationScope.Filter` を適用して検索する。セッション開始は不要、PlayMode は必要。
`label` はリッチテキストタグ除去後の部分一致（大文字小文字を区別）。省略すると全ラベル。
`kind` は `Button` / `Text` / `Toggle` / `Input` / `Selectable`、省略時は全種別。
`scope` は `visible` が既定、`all` はマスク外・画面外も含める。

応答 `text` は次の一件一行形式。改行や引用符はエスケープする。

```text
Button Canvas/List/Row label="冒険を開始" interactable=true blockedBy="" clipped=false rect=[10,20,30,40] → submit:"label:冒険を開始"
```

推奨 spec は通常はパス。全観測内に同名要素がある場合は
`UiInputLocator.CreateLabelTargetSpec` による `label:` 指定を使う。ラベル自体も重複する場合は
既存 Locator の優先順位に従う。0 件は `ok:true`、`text:""`、`message:"見つかりません"`。

### agent.act の expect

```json
{"action":{"submit":"StartButton"},"expect":[{"kind":"sceneIs","value":"Game"}]}
```

単一行動は引数直下または `action.expect` に `ScenarioExpectation[]` を指定できる。
両方指定した場合は `action.expect` を優先する。`steps` は各行動オブジェクト内に指定する。

```json
{"steps":[{"scrollTo":"label:ステージ5"},{"submit":"label:ステージ5","expect":[{"kind":"textVisible","value":"準備完了"}]}]}
```

非同期経路は落ち着き待ち後に `AgentExpectationEvaluator.Evaluate` で評価する。
`textVisible` / `sceneIs` / `focused` など既存評価器の語彙・判定規則を使用し、`changed` は行動前後の差分を渡す。
応答に `expectOk:bool` と `expectFailures:string[]` を追加する。未指定は `true` と空配列。
未達理由は ` - kind target=... value=... message=...` の goalFailures と同じ一行形式。
事後条件未達だけでは `ok` を false にしない。一括実行は未達の手で停止し、
`message:"expect 未達で打ち切り"` とその手の結果を返す。
準備待ち・落ち着き待ちの既存タイムアウト契約は維持する。
同期経路はフレームを進めず、その時点の観測で評価するため `settled:false`。
行動後の画面変化を検証するクライアントはメールボックスの非同期経路を使う。

### scrollTo

`AgentAction.scrollTo` と `UiScenarioStep.scrollTo` は同じ対象指定を使い、
`UiInputLocator.FindTarget` で対象を解決する。祖先 ScrollRect を内側から順に扱い、
対象矩形が viewport に収まる最小移動を、座標系変換して `content.anchoredPosition` へ反映する。
有効な縦横軸だけを動かし、慣性を停止する。フォーカスは変更せず、Input System に依存しない。
対象が viewport より大きい軸は表示範囲を覆う位置まで最小移動し、既に覆っていれば動かさない。
ScrollRect が無い場合は「ScrollRect がありません」。準備待ちは対象の存在だけで判定する。
ログ種別は `scrollTo`、対象は指定文字列。シナリオへは `scrollTo` を保持して出力し、
`scroll` へ変換しない。各手の `expect` も保持し、最終手にはセッション目標を追加する。
入力候補には入力モードに関係なく ` - scrollTo=<target>` を表示する。

### メールボックスの省電力ポーリング

通常は `_pollIntervalSeconds=0.05` 秒。直近の応答処理完了から `_idleAfterSeconds=5` 秒以上
要求を処理していなければ `_idlePollIntervalSeconds=0.25` 秒へ伸ばす。起動直後は起動時刻を基準とする。
Prefab の SerializeField で調整できる。間隔は `ResolvePollInterval(lastHandledAt, now)` の純関数で解決し、
要求を一件処理した時点で最終処理時刻と次回ポーリング予定を更新して通常間隔に戻す。
`ai_mailbox --status` は `pollIntervalSeconds` と UTC ISO 8601 形式の `lastHandledAt` を追加する。
停止中の間隔は 0、未処理または停止中の最終処理時刻は空文字列。

### フレーム内のスナップショット共有

Play 中の `UiSnapshot.Capture()` は同じ `Time.frameCount` では同じ `UiSnapshotDocument` 参照を返す。
フレームが変わると再収集し、SubsystemRegistration でキャッシュをリセットする。
Play 停止中は毎回収集する。内部の `Capture(int frameCount)` で共有と更新を EditMode テストできる。
返された共有ドキュメントは変更せずに利用する。フレーム内の入力直後も最初の観測が返るため、
入力結果の再観測には次フレーム以降を使う。

### 追加テスト

- `AgentFindTest`: タグ除去・部分一致・種別・同名行の推奨 spec・0 件・scope・不正 kind。
- `AgentExpectTest`: 応答 JSON、未指定時の既定値、二手目未達で停止、単一行動の引数、changed 差分。
- `AiMailboxServerPollingTest`: 待機閾値、処理後の復帰、設定間隔の適用。
- `UiSnapshotCacheTest`: 同一参照、フレーム更新、Play 停止中のキャッシュ無効。
- `UiScrollToTest`: 最小移動とシナリオ語彙。`AgentActionExecutorTest` に scrollTo の種別・対象判定を追加。

## PR6: ゲーム側の busy 判定（`IGameBusyProvider`）

落ち着き待ち（`AiSettleWait`）はシーンロードと継続入力しか見ないため、フェード遷移や演出中の入力ブロック中でも `settled=true` を返すことがあった（Codex の所感「settled でもフェード中の応答があった」）。

- `GameAdapterRegistry.BusyProvider` にゲーム側が `IGameBusyProvider`（`IsBusy` / `Reason`）を登録する。未登録なら従来どおり
- `AiSettleWait` は busy の間を「静止していない」と扱い、静止時間の計測をやり直す（上限 `settleTimeoutSeconds` は従来どおり）
- 観測テキストに `agent: busy=<reason>` を出す。AI はこの行があれば途中経過として扱い、再観測する
- karakuri では `IInputBlockManager.BlockedInput`（ローディング・演出中の入力ブロック）を busy として登録する

## PR7: export の expect 化と scenario.run

`agent.act` に渡した `expect` は、単一 action・引数直下・steps のどの形式でも
各手の `UiScenarioStep.expect` へそのまま保存する。PR5 の `AgentActExpectation` は
配列を保管する型ではなく評価器なので、既存の `AgentAction.expect` のコピーを維持し、
評価後の `expectOk` を今回記録されたステップへ戻す。
未達だった手も削除せず、`comment: "元の実行では未達"` を付ける。
拒否されて記録が増えなかった手の評価は、直前のステップへ反映しない。

`agent.export` は `path` に scenario.json の絶対パス、`text` に
`steps=<全ステップ数> expectSteps=<expect を持つステップ数>` を返す。
freePlay の書き出しは従来どおり目標達成不要で、最終手へ目標条件を追加しない。
目標付きセッションの達成チェックと最終手への目標追加は従来どおり。

| op | 引数 | 応答 |
|---|---|---|
| `scenario.run` | `path` 必須（Editor はプロジェクト相対、実機は persistentDataPath 相対、または絶対）、`name` 任意、`scenarioTimeoutSeconds` 既定 900 秒 | `path` は結果 JSON の絶対パス。同期は `status: "running"`、非同期は完了まで待ち `status: "completed"` と `verdict` を返す |
| `scenario.status` | なし | 直前に開始した結果の `path`、`status`、`verdict`、`failedSteps`、`warningCount` |

完了時の `scenario.run` も `failedSteps` と `warningCount` を返す。
`ok` はコマンド処理の成否で、回帰結果が `verdict: "fail"` でも `ok: true`。
完了前の `verdict` は空文字列。結果未作成・書き込み途中は `running` とする。
タイムアウトは `ok: false` / `error: "scenario timeout"` と予定 `path` を返す。
ランナーは停止せず、後から `scenario.status` で完了結果を回収できる。
待機先は起動した要求固有のパスで固定し、同名の連続実行でも前回結果と衝突させない。

`AiCommandDispatcher` が直前の結果パスを所有し、`AiScenarioExecution` が
既存 `UiScenarioRunner` の起動・結果読取・専用タイムアウトでの待機を共有する。
`ai_scenario_run` / `ai_scenario_status` もディスパッチャ経由とし、CLI の従来の返却形式
（開始時は予定パス、status は `resultFilePath` を持つオブジェクト）は維持する。
`settleTimeoutSeconds` はシナリオ全体の待機には使わない。
メールボックスは一要求ずつ処理するため、run 待機中の status 要求はその後に処理される。

```sh
python3 Assets/UniLab.AI/Tools/ai_client.py agent.export '{"name":"regression"}'
python3 Assets/UniLab.AI/Tools/ai_client.py scenario.run '{"path":"<export 応答の path>","name":"regression","scenarioTimeoutSeconds":900}' --timeout 930
```

クライアント側の `--timeout` はサーバーの `scenarioTimeoutSeconds` より長く設定する。
追加テストは `AgentExportTest`、`AiScenarioExecutionTest`、`InputOverlayInputStateTest`、
`AiCommandDispatcherTest` のシナリオ操作契約。Unity のコンパイル・再生・録画確認は別途行う。

## T3: 対話操作の準備待ち・オブジェクト断定・階層ダンプ（2026-09-11）

`AgentAction` は `waitForText` / `waitForObject` / `waitForFocus` / `waitForScene` と
`timeoutSeconds` を持つ。既定値は `UiScenarioStep.DefaultTimeoutSeconds` の 30 秒で共用する。
JSON の省略値を確実に保持するため、ゲートウェイとセッションコマンドは新しい `AgentAction` に
`JsonUtility.FromJsonOverwrite` で値を適用する。action の上限は有限の正数のみ受け付ける。

`AgentActionWait` は `UiScenarioStepReader.CreateAnchor` でアンカーを一度生成し、
`UiInputLocator.IsAnchorSatisfied` が成立するまで非同期ディスパッチャ内で待つ。
複数条件は AND。既存の可視テキスト・対象解決・遮蔽・操作可否・フォーカス・シーンロードの判定を共用し、
同等の判定を別実装しない。時刻はシナリオと同じ `Time.realtimeSinceStartupAsDouble` を使う。
成立後に既存の `readyTimeoutSeconds` による対象の準備待ち、入力、落ち着き待ちへ進む。

アンカーのタイムアウトでは行動を呼ばず、`ok:false, ready:false, settled:false` と `message` を返す。
`waitedMs` は明示アンカー待ちと対象の自動準備待ちの合計。
行動のない `waitFor*` だけの要求は、成立時に一手として記録し、
`ok:true, ready:true, settled:true, message:"待機条件が成立しました。"` を返す。
この要求は入力後の落ち着き待ちを追加せず、その時点の `expect` を一回評価する。
同期 CLI はフレームを進めない既存構造を維持し、明示アンカーが未成立なら入力を送らず、
`ok:false, message` で非同期メールボックスの利用を案内する。

`actions.jsonl` に同名の `waitFor*` 4 フィールドと `timeoutSeconds` を追加する。
待機だけの `actionKind` は `wait`。タイムアウトは `status:"rejected"` で条件を記録するが、
実行したステップには加えない。実行した手の export は `UiScenarioStep` の同名フィールドへ写す。
実物の `UiScenarioStep` に上限フィールドがなかったため `timeoutSeconds` を追加し、
ランナーの準備待ち・操作後のシーン待ちにも使う。旧 JSON の省略・0 以下は従来の 30 秒。
ステップ全体の保護上限は `max(30, timeoutSeconds) × 2` 秒とし、短い待機指定で従来の実行猶予を縮めない。

`objectExists` / `objectAbsent` は `target` を `UiInputLocator.FindTarget` で一回解決し、
アクティブな GameObject の存在／不在を断定する。非 UI オブジェクトも対象で、待機はしない。
シナリオは `ScenarioExpectationEvaluator`、対話操作・目標は実物で別の `AgentExpectationEvaluator` を使うため、
両方の switch に同じ二語を追加する。`exists` / `absent` の UI スナップショットに対する意味は維持する。

`scene.dump` は `SceneHierarchyDumper.Dump()` の結果を `SceneHierarchyDumpText.Format` で整形する。
`depth` はルートを 0 とする最大深度（既定 3）、`maxNodes` は表示ノードの全シーン通算上限（既定 200）。
`filter` は GameObject 名の大文字・小文字を区別する部分一致。表示対象だけを件数に数える。
`text` は `scene=<名前>` と、深さごとに半角空白 2 個でインデントした
`<名前> activeInHierarchy=true|false` の行から成る。名前の改行は `\r` / `\n` として表示する。
超過時は末尾に `... maxNodes=<上限>`。一致するノードがなければ空文字列を返す。

実物のノードは `activeSelf` と `parentIndex` を持つため、親が先に並ぶ既存の連続 index を利用して
祖先の状態を伝播し、`activeInHierarchy` を算出する。保存 JSON のスキーマは変えない。
`save:true` は全階層を `DebugOutput/scene/hierarchy-<yyyyMMdd-HHmmss-fff>.json` に保存して絶対パスを返す。
深さ・件数・フィルタによる制限はテキストのみ。`save:false` の `path` は空。
CLI は `Pipeline/Scene/AiSceneDumpCliCommand.cs` の `ai_scene_dump` から共通ディスパッチャを呼ぶ。


## T5: Development Build の自律実行

`DebugOutputPath.Resolve(bool isEditor, string projectRoot, string persistentDataPath)` は
Editor の `<projectRoot>/DebugOutput` と実機の `<persistentDataPath>/DebugOutput` を純関数で切り替える。
`DirectoryPath` は `Application.isEditor` で分岐する。
`ResolveRelative(string relativeOrAbsolute)` は Editor でプロジェクトルート、実機で `persistentDataPath` を基準とし、
絶対パスはそのまま正規化する。`AiCaptureSupport.Request` の `outputDirectory`（op 引数名は `directory`）と
`AiScenarioExecution.Start` のシナリオ `path` はこの共通解決を使う。

`ScenarioAutorun` は `AfterSceneLoad` で一度だけ起動設定を読む。
`Resources.Load<UniTestifySettings>("UniTestifySettings")` のビルド設定を基に、
`DebugOutput/scenario-autorun.json` の指定フィールドを上書きする。
Standalone / Editor では起動引数 `-unitestify-scenario <path>` をさらに適用する（パスのみ）。
`AiMailboxServer` や `.enabled` を必要とせず、op は追加しない。

| 設定元 | フィールドと既定値 |
|---|---|
| `Runtime/Resources/UniTestifySettings.asset` | `autorunScenarioPath:""` / `autorunDelaySeconds:2` |
| `scenario-autorun.json` | `path` / `name` / `delaySeconds`。省略項目はビルド設定を維持し、name は空を既定とする |

`path` / `name` は文字列、`delaySeconds` は有限の 0 以上の数値。
空のパスでは実行しない。0 秒の明示指定は待機なし。名前の省略・空文字はシナリオファイル名から補う。
不正な設定はログを出して中止し、黙ってビルド時の別シナリオを実行しない。
設定は別の一時オブジェクトへ読み込み、Resources のアセットは変更しない。

実時間で待機後、既存入口と同じ `UiScenarioJsonPresence.Apply` を通して `UiScenarioRunner.Run` を開始する。
結果保存先は `DebugOutput/scenario-results/<name>-<日時>-<識別子>/result.json`。
既存メニュー／`scenario.run` の `<name>.json` と `ScenarioResult` のスキーマは維持する。
実行中はランナーの生存確認だけとし、結果 JSON の読み込みは終了後の一回。
空シナリオが `Run` の内部で同期完了しても、起動後のイベント購読に依存しないため完了を取りこぼさない。

完了時は `DebugOutput/scenario-autorun.done.json` に次の形式で保存する:

```json
{"path":"<結果 JSON の絶対パス>/result.json","verdict":"pass"}
```

`verdict` は結果ファイルの `pass` / `fail` / `error` をそのまま写す。
前回の完了ファイルは自律実行の待機開始前に削除し、今回の通知は一時ファイルを閉じてから移動して公開する。
読み込み・起動・保存に失敗した場合は
`[ScenarioAutorun]` のログを残し、完了ファイルは生成しない。設定は消費せず、次の起動でも再実行する。
シナリオ本体の配布・Android の `StreamingAssets` 読み込みは含まない。
仮想 `Touchscreen` の入力と Editor／Android 間の verdict 一致は依頼者が実機確認する。
