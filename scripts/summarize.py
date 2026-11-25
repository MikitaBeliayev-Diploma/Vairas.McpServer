from pathlib import Path
import json
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[1]
file = root/'evidence/verification-summary.json'
summary = json.loads(file.read_text(encoding='utf-8-sig'))
run = Path(summary['run']).resolve()
assert run.is_relative_to((root/'evidence/runs').resolve())
tests = {}
for trx in run.rglob('*.trx'):
    node = next(n for n in ET.parse(trx).iter() if n.tag.rsplit('}',1)[-1]=='Counters')
    counts = {k:int(node.attrib[k]) for k in ['total','passed','failed']}
    assert counts['total']>0 and counts['total']==counts['passed'] and counts['failed']==0, (trx,counts)
    tests[trx.parent.name] = counts
summary['testCounts']=tests
summary['sourceRun']=run.relative_to(root).as_posix()
summary['status']='PASSED'
file.write_text(json.dumps(summary,indent=2)+'\n',encoding='utf-8')
print(json.dumps(summary))
