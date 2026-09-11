# AGENTS.md — UniTestify（Codex 向け）

このリポジトリは、AI エージェントが Unity ゲームを動かして検証するためのツール群。Codex はここで **実装だけ**を担当する。設計・レビュー・コンパイル・テスト・実機確認は依頼者（Claude Code）が行う。

## 絶対に守ること

- **Unity を起動しない。`dotnet build` もしない。** コンパイルとテストは依頼者が `TestProject/` または利用側プロジェクトで行う
- ゲーム本体で使う類のライブラリ（Rx 実装・非同期ライブラリ・DI コンテナ）に依存しない。依存は `UnityEngine`・.NET 標準・`Unity.TextMeshPro`・`Unity.InputSystem` のみ
- 名前空間は `UniTestify`。`Debug` という語を名前空間に使わない
- op を足すときは `AiCommandDispatcher` に足す（CLI とメールボックスは同じディスパッチャを呼ぶ）。片方だけに機能を足さない
- 観測テキスト・成果物 JSON の形式を変えるときは `docs/ops-reference.md` と設計書 12 を同時に更新し、既存テストの期待値を意図をもって直す
- 毎フレーム処理（`AiMailboxServer.Update`、オーバーレイ描画）にアロケーション・`GetComponent`・LINQ を足さない。観測時（`UiSnapshot.Capture`）だけ可。意図は `// perf:` で残す
- Runtime のファイルは `#if UNITY_EDITOR || DEVELOPMENT_BUILD` で囲う。`Pipeline/` は `#if TESTIFY_PIPELINE`
- `TestProject/` の `Library/` 等の生成物や `DebugOutput/` をコミットしない

## コーディング規約

`~/.codex/skills/unity-csharp-standards/SKILL.md` に従う。要点: 日本語コメント、public / internal に `<summary>` 必須、省略名禁止、ブレース省略禁止、4 段以上のネスト禁止、マジックナンバー定数化、LINQ クエリ構文禁止、What コメント禁止（Why を書く）。

## テスト

- **テストを機械的に追加しない。** 壊れたときに実害がある振る舞い（分岐・境界値・変換・状態遷移・
  エラー処理・回帰防止）だけを守る。getter / setter、薄い委譲、定数の対応、Unity API の素通しは書かない
- 追加する前に「どんなバグを検出できるか」「リファクタしても有効か」「保守コストに見合うか」を確認する。
  **答えが出ないなら書かない。**書かないと判断したら完了報告に「追加テストは不要。理由: ○○」と記す
- 新規ファイルを増やす前に**既存テストの拡張**を検討する。同じ assert を重複させない
- `Tests/EditMode/` は PlayMode 不要の純ロジックだけ書く（`InternalsVisibleTo("UniTestify.Tests.EditMode")` 済み）
- PlayMode が要る確認は依頼者が実機で行う。テストを書けない挙動は完了報告に「実機確認が必要」と明記する

## 完了報告に書くこと

- 変更・追加ファイルの一覧（`.meta` の追加も）
- 追加した op / 引数 / 応答フィールド
- 追加したテスト名
- 実行していないこと（Unity・ビルド・テスト）を明記

## Git

- コミットメッセージは `<type>: <日本語の説明>` の 1 行形式。`develop` 直コミット禁止（ブランチが作れない環境ならスキップして続行してよい）
