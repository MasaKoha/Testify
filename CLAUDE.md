# CLAUDE.md — Testify

AI エージェント（Claude Code / Codex）が Unity ゲームを動かして検証するためのツール群。旧 `UniLab.AI`。

## このリポジトリの構成

| パス | 役割 |
|---|---|
| `Runtime/` | 観測（`UiSnapshot`）・操作（`AgentSession` 群、`InputInjector`）・ゲートウェイ（`AiCommandDispatcher`、`AiMailboxServer`）・シナリオ（`UiScenarioRunner`）・録画・診断 |
| `Editor/` | メニュー（スナップショット、監査、階層ダンプ、視覚回帰、RunArchive、メールボックス起動） |
| `Pipeline/` | Unity 公式 CLI（`com.unity.pipeline`）向けの `[CliCommand]` 薄ラッパ。`TESTIFY_PIPELINE` define で任意依存 |
| `Tests/EditMode/` | 純ロジックの EditMode テスト（PlayMode が要るものは書かない。利用側で実機確認する） |
| `Tools/ai_client.py` | 標準ライブラリだけの共通クライアント（メールボックス経由） |
| `TestProject/` | テスト実行用の最小 Unity プロジェクト。`Packages/manifest.json` がこのパッケージを `file:../../` で参照する |
| `docs/` | 利用者向け解説。`docs/design/` は設計書（判断の記録） |

## 守ること（設計の鉄則）

1. **ゲーム本体のライブラリに依存しない。** UniLab / R3 / UniTask / VContainer を参照しない。依存は `UnityEngine`・.NET 標準・`Unity.TextMeshPro`・`Unity.InputSystem` に限る
2. **名前空間は `Testify`**（`Testify.Editor` / `Testify.Pipeline` / `Testify.Tests`）。`Debug` という語を名前空間に使わない（`UnityEngine.Debug` と衝突した前例）
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
3. 利用側（例: karakuri-client の `Assets/Testify/`）へは `rsync` で同期し、利用側でも PR を作る（同期先の手順は `docs/architecture.md`）
4. 実機確認は利用側の PlayMode で行い、確認したら **必ず Play を止める**

## 既知の罠

- `ai_agent_begin --goal` の JSON は `AgentGoal` の形（`{"goal":[{"kind":"textVisible","value":"…"}]}` か `{"freePlay":true}`）。キー違いは `JsonUtility` が黙って null にするため、`AgentGoalValidator` で拒否している
- `codex exec` を非 TTY から呼ぶときは stdin を閉じる（`stdin=DEVNULL`）。閉じないと入力待ちで固まる
- `ScreenCapture.CaptureScreenshot` は次フレーム末に非同期保存される。ファイル生成を待ってから読む（`AiCaptureSupport`）
- Unity 6.4 では `FindObjectsOfType` が obsolete。`FindObjectsByType` を使う
