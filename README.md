# UniTestify

UniTestify は、Unity アプリケーションの画面状態を構造化テキストに変換し、AI エージェントや自動テストによる操作と検証を可能にするテスト支援ライブラリである。
UI 要素、フォーカス、遮蔽状態をテキストとして取得し、事後条件の機械判定やシナリオによる回帰テストを支援する。

## 解決したい問題

AI エージェント（コーディング支援 AI）に Unity ゲームを検証させようとするとき、素朴な方法ではスクリーンショットを撮影して AI に渡すことになる。
しかし、画像は読み取りの費用が高く、さらに「ボタンが押せる状態にあるか」「何かに隠れていないか」といった状態を画像から確実に判断することはできない。

UniTestify は、画面を**構造化されたテキスト**に変換する。どのボタンが存在し、どれにフォーカスがあり、何が遮蔽され、何が画面外にあるかまでを文字で返す。
さらに、操作した結果が期待どおりかを**事後条件として機械が判定**する。これにより、人間が目視で確かめる工程を置き換える。
検証の手順は JSON の回帰シナリオとして保存でき、繰り返し実行できる。

## できること

| 分類 | 何ができるか | 入口 |
|---|---|---|
| 観測 | 画面の文字・ボタン・フォーカス・遮蔽・スクロール外を 1 枚のテキストにする。ゲーム側が登録した状態（所持金・HP 等）も同梱 | `agent.observe` / `snapshot` |
| 操作 | ボタン名・ラベル部分一致・パッド・キーボード・マウス・タッチの語彙で 1 手ずつ操作。対象が押せるまで待ち、画面が落ち着いてから観測を返す | `agent.act` |
| 待ち | 文字・オブジェクト・フォーカス・シーンの出現を待つ。外側でポーリングを書かなくてよい | `agent.act` の `waitFor*` |
| 検索 | 語を含むボタンを探して指定文字列を返す。観測全文を読み直さない | `agent.find` |
| 撮影 | Game View か Device Simulator を PNG に。大きさと白紙判定つき | `capture` |
| 回帰 | JSON シナリオを条件待ちで実行し、撮影・監査・録画・合否 JSON を出す | `scenario.run` |
| 診断 | 例外時のスクショと UI 状態の自動保存、UI レイアウト監査、シーン階層ダンプ、コンソールログ取得 | `console` / `scene.dump` |
| 録画 | 実時間どおりの連番 JPG と音声 WAV、入力可視化オーバーレイ | シナリオの `recordStart` / `recordStop` |
| 実機 | Development Build でのシナリオ自律実行と、HTTP による 1 手ずつの対話操作 | 設定ファイル / `POST /op` |
| エディタ制御 | Play していない Unity に対して Play / Stop / Pause / メニュー実行 | Editor メールボックス |

## 動作環境

- Unity 6000.x
- 必須パッケージ: Input System、TextMeshPro
- **ゲーム本体で使う類のライブラリ（R3 / UniTask / DI コンテナ等）に依存しない。** 依存は UnityEngine と .NET 標準、上記 2 パッケージだけである
- 入口は 3 つ。ファイル I/O のメールボックス（ネットワーク不要）、HTTP（実機向け）、Unity 公式 CLI（`com.unity.pipeline` を入れた場合の任意）
- 実機は Android / iOS / PC Standalone の Development Build

## 導入

- `Packages/manifest.json` に `"com.pisuke.unitestify": "https://github.com/MasaKoha/UniTestify.git"` を追加する
- コピーで入れる場合はリポジトリ直下の `Runtime/ Editor/ Pipeline/ Tests/ Tools/ package.json` を `Assets/` 配下へ置く
- Play に入る前にプロジェクト直下へ `DebugOutput/agent-mailbox/.enabled` を置く。Python クライアントが自動で作る
- 成果物はすべて `DebugOutput/` 配下に出る。バージョン管理から除外する

## 最小の使い方

`Tools/ai_client.py` を使う。

```sh
python3 Tools/ai_client.py ping
python3 Tools/ai_client.py agent.act '{"action":{"submit":"StartButton"},"waitForObject":"HomeView"}'
python3 Tools/ai_client.py agent.find '{"label":"開始","kind":"Button"}'
python3 Tools/ai_client.py agent.observe '{"capture":"home"}'
```

`agent.act` は対象が押せるまで待ち、`waitForObject` で遷移の完了も待つ。呼ぶ側にポーリングは要らない。

## 性能

計測環境: Apple M2 Pro / macOS / Unity 6000.x / Editor の Play モード

| 項目 | 実測 |
|---|---|
| 1 往復（同一プロセスから連続）| 中央値 52 ms（最小 15 ms） |
| 1 往復（コマンド 1 回ごとに起動）| 中央値 117 ms |
| 撮影を含む 1 往復（コマンド起動込み）| 中央値 165 ms |
| 撮影の出力 | 1280x720 の PNG で約 32 KB |

- 往復時間は op の種類でほとんど変わらない。**要求ファイルを監視する間隔（既定 50 ms）が支配的**なためである
- 要求が続かない間は監視間隔が 250 ms へ落ちる。待機中の負荷を下げるためである
- コマンドを 1 回ずつ起動する経路では、Python の起動時間が上乗せされる

## できないこと

- 音声は録らない。UI と見た目の検証が目的のため
- 3D オブジェクトは操作できない。存在の確認だけできる（シーン階層のダンプ）
- 同時に処理する要求は 1 件。複数の入口から同じセッションへ同時に送ることは想定していない
- LAN 経由の HTTP 接続では、環境によって IPv6 のサブネット判定ができない。その場合は IPv4 で接続する
- 実機でゲーム側の状態を観測に載せるには、アダプタの実装を利用側のプロジェクトに置く必要がある。エディタではコンパイル済みアセンブリの注入で回避できる

## ドキュメント

| ページ | 内容 |
|---|---|
| docs/getting-started.md | 導入、メールボックスと CLI の起動、ゲーム側の状態提供の登録、実機での実行 |
| docs/ops-reference.md | 全 op の引数・応答・観測テキストの読み方 |
| docs/scenario-guide.md | 回帰シナリオ JSON の語彙と、探索セッションからの書き出し |
| docs/recording-and-artifacts.md | 録画と `DebugOutput/` の成果物 |
| docs/architecture.md | モジュール構成と依存の方針 |

## 開発環境

- `TestProject/` を Unity で開き、Test Runner の EditMode を実行する。このパッケージを `file:../../` で参照している
- Python 側のツールは `python3 -m unittest discover -s Tools -p "test_*.py"` で実行する

## ライセンス

MIT License
