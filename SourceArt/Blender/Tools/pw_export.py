"""Project FBX export preset. One preset for every agent and human (Asset Production Bible).

Usage inside Blender:
    exec(open(r"<project>/SourceArt/Blender/Tools/pw_export.py").read())
    export_species_model("Rabbit")                 # from BL_Animal_<Species>.blend
    export_animation_library("SmallQuadruped")     # from BL_Rig_<Family>.blend
    export_static("Assets/_Game/Art/Eggs/Models/SM_Egg_Base.fbx", ("SM_Egg_Base_",))  # props, eggs

Only objects in the EXPORT collection are written. The .blend source is never modified.
"""
import os
import bpy

FBX_PRESET = dict(
    use_selection=True,
    apply_unit_scale=True,
    apply_scale_options='FBX_SCALE_UNITS',
    axis_forward='-Z',
    axis_up='Y',
    bake_space_transform=True,
    use_mesh_modifiers=False,
    mesh_smooth_type='FACE',
    use_tspace=False,
    use_triangles=False,
    use_custom_props=False,
    use_armature_deform_only=True,
    add_leaf_bones=False,
    primary_bone_axis='Y',
    secondary_bone_axis='X',
    path_mode='STRIP',
    embed_textures=False,
    colors_type='LINEAR',
)


def project_root():
    path = bpy.data.filepath
    marker = os.sep + "SourceArt" + os.sep
    if marker not in path:
        raise RuntimeError("Open a .blend saved under SourceArt/ before exporting.")
    return path.split(marker)[0]


def _select_export(types):
    export = bpy.data.collections["EXPORT"]
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    chosen = [o for o in export.all_objects if o.type in types]
    for o in chosen:
        o.hide_set(False)
        o.select_set(True)
    armatures = [o for o in chosen if o.type == 'ARMATURE']
    if armatures:
        bpy.context.view_layer.objects.active = armatures[0]
    return chosen


def export_species_model(species):
    out = os.path.join(project_root(), "Assets", "_Game", "Art", "Animals", species, "Models", f"SKM_{species}.fbx")
    os.makedirs(os.path.dirname(out), exist_ok=True)
    arm = next(o for o in bpy.data.collections["EXPORT"].all_objects if o.type == 'ARMATURE')
    if arm.animation_data:
        arm.animation_data.action = None
    for pb in arm.pose.bones:
        pb.location = (0, 0, 0)
        pb.rotation_euler = (0, 0, 0)
        pb.scale = (1, 1, 1)
    chosen = _select_export({'ARMATURE', 'MESH', 'EMPTY'})
    bpy.ops.export_scene.fbx(filepath=out, object_types={'ARMATURE', 'MESH', 'EMPTY'}, bake_anim=False, **FBX_PRESET)
    return out, [o.name for o in chosen]


def export_animation_library(family):
    out = os.path.join(project_root(), "Assets", "_Game", "Art", "Animations", family, f"AN_{family}.fbx")
    os.makedirs(os.path.dirname(out), exist_ok=True)
    chosen = _select_export({'ARMATURE'})
    arm = chosen[0]
    arm.animation_data_create()
    arm.animation_data.action = None
    bpy.ops.export_scene.fbx(
        filepath=out,
        object_types={'ARMATURE'},
        bake_anim=True,
        bake_anim_use_all_bones=True,
        bake_anim_use_nla_strips=False,
        bake_anim_use_all_actions=True,
        bake_anim_force_startend_keying=True,
        bake_anim_step=1.0,
        bake_anim_simplify_factor=0.0,
        **FBX_PRESET,
    )
    return out, [a.name for a in bpy.data.actions if a.name.startswith(f"AN_{family}_")]


def export_static(relative_path, name_prefixes):
    """Static meshes from EXPORT whose names start with one of name_prefixes. Pivots and names kept."""
    out = os.path.join(project_root(), *relative_path.split("/"))
    os.makedirs(os.path.dirname(out), exist_ok=True)
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    chosen = [o for o in bpy.data.collections["EXPORT"].all_objects
              if o.type == 'MESH' and o.name.startswith(tuple(name_prefixes))]
    if not chosen:
        raise RuntimeError("Nothing to export for " + ", ".join(name_prefixes))
    for o in chosen:
        o.hide_set(False)
        o.select_set(True)
    bpy.context.view_layer.objects.active = chosen[0]
    bpy.ops.export_scene.fbx(filepath=out, object_types={'MESH'}, bake_anim=False, **FBX_PRESET)
    return out, [o.name for o in chosen]
