#!/usr/bin/env python3
"""Unity 内蔵メールボックスまたは HTTP 入口を標準ライブラリだけで呼び出す。

自由行動の開始例:
    ai_client.py agent.begin '{"goal":{"freePlay":true,"maxSteps":5000,"maxSeconds":14400}}'
探索した動線を書き出して回帰再生（export 応答の path を指定）:
    ai_client.py agent.export '{"name":"regression"}'
    ai_client.py scenario.run '{"path":"/absolute/path/scenario.json","name":"regression","scenarioTimeoutSeconds":900}' --timeout 930
直前のシナリオの実行状態:
    ai_client.py scenario.status
同じフレームの観測と撮影:
    ai_client.py agent.observe '{"capture":"turn_01"}'
実機への HTTP 接続（環境変数 TESTIFY_HTTP_TOKEN にトークンを設定）:
    ai_client.py --transport http --url http://127.0.0.1:7910 agent.observe
"""

import argparse
from http.client import HTTPException
from http import HTTPStatus
import json
import os
from pathlib import Path
import sys
import time
import uuid
from urllib.error import HTTPError
from urllib.parse import urlsplit
from urllib.request import HTTPRedirectHandler, ProxyHandler, Request, build_opener

DEFAULT_TIMEOUT_SECONDS = 60.0
POLL_INTERVAL_SECONDS = 0.05
MAILBOX_RELATIVE_PATH = Path("DebugOutput") / "agent-mailbox"
HTTP_TOKEN_ENVIRONMENT = "TESTIFY_HTTP_TOKEN"


class RejectHttpRedirects(HTTPRedirectHandler):
    """認証先が別 URL へ変わっても Bearer トークンを転送しない。"""

    def redirect_request(self, request, response, code, message, headers, new_url):
        return None


def resolve_mailbox(explicit_directory):
    """明示指定、環境変数、親ディレクトリ探索の順で解決する。"""
    configured = explicit_directory or os.environ.get("TESTIFY_MAILBOX")
    if configured:
        return Path(configured).expanduser().resolve()
    current = Path.cwd().resolve()
    for parent in (current, *current.parents):
        candidate = parent / MAILBOX_RELATIVE_PATH
        if candidate.is_dir():
            return candidate
    # 初回でもマーカーを置けるよう、既存の Unity プロジェクト構造を使う。
    for parent in (current, *current.parents):
        if (parent / "Assets").is_dir() and (parent / "ProjectSettings").is_dir():
            return parent / MAILBOX_RELATIVE_PATH
    raise ValueError("メールボックスが見つかりません。--mailbox DIR を指定してください。")


def write_atomic(path, payload):
    """読者から書きかけが見えないよう同一ディレクトリ内で公開する。"""
    temporary_path = path.with_name(path.name + ".tmp")
    try:
        with temporary_path.open("x", encoding="utf-8") as stream:
            json.dump(payload, stream, ensure_ascii=False, allow_nan=False)
        temporary_path.rename(path)
    finally:
        temporary_path.unlink(missing_ok=True)


def reject_nonfinite(value):
    """Unity が扱えない JSON 拡張の非有限数を拒否する。"""
    raise ValueError(f"JSON に非有限数は使えません: {value}")


def request(mailbox, operation, arguments, timeout):
    """一意な要求を公開し、対応する完成済み応答だけを待つ。"""
    mailbox.mkdir(parents=True, exist_ok=True)
    (mailbox / ".enabled").touch(exist_ok=True)
    identifier = uuid.uuid4().hex
    request_path = mailbox / f"req-{identifier}.json"
    response_path = mailbox / f"res-{identifier}.json"
    payload = {"op": operation, "args": json.dumps(arguments, ensure_ascii=False, allow_nan=False)}
    write_atomic(request_path, payload)
    deadline = time.monotonic() + timeout
    while True:
        if response_path.is_file():
            response = json.loads(response_path.read_text(encoding="utf-8-sig"))
            if not isinstance(response, dict) or not isinstance(response.get("ok"), bool):
                raise ValueError("メールボックスの応答形式が不正です。")
            return response
        remaining = deadline - time.monotonic()
        if remaining <= 0:
            raise TimeoutError(f"応答待ちがタイムアウトしました: {request_path}（要求は残っています）")
        time.sleep(min(POLL_INTERVAL_SECONDS, remaining))


def request_http(url, operation, arguments, timeout):
    """メールボックスと同じ要求を POST し、HTTP エラーの共通応答も呼び出し元へ返す。"""
    if not url:
        raise ValueError("--transport http では --url http://HOST:PORT を指定してください。")
    parsed_url = urlsplit(url)
    if (parsed_url.scheme != "http" or not parsed_url.hostname or parsed_url.username is not None
            or parsed_url.password is not None or parsed_url.path not in ("", "/")
            or parsed_url.query or parsed_url.fragment):
        raise ValueError("--url は http://HOST:PORT の形式で指定してください。")
    token = os.environ.get(HTTP_TOKEN_ENVIRONMENT, "")
    if not token or any(character < "!" or character > "~" for character in token):
        raise ValueError(f"{HTTP_TOKEN_ENVIRONMENT} に HTTP 入口のトークンを設定してください。")
    payload = {"op": operation, "args": json.dumps(arguments, ensure_ascii=False, allow_nan=False)}
    encoded_payload = json.dumps(payload, ensure_ascii=False, allow_nan=False).encode("utf-8")
    http_request = Request(url.rstrip("/") + "/op", data=encoded_payload, method="POST", headers={
        "Content-Type": "application/json; charset=utf-8",
        "Authorization": f"Bearer {token}",
    })
    # 実機への直結用 URL を使い、環境のプロキシやリダイレクトへ認証情報を渡さない。
    opener = build_opener(ProxyHandler({}), RejectHttpRedirects())
    try:
        connection = opener.open(http_request, timeout=timeout)
    except HTTPError as exception:
        connection = exception
    with connection:
        response = json.loads(connection.read().decode("utf-8-sig"), parse_constant=reject_nonfinite)
        if not isinstance(response, dict) or not isinstance(response.get("ok"), bool):
            raise ValueError("HTTP の応答形式が不正です。")
        if connection.getcode() != HTTPStatus.OK:
            response["ok"] = False
        return response


def main(argv=None):
    """メタデータを先頭の一行 JSON、観測を続く本文として表示する。"""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--transport", choices=("file", "http"), default="file")
    parser.add_argument("--url", help="HTTP 入口のベース URL（http://HOST:PORT）")
    parser.add_argument("--mailbox", metavar="DIR")
    parser.add_argument("--timeout", type=float, default=DEFAULT_TIMEOUT_SECONDS)
    parser.add_argument("op")
    parser.add_argument("args", nargs="?", default="{}", help="引数の JSON オブジェクト")
    options = parser.parse_args(argv)
    try:
        if not 0 < options.timeout < float("inf"):
            raise ValueError("--timeout は有限の正数で指定してください。")
        arguments = json.loads(options.args, parse_constant=reject_nonfinite)
        if not isinstance(arguments, dict):
            raise ValueError("引数は JSON オブジェクトで指定してください。")
        if options.transport == "http":
            response = request_http(options.url, options.op, arguments, options.timeout)
        else:
            response = request(resolve_mailbox(options.mailbox), options.op, arguments, options.timeout)
        text = response.pop("text", "")
        print(json.dumps(response, ensure_ascii=False))
        if text:
            print(text)
        return 0 if response["ok"] else 1
    except (OSError, ValueError, TimeoutError, HTTPException) as exception:
        print(json.dumps({"ok": False, "op": options.op, "error": str(exception)}, ensure_ascii=False))
        return 1


if __name__ == "__main__":
    sys.exit(main())
