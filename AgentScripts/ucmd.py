"""Minimal client for the Unity Pipeline server of the open Editor (loopback only).

Usage:
    python ucmd.py eval  "<C# statements>"        run C# in the Editor (script eval)
    python ucmd.py evalf path/to/file.cs           same, code read from a file
    python ucmd.py cmd   "<command line>"          any pipeline command line, e.g. "editor status"
    python ucmd.py list                            list the commands the Editor exposes

Reads Library/Pipeline/.unity-pipeline-port next to this script's project for the port + token.
The token is never printed.
"""
import json
import os
import sys
import urllib.request

PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DESCRIPTOR = os.path.join(PROJECT, "Library", "Pipeline", ".unity-pipeline-port")


def _conn():
    # The Editor rewrites the descriptor every 15 s; retry briefly if we catch it mid-write.
    import time
    for attempt in range(20):
        try:
            with open(DESCRIPTOR, encoding="utf-8") as f:
                d = json.load(f)
            return f"http://127.0.0.1:{d['port']}", d["evalToken"]
        except (PermissionError, ValueError, FileNotFoundError):
            time.sleep(0.25)
    raise RuntimeError("could not read the Pipeline descriptor")


def _request(path, body=None, timeout=600):
    base, token = _conn()
    data = None if body is None else json.dumps(body).encode("utf-8")
    req = urllib.request.Request(base + path, data=data, method="POST" if data else "GET")
    req.add_header("Authorization", "Bearer " + token)
    if data:
        req.add_header("Content-Type", "application/json")
    try:
        with urllib.request.urlopen(req, timeout=timeout) as r:
            return r.status, r.read().decode("utf-8", "replace")
    except urllib.error.HTTPError as e:
        return e.code, e.read().decode("utf-8", "replace")


def run(argv, timeout=600):
    # the server takes milliseconds
    body = {"argv": argv, "timeout": timeout * 1000, "caller": "plugin", "skill": "unity-cli"}
    return _request("/api/exec", body, timeout + 30)


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        return 2
    mode = sys.argv[1]
    if mode == "list":
        status, text = _request("/api/commands")
    elif mode == "status":
        status, text = _request("/api/editor_status")
    elif mode == "eval":
        status, text = run(["script", "eval", "--code", sys.argv[2]])
    elif mode == "evalf":
        with open(sys.argv[2], encoding="utf-8") as f:
            code = f.read()
        status, text = run(["script", "eval", "--code", code])
    elif mode == "cmd":
        import shlex
        status, text = run(shlex.split(sys.argv[2]))
    else:
        print(__doc__)
        return 2
    try:
        parsed = json.loads(text)
        out = json.dumps(parsed, indent=1)
    except ValueError:
        out = text
    if len(out) > 12000:
        out = out[:12000] + "\n... (truncated)"
    print(f"HTTP {status}")
    print(out)
    return 0 if status == 200 else 1


if __name__ == "__main__":
    sys.exit(main())
