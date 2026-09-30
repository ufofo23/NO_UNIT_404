"""Repair shoe weights in the saved character without rebaking its approved texture."""
import bpy, pathlib

ROOT = pathlib.Path(__file__).resolve().parents[1]
SOURCE = ROOT / 'Docs/Art/ReferenceGuest/ReferenceGuest_Working.blend'
bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
mesh = bpy.data.objects['ReferenceWorker']
rig = mesh.find_armature()
assert rig is not None
for vertex in mesh.data.vertices:
    x, y, z = vertex.co
    if z >= .24:
        continue
    for group in mesh.vertex_groups:
        group.remove([vertex.index])
    side = 'Left' if x >= 0 else 'Right'
    t = max(0., min(1., (z - .16) / .08))
    shin = t * t * (3 - 2 * t)
    mesh.vertex_groups[side + 'FootPivot'].add([vertex.index], 1 - shin, 'REPLACE')
    if shin > 0:
        mesh.vertex_groups[side + 'ShinPivot'].add([vertex.index], shin, 'REPLACE')
bpy.ops.object.select_all(action='DESELECT')
mesh.select_set(True)
rig.select_set(True)
bpy.context.view_layer.objects.active = rig
bpy.ops.export_scene.fbx(
    filepath=str(ROOT / 'Assets/_Project/Resources/NO404/Characters/FirstGuest/FirstGuest.fbx'),
    use_selection=True, object_types={'MESH','ARMATURE'}, axis_forward='-Z', axis_up='Y',
    apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS', bake_space_transform=False,
    add_leaf_bones=False, bake_anim=False, path_mode='AUTO')
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE))
print('Shoe weights repaired; approved mesh shape and texture retained.')
