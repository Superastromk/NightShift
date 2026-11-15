import argparse
import hashlib
import json
import shutil
from pathlib import Path


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--role', choices=['Boss', 'Colleague', 'Intern'], required=True)
    parser.add_argument('--source', required=True)
    parser.add_argument('--model')
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    source = Path(args.source).resolve()
    source.relative_to(root / 'SourceAssets' / 'Humans')
    models = [p for p in source.glob('*.fbx') if not p.stem.endswith('_base')]
    if args.model:
        models = [p for p in models if p.name == args.model]
    if len(models) != 1:
        raise ValueError('Choose one final FBX with --model; candidates: ' + str([p.name for p in models]))
    model = models[0]
    source_fbm = source / (model.stem + '.fbm')
    if not source_fbm.is_dir():
        raise FileNotFoundError(source_fbm)
    opaque_path = source / 'opaque_derivatives.json'
    opaque = json.loads(opaque_path.read_text(encoding='utf-8')) if opaque_path.exists() else []
    superseded = {Path(item['source']).name for item in opaque}
    textures = {}
    for folder in [source_fbm, source / 'textures']:
        if not folder.is_dir():
            continue
        for path in folder.iterdir():
            if path.suffix.lower() in {'.png', '.jpg', '.jpeg', '.tga', '.tif', '.tiff'} and path.name not in superseded:
                textures[path.name] = path
    if not textures:
        raise ValueError('No textures found')
    target = root / 'Assets' / 'Art' / 'Humans' / args.role
    if target.exists():
        shutil.rmtree(target)
    target_fbm = target / (args.role + '.fbm')
    target_fbm.mkdir(parents=True)
    files = []
    copied_model = target / (args.role + '.fbx')
    shutil.copy2(model, copied_model)
    files.append({'path': str(copied_model.relative_to(root)), 'sha256': digest(copied_model), 'bytes': copied_model.stat().st_size})
    for name, path in sorted(textures.items()):
        copied = target_fbm / name
        shutil.copy2(path, copied)
        if digest(path) != digest(copied):
            raise ValueError('Texture copy mismatch: ' + name)
        files.append({'path': str(copied.relative_to(root)), 'sha256': digest(copied), 'bytes': copied.stat().st_size})
    output = root / 'qa' / 'human-staging'
    output.mkdir(parents=True, exist_ok=True)
    report = {'role': args.role, 'source': str(source), 'fbx': model.name, 'textures': len(textures), 'superseded_rgba': sorted(superseded), 'files': files}
    (output / (args.role + '.json')).write_text(json.dumps(report, indent=2), encoding='utf-8')
    print(json.dumps({'role': args.role, 'fbx': str(copied_model), 'textures': len(textures), 'superseded_rgba': sorted(superseded)}, indent=2))


if __name__ == '__main__':
    main()
