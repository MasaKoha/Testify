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

## フォルダ構成

各行のファイル数は直下の `.cs` のみ（子フォルダ・`.meta`・`.asmdef` は含めない）。
機能を境界とし、すべて上限 10 以下。入口の型を機能フォルダ直下、実装詳細を下位へ置く。
名前空間はフォルダ階層に連動させず、`UniTestify` / `UniTestify.Editor` / `UniTestify.Pipeline` / `UniTestify.Tests` のまま。

| アセンブリルート | 直下の C# | 据え置く定義 |
|---|---:|---|
| `Runtime/` | 0 | `Runtime/UniTestify.asmdef` |
| `Editor/` | 0 | `Editor/UniTestify.Editor.asmdef` |
| `Pipeline/` | 0 | `Pipeline/UniTestify.Pipeline.asmdef` |
| `Tests/EditMode/` | 0 | `Tests/EditMode/UniTestify.Tests.EditMode.asmdef` |

| フォルダ | C# 数 | 配置する責務 |
|---|---:|---|
| `Runtime/Agent/` | 7 | セッションの入口・寿命、設定、応答、要素検索、観測テキスト |
| `Runtime/Agent/Actions/` | 3 | 行動 JSON、入力送出、行動の事後条件 |
| `Runtime/Agent/Goals/` | 3 | 目標 JSON、目標の妥当性検証・達成判定 |
| `Runtime/Agent/Session/` | 4 | セッションの停止判定、履歴・成果物・終了レポート |
| `Runtime/Gateway/` | 8 | 共通ディスパッチャ、要求・応答・引数、JSON 検証、直近ログ、実行状態 |
| `Runtime/Gateway/Execution/` | 3 | 撮影の発行・完了待ち、シナリオ起動・結果待ち、入力後の静止待ち |
| `Runtime/Gateway/Mailbox/` | 3 | ファイル要求・応答、ポーリングサーバー、Prefab の型付き参照 |
| `Runtime/Scenario/` | 8 | シナリオの入口・ステップ解釈、入力実行、成果物保存、記録の開始停止 |
| `Runtime/Scenario/Expectations/` | 3 | シナリオ期待値、評価器、失敗理由 |
| `Runtime/Scenario/Results/` | 3 | シナリオ全体・ステップの結果、証拠パス |
| `Runtime/Snapshot/` | 6 | UI スナップショットの収集・保存・整形・比較と観測モデル |
| `Runtime/Input/` | 9 | 入力注入・記録・再生、イベント・待機アンカー・再生結果、入力の語彙 |
| `Runtime/Input/Overlay/` | 8 | 入力可視化の入口・制御・描画・履歴・表示設定 |
| `Runtime/Input/Overlay/Input/` | 4 | 入力 API ごとの取得と押下・解放・保持状態 |
| `Runtime/Monkey/` | 8 | ランダム探索、設定、網羅率、操作履歴、違反・終了結果 |
| `Runtime/Performance/` | 5 | 性能計測の入口・フレーム採取、ステップ・全体レポート |
| `Runtime/Recording/` | 5 | 動画・音声記録、manifest、マーカー、録画結果 |
| `Runtime/Forensics/` | 6 | 例外時の証拠収集、文脈・保留ログ、ファイルログ出力 |
| `Runtime/Scene/` | 4 | シーン階層の収集、シーン・ノード・ダンプモデル |
| `Runtime/Ui/` | 9 | UI 入力対象の解決、可視判定・観測範囲・準備状態、スクロール、レイアウト監査 |
| `Runtime/Adapters/` | 4 | ゲーム状態・busy・コマンドの接続契約と登録窓口 |
| `Runtime/Core/` | 3 | 出力先、アセンブリ属性、SerializeField 結線情報 |
| `Runtime/RunArchive/` | 3 | ラン概要と性能・視覚回帰の要約モデル |
| `Editor/Gateway/` | 0 | メールボックスの Editor 操作を束ねる親フォルダ |
| `Editor/Gateway/Mailbox/` | 1 | メールボックス起動メニュー |
| `Editor/RunArchive/` | 8 | 成果物の集約・索引生成、シナリオ成果物の読取モデル、メニュー |
| `Editor/VisualRegression/` | 9 | 画像比較、無視領域の解析・設定、比較結果・レポート、メニュー |
| `Editor/Scenario/` | 1 | シナリオ実行メニュー |
| `Editor/Scene/` | 1 | シーン階層ダンプメニュー |
| `Editor/Snapshot/` | 1 | スナップショット保存・入力オーバーレイ確認メニュー |
| `Editor/Ui/` | 1 | UI レイアウト監査メニュー |
| `Pipeline/Agent/` | 6 | エージェントの開始・行動・観測・目標判定・終了・書出し CLI |
| `Pipeline/Gateway/` | 3 | CLI 引数・実行支援、op 一覧 CLI |
| `Pipeline/Gateway/Execution/` | 1 | 撮影 CLI |
| `Pipeline/Gateway/Mailbox/` | 1 | メールボックス CLI |
| `Pipeline/Scenario/` | 3 | シナリオ実行・状態取得 CLI と状態応答 |
| `Pipeline/Snapshot/` | 1 | UI スナップショット CLI |
| `Pipeline/Forensics/` | 2 | 最新フォレンジック CLI と応答 |
| `Pipeline/Monkey/` | 1 | ランダム探索 CLI |
| `Tests/EditMode/Runtime/` | 0 | 実装のアセンブリ・機能階層に対応する親フォルダ（直下の C# なし） |
| `Tests/EditMode/Runtime/Input/` | 0 | 実装のアセンブリ・機能階層に対応する親フォルダ（直下の C# なし） |
| `Tests/EditMode/Runtime/Input/Overlay/` | 0 | 実装のアセンブリ・機能階層に対応する親フォルダ（直下の C# なし） |
| `Tests/EditMode/Runtime/Agent/` | 2 | `Runtime/Agent/` に対応する EditMode テスト |
| `Tests/EditMode/Runtime/Agent/Actions/` | 2 | `Runtime/Agent/Actions/` に対応する EditMode テスト |
| `Tests/EditMode/Runtime/Agent/Goals/` | 1 | `Runtime/Agent/Goals/` に対応する EditMode テスト |
| `Tests/EditMode/Runtime/Agent/Session/` | 2 | `Runtime/Agent/Session/` に対応する EditMode テスト |
| `Tests/EditMode/Runtime/Gateway/` | 3 | `Runtime/Gateway/` に対応する EditMode テスト |
| `Tests/EditMode/Runtime/Gateway/Execution/` | 2 | `Runtime/Gateway/Execution/` に対応する EditMode テスト |
| `Tests/EditMode/Runtime/Gateway/Mailbox/` | 2 | `Runtime/Gateway/Mailbox/` に対応する EditMode テスト |
| `Tests/EditMode/Runtime/Snapshot/` | 2 | `Runtime/Snapshot/` に対応する EditMode テスト |
| `Tests/EditMode/Runtime/Input/Overlay/Input/` | 1 | `Runtime/Input/Overlay/Input/` に対応する EditMode テスト |
| `Tests/EditMode/Runtime/Ui/` | 3 | `Runtime/Ui/` に対応する EditMode テスト |

`Runtime/Prefabs/`（メールボックスの Prefab）と `Runtime/Resources/`（型付き参照アセット）は既存位置を維持する。
既存スクリプトの `.meta` はスクリプトと対で移し、追加フォルダにも `.meta` を置く。

テストは `Tests/EditMode/<実装アセンブリのルート>/<同じ機能パス>/` へ対応させる。
現存する 20 ファイルはすべて Runtime 対象のため `Tests/EditMode/Runtime/` 以下に置く。
Editor / Pipeline のテストを追加する場合も同じ対応規則に従い、テストのない機能に空フォルダは作らない。
複数機能を検証する既存テストは主対象で配置する（`AgentExpectTest` は `Agent/Actions/`、
`AgentExportTest` は `Agent/Session/`）。全ファイルの移動対応と判断は [実装記録](implementation.md) を参照。

## 依存の鉄則

1. ゲーム本体のライブラリに依存しない（UniLab / R3 / UniTask / VContainer）。依存は `UnityEngine`・.NET 標準・`Unity.TextMeshPro`・`Unity.InputSystem`
2. `Pipeline/` は `com.unity.pipeline` が無くてもコンパイルできる（asmdef の `versionDefines` で `TESTIFY_PIPELINE`）
3. 毎フレーム処理（`AiMailboxServer.Update`、オーバーレイ描画）はアロケーションを増やさない。観測時（`UiSnapshot.Capture`）だけ `GetComponent` 可
4. 名前空間は `UniTestify`。`Debug` という語を名前空間に使わない

## 速さのための設計

- **Unity 内蔵メールボックス**: 外部中継プロセスと CLI 起動（node）を経由しない。1 往復 0.10 秒
- **準備待ちと落ち着き待ちを Unity 側で完結**: `submit` は対象が押せるまで、その後は継続入力・シーンロード・ゲームの busy が収まって 0.35 秒静止するまで待ってから観測する。AI は空押しの待ち手を入れなくてよい
- **観測のダイエット**: 画面外・マスク外・背面の要素を既定で出さない。同名行は先頭 3 件＋件数に畳む。同一フレームのスナップショットは共有
- **往復の削減**: `steps` 一括、`expect` 同時検証、`agent.find`、`scrollTo`、観測と撮影の同一フレーム
- **省電力**: 要求が 5 秒無ければポーリングを 0.05 → 0.25 秒に伸ばす

## 切り出しの経緯

- 2026-09-02〜05 に UniLab リポジトリの `Assets/UniLab.AI/` として実装（設計書 01〜12）
- 2026-09-05 に AI ゲートウェイ（PR1〜PR7）で Codex / Claude の両方から同じ経路で使えるようになり、UniLab 本体への依存が無いことを保ったまま **UniTestify** として独立
- 名前は音ゲー曲（Arcaea「UniTestify」）から。「検証して証言する」

## 利用側への同期

パッケージ参照（git URL）が基本。コピー導入の利用側（karakuri-client の `Assets/UniTestify/`）へは:

```bash
rsync -a --delete --exclude TestProject --exclude docs --exclude .git --exclude .gitignore --exclude CLAUDE.md --exclude AGENTS.md --exclude README.md --exclude LICENSE \
  /Users/masakoha/GitHub/pisuke-root/UniTestify/ \
  /Users/masakoha/GitHub/pisuke-root/karakuri/karakuri-client/Assets/UniTestify/
```

変更は UniTestify 側で PR → マージ → 利用側で同期 PR、の順。利用側で直接 `Assets/UniTestify/` を編集しない。

## テスト

- `Tests/EditMode/` は純ロジックのみ（PlayMode 不要）。`TestProject/` を Unity で開いて Test Runner で回す
- PlayMode が要る確認（落ち着き待ち・撮影・シナリオ）は利用側の実機で行う。確認後は必ず Play を止める
