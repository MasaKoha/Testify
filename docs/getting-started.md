# はじめかた

## 1. 導入

### パッケージとして入れる（推奨）

`Packages/manifest.json` に追加する。

```json
{
  "dependencies": {
    "com.pisuke.unitestify": "https://github.com/MasaKoha/UniTestify.git",
    "com.unity.inputsystem": "1.14.0"
  }
}
```

- 必要なパッケージ: Input System（生入力の注入に使う。無ければ `submit` / `move` 等の UI 経路だけ動く）、uGUI（TextMeshPro を含む）
- 任意: Unity 公式 CLI `com.unity.pipeline`。入れると `unity command ai_*` から同じ機能を叩ける（`TESTIFY_PIPELINE` define が自動で立つ）

### コピーして入れる（利用側で改造したい場合）

リポジトリ直下の `Runtime/ Editor/ Pipeline/ Tests/ Tools/ package.json` を利用側の `Assets/UniTestify/` へ置く。karakuri-client はこの方式で、`rsync` で同期している（`architecture.md`）。

## 2. Play 中にメールボックスを起動する

AI クライアントは **ファイル I/O だけ** で Unity と話す（サンドボックスから localhost に届かない Codex でも使える）。Unity 側の `AiMailboxServer` が `DebugOutput/agent-mailbox/` を監視し、`req-*.json` を処理して `res-*.json` を書く。

起動方法は 3 つ（どれか 1 つ）:

1. **自動起動**: Play を始める前に `DebugOutput/agent-mailbox/.enabled` を置く。`ai_client.py` は初回に自動で置く
2. Editor メニュー `UniTestify/Mailbox/Start`（`Stop` で停止）
3. Unity 公式 CLI: `unity command ai_mailbox --start`（`--status` で間隔と最終処理時刻）

### Editor 操作（Play 停止中も利用可能）

パッケージを読み込んだ Editor では `EditorControlMailbox` が自動で常駐し、
プロジェクト直下の `DebugOutput/editor-mailbox/` を監視する。開始メニューや `.enabled` は不要。
`editor_ctl.py` で、起動済み Editor の Play / Stop / Pause / フォーカス / メニューを操作できる。

Unity プロジェクトのルートから実行する:

```sh
EDITOR_CLIENT=Packages/com.pisuke.unitestify/Tools/editor_ctl.py   # コピー導入なら Assets/UniTestify/Tools/editor_ctl.py

python3 "$EDITOR_CLIENT" status
python3 "$EDITOR_CLIENT" play
python3 "$EDITOR_CLIENT" status
python3 "$EDITOR_CLIENT" pause
python3 "$EDITOR_CLIENT" unpause
python3 "$EDITOR_CLIENT" focus_game_view
python3 "$EDITOR_CLIENT" simulator_view
python3 "$EDITOR_CLIENT" menu 'Window/General/Console'
python3 "$EDITOR_CLIENT" stop
python3 "$EDITOR_CLIENT" status
```

出力は `{"ok":true,"message":"…"}` の一行 JSON。`play` / `stop` / `pause` の
成功は**要求受理**で、状態変更は応答後の Editor 更新で行う。
`status` の `message` に `isPlaying=True` が現れるまで照会して Play 開始を確認する。
停止確認は `isPlaying=False`、Pause 確認は `isPaused=True`、解除確認は `isPaused=False`。
`isCompiling=True` の間は `status` 以外が `ok:false` になるため、コンパイル完了を確認してから再要求する。
`simulator_view` は Device Simulator を前面に出す。利用できない環境では `ok:false` を返す。

`--mailbox DIR` を省略すると、カレントから親へ `DebugOutput/editor-mailbox` を探索する。
初回は `Assets/` と `ProjectSettings/` のある親も探す。パッケージリポジトリから
`TestProject/` の Editor を操作する場合は `--mailbox TestProject/DebugOutput/editor-mailbox` を指定する。
Runtime 用の環境変数 `TESTIFY_MAILBOX` は参照しない。
`--timeout` は応答待ちの秒数（既定 60 秒）。終了コードは成功 0、失敗 1。
タイムアウト時は要求が残り、後から実行される場合がある。
要求・応答と全 op は [Editor メールボックス](ops-reference.md#editor-メールボックス) を参照。

## 3. クライアントから操作する

```sh
CLIENT=Packages/com.pisuke.unitestify/Tools/ai_client.py   # コピー導入なら Assets/UniTestify/Tools/ai_client.py

python3 $CLIENT ping
python3 $CLIENT agent.begin '{"goal":{"freePlay":true,"maxSteps":5000,"maxSeconds":14400}}'
python3 $CLIENT agent.observe
python3 $CLIENT agent.act '{"action":{"submit":"NewGameButton"}}'
python3 $CLIENT agent.act '{"steps":[{"press":"east"},{"submit":"TabButton1","expect":[{"kind":"textVisible","value":"ルーン"}]}]}'
python3 $CLIENT agent.find '{"label":"雷撃"}'
python3 $CLIENT agent.act '{"action":{"scrollTo":"MarketRuneListRow8"}}'
python3 $CLIENT agent.observe '{"capture":"market"}'
python3 $CLIENT console '{"count":40,"level":"error"}'
python3 $CLIENT agent.export '{"name":"my-tour"}'
python3 $CLIENT agent.end
```

- 出力は 1 行目がメタ情報の JSON（`ok` / `settled` / `ready` / `elapsedMs` …）、2 行目以降が観測テキスト
- メールボックスの場所は `--mailbox DIR` → 環境変数 `TESTIFY_MAILBOX` → カレントから上へ `DebugOutput/agent-mailbox` を探索、の順で決まる

### Claude Code から

- 上のクライアントをそのまま Bash から叩く。Unity 公式 CLI があれば `unity command ai_agent_observe` 等の同期版も使える（こちらは落ち着き待ちをしない）
- 回帰撮影はシナリオ JSON を `scenario.run` op（またはランナーのメニュー）で流す。撮った PNG は画像で確認する

### Codex から

- サンドボックスは localhost に届かないので **メールボックス一択**。Codex には「このクライアントだけを使う。Unity を起動・終了しない」と指示する
- 指示書のひな形は利用側リポジトリに置く（karakuri: `tools/codex_playtest/brief_*.md`）。「どの画面を辿るか」「何を報告するか」を書き、Codex は `observe` の文字を根拠に判断する

## 4. ゲーム側の状態を観測に載せる（任意だが強く推奨）

観測テキストの `game:` 行に、ゲーム固有の値（ゴールド・HP・フロア）を出せる。AI が画像を見ずに数値を突き合わせられる。

```csharp
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UniTestify;

public sealed class MyGameStateProvider : IGameStateProvider
{
    public IReadOnlyDictionary<string, object> GetState()
    {
        return new Dictionary<string, object> { ["gold"] = _assets.Gold, ["battle.ally.0.hp"] = _allies[0].CurrentHp };
    }
}

// 起動時（DI のビルドコールバック等）
GameAdapterRegistry.StateProvider = new MyGameStateProvider(...);
GameAdapterRegistry.BusyProvider = new MyGameBusyProvider(...);   // 遷移・演出中を IsBusy で返すと act が待ってくれる
GameAdapterRegistry.CommandHandler = new MyGameCommandHandler(...); // 素材付与などのデバッグコマンド（任意）
#endif
```

- `IGameBusyProvider.IsBusy` が true の間、`agent.act` は「落ち着いていない」として観測を待ち、観測に `agent: busy=<Reason>` が出る。ローディングオーバーレイや入力ブロックの状態をそのまま返せばよい

## 5. 実機 Development Build で自律実行する

`ScenarioAutorun` が起動シーンの読み込み後に設定を一度読み、指定秒数後に `UiScenarioRunner.Run` を開始する。
メールボックスの起動・`.enabled` は不要。待機は `Time.timeScale` に依存しない実時間。
シナリオ実行後もアプリは終了しない。設定が残っていれば、次回のアプリ起動／Editor の Play 開始でも実行する。

### 初回起動用の設定をビルドへ含める

初回起動前は `persistentDataPath` 配下へ設定ファイルを置けないため、
付属の `Runtime/Resources/UniTestifySettings.asset` を Inspector で編集して Development Build に含める。
`Resources.Load<UniTestifySettings>("UniTestifySettings")` で読み込まれる。

| フィールド | 既定値 | 指定する内容 |
|---|---|---|
| `autorunScenarioPath` | 空文字 | シナリオ JSON のパス。空なら自律実行しない |
| `autorunDelaySeconds` | `2` | 起動シーン読み込み後の待機秒数。0 は待機なし。有限の 0 以上 |

- コピー導入なら `Assets/UniTestify/Runtime/Resources/UniTestifySettings.asset` を編集する。
- UPM 導入ならパッケージを埋め込み／ローカル化して、パッケージ内の付属アセットを編集する。
- アセットを作り直す場合は `Assets > Create > UniTestify > Settings` で作成し、
  上記の `Runtime/Resources/UniTestifySettings.asset` に置く。同じ Resources パスの設定アセットは 1 個にする。

このアセットに入るのは設定だけで、シナリオ本体はコピーされない。
シナリオは `persistentDataPath` 配下、または設定で指すファイルとして読み取れるパスへ別途配置する。
初回から実行する場合も、指定した待機時間の終了までにそのパスでシナリオを読める状態にする。
Android の APK 内 `StreamingAssets` を `UnityWebRequest` で読む処理は含まない。

### 外部ファイルで上書きする

実機の `<Application.persistentDataPath>/DebugOutput/scenario-autorun.json` に置く:

```json
{"path":"scenarios/tour.json","name":"device-tour","delaySeconds":2.0}
```

`path` / `name` は文字列、`delaySeconds` は有限の 0 以上の数値。
JSON の指定フィールドがビルド設定を上書きし、省略フィールドはビルド設定を維持する。
`name` の省略・空文字はシナリオファイルの拡張子を除いた名前を使う。
`{"path":""}` でビルド時の自律実行を無効にできる。壊れた JSON・型違い・不正な秒数はログへ出して起動を中止する。

相対パスは Editor では Unity プロジェクトルート、実機では `Application.persistentDataPath` を基準にする。
上の例の実機シナリオ本体は `<persistentDataPath>/scenarios/tour.json`。
絶対パスも指定できる。この基準は `scenario.run` の `path` と、`capture` / `agent.observe` の
`directory` 指定でも共通。撮影先を省略すると `<DebugOutputPath.DirectoryPath>/captures` になる。
シナリオ本体の `outputDirectory` は既存仕様のままなので、実機で既定出力先を使う場合は省略する。

### Android へ配置・結果を回収する

Development Build をインストールして一度起動し、実際の `Application.persistentDataPath` を確認して停止する。
Android では通常 `/storage/emulated/0/Android/data/<アプリID>/files`。
実際の端末のパスを使う（[Unity の persistentDataPath](https://docs.unity3d.com/ScriptReference/Application-persistentDataPath.html)）。
次のアプリ ID・起動 Activity・永続データパスは対象ビルドに合わせる:

```sh
APPLICATION_ID='com.example.game'
LAUNCH_COMPONENT='com.example.game/com.unity3d.player.UnityPlayerActivity'
DEVICE_DATA="/storage/emulated/0/Android/data/${APPLICATION_ID}/files"
DEVICE_OUTPUT="${DEVICE_DATA}/DebugOutput"

adb shell am force-stop "$APPLICATION_ID"
adb shell mkdir -p "$DEVICE_OUTPUT" "${DEVICE_DATA}/scenarios"
adb push ./tour.json "${DEVICE_DATA}/scenarios/tour.json"
adb push ./scenario-autorun.json "${DEVICE_OUTPUT}/scenario-autorun.json"
adb shell rm -f "${DEVICE_OUTPUT}/scenario-autorun.done.json"
adb shell am start -n "$LAUNCH_COMPONENT"
```

完了時に `DebugOutput/scenario-autorun.done.json` ができる。内容は結果の絶対パスと既存ランナーの判定:

```json
{"path":"<persistentDataPath>/DebugOutput/scenario-results/device-tour-<日時>-<識別子>/result.json","verdict":"pass"}
```

前回の完了ファイルは自律実行の待機開始前に削除する。結果は起動ごとのディレクトリへ保存し、過去の結果を上書きしない。
シナリオが開始できない場合や結果を保存できない場合は、完了ファイルを生成せず `[ScenarioAutorun]` のログへ理由を出す。
完了ファイルの生成後に回収する:

```sh
adb pull "${DEVICE_OUTPUT}/scenario-autorun.done.json" ./scenario-autorun.done.json
adb pull "$DEVICE_OUTPUT" ./device-DebugOutput
```

回収した `result.json` の `verdict` を、同じシナリオを Editor で実行した結果と比較する。
`tap` / `swipe` / `pinch` を含むシナリオでは、仮想 `Touchscreen` の入力が実機 UI に届くことも依頼者が確認する。

### Standalone / Editor の起動引数

```sh
./Game -unitestify-scenario scenarios/tour.json
```

`-unitestify-scenario <path>` はパスだけを JSON より後に上書きする。名前と待機秒数は JSON／ビルド設定を使う。
`-` で始まるファイル名は `./` を付けるか絶対パスで指定する。
Editor も同じ起動引数を受け、Play 開始時に実行する。Editor の外部設定と成果物はプロジェクト直下の `DebugOutput/`。
Android ではこの起動引数を読まない。

## 6. うまくいかないとき

| 症状 | 見るところ |
|---|---|
| `ok:false, error:"応答待ちがタイムアウト"` | Play 中か、`.enabled` を置いた後に Play を始めたか。`UniTestify/Mailbox/Start` で手動起動 |
| `playMode が必要です` | `agent.*` は PlayMode 専用 |
| `目標 JSON に期待値がありません` | `{"goal":{"freePlay":true}}` か `{"goal":{"goal":[{"kind":…}]}}` の形にする |
| `submit 対象が見つかりません` | `agent.find` で名前を確認。同名行は `label:<部分一致>` で指定 |
| 撮影が `blank:true` | 一様な画面（ローディング等）。遷移後に撮り直す |
| 観測に画面外の行が出ない | 既定 `scope:"visible"`。全部見るなら `scope:"all"`（`[clipped]` / `blocked:` が付く） |
