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

## 2026-09-10 巨大ファイル三件の責務分割

対象は `RunArchive.cs`・`UiSnapshot.cs`・`VideoRecorder.cs`。入口の名前・配置・公開 API を維持し、
独立した通常クラスへ委譲した。`partial`、新しい外部依存、op・引数・応答フィールドの追加はない。
既存ファイルの変更はこの三件と本記録だけ。既存の呼び出し側・テスト・asmdef・シリアライズモデル・`.meta` は変更していない。
作業開始時の `.git/HEAD` は `refactor/split-large-files`。ブランチ変更・ステージング・コミットは行っていない。

### 変更・追加ファイルと分割前後の行数

行数は空行・コメント・条件付きコンパイルを含む。分割先はすべて 300 行以下で、超過の例外はない。
以下の「追加」22 件と後掲のテスト四件には、それぞれ同名の `.cs.meta` を追加した。

| ファイル | 分割前 | 分割後 | 責務 |
|---|---:|---:|---|
| `Editor/RunArchive/RunArchive.cs` | 993 | 116 | 公開入口・集約順序 |
| `Editor/RunArchive/Export/RunArchiveFileStorage.cs` | 追加 | 124 | 時間範囲による選択・コピー・出力先とパス規則 |
| `Editor/RunArchive/Export/RunArchiveScenarioArtifacts.cs` | 追加 | 140 | キャプチャ・監査・観測の配送と証拠対応表 |
| `Editor/RunArchive/Export/RunArchiveDirectoryArtifacts.cs` | 追加 | 160 | 録画・例外記録・探索ディレクトリの選択と配送 |
| `Editor/RunArchive/Export/RunArchiveReportArtifacts.cs` | 追加 | 108 | 視覚回帰・性能・ログの選択と配送 |
| `Editor/RunArchive/References/RunArchiveScenarioResultWriter.cs` | 追加 | 111 | シナリオ結果の読込・参照書換え・保存 |
| `Editor/RunArchive/References/RunArchiveReferenceRewriter.cs` | 追加 | 89 | 録画 manifest と視覚回帰レポートの参照書換え |
| `Editor/RunArchive/Summary/RunArchiveSummaryBuilder.cs` | 追加 | 161 | 件数集計・ラン概要・コミット情報 |
| `Editor/RunArchive/Summary/RunArchiveTiming.cs` | 追加 | 81 | 結果の時刻と所要時間による期間解決 |
| `Editor/RunArchive/Index/RunArchiveIndexWriter.cs` | 追加 | 65 | 保存済みランから索引を再構築 |
| `Runtime/Snapshot/UiSnapshot.cs` | 836 | 80 | 公開入口・フレームキャッシュとリセット |
| `Runtime/Snapshot/Collection/UiSnapshotCollector.cs` | 追加 | 33 | シーン・画面・要素・ゲーム状態の観測文書化 |
| `Runtime/Snapshot/Collection/UiSnapshotElementCollector.cs` | 追加 | 97 | UI 探索と画面順ソート |
| `Runtime/Snapshot/Collection/UiSnapshotElementFactory.cs` | 追加 | 190 | 操作対象・テキストの意味と可視性の変換 |
| `Runtime/Snapshot/Collection/UiSnapshotGameStateCollector.cs` | 追加 | 73 | ゲーム状態の取得・キー順・値の文字列化 |
| `Runtime/Snapshot/Comparison/UiSnapshotComparer.cs` | 追加 | 143 | 要素・フォーカス・シーンの差分 |
| `Runtime/Snapshot/Output/UiSnapshotCompactTextFormatter.cs` | 追加 | 285 | compact text の表記・省略・整列 |
| `Runtime/Snapshot/Output/UiSnapshotStorage.cs` | 追加 | 55 | JSON 保存とファイル命名 |
| `Runtime/Recording/VideoRecorder.cs` | 790 | 247 | 公開入口・時間進行・停止と破棄の順序 |
| `Runtime/Recording/Capture/VideoCaptureGeometry.cs` | 追加 | 137 | 開始時の画面寸法・切り抜き・変更警告 |
| `Runtime/Recording/Capture/VideoCaptureBuffers.cs` | 追加 | 115 | GPU readback・テクスチャと永続バッファの所有 |
| `Runtime/Recording/Encoding/VideoFrameWriter.cs` | 追加 | 146 | エンコード・非同期書込・一時配列の破棄とバッファ返却 |
| `Runtime/Recording/Output/VideoRecordingArtifacts.cs` | 追加 | 198 | フレーム履歴・失敗記録・目印・manifest と一覧・結果 |
| `Runtime/Recording/Output/VideoRecordingCommand.cs` | 追加 | 29 | ffmpeg コマンドの表記 |
| `Runtime/Recording/Session/VideoRecordingEnvironment.cs` | 追加 | 143 | 音声・描画レート・入力可視化の変更と復元 |

### 所有権と互換性の確認方法

- 変更前の三ファイルを退避し、公開メソッド宣言・オーバーロード・既定引数・プロパティ・公開定数をソース比較した。
  `UiSnapshot.Capture(int)` とキャッシュリセット属性も維持。名前空間は `UniTestify` / `UniTestify.Editor` のまま。
- `UiSnapshot` の既存 33 メソッドは、追加した委譲先の型名を除き本文が一致する。
  中間リスト・配列・LINQ・収集時のインスタンス生成を分割目的で追加していない。
  キャッシュ判定・フレーム番号・非 Play 時の再収集は入口に残した。
- `RunArchive` の既存 36 メソッド中 34 件は同じ基準で本文が一致する。
  `CopyRecordings` / `CopyForensics` は、期間内ディレクトリの列挙を private メソッドへ抽出して深いネストを解消した。
  明示パスの優先、並べ替え、`_current` 除外、期間判定、上書きの順序は維持。
  シナリオ・録画・視覚回帰の書換え本文、相対／絶対パスの扱い、区切り文字の正規化は変更していない。
- 三ファイルの文字列・文字リテラルの綴りを分割先へ照合した。補間ログのフィールド参照を所有先へ読み替えたうえで、
  `UiSnapshot` 50 種、`RunArchive` 41 種、`VideoRecorder` 21 種の既存リテラルを確認した。
  JSON モデルとフィールド順、`JsonUtility.ToJson(..., true)`、ファイル名・パスの組立て、ffmpeg の引用符・引数順・小数書式を維持。
- `VideoRecorder.StopRecording` は「ループ停止 → 実測時間確定 → 音声停止 → 描画設定復元 → GPU 待機 → 書込待機 →
  テクスチャ・永続バッファ解放 → オーバーレイ復元 → 成果物保存 → GameObject 破棄」を維持した。
  `OnDestroy` 側の「描画設定復元 → 音声停止」の順序も変更していない。最終解放済みフラグは入口に残る。
- 永続バッファとテクスチャは `VideoCaptureBuffers` が所有し、GPU と書込の完了を待った入口だけが最終解放を指示する。
  `VideoFrameWriter` は借用したバッファを使い、`finally` で JPEG → 上下反転 → 切り抜きの一時配列を破棄してから返却する。
  readback 失敗時は即返却し、書込失敗時は失敗フレームを記録する。ロックと待機の位置、バッファ数は維持した。
  分割用オブジェクトの生成は録画コンポーネント生成時だけで、フレームごとの中間コレクションを追加していない。
- 変更・追加した C# 29 件について、文字列境界・エスケープ・括弧対応・内部フィールドと委譲先メンバーの存在、
  public / internal の日本語 summary、Runtime の条件付きコンパイルを静的確認した。
  using は移動元の宣言を出発点とし、リポジトリ内の型定義・名前空間を検索して不要分だけ除去した。
  テストの JSON 期待値三件は、文字列を復元して JSON として構文を確認した。
- `GetComponent` 系と `AddComponent` の検索結果は、既存の UI 観測と録画コンポーネント生成だけ。
  観測基盤の許可範囲であり、毎フレーム処理への追加はない。
- 作業前に記録した SHA-256 と比較し、対象外の既存ファイル、既存 GUID、呼び出し側、既存テスト、asmdef の内容一致を確認した。
  これらはソースの静的確認であり、コンパイル成功や実行時の出力一致を保証する実行確認ではない。

### 追加テスト

既存テストの期待値は変更していない。追加は以下の四ファイル、9 テストメソッド（TestCase 展開で 13 ケース）。
いずれも PlayMode や GPU readback を必要としない。テストコードを書いたが実行していない。

| 追加ファイル | 行数 | 追加したテスト名 |
|---|---:|---|
| `Tests/EditMode/Runtime/Snapshot/UiSnapshotCompatibilityTest.cs` | 139 | `CompactTextPreservesExactStatusAndGameText`、`CompactTextPreservesExactCollapsedSequence`、`ComparePreservesDuplicatePathAndFieldOrdering`、`SavePreservesNamedJsonAndEscapedLabels` |
| `Tests/EditMode/Runtime/Recording/Capture/VideoCaptureGeometryTest.cs` | 29 | `ClampPixelRectPreservesRoundingAndBounds` |
| `Tests/EditMode/Runtime/Recording/Output/VideoRecordingCommandTest.cs` | 51 | `CreateFfmpegCommandPreservesExactArguments` |
| `Tests/EditMode/Runtime/Recording/Output/VideoRecordingArtifactsTest.cs` | 118 | `FrameListPreservesSurvivingFramesAndDurations`、`FrameListIsAbsentWithoutSurvivingFrames`、`FrameListPreservesMinimumDuration` |

### フォルダと .meta

追加した `.meta` は C# 用 26 件とフォルダ用 14 件、計 40 件。同名アセットとの対応と既存を含めた GUID の重複なしを確認した。
フォルダ用の追加一覧は次のとおり。

- `Editor/RunArchive/Export.meta`、`Editor/RunArchive/References.meta`、`Editor/RunArchive/Summary.meta`、`Editor/RunArchive/Index.meta`
- `Runtime/Snapshot/Collection.meta`、`Runtime/Snapshot/Comparison.meta`、`Runtime/Snapshot/Output.meta`
- `Runtime/Recording/Capture.meta`、`Runtime/Recording/Encoding.meta`、`Runtime/Recording/Output.meta`、`Runtime/Recording/Session.meta`
- `Tests/EditMode/Runtime/Recording.meta`、`Tests/EditMode/Runtime/Recording/Capture.meta`、`Tests/EditMode/Runtime/Recording/Output.meta`

入口フォルダの C# 数は RunArchive 8、Snapshot 6、Recording 5 のまま。
新規実装フォルダの最大は 4、追加先テストフォルダの最大は 3 で、すべて上限 10 以下。

### 未実行の確認事項

- Unity の起動・インポート、コンパイル、`dotnet build`、既存／追加 EditMode テスト、PlayMode テストは未実行。依頼者が行う。
- 録画の正常停止・連続停止・録画中の GameObject 破棄、readback／書込失敗、音声あり／なし、画面サイズ変更時の動作は実機確認が必要。
- 同じ入力成果物からのラン集約について、コピー先・書換え後 JSON・索引の実行比較は未実行。
  実機確認では生成時刻・ラン連番・出力ルートを揃えて比較する。
- フレームキャッシュと観測時アロケーションの実測は未実行。
- 追加した op / 引数 / 応答フィールド: なし。

## 提案

このランでは実装していない。残る四ファイルは次の責務で分けるべき。

1. `InputOverlayRenderer`（731 行）は、ゲームパッド描画、キーボード描画、表示デバイス切替方針、ルートの組立てを分けるべき。
   理由は機器別の図形変更と入力履歴に基づく切替判断で変更理由が異なるため。既存のポインタ描画・入力状態の分離を維持する。
2. `UiInputLocator`（601 行）は、パス／ラベル検索、ラベル正規化と候補優先順位、可視性・遮蔽判定、操作・リプレイアンカー判定を分けるべき。
   理由は対象特定の規則と操作成立条件を独立して確認できるため。
3. `InputInjector`（577 行）は、仮想デバイスの所有・解放、ゲームパッド／キーボード注入、マウス注入、タッチジェスチャーを分けるべき。
   理由はデバイス寿命と各入力系列の状態遷移で変更理由が異なるため。押下解除と Dispose の順序は入口で統括する。
4. `UiScenarioRunner`（513 行）は、ステップ進行、準備・シーン待機、結果／失敗の蓄積、中断・終了時の後処理を分けるべき。
   理由は実行順序と待機条件と証拠・結果の確定を独立して読めるため。既存の実行セッション境界を維持する。

## 2026-09-11 — T1 撮影対象指定・T7 ターゲット指定と待機の明文化

### 変更内容

指定済みの `feature/capture-view-and-docs` を前提に作業し、ブランチ変更・ステージング・コミットは行っていない。
T1 は `capture` / `agent.observe` に `view`（空 / `game` / `simulator`）を追加し、不正値を
`ArgumentException` で拒否する。ディスパッチャの既存の例外処理により、外部には `ok:false` / `error` を返す。
応答に今回適用した `view` を追加する。新規 op はない。

Editor が `FocusHandler` を登録し、全ロード済みアセンブリから対象ウィンドウの型を探す。
メールボックスの要求処理はフォーカス → 解像度の安定待ち → 従来処理の順とし、
`Screen.width/height` が 2 フレーム連続で変化しなくなるまで、最大 5 フレーム待つ。
待機は観測前に限定し、観測と撮影要求の間では yield しない。
Handler 未登録・対象の型やウィンドウが利用不能・バッチモードでは従来処理を続け、
`view:""` と `message` の補足を返す。

同期 CLI の `--view` 適用成功時はフォーカスだけを行う。フレーム反映後、次の呼び出しで
`--view` を省略して撮影・観測する。適用不能時または未指定時は従来処理を維持する。
`width` / `height` は既存どおり非同期 PNG 読込時に実寸を返し、同期経路では 0。

T7 は文書のみ。`click` / `tap` がターゲット名を受け RectTransform 中心へ送ること、
`OnPointerClick` だけで反応する UI で使うこと、アンカーが成立まで待つこと、
ランナーが対象の準備を自動で待つため外部の存在確認ポーリングが不要であることを記載した。

### 仕様表と現物の差異・対応

| 箇所 | 現物 | 対応 |
|---|---|---|
| `AiMailboxServer` の要求処理 | 実行・フレーム待機は `AiCommandDispatcher.ExecuteAsync` へ委譲済み | 既存の委譲先にフォーカスと安定待ちを追加した。サーバー本体と毎フレームの `Update` は変更していない |
| Editor から internal Handler を登録 | Runtime の `InternalsVisibleTo` はテストアセンブリだけ | `Runtime/Core/AssemblyInfo.cs` に `UniTestify.Editor` を追加した |
| CLI の引数転送 | `AiCommandArguments` とは別に `AiCliArguments` で JSON を生成 | 転送モデルにも `view` を追加した |
| `AiAgentObserveCliCommand` | 公開引数は `diffOnly` のみ。`capture` / `directory` / `scope` は未公開 | 指定どおり `--view` だけを追加。CLI の撮影は次回の `ai_capture`、同一要求での観測＋撮影はメールボックスと文書に明記した |
| 構成表の既存件数 | 直前の責務分割で追加されたフォルダ行が未掲載。テスト実数は 20 ではなく 24 | T1 の 3→4・0→1・テスト 2→3 に加え、既存フォルダの掲載漏れとテスト件数を実数へ修正した。今回追加後のテストは 25 ファイル |

### 追加・変更ファイル一覧

| ファイル | 種別 | 内容 |
|---|---|---|
| `Runtime/Gateway/Execution/AiPlayModeViewFocus.cs` | 追加 | Handler、view 検証、フォーカス仲介、解像度の安定待ち |
| `Runtime/Gateway/Execution/AiPlayModeViewFocus.cs.meta` | 追加 | 新規 C# の GUID |
| `Editor/Gateway/PlayModeViewFocus.cs` | 追加 | Editor 起動時の登録、型検索とウィンドウのフォーカス |
| `Editor/Gateway/PlayModeViewFocus.cs.meta` | 追加 | 新規 C# の GUID |
| `Tests/EditMode/Runtime/Gateway/Execution/AiPlayModeViewFocusTest.cs` | 追加 | Handler と同期フォーカスの契約テスト |
| `Tests/EditMode/Runtime/Gateway/Execution/AiPlayModeViewFocusTest.cs.meta` | 追加 | 新規テストの GUID |
| `Runtime/Core/AssemblyInfo.cs` | 変更 | Editor アセンブリから internal 登録口へのアクセス |
| `Runtime/Gateway/AiCommandArguments.cs` | 変更 | 入力 `view` |
| `Runtime/Gateway/AiCommandContext.cs` | 変更 | 対象 op の view 検証と internal メンバーの summary |
| `Runtime/Gateway/AiCommandDispatcher.cs` | 変更 | 同期のフォーカスのみ応答、非同期の観測前待機、適用結果と補足 |
| `Runtime/Gateway/AiCommandResponse.cs` | 変更 | 応答 `view` |
| `Pipeline/Gateway/AiCliArguments.cs` | 変更 | CLI から view の転送 |
| `Pipeline/Gateway/Execution/AiCaptureCliCommand.cs` | 変更 | `ai_capture --view` |
| `Pipeline/Agent/AiAgentObserveCliCommand.cs` | 変更 | `ai_agent_observe --view` |
| `Tests/EditMode/Runtime/Gateway/AiCommandArgumentsTest.cs` | 変更 | view の省略値と不正値 |
| `Tests/EditMode/Runtime/Gateway/AiCommandDispatcherTest.cs` | 変更 | 同期・非同期入口の不正 view 応答 |
| `Tests/EditMode/Runtime/Gateway/Mailbox/AiMailboxProtocolTest.cs` | 変更 | 既存の JSON 往復テストで view を検証 |
| `docs/ops-reference.md` | 変更 | 引数・応答・CLI 運用と click / tap の対象指定 |
| `docs/scenario-guide.md` | 変更 | click / tap の例、アンカーと自動準備待ちの指針 |
| `docs/architecture.md` | 変更 | フォーカス経路と実ファイル数の構成表 |
| `docs/design/design-unilab-ai-12-ai-gateway.md` | 変更 | view の引数・応答契約と同期／非同期の処理順 |
| `docs/implementation.md` | 変更 | 本記録 |

### 追加したテスト名

追加は 9 メソッド（TestCase 展開後 22 ケース）。実行していない。

| テストクラス | 追加したメソッド |
|---|---|
| `AiPlayModeViewFocusTest` | `MissingHandlerReturnsFalse`、`EmptyViewDoesNotInvokeHandler`、`RegisteredHandlerReceivesViewAndReturnsResult`、`InvalidViewThrowsArgumentException`、`SynchronousCaptureWithViewOnlyFocuses` |
| `AiCommandArgumentsTest` | `ViewDefaultsToEmpty`、`InvalidViewThrowsArgumentException` |
| `AiCommandDispatcherTest` | `InvalidViewReturnsFailure`、`AsyncInvalidViewReturnsFailure` |

既存の `AiMailboxProtocolTest.RequestAndResponseRoundTrip` は `view:"simulator"` の往復を期待値へ追加した。
旧 `AgentCommandResult` による読み取り確認は維持し、追加フィールドが既存本文・成果物パスを壊さない契約を残した。
Handler を差し替えるテストは元の登録を退避・復元し、`Parallelizable(ParallelScope.None)` で並列実行しない。
ローカルの Unity 同梱 NUnit は `NonParallelizable` が未収録のため、収録されている属性・列挙値に合わせた。

### 静的確認結果

- 変更・追加 C# のライフサイクル／コンポーネント API 検索に追加対象のヒットなし。ゲーム用ライブラリへの依存追加なし。
- 新規 `.cs` と `.cs.meta` は 3 組。既存 `.meta` の変更なし、既存を含む GUID の重複なし。
- Runtime / Pipeline の条件付きコンパイル、内部型の namespace とアセンブリ参照、EditorWindow 等のローカル API 定義を確認した。
- 構成表の全掲載行の C# 数が実数と一致し、C# を持つフォルダの掲載漏れなし。
  全体は Runtime 133、Editor 32、Pipeline 18、EditMode 25。各フォルダは上限 10 以下。
- T7 の対象となる入力・ターゲット解決・シナリオ実行コードは変更していない。

### 未実行の確認事項・依頼者の受け入れ確認

Unity の起動・インポート・コンパイル、`dotnet build`、EditMode / PlayMode テストは実行していない。
以下は依頼者による実機確認が必要。

1. Game View を任意のアスペクトに設定し、前面状態を変えて `capture {"name":"a","view":"simulator"}` を送る。
   PNG と `width` / `height` が Device Simulator の解像度になり、`view:"simulator"` を返すこと。
2. `view:"game"` で Game View の解像度へ戻ること。`view` 未指定・空の場合は従来の撮影対象と挙動を維持すること。
3. 開始済みのセッションで `agent.observe {"capture":"b","view":"simulator"}` を送り、
   安定待ち後に観測と撮影要求が同一フレームで発行され、本文・PNG・view が揃うこと。
4. フォーカス後の寸法変更が複数フレーム続く状況でも、連続安定または最大 5 フレームまで観測を始めないこと。
5. 対象が利用できない環境・Handler 未登録時に従来の撮影を試み、`view:""` と理由付き `message` を返すこと。
   撮影自体の完了・タイムアウトは既存の撮影機能の条件に従う。
6. Pipeline 有効環境で `ai_capture --name a --view simulator` はフォーカスだけを行い、
   フレーム反映後の `ai_capture --name a` で PNG が生成されること。`ai_agent_observe --view` も同じ二段階で観測すること。
7. 追加 EditMode テストと既存の JSON 往復テストを実行すること。

### 提案（このランでは未実装）

1. 同名要素で実際に曖昧な選択が必要になった時点で `path#index` を別タスクにすべき。
   理由は `FindByPathSegment` が最初の一致を返すため。今回の T7 では記法も検索実装も追加しない。

## 2026-09-11 — T2 Editor 操作メールボックス

### 変更内容

指定済みの `feature/editor-control-mailbox` を前提に実装し、Git 操作は行っていない。
T2 のみを追加した。`EditorControlMailbox` は `[InitializeOnLoad]` で起動し、
Play 停止中も `DebugOutput/editor-mailbox/` の要求を `EditorApplication.update` で処理する。
既存の `AiMailboxServer` / `AiCommandDispatcher` と Runtime の要求・応答形式は変更していない。

追加 op は `status` / `play` / `stop` / `pause` / `unpause` / `focus_game_view` / `simulator_view` / `menu`。
要求フィールドは `op` / `arg`（menu のメニューパスを渡す普通の文字列）。応答フィールドは `ok` / `message` のみ。
`status` の message に `isPlaying` / `isPaused` / `isCompiling` / `focusedWindow` を格納する。
bool は `True` / `False`、focusedWindow は前面 EditorWindow の完全型名、ウィンドウがなければ空文字列。

`play` / `stop` / `pause` は要求受理を返し、応答の原子的公開と要求削除を済ませてから次の Editor 更新で適用する。
反映の確認は呼び出し側が `status` で行う。コンパイル中は `status` 以外を拒否する。
`unpause` は `EditorApplication.isPaused = false` を実行して解除の応答を返す。
メニュー実行とフォーカスは成否を返し、フォーカス実装は T1 の `PlayModeViewFocus.TryFocus` を直接共用する。
要求 JSON の検証・復元と拒否判定は `EditorControlRequest`、応答生成・JSON 化は `EditorControlResponse` に分離した。

`AiMailboxFiles` の名前順走査・応答パス・原子的書き込みを共用し、JSON の構文検証には既存の `AiJsonObject` を使う。
応答の I/O 失敗時は応答を保持して書き込みを再試行する。既存応答のある要求は再実行せず削除する。
ポーリング間隔は定数 0.05 秒。初期化時に既存要求を走査し、その後は `FileSystemWatcher` の
公開通知がある間隔にだけ走査する。通知バッファのエラー時も再走査する。
`Directory.GetFiles` は必要な間隔につき最大1回、残ったパスは配列のまま持ち越す。
通知のない通常待機では、時刻・通知フラグ・配列位置の比較だけで追加確保をしない。
監視リソースとイベント購読はドメインリロード前・Editor 終了時に解放する。

`editor_ctl.py` は標準ライブラリのみで、UUID・`.tmp` → rename・応答待ち・一行 JSON・成功 0 / 失敗 1 の
流儀を `ai_client.py` に合わせた。`--mailbox` 省略時はカレントから親を探索し、
初回は Unity プロジェクト構造からも解決する。応答待ちは `--timeout`（既定 60 秒）。
Runtime 用の `.enabled` と `TESTIFY_MAILBOX` は使わない。

### 仕様表と現物の差異・対応

| 箇所 | 現物 | 対応 |
|---|---|---|
| 先に読む要求型 `AiMailboxRequest` | この型はなく、既存メールボックスは `AiCommandRequest`（`op` / `args`）を使う | 実際の `AiCommandRequest` と `AiMailboxFiles` を確認。T2 は指定の `EditorControlRequest`（`op` / `arg`）を新設 |
| T1 の `PlayModeViewFocus` 共用 | `TryFocus` が private | internal に変更し summary を追加。型検索・ウィンドウ生成・Focus はそのまま共用 |
| 要求・応答の配置と構成表 | 既存 Runtime の要求・応答は `Gateway/` 直下、`Mailbox/` は通信の責務 | Editor も要求・応答を `Gateway/` に配置。`Editor/Gateway/` は 1→3、`Editor/Gateway/Mailbox/` は指定どおり 1→2 |
| 毎フレームの確保 | 既存サーバーはポーリングごとに `Directory.GetFiles` を呼ぶ | T2 は空走査の確保も避けるため、ファイル通知を走査の契機に使用。要求処理・Editor API 呼び出しは Editor 更新上に限定 |
| Tests asmdef | `UniTestify.Editor` の参照なし | 参照を追加。要求・応答は既存 Runtime の DTO と同様 public とし、Editor 側の `InternalsVisibleTo` 追加は不要 |
| 構成表の Tools | Tools の件数表が未掲載。Python は既存2ファイル | `editor_ctl.py` 追加後の3ファイルを表に掲載 |

`status` の4値は、仕様の応答を `ok` / `message` の2項目に維持するため message に格納した。
既存成果物の JSON は変更していないが、新しい Editor 応答契約も設計書12へ併記した。

### 追加・変更ファイル一覧

| ファイル | 種別 | 内容 |
|---|---|---|
| `Editor/Gateway/EditorControlRequest.cs` | 追加 | 要求 JSON の解釈・op / コンパイル中の拒否判定 |
| `Editor/Gateway/EditorControlRequest.cs.meta` | 追加 | 新規 C# の GUID |
| `Editor/Gateway/EditorControlResponse.cs` | 追加 | 受理・成功・失敗応答と JSON 化 |
| `Editor/Gateway/EditorControlResponse.cs.meta` | 追加 | 新規 C# の GUID |
| `Editor/Gateway/Mailbox/EditorControlMailbox.cs` | 追加 | Editor 常駐・通知・ポーリング・要求実行・応答公開 |
| `Editor/Gateway/Mailbox/EditorControlMailbox.cs.meta` | 追加 | 新規 C# の GUID |
| `Editor/Gateway/PlayModeViewFocus.cs` | 変更 | T1 の共用メソッドを internal に公開 |
| `Tools/editor_ctl.py` | 追加 | Editor 操作用クライアント |
| `Tools/editor_ctl.py.meta` | 追加 | Python ファイルの GUID |
| `Tests/EditMode/Editor.meta` | 追加 | テストフォルダの GUID |
| `Tests/EditMode/Editor/Gateway.meta` | 追加 | テストフォルダの GUID |
| `Tests/EditMode/Editor/Gateway/Mailbox.meta` | 追加 | テストフォルダの GUID |
| `Tests/EditMode/Editor/Gateway/Mailbox/EditorControlRequestTest.cs` | 追加 | 要求解釈・拒否判定・応答契約の純ロジックテスト |
| `Tests/EditMode/Editor/Gateway/Mailbox/EditorControlRequestTest.cs.meta` | 追加 | 新規 C# の GUID |
| `Tests/EditMode/UniTestify.Tests.EditMode.asmdef` | 変更 | `UniTestify.Editor` 参照を追加 |
| `docs/getting-started.md` | 変更 | Editor 操作の導入・実行例・状態確認 |
| `docs/ops-reference.md` | 変更 | Editor 専用の要求・応答と8つの op 表 |
| `docs/architecture.md` | 変更 | 独立した Editor 経路、フォルダ件数、Tools、テスト対応 |
| `docs/design/design-unilab-ai-12-ai-gateway.md` | 変更 | T2 のプロトコル・受理応答・常駐処理の記録 |
| `docs/implementation.md` | 変更 | 本節 |

### 追加したテスト名

すべて `EditorControlRequestTest`。9メソッド、TestCase 展開後は38ケース。

- `FromJsonPreservesMenuArgument`
- `MissingArgumentDefaultsToEmpty`
- `SupportedOperationsAreAccepted`
- `CompilationRejectsEveryOperationExceptStatus`
- `UnknownOperationReturnsFailure`
- `MenuRequiresArgument`
- `InvalidJsonIsRejected`
- `AcceptedResponseDirectsCallerToStatus`
- `ResponseContainsOnlyContractFields`

### 静的確認と未実行の確認事項

- 型定義・namespace・既存 using・アセンブリの公開範囲を照合した。追加先のファイル数は
  `Editor/Gateway/` 3、`Editor/Gateway/Mailbox/` 2、Editor メールボックスのテストフォルダ 1、Tools の Python 3。
  EditMode の C# は合計26ファイル。新規ファイル・フォルダに `.meta` を追加した。
- 変更 C# を検索し、禁止依存・GetComponent 系・Awake / Start / Update の追加がないことを確認した。
- Unity 起動・インポート・コンパイル・ビルド・C# / Python のテスト・クライアント実行はすべて未実行。
  依頼者によるレビューと実機確認が必要。
- 受け入れ確認: Play 停止状態で `editor_ctl.py play` の受理後、`status` を繰り返し、
  `message` に `isPlaying=True` が現れること。`simulator_view` で Device Simulator が前面になること。
- 併せて Pause / 解除 / Stop 後の status、Game View フォーカス、メニューの成功・失敗、
  コンパイル中の status 許可と他 op 拒否、ドメインリロードあり／なしの Play 往復、
  待機時の GC Alloc、原子的な要求公開の通知が利用側 Editor で機能することを確認する。
  確認後は `stop` を送り、`isPlaying=False` を確認する。

状態変更の非同期性は [Unity の isPlaying API](https://docs.unity3d.com/ScriptReference/EditorApplication-isPlaying.html)、
監視の型・イベント・解放 API は [.NET の FileSystemWatcher 定義](https://github.com/dotnet/runtime/blob/main/src/libraries/System.IO.FileSystem.Watcher/src/System/IO/FileSystemWatcher.cs) でも確認した。

### 提案（このランでは未実装）

1. Editor メールボックスの応答保持期限を決めるべき。理由は常駐期間中に `res-*.json` が蓄積するため。
2. `editor_ctl.py` のパス探索・タイムアウト・異常応答を Python のテストで固定すべき。理由は Editor を使わずに通信側の回帰を検出できるため。
