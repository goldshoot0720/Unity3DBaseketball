"""Convert the Tripo scene props in SourceAssets into game-ready OBJ assets.

The props arrive as single-object GLBs of one to two million triangles, Y up, base on
the ground plane and normalised to roughly one unit. This decimates each one to a
budget the WebGL and Android builds can carry, scales it to its real-world size and
writes `<PropId>.obj` plus the three PBR maps into `Assets/Art/Environment/Props/`.

No Blender. Run it with a virtual environment:

    python3 -m venv .venv
    .venv/bin/pip install numpy trimesh fast-simplification pillow
    .venv/bin/python Tools/prepare_props.py
    .venv/bin/python Tools/prepare_props.py CourtBench CourtFence

Sources remain unmodified. This is a reproducible asset conversion, not a rig.
"""
import json
import sys
from pathlib import Path

import numpy as np
import trimesh
import fast_simplification

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / 'SourceAssets'
OUT = ROOT / 'Assets' / 'Art' / 'Environment' / 'Props'

# faces: triangle budget. size: metres along axis. The scooter is measured along its
# length and everything else stands on the ground at its own height. The ball matches
# BasketballRules.BallRadius, which is deliberately oversized for a forgiving arc.
PROPS = {
    'CourtBall':       dict(name='籃球',     faces=2878,  axis='y', size=0.400),
    'CourtFence':      dict(name='圍籬',     faces=20000, axis='y', size=3.000),
    'CourtBench':      dict(name='場邊長椅', faces=6000,  axis='y', size=0.920),
    'CourtFloodlight': dict(name='球場燈柱', faces=5000,  axis='y', size=6.000),
    'CourtTrashBin':   dict(name='垃圾桶',   faces=4000,  axis='y', size=0.900),
    'CourtScooter':    dict(name='機車',     faces=8000,  axis='x', size=1.800),
}
AXIS = {'x': 0, 'y': 1, 'z': 2}


def load(path):
    scene = trimesh.load(path, force='scene')
    return trimesh.util.concatenate(list(scene.dump()))


def decimate(vertices, faces, target, passes=6):
    """Collapse edges until the budget is met, carrying a vertex map for the UVs.

    One pass stops early on meshes built from many loose shells - the chain-link
    fence is thousands of separate wire loops - so it runs again on its own result.
    """
    mapping = np.arange(len(vertices), dtype=np.int64)
    for _ in range(passes):
        if len(faces) <= target:
            break
        reduction = min(0.9, max(0.1, 1.0 - target / len(faces)))
        new_vertices, new_faces, collapses = fast_simplification.simplify(
            vertices, faces, reduction, return_collapses=True)
        _, _, step = fast_simplification.replay_simplification(vertices, faces, collapses)
        step = np.asarray(step, dtype=np.int64)
        if len(new_faces) >= len(faces) * 0.98:
            break
        live = mapping >= 0
        mapping[live] = step[mapping[live]]
        vertices, faces = new_vertices, new_faces
    return vertices, faces, mapping


def convert(prop_id, spec):
    mesh = load(SOURCE / (prop_id + '.glb'))
    material = mesh.visual.material
    uv = np.asarray(mesh.visual.uv, dtype=np.float64)
    vertices = np.asarray(mesh.vertices, dtype=np.float32)
    faces = np.asarray(mesh.faces, dtype=np.int32)
    before = len(faces)

    vertices, faces, mapping = decimate(vertices, faces, spec['faces'])
    kept = (mapping >= 0) & (mapping < len(vertices))
    new_uv = np.zeros((len(vertices), 2))
    new_uv[mapping[kept]] = uv[kept]

    out = trimesh.Trimesh(vertices=vertices, faces=faces, process=False)
    # Real-world metres, centred on the ground under its own footprint. Unity's OBJ importer
    # negates X on the way in, so the mesh is mirrored here to arrive the way Tripo drew it -
    # trimesh flips the winding with it. Read these OBJs as left-handed, Unity's own handedness.
    scale = spec['size'] / out.extents[AXIS[spec['axis']]]
    out.apply_scale([-scale, scale, scale])
    low, high = out.bounds
    out.apply_translation([-(low[0] + high[0]) * .5, -low[1], -(low[2] + high[2]) * .5])
    out.visual = trimesh.visual.TextureVisuals(uv=new_uv, material=material)

    folder = OUT / prop_id
    folder.mkdir(parents=True, exist_ok=True)
    obj, textures = trimesh.exchange.obj.export_obj(
        out, include_texture=True, return_texture=True, mtl_name=prop_id + '.mtl')
    written = {}
    for file_name, blob in textures.items():
        if file_name.endswith('.mtl'):
            continue
        written[file_name] = prop_id + '_BaseColor.png'
    for file_name, blob in textures.items():
        target = folder / written.get(file_name, file_name)
        if file_name.endswith('.mtl'):
            text = blob.decode('utf-8') if isinstance(blob, bytes) else blob
            for old, new in written.items():
                text = text.replace(old, new)
            target.write_text(text, encoding='utf-8')
        else:
            target.write_bytes(blob)
    for old, new in written.items():
        obj = obj.replace(old, new)
    (folder / (prop_id + '.obj')).write_text(obj, encoding='utf-8')
    # Only the normal map travels with the base colour. Tripo's metal/roughness map follows the
    # glTF packing - roughness in green, metal in blue - which is not the channel layout URP's
    # Lit shader reads, so shipping it would only mislead. The props use the shader's own values.
    normal = getattr(material, 'normalTexture', None)
    if normal is not None:
        normal.save(folder / f'{prop_id}_Normal.png')

    return dict(prop=prop_id, name=spec['name'], source=prop_id + '.glb',
                triangles=int(len(out.faces)), source_triangles=int(before),
                vertices=int(len(out.vertices)),
                size=[round(float(v), 3) for v in out.extents],
                textures='2048x2048 base colour and normal maps, imported at 1024.')


def main():
    names = sys.argv[1:] or list(PROPS)
    report = []
    for prop_id in names:
        entry = convert(prop_id, PROPS[prop_id])
        report.append(entry)
        print('{prop}: {source_triangles} -> {triangles} triangles, {size} m'.format(**entry))
    path = ROOT / 'Documentation' / 'prop-conversion.json'
    existing = json.loads(path.read_text(encoding='utf-8')) if path.exists() else []
    by_prop = {item['prop']: item for item in existing}
    for entry in report:
        by_prop[entry['prop']] = entry
    ordered = [by_prop[key] for key in PROPS if key in by_prop]
    path.write_text(json.dumps(ordered, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


if __name__ == '__main__':
    main()
