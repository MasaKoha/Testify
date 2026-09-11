# CLAUDE.md — UniTestify

AI エージェント（Claude Code / Codex）が Unity ゲームを動かして検証するためのツール群。旧 `UniLab.AI`。

## このリポジトリの構成

| パス | 役割 |
|---|---|
| `Runtime/` | `UniTestify.asmdef` を維持。C# は機能フォルダへ配置 |
| `Runtime/Agent/` | セッション入口・観測・検索。`Actions/` は行動、`Goals/` は目標判定、`Session/` は停止判定・履歴・成果物 |
| `Runtime/Gateway/` | `AiCommandDispatcher` と要求・応答。`Execution/` は撮影・シナリオ・静止待ち、`Mailbox/` はファイル要求・応答、`Http/` は実機向け HTTP 入口 |
| `Runtime/Scenario/` | シナリオ実行。`Expectations/` は期待値判定、`Results/` は結果・証拠モデル |
| `Runtime/Snapshot/`・`Runtime/Ui/`・`Runtime/Scene/` | UI 観測、UI 操作対象・可視性・監査、シーン階層 |
| `Runtime/Input/` | 入力注入・記録・再生。`Overlay/` は可視化、`Overlay/Input/` は入力取得・押下状態 |
| `Runtime/Monkey/`・`Runtime/Performance/`・`Runtime/Forensics/` | ランダム探索、性能計測、例外時の証拠収集・ログ |
| `Runtime/Recording/`・`Runtime/RunArchive/` | 動画・音声記録、ラン概要モデル |
| `Runtime/Adapters/`・`Runtime/Core/` | ゲーム接続契約・登録窓口、出力先・アセンブリ属性・結線情報 |
| `Runtime/Prefabs/`・`Runtime/Resources/` | メールボックスの Prefab と型付き参照アセット（既存位置を維持） |
| `Editor/` | asmdef を維持。`RunArchive/`・`VisualRegression/` に集約・比較機能、`Gateway/Mailbox/`・`Scenario/`・`Snapshot/`・`Scene/`・`Ui/` に各機能のメニュー |
| `Pipeline/` | asmdef を維持。`Agent/`・`Gateway/`（`Execution/`・`Mailbox/`）・`Scenario/`・`Snapshot/`・`Forensics/`・`Monkey/` に Unity 公式 CLI の薄いラッパ。`TESTIFY_PIPELINE` define で任意依存 |
| `Tests/EditMode/` | asmdef を維持。`Runtime/`・`Editor/` の実装と同じ機能パスへ純ロジックのテストを対応配置 |
| `Tools/ai_client.py` | 標準ライブラリだけの共通クライアント（既定 file、実機向け http） |
| `TestProject/` | テスト実行用の最小 Unity プロジェクト。`Packages/manifest.json` がこのパッケージを `file:../../` で参照する |
| `docs/` | 利用者向け解説。`docs/design/` は設計書（判断の記録） |

フォルダ直下の C# は上限 10。名前空間は据え置き、スクリプトと `.meta` は必ず対で移す。
各フォルダの詳細とテストの対応規則は [構成表](docs/architecture.md#フォルダ構成)、
移動ファイル一覧・件数・確認結果は [実装記録](docs/implementation.md) を参照。

## 守ること（設計の鉄則）

1. **ゲーム本体のライブラリに依存しない。** UniLab / R3 / UniTask / VContainer を参照しない。依存は `UnityEngine`・.NET 標準・`Unity.TextMeshPro`・`Unity.InputSystem` に限る
2. **名前空間は `UniTestify`**（`UniTestify.Editor` / `UniTestify.Pipeline` / `UniTestify.Tests`）。`Debug` という語を名前空間に使わない（`UnityEngine.Debug` と衝突した前例）
3. **毎フレーム処理でアロケーションを増やさない。** `AiMailboxServer.Update` など常駐処理には `GetComponent` / LINQ / `new` を足さない。観測時（`UiSnapshot.Capture`）だけは可。意図は `// perf:` で残す
4. **op は `AiCommandDispatcher` だけに足す。** CLI（`Pipeline/`）とメールボックスは同じディスパッチャを呼ぶ。片方だけに機能を足さない
5. **観測テキストと成果物 JSON の形式は互換を保つ。** 変えるときは設計書 12 と `docs/ops-reference.md` を同時に更新し、既存テストの期待値を意図をもって直す
6. **`#if UNITY_EDITOR || DEVELOPMENT_BUILD` の囲い**を Runtime のファイルに揃える（プレイヤービルドに含めない）

## コーディング規約

`~/.claude/rules/coding-principles.md` と `~/.claude/rules/unity-csharp.md` に従う。要点:
- コメント・`<summary>` は日本語。public / internal の型とメンバーに `<summary>` 必須
- 省略名禁止、ブレース省略禁止、4 段以上のネスト禁止、マジックナンバーは定数化、LINQ クエリ構文禁止
- What コメントではなく Why を書く

## 作業手順

1. `develop` から `feature/…` / `fix/…` / `refactor/…` を切る。`develop` 直コミット禁止、PR 経由（squash）
2. 実装は Codex に委譲してよい（`codex_run.sh` 経由）。**Codex に Unity を起動させない。** コンパイル・テストはこちらで:
   - `TestProject/` を Unity で開いて Test Runner（EditMode）を回す。または利用側プロジェクトへ同期して `recompile` → テスト
3. 利用側（例: karakuri-client の `Assets/UniTestify/`）へは `rsync` で同期し、利用側でも PR を作る（同期先の手順は `docs/architecture.md`）
4. 実機確認は利用側の PlayMode で行い、確認したら **必ず Play を止める**

## 既知の罠

- `ai_agent_begin --goal` の JSON は `AgentGoal` の形（`{"goal":[{"kind":"textVisible","value":"…"}]}` か `{"freePlay":true}`）。キー違いは `JsonUtility` が黙って null にするため、`AgentGoalValidator` で拒否している
- `codex exec` を非 TTY から呼ぶときは stdin を閉じる（`stdin=DEVNULL`）。閉じないと入力待ちで固まる
- `ScreenCapture.CaptureScreenshot` は次フレーム末に非同期保存される。ファイル生成を待ってから読む（`AiCaptureSupport`）
- Unity 6.4 では `FindObjectsOfType` が obsolete。`FindObjectsByType` を使う
