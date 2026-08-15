// DQP Studio - self-contained importer for the bundled Range Rover asset.
// The custom .dqpcar extension intentionally avoids conflicts with Piglet,
// glTFast, UniGLTF, and other .glb ScriptedImporters in the same project.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;
using UnityEngine.Rendering;

namespace DQPStudio.RangeRover
{
    [ScriptedImporter(1, "dqpcar")]
    public sealed class DQPRangeRoverImporter : ScriptedImporter
    {
        public bool importDoorAnimation = true;

        public override void OnImportAsset(AssetImportContext ctx)
        {
            try
            {
                byte[] file = File.ReadAllBytes(ctx.assetPath);
                GlbData glb = GlbData.Read(file);
                GltfRoot gltf = JsonUtility.FromJson<GltfRoot>(glb.json);
                if (gltf == null || gltf.nodes == null || gltf.meshes == null)
                    throw new InvalidDataException("Invalid embedded model JSON.");

                Texture2D[] textures = ImportTextures(ctx, gltf, glb.bin);
                Material[] materials = ImportMaterials(ctx, gltf, textures);
                Mesh[] meshes = ImportMeshes(ctx, gltf, glb.bin);
                GameObject root = BuildHierarchy(gltf, meshes, materials);

                if (importDoorAnimation && gltf.animations != null && gltf.animations.Length > 0)
                    ImportAnimations(ctx, gltf, glb.bin, root);

                ctx.AddObjectToAsset("RangeRover_Main", root);
                ctx.SetMainObject(root);
            }
            catch (Exception exception)
            {
                ctx.LogImportError("DQP Range Rover import failed: " + exception);
            }
        }

        private static Texture2D[] ImportTextures(AssetImportContext ctx, GltfRoot gltf, byte[] bin)
        {
            if (gltf.images == null) return new Texture2D[0];
            Texture2D[] output = new Texture2D[gltf.images.Length];
            for (int i = 0; i < output.Length; i++)
            {
                GltfImage image = gltf.images[i];
                GltfBufferView view = gltf.bufferViews[image.bufferView];
                byte[] bytes = new byte[view.byteLength];
                Buffer.BlockCopy(bin, view.byteOffset, bytes, 0, view.byteLength);
                Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, true, false);
                texture.name = TextureName(i);
                texture.wrapMode = TextureWrapMode.Repeat;
                texture.filterMode = FilterMode.Bilinear;
                if (!texture.LoadImage(bytes, false))
                    throw new InvalidDataException("Cannot decode embedded texture " + i + ".");
                output[i] = texture;
                ctx.AddObjectToAsset("Texture_" + i + "_" + texture.name, texture);
            }
            return output;
        }

        private static string TextureName(int index)
        {
            string[] names = { "BrakeDisc", "BlackLeather", "LightLeather", "Lamp", "Dashboard", "Wood" };
            return index < names.Length ? names[index] : "Texture_" + index;
        }

        private static Material[] ImportMaterials(AssetImportContext ctx, GltfRoot gltf, Texture2D[] images)
        {
            Material[] output = new Material[gltf.materials != null ? gltf.materials.Length : 0];
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            bool urp = shader != null;
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("Neither URP/Lit nor Standard shader is available.");

            for (int i = 0; i < output.Length; i++)
            {
                GltfMaterial source = gltf.materials[i];
                Material target = new Material(shader) { name = string.IsNullOrEmpty(source.name) ? "Material_" + i : source.name };
                GltfPbr pbr = source.pbrMetallicRoughness ?? new GltfPbr();
                Color color = ReadColor(pbr.baseColorFactor, Color.white);
                float metallic = pbr.metallicFactor;
                float smoothness = 1f - pbr.roughnessFactor;

                SetColor(target, urp ? "_BaseColor" : "_Color", color);
                SetFloat(target, "_Metallic", metallic);
                SetFloat(target, urp ? "_Smoothness" : "_Glossiness", smoothness);

                if (pbr.baseColorTexture != null && pbr.baseColorTexture.index >= 0 && pbr.baseColorTexture.index < gltf.textures.Length)
                {
                    Texture2D texture = ResolveTexture(gltf, images, pbr.baseColorTexture.index);
                    if (texture != null) SetTexture(target, urp ? "_BaseMap" : "_MainTex", texture);
                }

                if (source.emissiveFactor != null && source.emissiveFactor.Length >= 3)
                {
                    Color emission = new Color(source.emissiveFactor[0], source.emissiveFactor[1], source.emissiveFactor[2], 1f);
                    SetColor(target, "_EmissionColor", emission);
                    target.EnableKeyword("_EMISSION");
                }
                if (source.emissiveTexture != null && source.emissiveTexture.index >= 0 && source.emissiveTexture.index < gltf.textures.Length)
                {
                    Texture2D emission = ResolveTexture(gltf, images, source.emissiveTexture.index);
                    if (emission != null) SetTexture(target, "_EmissionMap", emission);
                    target.EnableKeyword("_EMISSION");
                }

                if (string.Equals(source.alphaMode, "BLEND", StringComparison.OrdinalIgnoreCase))
                    ConfigureTransparent(target, urp);
                else
                    ConfigureOpaque(target, urp);

                target.doubleSidedGI = source.doubleSided;
                if (source.doubleSided && target.HasProperty("_Cull")) target.SetFloat("_Cull", (float)CullMode.Off);
                output[i] = target;
                ctx.AddObjectToAsset("Material_" + i + "_" + target.name, target);
            }
            return output;
        }

        private static Texture2D ResolveTexture(GltfRoot gltf, Texture2D[] images, int textureIndex)
        {
            if (gltf.textures == null || textureIndex < 0 || textureIndex >= gltf.textures.Length) return null;
            int source = gltf.textures[textureIndex].source;
            return source >= 0 && source < images.Length ? images[source] : null;
        }

        private static void ConfigureOpaque(Material material, bool urp)
        {
            material.renderQueue = -1;
            material.SetOverrideTag("RenderType", "Opaque");
            if (material.HasProperty("_Mode")) material.SetFloat("_Mode", 0f);
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 0f);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 1f);
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHABLEND_ON");
        }

        private static void ConfigureTransparent(Material material, bool urp)
        {
            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetOverrideTag("RenderType", "Transparent");
            if (material.HasProperty("_Mode")) material.SetFloat("_Mode", 3f);
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword(urp ? "_SURFACE_TYPE_TRANSPARENT" : "_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        }

        private static Mesh[] ImportMeshes(AssetImportContext ctx, GltfRoot gltf, byte[] bin)
        {
            Mesh[] output = new Mesh[gltf.meshes.Length];
            for (int meshIndex = 0; meshIndex < output.Length; meshIndex++)
            {
                GltfMesh source = gltf.meshes[meshIndex];
                if (source.primitives == null || source.primitives.Length != 1)
                    throw new NotSupportedException("This bundled asset expects exactly one primitive per mesh.");
                GltfPrimitive primitive = source.primitives[0];
                Vector3[] positions = ReadVec3(gltf, bin, primitive.attributes.POSITION, true);
                Vector3[] normals = ReadVec3(gltf, bin, primitive.attributes.NORMAL, true);
                Vector4[] tangents = ReadTangents(gltf, bin, primitive.attributes.TANGENT);
                Vector2[] uv = ReadVec2(gltf, bin, primitive.attributes.TEXCOORD_0, true);
                int[] sourceIndices = ReadIndices(gltf, bin, primitive.indices);
                int[] triangles = primitive.mode == 5 ? StripToTriangles(sourceIndices) : sourceIndices;

                Mesh mesh = new Mesh();
                mesh.name = "RR_Mesh_" + meshIndex;
                if (positions.Length > 65535) mesh.indexFormat = IndexFormat.UInt32;
                mesh.vertices = positions;
                if (normals.Length == positions.Length) mesh.normals = normals;
                if (tangents.Length == positions.Length) mesh.tangents = tangents;
                if (uv.Length == positions.Length) mesh.uv = uv;
                mesh.triangles = triangles;
                mesh.RecalculateBounds();
                if (normals.Length != positions.Length) mesh.RecalculateNormals();
                output[meshIndex] = mesh;
                ctx.AddObjectToAsset("Mesh_" + meshIndex, mesh);
            }
            return output;
        }

        private static int[] StripToTriangles(int[] strip)
        {
            List<int> triangles = new List<int>(Math.Max(0, strip.Length - 2) * 3);
            for (int i = 0; i + 2 < strip.Length; i++)
            {
                int a = strip[i];
                int b = strip[i + 1];
                int c = strip[i + 2];
                if ((i & 1) != 0) { int swap = a; a = b; b = swap; }
                if (a == b || b == c || a == c) continue;
                // X-axis handedness conversion changes glTF CCW to Unity clockwise.
                triangles.Add(a); triangles.Add(b); triangles.Add(c);
            }
            return triangles.ToArray();
        }

        private static GameObject BuildHierarchy(GltfRoot gltf, Mesh[] meshes, Material[] materials)
        {
            GameObject root = new GameObject("Range_Rover_Material_Fixed");
            GameObject[] nodes = new GameObject[gltf.nodes.Length];
            for (int i = 0; i < nodes.Length; i++)
            {
                GltfNode source = gltf.nodes[i];
                GameObject node = new GameObject(string.IsNullOrEmpty(source.name) ? "Node_" + i : source.name);
                nodes[i] = node;
                node.transform.localPosition = ReadPosition(source.translation);
                node.transform.localRotation = ReadRotation(source.rotation);
                node.transform.localScale = ReadScale(source.scale);

                bool hasMesh = source.name != null && source.name.StartsWith("_gltfNode_", StringComparison.Ordinal);
                if (hasMesh && source.mesh >= 0 && source.mesh < meshes.Length)
                {
                    MeshFilter filter = node.AddComponent<MeshFilter>();
                    MeshRenderer renderer = node.AddComponent<MeshRenderer>();
                    filter.sharedMesh = meshes[source.mesh];
                    int materialIndex = gltf.meshes[source.mesh].primitives[0].material;
                    if (materialIndex >= 0 && materialIndex < materials.Length)
                        renderer.sharedMaterial = materials[materialIndex];
                }
            }

            bool[] isChild = new bool[nodes.Length];
            for (int i = 0; i < gltf.nodes.Length; i++)
            {
                int[] children = gltf.nodes[i].children;
                if (children == null) continue;
                foreach (int child in children)
                {
                    if (child < 0 || child >= nodes.Length) continue;
                    nodes[child].transform.SetParent(nodes[i].transform, false);
                    isChild[child] = true;
                }
            }
            for (int i = 0; i < nodes.Length; i++)
                if (!isChild[i]) nodes[i].transform.SetParent(root.transform, false);
            return root;
        }

        private static void ImportAnimations(AssetImportContext ctx, GltfRoot gltf, byte[] bin, GameObject root)
        {
            for (int animationIndex = 0; animationIndex < gltf.animations.Length; animationIndex++)
            {
                GltfAnimation source = gltf.animations[animationIndex];
                AnimationClip clip = new AnimationClip
                {
                    name = "Door_Animation",
                    legacy = true,
                    wrapMode = WrapMode.ClampForever,
                    frameRate = 30f
                };
                if (source.channels != null)
                {
                    foreach (GltfAnimationChannel channel in source.channels)
                    {
                        if (channel.target == null || channel.target.path != "rotation") continue;
                        GltfAnimationSampler sampler = source.samplers[channel.sampler];
                        float[] times = ReadScalars(gltf, bin, sampler.input);
                        Vector4[] values = ReadQuaternions(gltf, bin, sampler.output);
                        Transform target = FindNodeTransform(root.transform, gltf.nodes[channel.target.node].name);
                        if (target == null) continue;
                        string path = AnimationUtility.CalculateTransformPath(target, root.transform);
                        AnimationCurve x = new AnimationCurve();
                        AnimationCurve y = new AnimationCurve();
                        AnimationCurve z = new AnimationCurve();
                        AnimationCurve w = new AnimationCurve();
                        int count = Math.Min(times.Length, values.Length);
                        for (int i = 0; i < count; i++)
                        {
                            x.AddKey(times[i], values[i].x); y.AddKey(times[i], values[i].y);
                            z.AddKey(times[i], values[i].z); w.AddKey(times[i], values[i].w);
                        }
                        clip.SetCurve(path, typeof(Transform), "localRotation.x", x);
                        clip.SetCurve(path, typeof(Transform), "localRotation.y", y);
                        clip.SetCurve(path, typeof(Transform), "localRotation.z", z);
                        clip.SetCurve(path, typeof(Transform), "localRotation.w", w);
                    }
                }
                clip.EnsureQuaternionContinuity();
                ctx.AddObjectToAsset("Animation_" + animationIndex, clip);
                Animation animation = root.GetComponent<Animation>();
                if (animation == null) animation = root.AddComponent<Animation>();
                animation.AddClip(clip, clip.name);
                animation.clip = clip;
                animation.playAutomatically = false;
            }
        }

        private static Transform FindNodeTransform(Transform root, string nodeName)
        {
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform item in all) if (item.name == nodeName) return item;
            return null;
        }

        private static Vector3[] ReadVec3(GltfRoot gltf, byte[] bin, int accessorIndex, bool flipX)
        {
            if (accessorIndex < 0) return new Vector3[0];
            GltfAccessor accessor = gltf.accessors[accessorIndex];
            Vector3[] output = new Vector3[accessor.count];
            int stride = AccessorStride(gltf, accessor, 12);
            int offset = AccessorOffset(gltf, accessor);
            for (int i = 0; i < output.Length; i++)
            {
                int p = offset + i * stride;
                float x = ReadFloat(bin, p); float y = ReadFloat(bin, p + 4); float z = ReadFloat(bin, p + 8);
                output[i] = flipX ? new Vector3(-x, y, z) : new Vector3(x, y, z);
            }
            return output;
        }

        private static Vector4[] ReadTangents(GltfRoot gltf, byte[] bin, int accessorIndex)
        {
            if (accessorIndex < 0) return new Vector4[0];
            GltfAccessor accessor = gltf.accessors[accessorIndex];
            Vector4[] output = new Vector4[accessor.count];
            int stride = AccessorStride(gltf, accessor, 16);
            int offset = AccessorOffset(gltf, accessor);
            for (int i = 0; i < output.Length; i++)
            {
                int p = offset + i * stride;
                float x = ReadFloat(bin, p); float y = ReadFloat(bin, p + 4); float z = ReadFloat(bin, p + 8); float w = ReadFloat(bin, p + 12);
                output[i] = new Vector4(-x, y, z, -w);
            }
            return output;
        }

        private static Vector4[] ReadQuaternions(GltfRoot gltf, byte[] bin, int accessorIndex)
        {
            if (accessorIndex < 0) return new Vector4[0];
            GltfAccessor accessor = gltf.accessors[accessorIndex];
            Vector4[] output = new Vector4[accessor.count];
            int stride = AccessorStride(gltf, accessor, 16);
            int offset = AccessorOffset(gltf, accessor);
            for (int i = 0; i < output.Length; i++)
            {
                int p = offset + i * stride;
                float x = ReadFloat(bin, p); float y = ReadFloat(bin, p + 4); float z = ReadFloat(bin, p + 8); float w = ReadFloat(bin, p + 12);
                output[i] = new Vector4(x, -y, -z, w);
            }
            return output;
        }

        private static Vector2[] ReadVec2(GltfRoot gltf, byte[] bin, int accessorIndex, bool flipV)
        {
            if (accessorIndex < 0) return new Vector2[0];
            GltfAccessor accessor = gltf.accessors[accessorIndex];
            Vector2[] output = new Vector2[accessor.count];
            int stride = AccessorStride(gltf, accessor, 8);
            int offset = AccessorOffset(gltf, accessor);
            for (int i = 0; i < output.Length; i++)
            {
                int p = offset + i * stride;
                float u = ReadFloat(bin, p); float v = ReadFloat(bin, p + 4);
                output[i] = new Vector2(u, flipV ? 1f - v : v);
            }
            return output;
        }

        private static float[] ReadScalars(GltfRoot gltf, byte[] bin, int accessorIndex)
        {
            GltfAccessor accessor = gltf.accessors[accessorIndex];
            float[] output = new float[accessor.count];
            int stride = AccessorStride(gltf, accessor, 4);
            int offset = AccessorOffset(gltf, accessor);
            for (int i = 0; i < output.Length; i++) output[i] = ReadFloat(bin, offset + i * stride);
            return output;
        }

        private static int[] ReadIndices(GltfRoot gltf, byte[] bin, int accessorIndex)
        {
            GltfAccessor accessor = gltf.accessors[accessorIndex];
            int size = accessor.componentType == 5121 ? 1 : accessor.componentType == 5123 ? 2 : 4;
            int stride = AccessorStride(gltf, accessor, size);
            int offset = AccessorOffset(gltf, accessor);
            int[] output = new int[accessor.count];
            for (int i = 0; i < output.Length; i++)
            {
                int p = offset + i * stride;
                if (accessor.componentType == 5121) output[i] = bin[p];
                else if (accessor.componentType == 5123) output[i] = bin[p] | (bin[p + 1] << 8);
                else if (accessor.componentType == 5125) output[i] = (int)BitConverter.ToUInt32(bin, p);
                else throw new NotSupportedException("Unsupported index component type " + accessor.componentType);
            }
            return output;
        }

        private static int AccessorOffset(GltfRoot gltf, GltfAccessor accessor)
        {
            return gltf.bufferViews[accessor.bufferView].byteOffset + accessor.byteOffset;
        }

        private static int AccessorStride(GltfRoot gltf, GltfAccessor accessor, int packedSize)
        {
            int stride = gltf.bufferViews[accessor.bufferView].byteStride;
            return stride > 0 ? stride : packedSize;
        }

        private static float ReadFloat(byte[] data, int offset) { return BitConverter.ToSingle(data, offset); }
        private static Vector3 ReadPosition(float[] a) { return a != null && a.Length >= 3 ? new Vector3(-a[0], a[1], a[2]) : Vector3.zero; }
        private static Vector3 ReadScale(float[] a) { return a != null && a.Length >= 3 ? new Vector3(a[0], a[1], a[2]) : Vector3.one; }
        private static Quaternion ReadRotation(float[] a) { return a != null && a.Length >= 4 ? new Quaternion(a[0], -a[1], -a[2], a[3]) : Quaternion.identity; }
        private static Color ReadColor(float[] a, Color fallback) { return a != null && a.Length >= 4 ? new Color(a[0], a[1], a[2], a[3]) : fallback; }
        private static void SetFloat(Material m, string p, float v) { if (m.HasProperty(p)) m.SetFloat(p, v); }
        private static void SetColor(Material m, string p, Color v) { if (m.HasProperty(p)) m.SetColor(p, v); }
        private static void SetTexture(Material m, string p, Texture v) { if (m.HasProperty(p)) m.SetTexture(p, v); }

        private sealed class GlbData
        {
            public string json;
            public byte[] bin;

            public static GlbData Read(byte[] file)
            {
                if (file.Length < 28 || ReadUInt(file, 0) != 0x46546C67 || ReadUInt(file, 4) != 2)
                    throw new InvalidDataException("Not a GLB 2.0 payload.");
                int jsonLength = (int)ReadUInt(file, 12);
                if (ReadUInt(file, 16) != 0x4E4F534A) throw new InvalidDataException("Missing GLB JSON chunk.");
                int binHeader = 20 + jsonLength;
                int binLength = (int)ReadUInt(file, binHeader);
                if (ReadUInt(file, binHeader + 4) != 0x004E4942) throw new InvalidDataException("Missing GLB BIN chunk.");
                byte[] binary = new byte[binLength];
                Buffer.BlockCopy(file, binHeader + 8, binary, 0, binLength);
                return new GlbData { json = Encoding.UTF8.GetString(file, 20, jsonLength).TrimEnd(' ', '\0'), bin = binary };
            }

            private static uint ReadUInt(byte[] data, int offset)
            {
                return (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24));
            }
        }
    }

    [Serializable] internal sealed class GltfRoot
    {
        public GltfScene[] scenes; public int scene; public GltfNode[] nodes; public GltfMesh[] meshes;
        public GltfAccessor[] accessors; public GltfBufferView[] bufferViews; public GltfMaterial[] materials;
        public GltfTexture[] textures; public GltfImage[] images; public GltfAnimation[] animations;
    }
    [Serializable] internal sealed class GltfScene { public int[] nodes; }
    [Serializable] internal sealed class GltfNode
    {
        public string name; public int mesh = -1; public int[] children; public float[] translation; public float[] rotation; public float[] scale;
    }
    [Serializable] internal sealed class GltfMesh { public string name; public GltfPrimitive[] primitives; }
    [Serializable] internal sealed class GltfPrimitive
    {
        public GltfAttributes attributes; public int indices = -1; public int material = -1; public int mode = 4;
    }
    [Serializable] internal sealed class GltfAttributes
    {
        public int POSITION = -1; public int NORMAL = -1; public int TANGENT = -1; public int TEXCOORD_0 = -1;
    }
    [Serializable] internal sealed class GltfAccessor
    {
        public int bufferView = -1; public int byteOffset; public int componentType; public int count; public string type; public bool normalized;
    }
    [Serializable] internal sealed class GltfBufferView { public int buffer; public int byteOffset; public int byteLength; public int byteStride; }
    [Serializable] internal sealed class GltfMaterial
    {
        public string name; public bool doubleSided; public string alphaMode; public float[] emissiveFactor;
        public GltfTextureInfo emissiveTexture; public GltfPbr pbrMetallicRoughness;
    }
    [Serializable] internal sealed class GltfPbr
    {
        public float[] baseColorFactor; public float metallicFactor; public float roughnessFactor;
        public GltfTextureInfo baseColorTexture;
    }
    [Serializable] internal sealed class GltfTextureInfo { public int index = -1; }
    [Serializable] internal sealed class GltfTexture { public int source = -1; }
    [Serializable] internal sealed class GltfImage { public int bufferView = -1; public string mimeType; }
    [Serializable] internal sealed class GltfAnimation
    {
        public string name; public GltfAnimationSampler[] samplers; public GltfAnimationChannel[] channels;
    }
    [Serializable] internal sealed class GltfAnimationSampler { public int input; public int output; public string interpolation; }
    [Serializable] internal sealed class GltfAnimationChannel { public int sampler; public GltfAnimationTarget target; }
    [Serializable] internal sealed class GltfAnimationTarget { public int node; public string path; }
}
