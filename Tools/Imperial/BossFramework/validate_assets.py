from pathlib import Path
import argparse
import json
import math
from PIL import Image

parser = argparse.ArgumentParser()
parser.add_argument('--root', type=Path, default=Path(__file__).resolve().parents[3])
parser.add_argument('--report', type=Path)
args = parser.parse_args()
root = args.root.resolve()
textures = root / 'Resources/Textures/Imperial/Medieval'
directories = [
    'Mobs/Bosses/RevenantBoss', 'Effects/Boss', 'Structures/RevenantArena',
    'Structures/RevenantBossBrazier', 'Structures/RevenantBossConstellation',
    'Structures/RevenantBossCoffer', 'Structures/RevenantBossCrystal',
    'Structures/BossArenaGate', 'Tiles/RevenantArena',
    'Melee/revenant_scythe.rsi', 'Melee/revenant_scythe_swipe.rsi',
    'Misc/veyrath_key.rsi',
]
errors = []
records = []
for directory in directories:
    for path in sorted((textures / directory).rglob('meta.json')):
        meta = json.loads(path.read_text(encoding='utf-8-sig'))
        width, height = meta['size']['x'], meta['size']['y']
        if width <= 0 or height <= 0 or width % 32 or height % 32:
            errors.append(f'{path}: canvas must use positive 32-pixel units')
        names = set()
        for state in meta['states']:
            name = state['name']
            if name in names:
                errors.append(f'{path}: duplicate state {name}')
            names.add(name)
            directions = state.get('directions', 1)
            if directions not in (1, 4, 8):
                errors.append(f'{path}: unsupported directions {directions}')
            delays = state.get('delays', [[1]])
            if len(delays) not in (1, directions) or any(not row for row in delays):
                errors.append(f'{path}: invalid delay rows in {name}')
            if any(not math.isfinite(delay) or delay <= 0 for row in delays for delay in row):
                errors.append(f'{path}: invalid frame duration in {name}')
            frames = sum(map(len, delays)) * (directions if len(delays) == 1 else 1)
            with Image.open(path.parent / (name + '.png')) as sheet:
                if sheet.width % width or sheet.height % height:
                    errors.append(f'{path}: {name} is not packed on its frame grid')
                if sheet.width // width * (sheet.height // height) < frames:
                    errors.append(f'{path}: {name} is missing frames')
            records.append({'rsi': path.parent.relative_to(root).as_posix(), 'state': name,
                            'size': [width, height], 'directions': directions, 'source_frames': frames})
    for path in sorted((textures / directory).rglob('*.png')):
        if any(parent.name.endswith('.rsi') for parent in path.parents):
            continue
        with Image.open(path) as image:
            if image.width % 32 or image.height % 32:
                errors.append(f'{path}: loose texture must use 32-pixel units')
report = {'rsi_count': len({row['rsi'] for row in records}), 'state_count': len(records),
          'errors': errors, 'states': records}
if args.report:
    args.report.write_text(json.dumps(report, indent=2) + '\n')
print(json.dumps({key: value for key, value in report.items() if key != 'states'}, indent=2))
raise SystemExit(bool(errors))
