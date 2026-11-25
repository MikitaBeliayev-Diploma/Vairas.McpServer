from pathlib import Path
import sys

# The canonical policy sources live in ../security. Each contract is one type per file, so a policy is a set of files.
root = Path(__file__).resolve().parents[1]
services = ['Authentication', 'Gateway', 'UserManagement', 'SchoolManagement', 'Scheduling', 'LearningTracking', 'McpServer']
READ_PREVIEW = ['DelegatedContext.cs', 'ReadPreviewDelegation.cs']
CONFIRMED = ['LessonAction.cs', 'LessonActionBinding.cs', 'LessonActionReceipt.cs', 'LessonActions.cs', 'LessonAuthorization.cs', 'LessonAuthorizationQuery.cs']

def sync(label, names, targets):
    for name in names:
        source = (root / 'security' / name).read_bytes()
        for folder in targets:
            file = folder / 'Security' / name
            if '--write' in sys.argv:
                file.parent.mkdir(parents=True, exist_ok=True)
                file.write_bytes(source)
            assert file.read_bytes() == source, file
    print(f'{label}: PASS')

sync('ReadPreviewV1 policy copies', READ_PREVIEW, [root.parent / f'Vairas.{n}/Vairas.{n}' for n in services])
sync('ConfirmedLessonActionsV1 contract copies', CONFIRMED,
     [root.parent / f'Vairas.{n}/Vairas.{n}.Business' if n in ['ChatBackend', 'Scheduling', 'McpServer'] else root.parent / f'Vairas.{n}/Vairas.{n}' for n in services + ['ChatBackend']])
