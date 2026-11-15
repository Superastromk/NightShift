"""Splits lines that hold several C# statements ("a(); b(); c();") onto one statement per line.

Only top-level semicolons are split (never inside parentheses, brackets, braces, strings or
char literals), so the result compiles to exactly the same code. Trailing // comments stay on
the last statement.

    python split_statements.py <min_line_length> <file.cs> [file.cs ...]
"""
import sys


def split_line(line):
    indent = line[: len(line) - len(line.lstrip())]
    body = line.strip()
    pieces, cur, depth, i = [], "", 0, 0
    in_str = in_chr = verbatim = False
    while i < len(body):
        ch = body[i]
        if in_str:
            cur += ch
            if verbatim:
                if ch == '"' and i + 1 < len(body) and body[i + 1] == '"':
                    cur += body[i + 1]; i += 2; continue
                if ch == '"': in_str = False
            else:
                if ch == "\\": cur += body[i + 1]; i += 2; continue
                if ch == '"': in_str = False
            i += 1; continue
        if in_chr:
            cur += ch
            if ch == "\\": cur += body[i + 1]; i += 2; continue
            if ch == "'": in_chr = False
            i += 1; continue
        if body.startswith("//", i):
            cur += body[i:]
            break
        if ch == '"':
            in_str = True
            verbatim = cur.endswith("@") or cur.endswith("$@") or cur.endswith("@$")
        elif ch == "'":
            in_chr = True
        elif ch in "([{":
            depth += 1
        elif ch in ")]}":
            depth -= 1
        cur += ch
        if ch == ";" and depth == 0:
            rest = body[i + 1:].strip()
            if rest and not rest.startswith("//"):
                pieces.append(cur.strip())
                cur = ""
        i += 1
    if cur.strip():
        pieces.append(cur.strip())
    if len(pieces) < 2:
        return [line]
    return [indent + p for p in pieces]


def main():
    min_len = int(sys.argv[1])
    for path in sys.argv[2:]:
        with open(path, encoding="utf-8") as f:
            lines = f.read().split("\n")
        out, changed = [], 0
        for line in lines:
            stripped = line.strip()
            if len(line) >= min_len and not stripped.startswith("//") and not stripped.startswith("for ") and not stripped.startswith("for("):
                parts = split_line(line)
                if len(parts) > 1:
                    changed += 1
                out.extend(parts)
            else:
                out.append(line)
        with open(path, "w", encoding="utf-8") as f:
            f.write("\n".join(out))
        print(f"{path}: split {changed} lines")


if __name__ == "__main__":
    main()
