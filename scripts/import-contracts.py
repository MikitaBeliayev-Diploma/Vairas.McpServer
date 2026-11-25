"""Freeze exact current owner success schemas; no edits to external Documentation."""
from pathlib import Path
import hashlib
import json
import sys

root = Path(__file__).resolve().parents[1]
schemas = {}; sources = []
definitions = [
    ('get_my_enrollments', 'SchoolManagement', 'school-management', '/api/v1/users/me/enrollments', 'get', 'enrollments'),
    ('get_my_lessons', 'Scheduling', 'scheduling', '/api/v1/users/me/lessons', 'get', 'lessons'),
    ('get_student_training_progress', 'LearningTracking', 'learning-tracking', '/api/v1/student-trainings/{enrollmentId}/progress', 'get', None),
    ('find_available_lessons', 'Scheduling', 'scheduling', '/api/v1/schools/{schoolId}/availability', 'get', None),
    ('preview_lesson_booking', 'Scheduling', 'scheduling', '/api/v1/practical-lessons/booking-previews', 'post', None),
    ('preview_lesson_rescheduling', 'Scheduling', 'scheduling', '/api/v1/practical-lessons/{lessonId}/rescheduling-previews', 'post', None),
]
for name, service, stem, path, method, wrapper in definitions:
    file = root.parent / f'Vairas.{service}/api/{stem}.openapi.json'
    raw = file.read_bytes(); document = json.loads(raw.decode('utf-8-sig'))
    def inline(node):
        if isinstance(node, list): return [inline(v) for v in node]
        if not isinstance(node, dict): return node
        if '$ref' in node:
            target = document
            for part in node['$ref'].removeprefix('#/').split('/'): target = target[part]
            return inline(target)
        return {k: inline(v) for k, v in node.items()}
    schema = inline(document['paths'][path][method]['responses']['200']['content']['application/json']['schema'])
    if wrapper: schema = {'type': 'object', 'required': [wrapper], 'properties': {wrapper: schema}, 'additionalProperties': False}
    schemas[name] = schema
    sources.append({'tool': name, 'path': str(file.relative_to(root.parent)).replace('\\','/'), 'sha256': hashlib.sha256(raw).hexdigest()})
payload = (json.dumps(schemas, ensure_ascii=False, indent=2)+'\n').encode('utf-8')
targets = [root / 'Vairas.McpServer.Business/Contracts/tool-outputs.json', root.parent / 'Vairas.ChatBackend/Vairas.ChatBackend/Contracts/tool-outputs.json']
for target in targets:
    if '--write' in sys.argv: target.parent.mkdir(parents=True, exist_ok=True); target.write_bytes(payload)
    assert target.read_bytes() == payload, f'Owner contract drift: {target}'
manifest = {'sources': sources, 'outputContractSha256': hashlib.sha256(payload).hexdigest()}
if '--write' in sys.argv: (root/'source-manifest.json').write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
else: assert json.loads((root/'source-manifest.json').read_text(encoding='utf-8-sig')) == manifest, 'Owner API source hash drift'
print('Six frozen owner output schemas and Chat/MCP copies: PASS')
