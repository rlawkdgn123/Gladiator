import argparse
import bpy
import os


PREFIX = "mixamorig:"


def strip_prefix(name: str) -> str:
    if name.startswith(PREFIX):
        return name[len(PREFIX):]
    return name


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--bake-anim", action="store_true")
    sys = __import__("sys")
    args = parser.parse_args(
        sys.argv[sys.argv.index("--") + 1 :]
    )

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=args.input, automatic_bone_orientation=False)

    # Rename bones on every imported armature.
    for obj in bpy.data.objects:
        if obj.type != "ARMATURE":
            continue
        for bone in obj.data.bones:
            new_name = strip_prefix(bone.name)
            if new_name != bone.name:
                bone.name = new_name

    # Keep mesh skinning aligned with renamed bones.
    for obj in bpy.data.objects:
        if obj.type != "MESH":
            continue
        for vg in obj.vertex_groups:
            new_name = strip_prefix(vg.name)
            if new_name != vg.name:
                vg.name = new_name

    os.makedirs(os.path.dirname(args.output), exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=args.output,
        use_selection=False,
        add_leaf_bones=False,
        bake_anim=args.bake_anim,
        path_mode="AUTO",
    )


if __name__ == "__main__":
    main()
