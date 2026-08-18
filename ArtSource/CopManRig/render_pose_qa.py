import bpy
import math
import os
from mathutils import Matrix, Vector


ROOT = os.path.dirname(os.path.abspath(__file__))
SOURCE_FBX = os.environ.get(
    "COPMAN_POSE_SOURCE", os.path.join(ROOT, "dist", "CopMan_Rigged.fbx")
)
OUTPUT_PREFIX = os.environ.get("COPMAN_POSE_PREFIX", "CopMan_Rig")
TEXTURE = os.path.abspath(
    os.path.join(ROOT, "..", "..", "Assets", "Character", "CopMan", "Textures", "texture_0.png")
)
OUTPUT_DIR = os.path.join(ROOT, "dist")


def reset_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)


def point_camera(camera, target):
    camera.rotation_euler = (Vector(target) - camera.location).to_track_quat("-Z", "Y").to_euler()


def ensure_textured_material(mesh):
    material = mesh.data.materials[0] if mesh.data.materials else bpy.data.materials.new("CopMan_Material")
    if not mesh.data.materials:
        mesh.data.materials.append(material)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()

    output = nodes.new("ShaderNodeOutputMaterial")
    shader = nodes.new("ShaderNodeBsdfPrincipled")
    image_node = nodes.new("ShaderNodeTexImage")
    image_node.image = bpy.data.images.load(TEXTURE, check_existing=True)
    links.new(image_node.outputs["Color"], shader.inputs["Base Color"])
    links.new(image_node.outputs["Alpha"], shader.inputs["Alpha"])
    links.new(shader.outputs["BSDF"], output.inputs["Surface"])
    shader.inputs["Roughness"].default_value = 0.82


def rotate_pose_bone(armature, bone_name, axis, angle_degrees):
    pose_bone = armature.pose.bones.get(bone_name)
    if pose_bone is None:
        raise RuntimeError(f"Missing pose bone: {bone_name}")
    bpy.context.view_layer.update()
    pivot = pose_bone.head.copy()
    rotation = Matrix.Rotation(math.radians(angle_degrees), 4, Vector(axis).normalized())
    pose_bone.matrix = Matrix.Translation(pivot) @ rotation @ Matrix.Translation(-pivot) @ pose_bone.matrix
    bpy.context.view_layer.update()


def render(scene, camera, filename, location, target=(0.0, 0.0, 0.5)):
    camera.location = location
    point_camera(camera, target)
    scene.render.filepath = os.path.join(OUTPUT_DIR, filename)
    bpy.ops.render.render(write_still=True)


if not SOURCE_FBX.lower().endswith(".blend"):
    reset_scene()
    bpy.ops.import_scene.fbx(filepath=SOURCE_FBX, automatic_bone_orientation=False)

mesh = next(obj for obj in bpy.context.scene.objects if obj.type == "MESH")
armature = next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")
ensure_textured_material(mesh)

scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE_NEXT"
scene.render.resolution_x = 700
scene.render.resolution_y = 900
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.film_transparent = False
if scene.world is None:
    scene.world = bpy.data.worlds.new("World")
scene.world.color = (0.045, 0.045, 0.045)

camera_data = bpy.data.cameras.new("Camera")
camera_data.type = "ORTHO"
camera_data.ortho_scale = 1.16
camera = bpy.data.objects.new("Camera", camera_data)
scene.collection.objects.link(camera)
scene.camera = camera

for name, energy, size, location in (
    ("Key", 700, 4.0, (-2.0, -2.5, 3.0)),
    ("Fill", 420, 3.0, (2.2, 1.5, 2.1)),
):
    light_data = bpy.data.lights.new(name, "AREA")
    light_data.energy = energy
    light_data.shape = "DISK"
    light_data.size = size
    light = bpy.data.objects.new(name, light_data)
    light.location = location
    scene.collection.objects.link(light)

render(scene, camera, f"{OUTPUT_PREFIX}_BindPose_QA.png", (0.0, -2.5, 0.5))

rotate_pose_bone(armature, "spine_02.x", (1, 0, 0), 5)
rotate_pose_bone(armature, "head.x", (0, 0, 1), 10)
rotate_pose_bone(armature, "arm_stretch.l", (0, 1, 0), 14)
rotate_pose_bone(armature, "arm_stretch.r", (0, 1, 0), -14)
rotate_pose_bone(armature, "forearm_stretch.l", (0, 1, 0), 68)
rotate_pose_bone(armature, "forearm_stretch.r", (0, 1, 0), -68)
rotate_pose_bone(armature, "thigh_stretch.l", (1, 0, 0), 20)
rotate_pose_bone(armature, "leg_stretch.l", (1, 0, 0), -32)
rotate_pose_bone(armature, "thigh_stretch.r", (1, 0, 0), -13)

render(scene, camera, f"{OUTPUT_PREFIX}_Pose_QA_Front.png", (0.0, -2.5, 0.5))
render(scene, camera, f"{OUTPUT_PREFIX}_Pose_QA_ThreeQuarter.png", (1.65, -2.25, 0.58))

print("COPMAN_POSE_QA_RENDERED", OUTPUT_DIR)
