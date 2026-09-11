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

## 5. うまくいかないとき

| 症状 | 見るところ |
|---|---|
| `ok:false, error:"応答待ちがタイムアウト"` | Play 中か、`.enabled` を置いた後に Play を始めたか。`UniTestify/Mailbox/Start` で手動起動 |
| `playMode が必要です` | `agent.*` は PlayMode 専用 |
| `目標 JSON に期待値がありません` | `{"goal":{"freePlay":true}}` か `{"goal":{"goal":[{"kind":…}]}}` の形にする |
| `submit 対象が見つかりません` | `agent.find` で名前を確認。同名行は `label:<部分一致>` で指定 |
| 撮影が `blank:true` | 一様な画面（ローディング等）。遷移後に撮り直す |
| 観測に画面外の行が出ない | 既定 `scope:"visible"`。全部見るなら `scope:"all"`（`[clipped]` / `blocked:` が付く） |
