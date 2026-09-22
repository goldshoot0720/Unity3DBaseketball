"""Convert the supplied GLBs into game-ready FBX assets with Blender 5.x.

Legacy. The roster it names was retired on 2026-09-22 and the current characters ship as
Mixamo-rigged FBX, installed with Tools/prepare_rigged_characters.py instead. Kept for the
next batch that arrives as GLB.

Run: blender --background --python Tools/prepare_characters.py
     blender --background --python Tools/prepare_characters.py -- TeethMei3D FishMei
Sources remain unmodified. This is a reproducible asset conversion, not a rig.
"""
import bpy
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'Assets' / 'Art' / 'Characters'
OUT.mkdir(parents=True, exist_ok=True)
ORDER = (
    'MiaByBy3D', 'MiaBuBu3D', 'GuguGaga3D',
    'TeethMei3D', 'FishMei', 'Huang3D', 'XiaoTu3D',
    'FengHsin3D', 'HsinFeng3D', 'Tu3D',
)
argv = sys.argv
extra = argv[argv.index('--') + 1:] if '--' in argv else []
names = tuple(extra) if extra else ORDER

report_path = ROOT / 'Documentation' / 'asset-conversion.json'
by_name = {}
if report_path.exists():
    for item in json.loads(report_path.read_text(encoding='utf-8')):
        by_name[item['character']] = item

def reset_scene():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    for collection in (bpy.data.meshes, bpy.data.materials, bpy.data.images, bpy.data.armatures):
        for item in list(collection):
            collection.remove(item)

def texture_suffix(image_name):
    lower = image_name.lower()
    if 'basecolor' in lower or 'albedo' in lower or 'diffuse' in lower:
        return 'BaseColor'
    if 'normal' in lower:
        return 'Normal'
    return 'MetalRoughness'

for name in names:
    reset_scene()
    bpy.ops.import_scene.gltf(filepath=str(ROOT / 'SourceAssets' / (name + '.glb')))
    objects = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    before = sum(len(o.data.polygons) for o in objects)
    folder = OUT / name
    folder.mkdir(exist_ok=True)
    for obj in objects:
        bpy.context.view_layer.objects.active = obj
        obj.name = name
        mod = obj.modifiers.new('Game mesh - retain silhouette', 'DECIMATE')
        mod.ratio = 0.04
        bpy.ops.object.modifier_apply(modifier=mod.name)
        for poly in obj.data.polygons:
            poly.use_smooth = True
        for slot in obj.material_slots:
            if not slot.material or not slot.material.use_nodes:
                continue
            slot.material.name = name + '_Surface'
            for node in slot.material.node_tree.nodes:
                if node.type == 'TEX_IMAGE' and node.image:
                    im = node.image
                    suffix = texture_suffix(im.name)
                    im.filepath_raw = str(folder / (name + '_' + suffix + '.png'))
                    im.file_format = 'PNG'
                    im.save()
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:
        obj.select_set(True)
    bpy.ops.export_scene.fbx(filepath=str(folder / (name + '.fbx')), use_selection=True,
        object_types={'MESH'}, axis_forward='-Z', axis_up='Y', apply_unit_scale=True,
        bake_anim=False, add_leaf_bones=False, path_mode='STRIP', use_mesh_modifiers=True)
    by_name[name] = {'character': name, 'sourceTriangles': before,
                     'gameTriangles': sum(len(o.data.polygons) for o in objects),
                     'animations': 'Procedural root motion; source has no rig or animation.'}
    print('CHARACTER_READY', json.dumps(by_name[name]), flush=True)

report = [by_name[n] for n in ORDER if n in by_name]
report_path.parent.mkdir(parents=True, exist_ok=True)
report_path.write_text(json.dumps(report, indent=2), encoding='utf-8')
print('ALL_CHARACTERS_READY', flush=True)
