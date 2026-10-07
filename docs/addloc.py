#!/usr/bin/env python3
"""Developer helper: merge localization entries into en.json and tr.json (keeps keys sorted).
Usage: python addloc.py <<< 'key|English text|Türkçe metin' (one entry per line)."""
import json, sys, pathlib
root = pathlib.Path(__file__).resolve().parent.parent / "src/FTC.TeamDesk.Localization/Resources"
en = json.loads((root/"en.json").read_text(encoding="utf-8"))
tr = json.loads((root/"tr.json").read_text(encoding="utf-8"))
for line in sys.stdin.read().splitlines():
    if not line.strip() or line.startswith("#"): continue
    k, e, t = line.split("|", 2)
    en[k.strip()] = e.strip(); tr[k.strip()] = t.strip()
for name, d in (("en.json", en), ("tr.json", tr)):
    (root/name).write_text(json.dumps(dict(sorted(d.items())), ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print(len(en), "keys")
