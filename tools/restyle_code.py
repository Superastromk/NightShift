import argparse
import hashlib
import json
import re
from pathlib import Path


def literal_end(text, start):
    quote = text[start]
    verbatim = quote == '"' and start > 0 and text[start - 1] == '@'
    i = start + 1
    while i < len(text):
        if text[i] == '\\' and not verbatim:
            i += 2
            continue
        if text[i] == quote:
            if quote == '"' and verbatim and i + 1 < len(text) and text[i + 1] == '"':
                i += 2
                continue
            return i + 1
        i += 1
    raise ValueError('Unclosed literal at character ' + str(start))


def is_literal_start(text, i):
    return text[i] in '\"\''



def remove_comments(text):
    out = []
    i = 0
    removed = 0
    while i < len(text):
        if is_literal_start(text, i):
            end = literal_end(text, i)
            out.append(text[i:end])
            i = end
        elif text.startswith('//', i):
            removed += 1
            end = text.find('\n', i + 2)
            i = len(text) if end < 0 else end
        elif text.startswith('/*', i):
            removed += 1
            end = text.find('*/', i + 2)
            if end < 0:
                raise ValueError('Unclosed block comment')
            segment = text[i:end + 2]
            out.append(''.join(c for c in segment if c in '\r\n'))
            i = end + 2
        else:
            out.append(text[i])
            i += 1
    return ''.join(out), removed


def compact(text):
    out = []
    i = 0
    while i < len(text):
        if is_literal_start(text, i):
            end = literal_end(text, i)
            out.append(text[i:end])
            i = end
        else:
            if not text[i].isspace():
                out.append(text[i])
            i += 1
    return ''.join(out)


def literal_segments(text):
    start = 0
    i = 0
    while i < len(text):
        if is_literal_start(text, i):
            if start < i:
                yield text[start:i], False
            end = literal_end(text, i)
            yield text[i:end], True
            i = end
            start = i
        else:
            i += 1
    if start < len(text):
        yield text[start:], False


def restyle(text, file_index):
    newline = '\r\n' if '\r\n' in text else '\n'
    result = []
    segment_index = file_index * 7
    for segment, literal in literal_segments(text):
        if literal:
            result.append(segment)
            continue
        mode = segment_index % 4
        if mode == 0:
            def brace(match):
                indent = re.match(r'[ \t]*', segment[segment.rfind('\n', 0, match.start()) + 1:]).group()
                return ')' + newline + indent + '{'
            segment = re.sub(r'\)[ \t]+\{', brace, segment)
        elif mode == 1:
            segment = re.sub(r'(?<![=+\-*/%&|^!<>]) = (?![=>])', '=', segment)
        elif mode == 2:
            segment = re.sub(r', (?=\S)', ',', segment)
        else:
            segment = re.sub(r'\b(if|for|foreach|while|switch|catch)\(', r'\1 (', segment)
        result.append(segment)
        segment_index += 1
    text = ''.join(result)
    text = re.sub(r'(\r?\n)[ \t]*(?:\r?\n[ \t]*){3,}', r'\1\1', text)
    return text.lstrip('\r\n')


def read_source(path):
    data = path.read_bytes()
    try:
        return data.decode('utf-8-sig')
    except UnicodeDecodeError:
        return data.decode('cp1252')


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--project', required=True)
    parser.add_argument('--apply', action='store_true')
    args = parser.parse_args()
    root = Path(args.project).resolve()
    folders = [root / 'Assets' / name for name in ('Scripts', 'Editor', 'QA')]
    folders.append(root / 'AgentScripts')
    report = {'root': str(root), 'applied': args.apply, 'files': [], 'errors': []}
    paths = sorted(path for folder in folders for path in folder.rglob('*.cs'))
    for file_index, path in enumerate(paths):
        source = read_source(path)
        try:
            without_comments, count = remove_comments(source)
            styled = restyle(without_comments, file_index)
            if compact(without_comments) != compact(styled):
                raise ValueError('Token-preservation check failed')
            remaining, remaining_count = remove_comments(styled)
            if remaining_count:
                raise ValueError(str(remaining_count) + ' comments remain')
            relative = str(path.relative_to(root)).replace('\\', '/')
            report['files'].append({'path': relative, 'comments_removed': count, 'changed': styled != source, 'before_sha256': hashlib.sha256(source.encode()).hexdigest(), 'after_sha256': hashlib.sha256(styled.encode()).hexdigest()})
            if args.apply and styled != source:
                path.write_text(styled, encoding='utf-8', newline='')
        except (ValueError, UnicodeError) as error:
            report['errors'].append({'path': str(path.relative_to(root)), 'error': str(error)})
    report['files_changed'] = sum(item['changed'] for item in report['files'])
    report['comments_removed'] = sum(item['comments_removed'] for item in report['files'])
    report['total_comments_remaining'] = sum(remove_comments(read_source(path))[1] for path in paths)
    destination = root / 'qa' / 'code-style'
    destination.mkdir(parents=True, exist_ok=True)
    (destination / ('restyle-applied.json' if args.apply else 'restyle-dry-run.json')).write_text(json.dumps(report, indent=2), encoding='utf-8')
    print(json.dumps({key: value for key, value in report.items() if key != 'files'}, indent=2))
    if report['errors']:
        raise SystemExit(1)


if __name__ == '__main__':
    main()
