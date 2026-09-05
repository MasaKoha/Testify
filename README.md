# Testify

**AI エージェント（Claude Code / Codex）が Unity のゲームを実際に動かし、自分で見て、何がおかしいかを判断する**ためのツール群。

人間がスクリーンショットを開いて目視する工程を、**構造化された観測テキスト**と**事後条件の自動判定**に置き換える。
Unity プロジェクトに入れると、AI はメールボックス（ファイル I/O）か CLI からゲームを 1 手ずつ操作し、押した結果を文字で受け取り、回帰シナリオに書き出せる。

- 旧名: `UniLab.AI`（UniLab リポジトリの `Assets/UniLab.AI/` から切り出し。C# の名前空間は互換のため `UniLab.AI` のまま）
- 対応: Unity 6000.x、Input System、TextMeshPro（uGUI）。ゲーム本体のライブラリ（UniLab / R3 / UniTask / VContainer）には依存しない

## できること

| 分類 | 何ができるか | 入口 |
|---|---|---|
| **観測** | 画面の文字・ボタン・フォーカス・遮蔽・スクロール外を 1 枚のテキストにする（`scene=… [Button] … *focused` 形式）。ゲーム側が登録した状態（ゴールド・HP 等）も同梱 | `agent.observe` / `snapshot` |
| **操作** | ボタン名・ラベル部分一致・パッド・キーボード・マウス・タッチの語彙で 1 手ずつ操作。**対象が押せるまで待ち、遷移が落ち着いてから観測を返す**。複数手の一括、事後条件（`expect`）の同時検証 | `agent.act` |
| **検索** | 「雷撃」を含むボタンを探して推奨の指定文字列を返す（観測全文を読み直さない） | `agent.find` |
| **撮影** | Game View を PNG に。大きさと白紙判定つき。観測と同一フレームでも撮れる | `capture` / `agent.observe {"capture":…}` |
| **回帰** | JSON シナリオを条件待ちで実行し、撮影・監査・録画・合否 JSON を出す。AI の探索セッションをそのままシナリオに書き出せる | `scenario.run` / `agent.export` |
| **診断** | 例外時のスクショ＋UI 状態の自動保存、UI レイアウト監査（はみ出し・重なり）、シーン階層ダンプ、コンソールログ取得 | `console` / `ai_forensics_latest` / メニュー |
| **録画** | 実時間どおりの連番 JPG ＋ 音声 WAV ＋ 入力可視化オーバーレイ。ffmpeg コマンドは manifest に同梱 | シナリオの `recordStart` / `recordStop` |
| **探索** | モンキーテスト（ランダム操作で壊れたら証拠を残す）、決定的リプレイ、視覚回帰、性能計測 | `ai_monkey` / メニュー |

## 3 分で試す

1. Unity プロジェクトへ導入（`Packages/manifest.json`）:
   ```json
   "com.pisuke.testify": "https://github.com/MasaKoha/testify.git"
   ```
   Unity 公式 CLI（`com.unity.pipeline`）を入れると `unity command ai_*` からも叩ける（任意）。
2. Play を開始する前に、プロジェクト直下に `DebugOutput/agent-mailbox/.enabled` を置く（Python クライアントが自動で置く）。Play に入ると Unity 内蔵のメールボックスサーバが起動する
3. AI（または人）からファイル経由で操作する:
   ```sh
   python3 Packages/com.pisuke.testify/Tools/ai_client.py ping
   python3 Packages/com.pisuke.testify/Tools/ai_client.py agent.begin '{"goal":{"freePlay":true,"maxSteps":5000,"maxSeconds":14400}}'
   python3 Packages/com.pisuke.testify/Tools/ai_client.py agent.act '{"action":{"submit":"NewGameButton"}}'
   python3 Packages/com.pisuke.testify/Tools/ai_client.py agent.find '{"label":"開始","kind":"Button"}'
   python3 Packages/com.pisuke.testify/Tools/ai_client.py agent.observe '{"capture":"title"}'
   python3 Packages/com.pisuke.testify/Tools/ai_client.py agent.end
   ```
   往復は 0.1 秒前後。`act` は対象が押せるまで待ち、落ち着いてから観測を返す。

## ドキュメント

| ページ | 内容 |
|---|---|
| [docs/getting-started.md](docs/getting-started.md) | 導入手順、メールボックス／CLI の起動、Claude Code と Codex それぞれの使い方、ゲーム側の状態提供の登録 |
| [docs/ops-reference.md](docs/ops-reference.md) | 全 op の引数・応答フィールド・観測テキストの読み方 |
| [docs/scenario-guide.md](docs/scenario-guide.md) | 回帰シナリオ JSON の語彙（操作・待ち・撮影・`expect`）と AI セッションからの書き出し |
| [docs/recording-and-artifacts.md](docs/recording-and-artifacts.md) | 録画・音声・オーバーレイ、`DebugOutput/` の成果物レイアウト、既知の制約 |
| [docs/architecture.md](docs/architecture.md) | モジュール構成、依存の鉄則、切り出しの経緯、利用側への同期手順 |
| [docs/design/](docs/design/) | 設計書 01〜12 とロードマップ（判断の記録） |

## 実績（KARAKURI での計測、2026-09-05）

- Codex（gpt-6-astra）が初見でプレイして 177 手・25 分で 15 件の問題を報告（確定バグ 6 件）
- 全 24 画面の動線確認＋撮影を 40 手・3 分 37 秒で完走、白紙 0
- 1 往復: 外付け中継＋CLI で 2〜3 秒 → 内蔵メールボックスで 0.10 秒

## 開発

- テスト: `TestProject/` を Unity で開き、Test Runner の EditMode を実行（このパッケージを `file:../../` で参照している）
- 規約と作業手順は [CLAUDE.md](CLAUDE.md)
- ライセンスは未設定（利用者が決める）
