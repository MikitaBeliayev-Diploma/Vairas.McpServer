from pathlib import Path
import json
import re
import subprocess
import sys

root = Path(__file__).resolve().parents[1]
for script in ['sync-policy.py', 'import-contracts.py']:
    subprocess.run([sys.executable, str(root/'scripts'/script)], check=True)
catalog = json.loads((root/'api/tools.json').read_text(encoding='utf-8-sig'))
names = ['get_my_enrollments','get_my_lessons','get_student_training_progress','find_available_lessons','preview_lesson_booking','preview_lesson_rescheduling']
assert sorted(t['name'] for t in catalog['tools']) == sorted(names)
assert catalog['executionProfile'] == 'PreviewOnlyV1'
for tool in catalog['tools']:
    assert tool['annotations']['readOnlyHint'] is True
    assert tool['inputSchema']['additionalProperties'] is False
    assert isinstance(tool['outputSchema'], dict)
routes = json.loads((root.parent/'Vairas.Gateway/Vairas.Gateway/routes.json').read_text(encoding='utf-8-sig'))['PublicRoutes']
assert len(routes) == 166 and not any('/mcp' in r['Path'] or r['Path'].startswith('/internal') for r in routes)
for file in [root/'README.md',root/'VALIDATION.md',*root.glob('documentation-update/*.md')]:
    for target in re.findall(r'\[[^\]]*\]\(<?([^\)]+?)>?\)', file.read_text(encoding='utf-8-sig')):
        if '://' in target or target.startswith('#'): continue
        assert (file.parent/target.split('#')[0]).exists(), f'{file}: broken {target}'
print('MCP catalogue, policy, schemas, private Gateway boundary and links: PASS')
