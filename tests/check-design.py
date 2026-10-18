"""Measure actual native text token pairs; also catch drift in documented preview colors."""
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
source = (ROOT / 'src/UI/DesignTokens.cs').read_text(encoding='utf-8')
colors = {name: tuple(map(int, rgb)) for name, *rgb in re.findall(
    r'Color (\w+) = .*?Color.FromArgb\((\d+), (\d+), (\d+)\)', source)}

def luminance(rgb):
    channels = [v / 255 for v in rgb]
    return sum(c * weight for c, weight in zip(
        [v / 12.92 if v <= 0.04045 else ((v + 0.055) / 1.055) ** 2.4 for v in channels],
        [0.2126, 0.7152, 0.0722]))

pairs = [('Text', 'Surface'), ('Muted', 'Raised'), ('Accent', 'Raised'),
         ('OnAccent', 'Accent'), ('Ink', 'Paper'), ('Orange', 'Paper'),
         ('Success', 'Surface'), ('Danger', 'Surface')]
results = []
for foreground, background in pairs:
    a, b = sorted([luminance(colors[foreground]), luminance(colors[background])])
    ratio = (b + 0.05) / (a + 0.05)
    assert ratio >= 4.5, f'{foreground}/{background}: {ratio:.2f} < 4.5'
    results.append({'foreground': foreground, 'background': background, 'ratio': round(ratio, 2), 'minimum': 4.5})
preview = (ROOT / 'docs/design/preview.html').read_text(encoding='utf-8').lower()
for name, rgb in colors.items():
    if name == 'Border':
        continue
    value = '#' + ''.join(f'{c:02x}' for c in rgb)
    assert value in preview, f'Preview does not contain native token {name}: {value}'
report = {'scope': 'Default native dark text pairs only; not a complete accessibility audit', 'pairs': results}
(ROOT / 'docs/qa/contrast.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
print(f'PASS: {len(results)} text contrast pairs >= 4.5:1; preview colors match native tokens')
