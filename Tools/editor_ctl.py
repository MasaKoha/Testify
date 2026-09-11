#!/usr/bin/env python3
"""Play 停止中も Editor 操作メールボックスを標準ライブラリだけで呼び出す。

使用例:
    editor_ctl.py play
    editor_ctl.py status
    editor_ctl.py simulator_view
    editor_ctl.py menu 'Window/General/Console'
"""

import argparse
import json
from pathlib import Path
import sys
import time
import uuid

DEFAULT_TIMEOUT_SECONDS = 60.0
POLL_INTERVAL_SECONDS = 0.05
MAILBOX_RELATIVE_PATH = Path("DebugOutput") / "editor-mailbox"


def resolve_mailbox(explicit_directory):
    """明示指定を優先し、カレントから親へ Editor 専用メールボックスを探索する。"""
    if explicit_directory:
        return Path(explicit_directory).expanduser().resolve()
    current = Path.cwd().resolve()
    for parent in (current, *current.parents):
        candidate = parent / MAILBOX_RELATIVE_PATH
        if candidate.is_dir():
            return candidate
    # Editor の初期化前でも Unity プロジェクト内なら要求を置けるようにする。
    for parent in (current, *current.parents):
        if (parent / "Assets").is_dir() and (parent / "ProjectSettings").is_dir():
            return parent / MAILBOX_RELATIVE_PATH
    raise ValueError("Editor メールボックスが見つかりません。--mailbox DIR を指定してください。")


def write_atomic(path, payload):
    """書きかけの要求を実行させないため、同一ディレクトリ内で公開する。"""
    temporary_path = path.with_name(path.name + ".tmp")
    try:
        with temporary_path.open("x", encoding="utf-8") as stream:
            json.dump(payload, stream, ensure_ascii=False, allow_nan=False)
        temporary_path.rename(path)
    finally:
        temporary_path.unlink(missing_ok=True)


def request(mailbox, operation, argument, timeout):
    """一意な要求に対応する応答を待つ。受理後の Editor 状態の変化は待たない。"""
    mailbox.mkdir(parents=True, exist_ok=True)
    identifier = uuid.uuid4().hex
    request_path = mailbox / f"req-{identifier}.json"
    response_path = mailbox / f"res-{identifier}.json"
    write_atomic(request_path, {"op": operation, "arg": argument})
    deadline = time.monotonic() + timeout
    while True:
        if response_path.is_file():
            response = json.loads(response_path.read_text(encoding="utf-8-sig"))
            if (
                not isinstance(response, dict)
                or not isinstance(response.get("ok"), bool)
                or not isinstance(response.get("message"), str)
            ):
                raise ValueError("Editor メールボックスの応答形式が不正です。")
            return response
        remaining = deadline - time.monotonic()
        if remaining <= 0:
            raise TimeoutError(f"応答待ちがタイムアウトしました: {request_path}（要求は残っています）")
        time.sleep(min(POLL_INTERVAL_SECONDS, remaining))


def main(argv=None):
    """受理・成功・失敗を同じ一行 JSON で表示する。"""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--mailbox", metavar="DIR")
    parser.add_argument("--timeout", type=float, default=DEFAULT_TIMEOUT_SECONDS)
    parser.add_argument("op", help="status / play / stop / pause / unpause / focus_game_view / simulator_view / menu")
    parser.add_argument("arg", nargs="?", default="", help="menu のメニューパス（JSON ではなく文字列）")
    options = parser.parse_args(argv)
    try:
        if not 0 < options.timeout < float("inf"):
            raise ValueError("--timeout は有限の正数で指定してください。")
        response = request(resolve_mailbox(options.mailbox), options.op, options.arg, options.timeout)
        print(json.dumps(response, ensure_ascii=False))
        return 0 if response["ok"] else 1
    except (OSError, ValueError, TimeoutError) as exception:
        print(json.dumps({"ok": False, "message": str(exception)}, ensure_ascii=False))
        return 1


if __name__ == "__main__":
    sys.exit(main())
