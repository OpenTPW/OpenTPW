"""Compare complete C# corpus traces with an independent, prebuilt C++ rse-run tool."""
import argparse
import csv
import difflib
import hashlib
import json
from pathlib import Path
import subprocess

parser = argparse.ArgumentParser()
parser.add_argument('corpus', type=Path)
parser.add_argument('reference', type=Path)
parser.add_argument('expected', type=int)
args = parser.parse_args()
rows = list(csv.DictReader((args.corpus / 'manifest.tsv').open(encoding='utf-8'), delimiter='\t'))
if len(rows) != args.expected:
    raise SystemExit(f'Incomplete manifest: {len(rows)} != {args.expected}')
if len({row['source'].casefold() for row in rows}) != len(rows) or [row['index'] for row in rows] != [f'{i:03}' for i in range(args.expected)]:
    raise SystemExit('Duplicate sources or incomplete script indices')
failures = []
for row in rows:
    stem = args.corpus / row['index']
    script = stem.with_suffix('.rse')
    if hashlib.sha256(script.read_bytes()).hexdigest() != row['sha256']:
        raise SystemExit(f'Input hash mismatch: {script}')
    try:
        result = subprocess.run([str(args.reference.resolve()), 'rse-run', str(script.resolve()), '--ms', '60000'],
                                capture_output=True, timeout=10)
        reference = result.stdout.decode('utf-8', errors='strict')
        stem.with_suffix('.cpp.trace').write_text(reference, encoding='utf-8')
        if result.returncode or result.stderr:
            raise ValueError(f'exit {result.returncode}, stderr {result.stderr.decode(errors="replace")}')
        # Only the reference's input-path header is omitted. Operand text, event order,
        # timestamps, PC, termination summary and every final variable remain compared.
        actual = stem.with_suffix('.cs.trace').read_text(encoding='utf-8').splitlines()
        expected = reference.splitlines()[1:]
        if not actual or not any(line.startswith('; still running after ') for line in expected):
            raise ValueError('Missing successful termination record')
        if actual != expected:
            stem.with_suffix('.diff').write_text('\n'.join(difflib.unified_diff(expected, actual, fromfile='cpp', tofile='csharp')), encoding='utf-8')
            raise ValueError('Complete traces differ')
    except (subprocess.TimeoutExpired, ValueError) as error:
        failures.append({'source': row['source'], 'error': str(error)})
summary = {'scripts': len(rows), 'identical': len(rows) - len(failures), 'failures': failures,
           'reference_sha256': hashlib.sha256(args.reference.read_bytes()).hexdigest()}
(args.corpus / 'comparison.json').write_text(json.dumps(summary, indent=2), encoding='utf-8')
print(json.dumps(summary))
raise SystemExit(bool(failures))
