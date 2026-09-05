# アーキテクチャ

## 全体像

```
 AI クライアント（Claude Code / Codex / 人）
   │  ファイル I/O（req/res JSON）              │ Unity 公式 CLI（unity command ai_*）
   ▼                                           ▼
 AiMailboxServer（MonoBehaviour、1 件ずつ非同期） Pipeline/[CliCommand]（同期）
   └────────────────────┬─────────────────────┘
                        ▼
                AiCommandDispatcher（op → 実装。唯一の入口）
      ┌───────────┬───────────┬───────────┬────────────┐
      ▼           ▼           ▼           ▼            ▼
 AgentSession   UiSnapshot  AiCaptureSupport  AiScenarioExecution  AiConsoleLog
 （観測・行動・記録）（目）    （撮影）        （ランナー起動・待機） （ログ）
      │
      ├─ AgentActionExecutor   … 行動 JSON の解釈と InputInjector への送出
      ├─ AgentObservationFormatter … 観測テキストの整形（候補・game・busy・goalFailures）
      ├─ AgentSessionArtifacts … actions.jsonl / session.json / scenario.json
      └─ AgentSessionGuards    … 予算・反復検出・forbid
```

ゲーム側との接点は `GameAdapterRegistry` だけ:

| 登録先 | ゲームが実装するもの | 使われ方 |
|---|---|---|
| `StateProvider` | `IGameStateProvider.GetState()` | 観測の `game:` 行 |
| `BusyProvider` | `IGameBusyProvider.IsBusy / Reason` | 落ち着き待ちと `agent: busy=` |
| `CommandHandler` | `IGameCommandHandler` | デバッグコマンド（素材付与等） |

## モジュール一覧

| 領域 | 主なクラス |
|---|---|
| 観測 | `UiSnapshot`（要素収集・可視判定・差分）、`UiVisibilityUtility`（矩形・遮蔽・マスク）、`UiObservationScope`、`UiReadiness` |
| 操作 | `InputInjector`（Input System へ生入力）、`UiInputLocator`（名前・ラベル解決、submit）、`UiScrollTo`、`FocusDirection` |
| エージェント | `AgentSession` 群、`AgentGoal` / `AgentGoalValidator`、`AgentOptions`、`AgentExpectationEvaluator`、`AgentFind`、`AgentActExpectation` |
| ゲートウェイ | `AiCommandDispatcher`、`AiCommandRequest` / `AiCommandResponse` / `AiCommandArguments` / `AiCommandContext`、`AiJsonObject`（壊れた JSON を入口で弾く）、`AiSettleWait`、`AiMailboxServer` / `AiMailboxFiles`、`AiCaptureSupport`、`AiConsoleLog`、`AiScenarioExecution` |
| シナリオ | `UiScenario` / `UiScenarioStep` / `UiScenarioStepReader`、`UiScenarioRunner`、`ScenarioInputExecutor`、`ScenarioExpectationEvaluator`、`ScenarioArtifactWriter`、`ScenarioRecordingCoordinator`、`ScenarioResult` |
| 記録 | `VideoRecorder`、`AudioRecorder`、`FileLogSink`、`InputRecorder` / `InputReplayer`、`PerformanceRecorder`、`RunArchive`（Editor） |
| 診断 | `ExceptionForensics`、`UiLayoutAuditor`、`SceneHierarchyDumper`、`MonkeyTester`、`VisualRegression`（Editor） |
| 可視化 | `InputOverlayController` ＋ Renderer / PointerRenderer / VisualPrimitives / History / InputState / HeldState / InputSystemSource / LegacyInputSource |

## 依存の鉄則

1. ゲーム本体のライブラリに依存しない（UniLab / R3 / UniTask / VContainer）。依存は `UnityEngine`・.NET 標準・`Unity.TextMeshPro`・`Unity.InputSystem`
2. `Pipeline/` は `com.unity.pipeline` が無くてもコンパイルできる（asmdef の `versionDefines` で `TESTIFY_PIPELINE`）
3. 毎フレーム処理（`AiMailboxServer.Update`、オーバーレイ描画）はアロケーションを増やさない。観測時（`UiSnapshot.Capture`）だけ `GetComponent` 可
4. 名前空間は `Testify`。`Debug` という語を名前空間に使わない

## 速さのための設計

- **Unity 内蔵メールボックス**: 外部中継プロセスと CLI 起動（node）を経由しない。1 往復 0.10 秒
- **準備待ちと落ち着き待ちを Unity 側で完結**: `submit` は対象が押せるまで、その後は継続入力・シーンロード・ゲームの busy が収まって 0.35 秒静止するまで待ってから観測する。AI は空押しの待ち手を入れなくてよい
- **観測のダイエット**: 画面外・マスク外・背面の要素を既定で出さない。同名行は先頭 3 件＋件数に畳む。同一フレームのスナップショットは共有
- **往復の削減**: `steps` 一括、`expect` 同時検証、`agent.find`、`scrollTo`、観測と撮影の同一フレーム
- **省電力**: 要求が 5 秒無ければポーリングを 0.05 → 0.25 秒に伸ばす

## 切り出しの経緯

- 2026-09-02〜05 に UniLab リポジトリの `Assets/UniLab.AI/` として実装（設計書 01〜12）
- 2026-09-05 に AI ゲートウェイ（PR1〜PR7）で Codex / Claude の両方から同じ経路で使えるようになり、UniLab 本体への依存が無いことを保ったまま **Testify** として独立
- 名前は音ゲー曲（Arcaea「Testify」）から。「検証して証言する」

## 利用側への同期

パッケージ参照（git URL）が基本。コピー導入の利用側（karakuri-client の `Assets/Testify/`）へは:

```bash
rsync -a --delete --exclude TestProject --exclude docs --exclude .git --exclude .gitignore --exclude CLAUDE.md --exclude AGENTS.md --exclude README.md --exclude LICENSE \
  /Users/masakoha/GitHub/pisuke-root/Testify/ \
  /Users/masakoha/GitHub/pisuke-root/karakuri/karakuri-client/Assets/Testify/
```

変更は Testify 側で PR → マージ → 利用側で同期 PR、の順。利用側で直接 `Assets/Testify/` を編集しない。

## テスト

- `Tests/EditMode/` は純ロジックのみ（PlayMode 不要）。`TestProject/` を Unity で開いて Test Runner で回す
- PlayMode が要る確認（落ち着き待ち・撮影・シナリオ）は利用側の実機で行う。確認後は必ず Play を止める
