"""Check compiled shader bindings, interfaces and uniform offsets against GLSL.

Uses only Python's standard library and DXC's built-in DXIL dump. No Vulkan
runtime or external SPIR-V tools are required. This checks static interfaces;
visual equivalence is verified when the SDL GPU renderer is connected.
"""
import argparse
import csv
import hashlib
import io
import json
from pathlib import Path
import re
import struct
import subprocess

ROOT = Path(__file__).resolve().parents[1]
SHADERS = ROOT / 'content' / 'shaders'


def clean(text):
    return re.sub(r'/\*.*?\*/|//[^\n]*', '', text, flags=re.S)


def sections(path):
    text = clean(path.read_text(encoding='utf-8-sig'))
    result = {}
    for match in re.finditer(r'\b(common|vertex|fragment)\s*(\{|"([^"]+)")', text):
        name = match[1]
        if match[3]:
            result[name] = clean((ROOT / match[3]).read_text(encoding='utf-8-sig'))
        else:
            depth, end = 1, match.end()
            while depth:
                if text[end] == '{': depth += 1
                if text[end] == '}': depth -= 1
                end += 1
            result[name] = text[match.end():end-1]
    return {stage: result.get('common', '') + '\n' + result[stage] for stage in ('vertex', 'fragment')}


def resources(text):
    pattern = r'layout\s*\(\s*set\s*=\s*(\d+)\s*,\s*binding\s*=\s*(\d+)\s*\)\s*uniform\s+(\w+)\s*(?:\{([^}]*)\}\s*(\w+)|(\w+))\s*;'
    return [dict(set=int(s), binding=int(b), kind=t, members=' '.join(m.split()), name=i or n)
            for s, b, t, m, i, n in re.findall(pattern, text, re.S)]


def spirv(path):
    data = path.read_bytes()
    assert len(data) % 4 == 0, path
    words = struct.unpack('<' + 'I' * (len(data)//4), data)
    assert words[0] == 0x07230203, path
    names, members, decorations, member_decorations, types, variables, entries = {}, {}, {}, {}, {}, {}, []
    string = lambda a: struct.pack('<'+'I'*len(a), *a).split(b'\0', 1)[0].decode('utf-8')
    index = 5
    while index < len(words):
        size, opcode = words[index] >> 16, words[index] & 65535
        assert size > 0 and index + size <= len(words), path
        a = words[index+1:index+size]
        if opcode == 5: names[a[0]] = string(a[1:])
        elif opcode == 6: members[a[0], a[1]] = string(a[2:])
        elif opcode == 15: entries.append((a[0], string(a[2:])))
        elif opcode == 71: decorations.setdefault(a[0], {})[a[1]] = list(a[2:])
        elif opcode == 72: member_decorations.setdefault((a[0], a[1]), {})[a[2]] = list(a[3:])
        elif 19 <= opcode <= 33: types[a[0]] = (opcode, a[1:])
        elif opcode == 59: variables[a[1]] = (a[0], a[2])
        index += size

    def shape(type_id):
        opcode, args = types[type_id]
        if opcode in (21, 22): return (opcode, *args)
        if opcode in (23, 24): return (opcode, shape(args[0]), args[1])
        return (opcode,)

    result = dict(entries=entries, resources=[], inputs={}, outputs={}, offsets={})
    for identifier, (pointer, storage) in variables.items():
        d = decorations.get(identifier, {})
        pointee = types[pointer][1][1]
        if 30 in d and storage in (1, 3):
            direction = 'inputs' if storage == 1 else 'outputs'
            result[direction][d[30][0]] = dict(name=names.get(identifier, ''), shape=shape(pointee), flat=14 in d)
        if 34 in d:
            assert 33 in d, path
            result['resources'].append(dict(name=names[identifier], set=d[34][0], binding=d[33][0],
                                            kind='uniform' if storage == 2 else 'combined_sampler', type_opcode=types[pointee][0]))
            if storage == 2:
                for member in range(len(types[pointee][1])):
                    md = member_decorations[(pointee, member)]
                    result['offsets'][members[pointee, member]] = md[35][0]
                    if member < 3: assert md[7] == [16], (path, md)
            else:
                assert storage == 0 and types[pointee][0] == 27, (path, 'expected combined image sampler')
    return result


def tsv(rows, columns):
    buffer = io.StringIO(newline='')
    writer = csv.DictWriter(buffer, fieldnames=columns, delimiter='\t', lineterminator='\n')
    writer.writeheader(); writer.writerows(rows)
    return buffer.getvalue()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--dxc', default='dxc')
    parser.add_argument('--compiled', type=Path, default=SHADERS/'compiled')
    parser.add_argument('--write-map', action='store_true')
    parser.add_argument('--report-dir', type=Path)
    args = parser.parse_args()
    if args.report_dir: args.report_dir.mkdir(parents=True, exist_ok=True)
    names = sorted(p.stem for p in SHADERS.glob('*.shader'))
    assert names == sorted(p.stem for p in SHADERS.glob('*.hlsl'))
    binary_names = {f'{name}.{stage}.{ext}' for name in names for stage in ('vert', 'frag') for ext in ('spv', 'dxil')}
    assert {p.name for p in args.compiled.iterdir() if p.is_file()} == binary_names | {'SHA256SUMS'}
    checksums = (args.compiled/'SHA256SUMS').read_text().splitlines()
    assert len(checksums) == len(binary_names)
    for line in checksums:
        digest, name = line.split('  ')
        assert name in binary_names and hashlib.sha256((args.compiled/name).read_bytes()).hexdigest() == digest

    mapping, reports = [], {}
    expected_offsets = dict(g_mModel=0, g_mView=64, g_mProj=128, g_vLightPos=192, _padding0=204,
                            g_vLightColor=208, _padding1=220, g_vCameraPos=224, _padding2=236,
                            g_flTime=236, g_fTime=236, g_vFogColor=240)
    for name in names:
        stages = sections(SHADERS/f'{name}.shader')
        pair = {}
        for stage, suffix, execution, entry in [('vertex','vert',0,'VSMain'), ('fragment','frag',4,'PSMain')]:
            key = f'{name}.{suffix}'
            info = spirv(args.compiled/f'{key}.spv')
            pair[stage] = info
            assert info['entries'] == [(execution, entry)], key
            old = resources(stages[stage])
            textures = [r for r in old if r['kind'] == 'texture2D']
            uniforms = [r for r in old if r['members']]
            actual = {(r['set'],r['binding']): r for r in info['resources']}
            # Only unlit's unused fragment uniform is removed by optimization.
            active_uniform = bool(uniforms) and not (name == 'unlit' and stage == 'fragment')
            uniform_set = 1 if stage == 'vertex' else 3
            expected = {(2, i) for i in range(len(textures))}
            if active_uniform: expected.add((uniform_set,0))
            assert set(actual) == expected, (key, actual, expected)
            for i, texture in enumerate(textures):
                assert actual[2,i]['name'] == texture['name'], key
            if active_uniform: assert actual[uniform_set,0]['kind'] == 'uniform'
            for member, offset in info['offsets'].items():
                assert offset == expected_offsets[member], (key, member, offset)

            dump = subprocess.check_output([args.dxc, '-dumpbin', str(args.compiled/f'{key}.dxil')], text=True, encoding='utf-8')
            assert f'EntryFunctionName: {entry}' in dump, key
            bindings = dump.split('; Resource Bindings:',1)[1].split('target datalayout',1)[0]
            dx = {(kind, int(slot), int(space)) for kind,slot,space in re.findall(r'\b(cb|s|t)(\d+),space(\d+)\s+1',bindings)}
            dx_expected = {(kind,i,2) for i in range(len(textures)) for kind in ('t','s')}
            if active_uniform: dx_expected.add(('cb',0,uniform_set))
            assert dx == dx_expected, (key, dx, dx_expected)
            for member, offset in re.findall(r'\b(g_\w+|_padding\d+)\s*;\s*; Offset:\s*(\d+)', dump):
                if member != 'g_oUbo': assert int(offset) == expected_offsets[member], (key,member,offset)

            for resource in old:
                note = 'Uniform buffer slot 0; preserves std140 member offsets'
                if resource['members']:
                    registers, slots = f'b0,space{uniform_set}', 'uniform 0'
                    new_set, binding = uniform_set, '0'
                    if not active_uniform: note = 'Declared in HLSL; unused and optimized out in both formats'
                elif resource['kind'] == 'texture2D':
                    slot = textures.index(resource)
                    registers, slots = f't{slot},space2', f'sampler {slot} (texture)'
                    new_set, binding = 2, str(slot)
                    note = 'Combined with the sampler at the same SDL slot / SPIR-V binding'
                else:
                    assert resource['kind'] == 'sampler' and textures, resource
                    registers = '; '.join(f's{i},space2' for i in range(len(textures)))
                    slots = '; '.join(f'sampler {i} (sampler)' for i in range(len(textures)))
                    new_set, binding = 2, '; '.join(str(i) for i in range(len(textures)))
                    note = 'Bind the same original sampler at every listed slot; SPIR-V combines it with each texture'
                mapping.append(dict(shader=name, stage=stage, glsl_set=resource['set'], glsl_binding=resource['binding'],
                                    glsl_type=resource['kind'], glsl_name=resource['name'], members=resource['members'],
                                    hlsl_register=registers, sdl_slot=slots, spirv_set=new_set, spirv_binding=binding, note=note))
            info['num_samplers'] = len(textures)
            info['num_uniform_buffers'] = int(active_uniform)
            reports[key] = info
            if args.report_dir: (args.report_dir/f'{key}.dxil.txt').write_text(dump,encoding='utf-8')
        # Vulkan links by location, not by source member name. Ignore optimized-out
        # vertex inputs and outputs; every active fragment input must be provided.
        for location, fragment in pair['fragment']['inputs'].items():
            vertex = pair['vertex']['outputs'][location]
            assert fragment['shape'] == vertex['shape'], (name,location)
            assert fragment['flat'] == vertex['flat'], (name,location,'interpolation')
        for suffix, text in [('vert',stages['vertex'])]:
            locations = {int(n) for n in re.findall(r'layout\s*\(\s*location\s*=\s*(\d+)\s*\)\s*in\s+',text)}
            assert set(pair['vertex']['inputs']) <= locations

    columns = ['shader','stage','glsl_set','glsl_binding','glsl_type','glsl_name','members',
               'hlsl_register','sdl_slot','spirv_set','spirv_binding','note']
    table = tsv(mapping, columns)
    path = SHADERS/'binding-map.tsv'
    if args.write_map: path.write_text(table,encoding='utf-8')
    else: assert path.read_text(encoding='utf-8') == table, 'binding-map.tsv is stale'
    if args.report_dir:
        (args.report_dir/'reflection.json').write_text(json.dumps(reports,indent=2)+'\n',encoding='utf-8')
    print(f'PASS: {len(names)} shaders / {len(binary_names)} binaries / {len(mapping)} original bindings; '
          'SDL GPU slots, combined samplers, uniform offsets, stage interfaces and checksums match.')


if __name__ == '__main__':
    main()
