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
