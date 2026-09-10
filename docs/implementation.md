# 実装記録

## 2026-09-10 — §2.5 に合わせた機能フォルダへの移動

### 対象・変更範囲

既存ブランチ `refactor/rename-to-unitestify` で C# 179 ファイルと対応 `.meta` 179 ファイルを移動。
C# のバイト列は維持し、ロジック、コメント、namespace、using、条件付きコンパイル、op・引数・応答フィールドは変更しない。
4 つの asmdef とその `.meta` はアセンブリルートに据え置く。テストの追加・変更はなく、既存 20 ファイルの配置だけを変更する。
`TestProject/`・`Tools/`・既存アセットの移動は行わない。文書は `CLAUDE.md` と `docs/architecture.md` の構成表を更新し、本記録を追加する。

`git mv Runtime/AgentSession.cs Runtime/Agent/` は `.git/index.lock: Operation not permitted` で失敗した。
この環境では `.git` に書き込めないため、作業ツリーのファイル移動を `.cs` → 同名 `.cs.meta` の対で実施した。
指定された `git mv` による移動・ステージングは未達。Git インデックスは変更しておらず、コミット・PR は作成していない。
依頼者側で旧パスの削除と新パスをステージし、内容一致の rename として確認する必要がある。

### 分け方の判断

- `Agent/` は入口の `AgentSession` / `AgentSessionCommands` と観測・検索を浅い場所へ残す。
  `Actions/` は入力行動と事後条件、`Goals/` は目標定義・検証・評価、`Session/` は停止判定と履歴・成果物を置く。
  セッション入口まで下位へ隠さず、変更理由ごとに分けた。
- `Gateway/Execution/` は撮影完了・シナリオ完了・入力後の静止という実行と待機を扱う型をまとめる。
  要求解釈・共通応答は `Gateway/`、ファイル配送は `Gateway/Mailbox/` に置く。
- `Scenario/` にランナー・ステップ解釈・入力・記録連携を残し、期待値判定を `Expectations/`、
  実行結果と証拠パスを `Results/` に分ける。結果モデルは小さな値型だけではないため、12 ファイル例外を使わない。
- `Input/Overlay/Input/` に入力 API の違いと押下状態を閉じ込め、入口・描画・履歴・表示設定を `Input/Overlay/` に残す。
- Runtime の `RunArchive*` は独立したラン成果物の概念なので `Core/` へ混ぜず `RunArchive/` に置く。
  Editor の集約・索引生成も `Editor/RunArchive/` と対応させる。
- Editor のメニューと Pipeline のコマンドも利用機能で分類する。視覚回帰は固有の変更理由を持つので `Editor/VisualRegression/` にまとめる。
- テストは `Tests/EditMode/Runtime/<実装と同じ機能パス>/` に対応する。
  `AgentExpectTest` は事後条件が主対象の `Agent/Actions/`、`AgentExportTest` は成果物書出しの `Agent/Session/`、
  `AgentGoalTest` は `Agent/Goals/` に置く。これらが他機能も呼ぶ既存テストである点は維持する。
  `AiScenarioExecutionTest` はシナリオ本体ではなくゲートウェイ支援のテストなので `Gateway/Execution/` に対応する。
  現在のテストはすべて Runtime 対象。将来の Editor / Pipeline 対象も実装ルートを含む同じパス規則に従う。
- すべて意味のある機能境界で 10 以下に分けられた。数合わせのフォルダや値型限定の 12 ファイル例外は使用しない。
  既存の巨大ファイル内の責務混在はこのランで変更せず、末尾の提案に残す。

### フォルダごとの移動前後・移動ファイル一覧

数値は直下の `.cs` 数。子フォルダ・`.meta`・asmdef・既存アセットは含めない。
新フォルダの移動前の数は 0。ファイル名欄の各 `.cs` は同名 `.cs.meta` も同じ移動先へ移している。
各ファイルの移動元は `Runtime/` / `Editor/` / `Pipeline/` / `Tests/EditMode/` の直下であり、ファイル名は変更しない。
「フォルダ .meta」欄が「追加」の全 52 行では、そのフォルダパスに `.meta` を付けたファイルを新規作成した（例: `Runtime/Agent.meta`）。

| フォルダ | 移動前 | 移動後 | 移動した C#（同名 .meta も対で移動） | フォルダ .meta |
|---|---:|---:|---|---|
| `Runtime/` | 119 | 0 | 下位の機能フォルダへ移動。asmdef は据え置き | 既存維持 |
| `Editor/` | 22 | 0 | 下位の機能フォルダへ移動。asmdef は据え置き | 既存維持 |
| `Pipeline/` | 18 | 0 | 下位の機能フォルダへ移動。asmdef は据え置き | 既存維持 |
| `Tests/EditMode/` | 20 | 0 | 下位の機能フォルダへ移動。asmdef は据え置き | 既存維持 |
| `Runtime/Agent/` | 0 | 7 | `AgentCommandResult.cs`、`AgentFind.cs`、`AgentObservationFormatter.cs`、`AgentOptions.cs`、`AgentSession.cs`、`AgentSessionCommands.cs`、`AgentSessionDriver.cs` | 追加 |
| `Runtime/Agent/Actions/` | 0 | 3 | `AgentActExpectation.cs`、`AgentAction.cs`、`AgentActionExecutor.cs` | 追加 |
| `Runtime/Agent/Goals/` | 0 | 3 | `AgentExpectationEvaluator.cs`、`AgentGoal.cs`、`AgentGoalValidator.cs` | 追加 |
| `Runtime/Agent/Session/` | 0 | 4 | `AgentActionLogEntry.cs`、`AgentSessionArtifacts.cs`、`AgentSessionGuards.cs`、`AgentSessionReport.cs` | 追加 |
| `Runtime/Gateway/` | 0 | 8 | `AiCommandArguments.cs`、`AiCommandContext.cs`、`AiCommandDispatcher.cs`、`AiCommandRequest.cs`、`AiCommandResponse.cs`、`AiConsoleLog.cs`、`AiJsonObject.cs`、`AiSessionState.cs` | 追加 |
| `Runtime/Gateway/Execution/` | 0 | 3 | `AiCaptureSupport.cs`、`AiScenarioExecution.cs`、`AiSettleWait.cs` | 追加 |
| `Runtime/Gateway/Mailbox/` | 0 | 3 | `AiMailboxFiles.cs`、`AiMailboxPrefab.cs`、`AiMailboxServer.cs` | 追加 |
| `Runtime/Scenario/` | 0 | 8 | `ScenarioArtifactWriter.cs`、`ScenarioInputExecutor.cs`、`ScenarioRecordingCoordinator.cs`、`UiScenario.cs`、`UiScenarioJsonPresence.cs`、`UiScenarioRunner.cs`、`UiScenarioStep.cs`、`UiScenarioStepReader.cs` | 追加 |
| `Runtime/Scenario/Expectations/` | 0 | 3 | `ScenarioExpectation.cs`、`ScenarioExpectationEvaluator.cs`、`ScenarioExpectationFailure.cs` | 追加 |
| `Runtime/Scenario/Results/` | 0 | 3 | `ScenarioResult.cs`、`ScenarioStepEvidence.cs`、`ScenarioStepResult.cs` | 追加 |
| `Runtime/Snapshot/` | 0 | 6 | `UiSnapshot.cs`、`UiSnapshotChange.cs`、`UiSnapshotDiff.cs`、`UiSnapshotDocument.cs`、`UiSnapshotElement.cs`、`UiSnapshotGameEntry.cs` | 追加 |
| `Runtime/Input/` | 0 | 9 | `FocusDirection.cs`、`InputInjector.cs`、`InputRecorder.cs`、`InputRecordingEvent.cs`、`InputReplayAnchor.cs`、`InputReplayer.cs`、`PointerButton.cs`、`ReplayManifest.cs`、`ReplayResult.cs` | 追加 |
| `Runtime/Input/Overlay/` | 0 | 8 | `InputOverlay.cs`、`InputOverlayController.cs`、`InputOverlayHistory.cs`、`InputOverlayOptions.cs`、`InputOverlayPointerRenderer.cs`、`InputOverlayRenderer.cs`、`InputOverlayVisualPrimitives.cs`、`OverlayCorner.cs` | 追加 |
| `Runtime/Input/Overlay/Input/` | 0 | 4 | `InputOverlayHeldState.cs`、`InputOverlayInputState.cs`、`InputOverlayInputSystemSource.cs`、`InputOverlayLegacyInputSource.cs` | 追加 |
| `Runtime/Monkey/` | 0 | 8 | `MonkeyChangeResult.cs`、`MonkeyCoverage.cs`、`MonkeyOptions.cs`、`MonkeySummary.cs`、`MonkeyTester.cs`、`MonkeyTraceEntry.cs`、`MonkeyViolation.cs`、`MonkeyViolationList.cs` | 追加 |
| `Runtime/Performance/` | 0 | 5 | `PerformanceRecorder.cs`、`PerformanceRecorderDriver.cs`、`PerformanceReport.cs`、`PerformanceStepReport.cs`、`PerformanceSummaryReport.cs` | 追加 |
| `Runtime/Recording/` | 0 | 5 | `AudioRecorder.cs`、`VideoRecorder.cs`、`VideoRecordingManifest.cs`、`VideoRecordingMarker.cs`、`VideoRecordingResult.cs` | 追加 |
| `Runtime/Forensics/` | 0 | 6 | `ExceptionForensics.cs`、`ExceptionForensicsDriver.cs`、`FileLogSink.cs`、`ForensicsContext.cs`、`ForensicsContextSnapshot.cs`、`ForensicsPendingLog.cs` | 追加 |
| `Runtime/Scene/` | 0 | 4 | `SceneHierarchyDump.cs`、`SceneHierarchyDumper.cs`、`SceneHierarchyNode.cs`、`SceneHierarchyScene.cs` | 追加 |
| `Runtime/Ui/` | 0 | 9 | `UiInputLocator.cs`、`UiLayoutAuditEntry.cs`、`UiLayoutAuditReport.cs`、`UiLayoutAuditor.cs`、`UiObservationScope.cs`、`UiOverlayMarker.cs`、`UiReadiness.cs`、`UiScrollTo.cs`、`UiVisibilityUtility.cs` | 追加 |
| `Runtime/Adapters/` | 0 | 4 | `GameAdapterRegistry.cs`、`IGameBusyProvider.cs`、`IGameCommandHandler.cs`、`IGameStateProvider.cs` | 追加 |
| `Runtime/Core/` | 0 | 3 | `AssemblyInfo.cs`、`DebugOutputPath.cs`、`SerializedFieldWiring.cs` | 追加 |
| `Runtime/RunArchive/` | 0 | 3 | `RunArchiveMeta.cs`、`RunArchivePerformanceSummary.cs`、`RunArchiveVisualRegressionSummary.cs` | 追加 |
| `Editor/Gateway/` | 0 | 0 | 親フォルダ（直下の C# なし） | 追加 |
| `Editor/Gateway/Mailbox/` | 0 | 1 | `AiMailboxMenu.cs` | 追加 |
| `Editor/RunArchive/` | 0 | 8 | `RunArchive.cs`、`RunArchiveIndex.cs`、`RunArchiveIndexEntry.cs`、`RunArchiveMenu.cs`、`RunArchiveScenarioFailure.cs`、`RunArchiveScenarioResult.cs`、`RunArchiveScenarioStepResult.cs`、`RunArchiveStepEvidence.cs` | 追加 |
| `Editor/VisualRegression/` | 0 | 9 | `VisualRegression.cs`、`VisualRegressionIgnoreParser.cs`、`VisualRegressionIgnoreRect.cs`、`VisualRegressionIgnoreRegion.cs`、`VisualRegressionIgnoreSettings.cs`、`VisualRegressionMenu.cs`、`VisualRegressionOptions.cs`、`VisualRegressionReport.cs`、`VisualRegressionResult.cs` | 追加 |
| `Editor/Scenario/` | 0 | 1 | `UiScenarioRunnerMenu.cs` | 追加 |
| `Editor/Scene/` | 0 | 1 | `SceneHierarchyDumperMenu.cs` | 追加 |
| `Editor/Snapshot/` | 0 | 1 | `UiSnapshotMenu.cs` | 追加 |
| `Editor/Ui/` | 0 | 1 | `UiLayoutAuditorMenu.cs` | 追加 |
| `Pipeline/Agent/` | 0 | 6 | `AiAgentActCliCommand.cs`、`AiAgentBeginCliCommand.cs`、`AiAgentEndCliCommand.cs`、`AiAgentExportCliCommand.cs`、`AiAgentGoalCliCommand.cs`、`AiAgentObserveCliCommand.cs` | 追加 |
| `Pipeline/Gateway/` | 0 | 3 | `AiCliArguments.cs`、`AiCliCommandSupport.cs`、`AiOpsCliCommand.cs` | 追加 |
| `Pipeline/Gateway/Execution/` | 0 | 1 | `AiCaptureCliCommand.cs` | 追加 |
| `Pipeline/Gateway/Mailbox/` | 0 | 1 | `AiMailboxCliCommand.cs` | 追加 |
| `Pipeline/Scenario/` | 0 | 3 | `AiScenarioRunCliCommand.cs`、`AiScenarioStatusCliCommand.cs`、`AiScenarioStatusResult.cs` | 追加 |
| `Pipeline/Snapshot/` | 0 | 1 | `AiSnapshotCliCommand.cs` | 追加 |
| `Pipeline/Forensics/` | 0 | 2 | `AiForensicsLatestCliCommand.cs`、`AiForensicsLatestResult.cs` | 追加 |
| `Pipeline/Monkey/` | 0 | 1 | `AiMonkeyCliCommand.cs` | 追加 |
| `Tests/EditMode/Runtime/` | 0 | 0 | 親フォルダ（直下の C# なし） | 追加 |
| `Tests/EditMode/Runtime/Input/` | 0 | 0 | 親フォルダ（直下の C# なし） | 追加 |
| `Tests/EditMode/Runtime/Input/Overlay/` | 0 | 0 | 親フォルダ（直下の C# なし） | 追加 |
| `Tests/EditMode/Runtime/Agent/` | 0 | 2 | `AgentFindTest.cs`、`AgentObservationFormatterTest.cs` | 追加 |
| `Tests/EditMode/Runtime/Agent/Actions/` | 0 | 2 | `AgentActionExecutorTest.cs`、`AgentExpectTest.cs` | 追加 |
| `Tests/EditMode/Runtime/Agent/Goals/` | 0 | 1 | `AgentGoalTest.cs` | 追加 |
| `Tests/EditMode/Runtime/Agent/Session/` | 0 | 2 | `AgentExportTest.cs`、`AgentSessionGuardsTest.cs` | 追加 |
| `Tests/EditMode/Runtime/Gateway/` | 0 | 3 | `AiCommandArgumentsTest.cs`、`AiCommandDispatcherTest.cs`、`AiConsoleLogTest.cs` | 追加 |
| `Tests/EditMode/Runtime/Gateway/Execution/` | 0 | 2 | `AiCaptureSupportTest.cs`、`AiScenarioExecutionTest.cs` | 追加 |
| `Tests/EditMode/Runtime/Gateway/Mailbox/` | 0 | 2 | `AiMailboxProtocolTest.cs`、`AiMailboxServerPollingTest.cs` | 追加 |
| `Tests/EditMode/Runtime/Snapshot/` | 0 | 2 | `UiSnapshotCacheTest.cs`、`UiSnapshotTest.cs` | 追加 |
| `Tests/EditMode/Runtime/Input/Overlay/Input/` | 0 | 1 | `InputOverlayInputStateTest.cs` | 追加 |
| `Tests/EditMode/Runtime/Ui/` | 0 | 3 | `UiObservationScopeTest.cs`、`UiScrollToTest.cs`、`UiVisibilityUtilityTest.cs` | 追加 |

`Runtime/Prefabs/`・`Runtime/Resources/` は各 0 → 0（C#）で、アセットと既存 `.meta` を維持する。
合計は Runtime 119 → 119、Editor 22 → 22、Pipeline 18 → 18、EditMode テスト 20 → 20、全体 179 → 179。
移動先の最大は 9 ファイルで、全フォルダが上限 10 を満たす。

### 静的確認結果

- C# 179 ファイルと対応 `.cs.meta` 179 ファイルについて、移動前後の SHA-256 が全件一致。既存 GUID、namespace、using、条件付きコンパイルを含む内容変更は 0 件。
- `.cs` / `.cs.meta` は Runtime 119 / 119、Editor 22 / 22、Pipeline 18 / 18、EditMode 20 / 20。件数だけでなく同名パスの集合も一致し、旧パスへの置き去りは 0 件。
- 各フォルダ直下の最大は Runtime 9、Editor 9、Pipeline 6、EditMode 3。4 アセンブリルートの直下 C# はすべて 0。
- 新規フォルダ `.meta` は 52 件。対象アセンブリ配下のすべてのフォルダに `.meta` が存在し、孤立した `.meta` は 0 件。Runtime / Editor / Pipeline / Tests 配下の GUID 240 件に重複なし。
- 4 asmdef とその `.meta` は元のパス・内容を維持。既存 Prefab・Resources アセットを含め、文書以外の既存追跡ファイル 409 件が移動先または元のパスで移動前の SHA-256 と一致。
- テスト 20 ファイルすべてについて、主対象の実装フォルダと `Tests/EditMode/` 以下の相対パスの一致を確認。
- `git status` の変更は旧 358 パスの削除、新 358 パス・フォルダ `.meta` 52 件・本記録の未追跡、構成表 2 ファイルの変更だけ。Git インデックスの差分は 0 件。
- `git diff --check` に指摘なし。これはファイル配置・内容一致の静的確認であり、Unity のコンパイルやテスト実行ではない。

### 未実行の確認事項

- Unity の起動・インポート、コンパイル、`dotnet build`、EditMode / PlayMode テストは未実行。依頼者が行う。
- メールボックス Prefab・Resources アセットの Unity 上での参照解決は実機確認が必要。
- Git のステージングと rename 差分確認は `.git` の書き込み制限により未実行。
- 追加した op / 引数 / 応答フィールド: なし。追加したテスト名: なし。

## 提案

このランでは実装しない。入口の既存 API を維持したうえで、責務の抽出を別ランで扱う。

1. `Editor/RunArchive/RunArchive.cs`（993 行）は、成果物の選択・コピー、シナリオ／録画／視覚回帰の参照パス書換え、
   ラン概要構築、索引再構築を別クラスへ分けるべき。理由はファイル配送・形式変換・一覧管理で変更理由が異なるため。
2. `Runtime/Snapshot/UiSnapshot.cs`（836 行）は、UI 要素の収集、スナップショット差分計算、compact text 整形・保存を分けるべき。
   理由は観測対象の追加と差分・テキスト形式の変更を独立して扱えるため。フレームキャッシュは入口に残す。
3. `Runtime/Recording/VideoRecorder.cs`（790 行）は、キャプチャ範囲解決、GPU readback とバッファ所有、
   エンコード・書込タスク、manifest・フレーム一覧・ffmpeg コマンド生成を分けるべき。
   理由は GPU リソースと非同期書込の寿命、および出力形式の責務をそれぞれ明示できるため。停止時の待機・解放順序は維持する。
