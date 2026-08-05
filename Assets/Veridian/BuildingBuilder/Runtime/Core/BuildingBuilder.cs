using System.Collections.Generic;
using UnityEngine;

namespace Veridian.BuildingSystem.Runtime
{
    public struct FootprintNode
    {
        public Vector3 position;
        public bool isWingPorchWall;
    }

    public class BuildingBuilder
    {
        public enum MatLayer { Walls = 0, Roof = 1, Glass = 2, Masonry = 3, Accents = 4, Frames = 5, Door = 6 }

        public class MeshData
        {
            public List<Vector3> vertices = new List<Vector3>(2048);
            public List<Vector2> uvs = new List<Vector2>(2048);
            public List<Vector3> normals = new List<Vector3>(2048);
            public List<int>[] triangles = new List<int>[7];

            public MeshData() { for (int i = 0; i < 7; i++) triangles[i] = new List<int>(512); }

            public void Clear()
            {
                vertices.Clear(); uvs.Clear(); normals.Clear();
                for (int i = 0; i < 7; i++) triangles[i].Clear();
            }

            public void AddPerimeterWalls(MatLayer layer, List<Vector3> pts, float bottomY, float topY, float uStart, float uvScale)
            {
                float u = uStart;
                for (int i = 0; i < pts.Count; i++)
                {
                    Vector3 p1 = pts[i];
                    Vector3 p2 = pts[(i + 1) % pts.Count];
                    float len = Vector3.Distance(p1, p2);
                    if (len < 0.001f) continue;

                    Vector3 bl = new Vector3(p1.x, bottomY, p1.z);
                    Vector3 tl = new Vector3(p1.x, topY, p1.z);
                    Vector3 br = new Vector3(p2.x, bottomY, p2.z);
                    Vector3 tr = new Vector3(p2.x, topY, p2.z);

                    Vector3 dir = (p2 - p1).normalized;
                    Vector3 normal = Vector3.Cross(Vector3.up, dir).normalized;

                    float uEnd = u + len * uvScale;
                    float vBottom = bottomY * uvScale;
                    float vTop = topY * uvScale;

                    AddQuad(layer, bl, tl, tr, br, normal,
                        new Vector2(u, vBottom), new Vector2(u, vTop),
                        new Vector2(uEnd, vTop), new Vector2(uEnd, vBottom));

                    u = uEnd;
                }
            }

            public void AddQuad(MatLayer layer, Vector3 bl, Vector3 tl, Vector3 tr, Vector3 br, Vector3 normal, Vector2 uv0, Vector2 uv1, Vector2 uv2, Vector2 uv3)
            {
                int start = vertices.Count;
                vertices.Add(bl); vertices.Add(tl); vertices.Add(tr); vertices.Add(br);
                uvs.Add(uv0); uvs.Add(uv1); uvs.Add(uv2); uvs.Add(uv3);
                normals.Add(normal); normals.Add(normal); normals.Add(normal); normals.Add(normal);

                List<int> tris = triangles[(int)layer];
                tris.Add(start); tris.Add(start + 1); tris.Add(start + 2);
                tris.Add(start); tris.Add(start + 2); tris.Add(start + 3);
            }

            public void AddTriangle(MatLayer layer, Vector3 v0, Vector3 v1, Vector3 v2, Vector2 uv0, Vector2 uv1, Vector2 uv2, Vector3 normal)
            {
                int start = vertices.Count;
                vertices.Add(v0); vertices.Add(v1); vertices.Add(v2);
                uvs.Add(uv0); uvs.Add(uv1); uvs.Add(uv2);
                normals.Add(normal); normals.Add(normal); normals.Add(normal);

                List<int> tris = triangles[(int)layer];
                tris.Add(start); tris.Add(start + 1); tris.Add(start + 2);
            }

            public void AddFlatQuad(MatLayer layer, Vector3 bl, Vector3 tl, Vector3 tr, Vector3 br, Vector3 normal, float uvScale)
            {
                AddQuad(layer, bl, tl, tr, br, normal,
                    new Vector2(bl.x, bl.z) * uvScale, new Vector2(tl.x, tl.z) * uvScale,
                    new Vector2(tr.x, tr.z) * uvScale, new Vector2(br.x, br.z) * uvScale);
            }

            public void AddCube(MatLayer layer, Vector3 min, Vector3 max, float uvScale, bool topCap = true, bool bottomCap = true)
            {
                List<Vector3> pts = new List<Vector3> {
                    new Vector3(max.x, 0, max.z), new Vector3(min.x, 0, max.z),
                    new Vector3(min.x, 0, min.z), new Vector3(max.x, 0, min.z)
                };
                AddPerimeterWalls(layer, pts, min.y, max.y, 0f, uvScale);
                if (topCap) AddFlatQuad(layer, new Vector3(min.x, max.y, min.z), new Vector3(min.x, max.y, max.z), new Vector3(max.x, max.y, max.z), new Vector3(max.x, max.y, min.z), Vector3.up, uvScale);
                if (bottomCap) AddFlatQuad(layer, new Vector3(max.x, min.y, min.z), new Vector3(max.x, min.y, max.z), new Vector3(min.x, min.y, max.z), new Vector3(min.x, min.y, min.z), Vector3.down, uvScale);
            }
        }

        private MeshData[] lodPool;

        public BuildingBuilder()
        {
            lodPool = new MeshData[3];
            for (int i = 0; i < 3; i++) lodPool[i] = new MeshData();
        }

        // PHASE 1: Fluid Continuous Footprint Trace mathematically solving Mirrored offset overlaps
        public static List<FootprintNode> GetFootprint(BuildingProfile profile, float offset = 0f)
        {
            float w2 = profile.width / 2f + offset;
            float l2 = profile.length / 2f + offset;

            List<FootprintNode> pts = new List<FootprintNode> {
                new FootprintNode { position = new Vector3(w2, 0, l2) },
                new FootprintNode { position = new Vector3(-w2, 0, l2) },
                new FootprintNode { position = new Vector3(-w2, 0, -l2) }
            };

            if (profile.addWing)
            {
                float maxZOffset = Mathf.Max(0, profile.length / 2f - profile.wingWidth / 2f);
                float wingCenterZ = profile.wingOffset * maxZOffset;
                float zMin = wingCenterZ - profile.wingWidth / 2f - offset;
                float zMax = wingCenterZ + profile.wingWidth / 2f + offset;
                float wingRight = profile.width / 2f + profile.wingLength + offset;

                if (profile.addMirroredBuilding)
                {
                    float mMaxZOffset = Mathf.Max(0, profile.mirroredLength / 2f - profile.wingWidth / 2f);
                    float mCenterZ = wingCenterZ + profile.mirroredOffset * mMaxZOffset;
                    float m_zMin = mCenterZ - profile.mirroredLength / 2f - offset;
                    float m_zMax = mCenterZ + profile.mirroredLength / 2f + offset;
                    float mLeft = profile.width / 2f + profile.wingLength - offset;
                    float mRight = profile.width / 2f + profile.wingLength + profile.mirroredWidth + offset;

                    if (Mathf.Abs(zMin - (-l2)) < 0.01f)
                    {
                        pts.Add(new FootprintNode { position = new Vector3(mLeft, 0, -l2) });
                    }
                    else
                    {
                        pts.Add(new FootprintNode { position = new Vector3(w2, 0, -l2) });
                        pts.Add(new FootprintNode { position = new Vector3(w2, 0, zMin) });
                        pts.Add(new FootprintNode { position = new Vector3(mLeft, 0, zMin) });
                    }

                    if (Mathf.Abs(m_zMin - zMin) >= 0.01f)
                        pts.Add(new FootprintNode { position = new Vector3(mLeft, 0, m_zMin) });

                    pts.Add(new FootprintNode { position = new Vector3(mRight, 0, m_zMin) });
                    pts.Add(new FootprintNode { position = new Vector3(mRight, 0, m_zMax) });

                    if (Mathf.Abs(m_zMax - zMax) >= 0.01f)
                        pts.Add(new FootprintNode { position = new Vector3(mLeft, 0, m_zMax) });

                    pts.Add(new FootprintNode { position = new Vector3(mLeft, 0, zMax) });
                }
                else
                {
                    if (Mathf.Abs(zMin - (-l2)) < 0.01f)
                    {
                        pts.Add(new FootprintNode { position = new Vector3(wingRight, 0, -l2), isWingPorchWall = true });
                    }
                    else
                    {
                        pts.Add(new FootprintNode { position = new Vector3(w2, 0, -l2) });
                        pts.Add(new FootprintNode { position = new Vector3(w2, 0, zMin) });
                        pts.Add(new FootprintNode { position = new Vector3(wingRight, 0, zMin), isWingPorchWall = true });
                    }
                    pts.Add(new FootprintNode { position = new Vector3(wingRight, 0, zMax) });
                }

                if (Mathf.Abs(zMax - l2) >= 0.01f)
                    pts.Add(new FootprintNode { position = new Vector3(w2, 0, zMax) });
            }
            else pts.Add(new FootprintNode { position = new Vector3(w2, 0, -l2) });

            // Redundant flush edges cleanly snapped out of perimeter list
            List<FootprintNode> cleaned = new List<FootprintNode>();
            for (int i = 0; i < pts.Count; i++)
            {
                if (cleaned.Count == 0 || Vector3.Distance(cleaned[cleaned.Count - 1].position, pts[i].position) > 0.001f)
                    cleaned.Add(pts[i]);
            }
            if (cleaned.Count > 1 && Vector3.Distance(cleaned[0].position, cleaned[cleaned.Count - 1].position) < 0.001f)
                cleaned.RemoveAt(cleaned.Count - 1);

            List<FootprintNode> finalPts = new List<FootprintNode>();
            for (int i = 0; i < cleaned.Count; i++)
            {
                FootprintNode prev = cleaned[(i - 1 + cleaned.Count) % cleaned.Count];
                FootprintNode curr = cleaned[i];
                FootprintNode next = cleaned[(i + 1) % cleaned.Count];
                if (Vector3.Angle((curr.position - prev.position).normalized, (next.position - curr.position).normalized) > 0.1f)
                    finalPts.Add(curr);
            }

            return finalPts;
        }

        public MeshData[] Generate(BuildingProfile profile)
        {
            for (int i = 0; i < 3; i++)
            {
                lodPool[i].Clear();
                if (i > 0 && !profile.generateLODGroup) break;
                BuildLOD(lodPool[i], profile, i);
            }
            return lodPool;
        }

        private void BuildLOD(MeshData md, BuildingProfile profile, int lod)
        {
            float baseY = profile.BuildingBaseY;
            float wallBottomY = (lod == 2 && profile.addFoundation) ? 0f : baseY;
            float topY = baseY + profile.TotalWallHeight;
            float uvScale = profile.textureScale;

            bool showAccents = lod < 1;
            bool showFrames = lod < 1;
            bool showGlass = lod < 2;
            bool showDoor = lod < 2;
            bool showMasonry = lod < 2;

            List<FootprintNode> fNodes = GetFootprint(profile);
            List<Vector3> pts = new List<Vector3>(fNodes.Count);
            foreach (var node in fNodes) pts.Add(node.position);

            md.AddPerimeterWalls(MatLayer.Walls, pts, wallBottomY, topY, 0f, uvScale);
            GenerateRoof(md, profile, wallBottomY, topY, uvScale, showAccents);

            if (showMasonry)
            {
                if (profile.addFoundation) GenerateFoundation(md, profile, uvScale);
                if (profile.addChimney && profile.roofType != BuildingProfile.RoofType.Flat) GenerateChimney(md, profile, topY, uvScale);
                if (profile.addPorch) GeneratePorch(md, profile, baseY, uvScale, showAccents, 0);
                if (profile.addBackPorch) GeneratePorch(md, profile, baseY, uvScale, showAccents, 1);

                // Logic Override: The physical presence of a mirrored building occupies the far-wing porch envelope
                if (profile.addWing && profile.addWingPorch && !profile.addMirroredBuilding) GeneratePorch(md, profile, baseY, uvScale, showAccents, 2);
            }

            for (int floor = 0; floor < profile.floorCount; floor++)
            {
                float floorY = baseY + floor * profile.wallHeight;
                GenerateWindowsAndDoors(md, profile, fNodes, floor, floorY, showGlass, showFrames, showDoor, showAccents, uvScale);
            }

            if (showAccents)
            {
                GenerateHorizontalTrims(md, profile, baseY, topY);
                GenerateCornerTrims(md, profile, wallBottomY, topY);
            }
        }

        private void AddRoofPolygon(MeshData md, Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3, Vector3 norm, System.Func<Vector3, Vector2> uvFunc)
        {
            bool d01 = Vector3.Distance(v0, v1) > 0.01f;
            bool d12 = Vector3.Distance(v1, v2) > 0.01f;
            bool d23 = Vector3.Distance(v2, v3) > 0.01f;
            bool d30 = Vector3.Distance(v3, v0) > 0.01f;

            if (!d30 && !d12) return;
            if (!d30) md.AddTriangle(MatLayer.Roof, v0, v1, v2, uvFunc(v0), uvFunc(v1), uvFunc(v2), norm);
            else if (!d12) md.AddTriangle(MatLayer.Roof, v0, v1, v3, uvFunc(v0), uvFunc(v1), uvFunc(v3), norm);
            else md.AddQuad(MatLayer.Roof, v0, v1, v2, v3, norm, uvFunc(v0), uvFunc(v1), uvFunc(v2), uvFunc(v3));
        }

        private void GenerateRoof(MeshData md, BuildingProfile profile, float wallBottomY, float roofBaseY, float uvScale, bool showAccents)
        {
            float W = profile.width; float L = profile.length; float O = profile.roofOverhang;
            float R = profile.roofHeight; float w2 = W / 2f; float l2 = L / 2f;
            float pitch = w2 > 0 ? (R / w2) : 0;
            float drop = O * pitch;
            float eavesY = roofBaseY - drop; float peakY = roofBaseY + R;

            float maxOffset = Mathf.Max(0, l2 - profile.wingWidth / 2f);
            float offsetZ = profile.wingOffset * maxOffset;
            float wingW2 = profile.wingWidth / 2f;
            float R_x = w2 + O; float R_z = wingW2 + O;
            float wingMax = w2 + profile.wingLength + O;

            float sLenMain = Vector2.Distance(new Vector2(0, peakY), new Vector2(R_x, eavesY));
            System.Func<Vector3, Vector2> GetUVMain = (pt) => new Vector2((l2 + O - pt.z) * uvScale, ((pt.y - eavesY) / Mathf.Max(0.001f, peakY - eavesY)) * sLenMain * uvScale);
            System.Func<Vector3, Vector2> GetUVWing = (pt) => new Vector2((pt.x - R_x) * uvScale, ((pt.y - eavesY) / Mathf.Max(0.001f, peakY - eavesY)) * sLenMain * uvScale);

            if (profile.roofType == BuildingProfile.RoofType.Gabled)
            {
                Vector3 eFL = new Vector3(-R_x, eavesY, l2 + O); Vector3 eBL = new Vector3(-R_x, eavesY, -l2 - O);
                Vector3 rF = new Vector3(0, peakY, l2 + O); Vector3 rB = new Vector3(0, peakY, -l2 - O);
                Vector3 eFR = new Vector3(R_x, eavesY, l2 + O); Vector3 eBR = new Vector3(R_x, eavesY, -l2 - O);

                Vector3 normL = Vector3.Cross(rF - eFL, rB - rF).normalized;

                // PHASE 2 FIX: Rewound left roof to enforce Clockwise Culling
                AddRoofPolygon(md, eFL, rF, rB, eBL, normL, GetUVMain);

                if (!profile.addWing)
                {
                    Vector3 normR = Vector3.Cross(rB - eBR, rF - rB).normalized;
                    AddRoofPolygon(md, eBR, rB, rF, eFR, normR, GetUVMain);
                }
                else
                {
                    if (profile.addMirroredBuilding)
                    {
                        float m_w2 = profile.mirroredWidth / 2f;
                        float m_l2 = profile.mirroredLength / 2f;
                        float mCenterX = profile.width / 2f + profile.wingLength + m_w2;

                        float mMaxZOffset = Mathf.Max(0, m_l2 - profile.wingWidth / 2f);
                        float mCenterZ = offsetZ + profile.mirroredOffset * mMaxZOffset;

                        // Force the mirrored building to flawlessly inherit the exact roof angle and intercept it dynamically
                        float m_peakY = roofBaseY + pitch * m_w2;

                        Vector3 V_main = new Vector3(0, peakY, offsetZ);
                        Vector3 V_mirrored = new Vector3(mCenterX, m_peakY, offsetZ);

                        Vector3 E_wF = new Vector3(R_x, eavesY, offsetZ + R_z);
                        Vector3 E_wB = new Vector3(R_x, eavesY, offsetZ - R_z);
                        Vector3 mE_wF = new Vector3(mCenterX - m_w2 - O, eavesY, offsetZ + R_z);
                        Vector3 mE_wB = new Vector3(mCenterX - m_w2 - O, eavesY, offsetZ - R_z);

                        Vector3 m_rF = new Vector3(mCenterX, m_peakY, mCenterZ + m_l2 + O);
                        Vector3 m_rB = new Vector3(mCenterX, m_peakY, mCenterZ - m_l2 - O);
                        Vector3 m_eFL = new Vector3(mCenterX - m_w2 - O, eavesY, mCenterZ + m_l2 + O);
                        Vector3 m_eBL = new Vector3(mCenterX - m_w2 - O, eavesY, mCenterZ - m_l2 - O);
                        Vector3 m_eFR = new Vector3(mCenterX + m_w2 + O, eavesY, mCenterZ + m_l2 + O);
                        Vector3 m_eBR = new Vector3(mCenterX + m_w2 + O, eavesY, mCenterZ - m_l2 - O);

                        float m_sLenMain = Vector2.Distance(new Vector2(0, m_peakY), new Vector2(m_w2 + O, eavesY));
                        System.Func<Vector3, Vector2> GetUVMirrored = (pt) => new Vector2((mCenterZ + m_l2 + O - pt.z) * uvScale, ((pt.y - eavesY) / Mathf.Max(0.001f, m_peakY - eavesY)) * m_sLenMain * uvScale);

                        // Mirrored valley triangulation safeguards entirely against non-planar quads
                        Vector3 normWFront = Vector3.Cross(V_mirrored - mE_wF, V_main - V_mirrored).normalized;
                        AddRoofPolygon(md, mE_wF, V_mirrored, V_main, E_wF, normWFront, GetUVWing);

                        Vector3 normWBack = Vector3.Cross(V_main - E_wB, V_mirrored - V_main).normalized;
                        AddRoofPolygon(md, E_wB, V_main, V_mirrored, mE_wB, normWBack, GetUVWing);

                        Vector3 normR = Vector3.Cross(V_main - E_wF, rF - V_main).normalized;
                        AddRoofPolygon(md, E_wF, V_main, rF, eFR, normR, GetUVMain);
                        AddRoofPolygon(md, eBR, rB, V_main, E_wB, normR, GetUVMain);

                        Vector3 normML = Vector3.Cross(m_rF - m_eFL, V_mirrored - m_rF).normalized;
                        AddRoofPolygon(md, m_eFL, m_rF, V_mirrored, mE_wF, normML, GetUVMirrored);
                        AddRoofPolygon(md, mE_wB, V_mirrored, m_rB, m_eBL, normML, GetUVMirrored);

                        Vector3 normMR = Vector3.Cross(m_rB - m_eBR, m_rF - m_rB).normalized;
                        AddRoofPolygon(md, m_eBR, m_rB, m_rF, m_eFR, normMR, GetUVMirrored);

                        md.AddTriangle(MatLayer.Walls, new Vector3(mCenterX + m_w2, roofBaseY, mCenterZ + m_l2), m_rF, new Vector3(mCenterX - m_w2, roofBaseY, mCenterZ + m_l2), new Vector2((mCenterX + m_w2) * uvScale, 0), new Vector2(mCenterX * uvScale, (m_peakY - roofBaseY) * uvScale), new Vector2((mCenterX - m_w2) * uvScale, 0), Vector3.forward);
                        md.AddTriangle(MatLayer.Walls, new Vector3(mCenterX - m_w2, roofBaseY, mCenterZ - m_l2), m_rB, new Vector3(mCenterX + m_w2, roofBaseY, mCenterZ - m_l2), new Vector2(-(mCenterX - m_w2) * uvScale, 0), new Vector2(-mCenterX * uvScale, (m_peakY - roofBaseY) * uvScale), new Vector2(-(mCenterX + m_w2) * uvScale, 0), Vector3.back);

                        if (showAccents && profile.addFasciaTrim)
                        {
                            float fh = profile.fasciaHeight; float bias = profile.detailOffsetBias;
                            if (Vector3.Distance(E_wF, eFR) > 0.01f) AddFasciaQuad(md, eFR, E_wF, fh, Vector3.right, bias, true, uvScale);
                            if (Vector3.Distance(eBR, E_wB) > 0.01f) AddFasciaQuad(md, E_wB, eBR, fh, Vector3.right, bias, true, uvScale);

                            AddFasciaQuad(md, m_eFL, m_rF, fh, Vector3.forward, bias, true, uvScale);
                            AddFasciaQuad(md, m_rF, m_eFR, fh, Vector3.forward, bias, true, uvScale);
                            AddFasciaQuad(md, m_eBR, m_rB, fh, Vector3.back, bias, true, uvScale);
                            AddFasciaQuad(md, m_rB, m_eBL, fh, Vector3.back, bias, true, uvScale);
                            AddFasciaQuad(md, m_eFR, m_eBR, fh, Vector3.right, bias, true, uvScale);

                            if (Vector3.Distance(mE_wF, m_eFL) > 0.01f) AddFasciaQuad(md, m_eFL, mE_wF, fh, Vector3.left, bias, true, uvScale);
                            if (Vector3.Distance(m_eBL, mE_wB) > 0.01f) AddFasciaQuad(md, mE_wB, m_eBL, fh, Vector3.left, bias, true, uvScale);

                            AddFasciaQuad(md, E_wF, mE_wF, fh, Vector3.forward, bias, true, uvScale);
                            AddFasciaQuad(md, mE_wB, E_wB, fh, Vector3.back, bias, true, uvScale);
                        }
                    }
                    else
                    {
                        Vector3 V = new Vector3(0, peakY, offsetZ);
                        Vector3 wR = new Vector3(wingMax, peakY, offsetZ);
                        Vector3 E_wF = new Vector3(R_x, eavesY, offsetZ + R_z);
                        Vector3 E_wB = new Vector3(R_x, eavesY, offsetZ - R_z);
                        Vector3 oE_wF = new Vector3(wingMax, eavesY, offsetZ + R_z);
                        Vector3 oE_wB = new Vector3(wingMax, eavesY, offsetZ - R_z);

                        Vector3 normR = Vector3.Cross(V - E_wF, rF - V).normalized;
                        AddRoofPolygon(md, E_wF, V, rF, eFR, normR, GetUVMain);
                        AddRoofPolygon(md, eBR, rB, V, E_wB, normR, GetUVMain);

                        Vector3 normWFront = Vector3.Cross(wR - oE_wF, V - wR).normalized;
                        AddRoofPolygon(md, oE_wF, wR, V, E_wF, normWFront, GetUVWing);

                        Vector3 normWBack = Vector3.Cross(V - E_wB, wR - V).normalized;
                        AddRoofPolygon(md, E_wB, V, wR, oE_wB, normWBack, GetUVWing);

                        Vector3 g_wB = new Vector3(wingMax - O, roofBaseY, offsetZ - wingW2);
                        Vector3 g_wPeak = new Vector3(wingMax - O, peakY, offsetZ);
                        Vector3 g_wF = new Vector3(wingMax - O, roofBaseY, offsetZ + wingW2);
                        md.AddTriangle(MatLayer.Walls, g_wB, g_wPeak, g_wF, new Vector2((offsetZ - wingW2) * uvScale, 0), new Vector2(offsetZ * uvScale, (peakY - roofBaseY) * uvScale), new Vector2((offsetZ + wingW2) * uvScale, 0), Vector3.right);

                        if (showAccents && profile.addFasciaTrim)
                        {
                            float fh = profile.fasciaHeight; float bias = profile.detailOffsetBias;

                            if (Vector3.Distance(E_wF, eFR) > 0.01f) AddFasciaQuad(md, eFR, E_wF, fh, Vector3.right, bias, true, uvScale);
                            if (Vector3.Distance(eBR, E_wB) > 0.01f) AddFasciaQuad(md, E_wB, eBR, fh, Vector3.right, bias, true, uvScale);

                            AddFasciaQuad(md, E_wF, oE_wF, fh, Vector3.forward, bias, true, uvScale);
                            AddFasciaQuad(md, oE_wB, E_wB, fh, Vector3.back, bias, true, uvScale);
                            AddFasciaQuad(md, oE_wF, wR, fh, Vector3.right, bias, true, uvScale);
                            AddFasciaQuad(md, wR, oE_wB, fh, Vector3.right, bias, true, uvScale);
                        }
                    }
                }

                md.AddTriangle(MatLayer.Walls, new Vector3(w2, roofBaseY, l2), new Vector3(0, peakY, l2), new Vector3(-w2, roofBaseY, l2), new Vector2(w2 * uvScale, 0), new Vector2(0, (peakY - roofBaseY) * uvScale), new Vector2(-w2 * uvScale, 0), Vector3.forward);
                md.AddTriangle(MatLayer.Walls, new Vector3(-w2, roofBaseY, -l2), new Vector3(0, peakY, -l2), new Vector3(w2, roofBaseY, -l2), new Vector2(-w2 * uvScale, 0), new Vector2(0, (peakY - roofBaseY) * uvScale), new Vector2(w2 * uvScale, 0), Vector3.back);

                if (showAccents && profile.addFasciaTrim)
                {
                    float fh = profile.fasciaHeight; float bias = profile.detailOffsetBias;
                    AddFasciaQuad(md, eFL, rF, fh, Vector3.forward, bias, true, uvScale); AddFasciaQuad(md, rF, eFR, fh, Vector3.forward, bias, true, uvScale);
                    AddFasciaQuad(md, eBR, rB, fh, Vector3.back, bias, true, uvScale); AddFasciaQuad(md, rB, eBL, fh, Vector3.back, bias, true, uvScale);
                    AddFasciaQuad(md, eBL, eFL, fh, Vector3.left, bias, true, uvScale);
                    if (!profile.addWing) AddFasciaQuad(md, eFR, eBR, fh, Vector3.right, bias, true, uvScale);
                }
            }
            else
            {
                md.AddFlatQuad(MatLayer.Roof, new Vector3(-R_x, roofBaseY, -l2 - O), new Vector3(-R_x, roofBaseY, l2 + O), new Vector3(R_x, roofBaseY, l2 + O), new Vector3(R_x, roofBaseY, -l2 - O), Vector3.up, uvScale);

                if (profile.addWing)
                {
                    if (profile.addMirroredBuilding)
                    {
                        float mWidth = profile.mirroredWidth;
                        float mLength = profile.mirroredLength;
                        float mMaxZOffset = Mathf.Max(0, mLength / 2f - profile.wingWidth / 2f);
                        float mCenterZ = offsetZ + profile.mirroredOffset * mMaxZOffset;

                        float mZMin = mCenterZ - mLength / 2f - O;
                        float mZMax = mCenterZ + mLength / 2f + O;
                        float mLeft = profile.width / 2f + profile.wingLength - O;
                        float mRight = profile.width / 2f + profile.wingLength + mWidth + O;

                        // Quads gracefully snap adjacent avoiding redundant Z-Overlap
                        md.AddFlatQuad(MatLayer.Roof, new Vector3(R_x, roofBaseY, offsetZ - R_z), new Vector3(R_x, roofBaseY, offsetZ + R_z), new Vector3(mLeft, roofBaseY, offsetZ + R_z), new Vector3(mLeft, roofBaseY, offsetZ - R_z), Vector3.up, uvScale);
                        md.AddFlatQuad(MatLayer.Roof, new Vector3(mLeft, roofBaseY, mZMin), new Vector3(mLeft, roofBaseY, mZMax), new Vector3(mRight, roofBaseY, mZMax), new Vector3(mRight, roofBaseY, mZMin), Vector3.up, uvScale);
                    }
                    else
                    {
                        md.AddFlatQuad(MatLayer.Roof, new Vector3(R_x, roofBaseY, offsetZ - R_z), new Vector3(R_x, roofBaseY, offsetZ + R_z), new Vector3(wingMax, roofBaseY, offsetZ + R_z), new Vector3(wingMax, roofBaseY, offsetZ - R_z), Vector3.up, uvScale);
                    }
                }

                if (showAccents && profile.addFasciaTrim)
                {
                    float fh = profile.fasciaHeight; float bias = profile.detailOffsetBias;
                    List<FootprintNode> traceNodes = GetFootprint(profile, O);
                    for (int i = 0; i < traceNodes.Count; i++)
                    {
                        Vector3 p1 = traceNodes[i].position; Vector3 p2 = traceNodes[(i + 1) % traceNodes.Count].position;
                        p1.y = roofBaseY; p2.y = roofBaseY;
                        Vector3 dir = (p2 - p1).normalized; Vector3 norm = Vector3.Cross(Vector3.up, dir).normalized;
                        AddFasciaQuad(md, p2, p1, fh, norm, bias, true, uvScale);
                    }
                }
            }
        }

        private void GenerateFoundation(MeshData md, BuildingProfile profile, float uvScale)
        {
            float ext = profile.foundationExtension; float H = profile.foundationHeight;
            List<FootprintNode> fNodes = GetFootprint(profile, ext);
            List<Vector3> pts = new List<Vector3>(fNodes.Count);
            foreach (var node in fNodes) pts.Add(node.position);

            md.AddPerimeterWalls(MatLayer.Masonry, pts, 0, H, 0f, uvScale);

            float w2 = profile.width / 2f; float l2 = profile.length / 2f;
            md.AddFlatQuad(MatLayer.Masonry, new Vector3(-w2 - ext, H, -l2 - ext), new Vector3(-w2 - ext, H, l2 + ext), new Vector3(w2 + ext, H, l2 + ext), new Vector3(w2 + ext, H, -l2 - ext), Vector3.up, uvScale);
            md.AddFlatQuad(MatLayer.Masonry, new Vector3(w2 + ext, 0, -l2 - ext), new Vector3(w2 + ext, 0, l2 + ext), new Vector3(-w2 - ext, 0, l2 + ext), new Vector3(-w2 - ext, 0, -l2 - ext), Vector3.down, uvScale);

            if (profile.addWing)
            {
                float maxOffset = Mathf.Max(0, l2 - profile.wingWidth / 2f); float offsetZ = profile.wingOffset * maxOffset;
                float wZ1 = offsetZ - profile.wingWidth / 2f; float wZ2 = offsetZ + profile.wingWidth / 2f;
                float mxLeft = profile.width / 2f + profile.wingLength;

                float quadWingR = profile.addMirroredBuilding ? mxLeft - ext : mxLeft + ext;
                md.AddFlatQuad(MatLayer.Masonry, new Vector3(w2 + ext, H, wZ1 - ext), new Vector3(w2 + ext, H, wZ2 + ext), new Vector3(quadWingR, H, wZ2 + ext), new Vector3(quadWingR, H, wZ1 - ext), Vector3.up, uvScale);
                md.AddFlatQuad(MatLayer.Masonry, new Vector3(quadWingR, 0, wZ1 - ext), new Vector3(quadWingR, 0, wZ2 + ext), new Vector3(w2 + ext, 0, wZ2 + ext), new Vector3(w2 + ext, 0, wZ1 - ext), Vector3.down, uvScale);

                if (profile.addMirroredBuilding)
                {
                    float m_w2 = profile.mirroredWidth / 2f;
                    float m_l2 = profile.mirroredLength / 2f;
                    float mCenterX = mxLeft + m_w2;
                    float mZOffset = profile.mirroredOffset * (m_l2 - profile.wingWidth / 2f);
                    float mCenterZ = offsetZ + mZOffset;

                    md.AddFlatQuad(MatLayer.Masonry, new Vector3(mCenterX - m_w2 - ext, H, mCenterZ - m_l2 - ext), new Vector3(mCenterX - m_w2 - ext, H, mCenterZ + m_l2 + ext), new Vector3(mCenterX + m_w2 + ext, H, mCenterZ + m_l2 + ext), new Vector3(mCenterX + m_w2 + ext, H, mCenterZ - m_l2 - ext), Vector3.up, uvScale);
                    md.AddFlatQuad(MatLayer.Masonry, new Vector3(mCenterX + m_w2 + ext, 0, mCenterZ - m_l2 - ext), new Vector3(mCenterX + m_w2 + ext, 0, mCenterZ + m_l2 + ext), new Vector3(mCenterX - m_w2 - ext, 0, mCenterZ + m_l2 + ext), new Vector3(mCenterX - m_w2 - ext, 0, mCenterZ - m_l2 - ext), Vector3.down, uvScale);
                }
            }

            if (profile.addPorch) GeneratePorchFoundation(md, profile, uvScale, H, 0);
            if (profile.addBackPorch) GeneratePorchFoundation(md, profile, uvScale, H, 1);
            if (profile.addWing && profile.addWingPorch && !profile.addMirroredBuilding) GeneratePorchFoundation(md, profile, uvScale, H, 2);
        }

        private void GeneratePorchFoundation(MeshData md, BuildingProfile profile, float uvScale, float H, int type)
        {
            float ext = profile.foundationExtension; float dx = -profile.doorOffsetFromCenter;
            Vector3 c = Vector3.zero; Vector3 fwd = Vector3.forward; Vector3 rht = Vector3.left; float len = profile.width;

            if (type == 0) { c = new Vector3(0, 0, profile.length / 2f + ext); }
            else if (type == 1) { c = new Vector3(0, 0, -profile.length / 2f - ext); fwd = Vector3.back; rht = Vector3.right; dx = profile.doorOffsetFromCenter; }
            else
            {
                c = new Vector3(profile.width / 2f + profile.wingLength + ext, 0, profile.wingOffset * Mathf.Max(0, profile.length / 2f - profile.wingWidth / 2f));
                fwd = Vector3.right; rht = Vector3.back; len = profile.wingWidth; dx = 0;
            }

            float pw = Mathf.Clamp(profile.porchWidth, 0f, len); Vector3 dC = c + rht * dx;
            Vector3 bl = dC + rht * (-pw / 2f - ext); Vector3 br = dC + rht * (pw / 2f + ext);
            Vector3 tl = bl + fwd * profile.porchDepth; Vector3 tr = br + fwd * profile.porchDepth;
            md.AddCube(MatLayer.Masonry, new Vector3(Mathf.Min(bl.x, tr.x), 0, Mathf.Min(bl.z, tr.z)), new Vector3(Mathf.Max(bl.x, tr.x), H, Mathf.Max(bl.z, tr.z)), uvScale);
        }

        private void GeneratePorch(MeshData md, BuildingProfile profile, float baseY, float uvScale, bool accents, int type)
        {
            float dx = -profile.doorOffsetFromCenter; float ps = profile.porchColumnSize; float pd = profile.porchDepth;
            float pTop = baseY + profile.porchHeight; float pH = pTop - baseY;
            Vector3 c = Vector3.zero; Vector3 fwd = Vector3.forward; Vector3 rht = Vector3.left; float len = profile.width;

            if (type == 0) { c = new Vector3(0, baseY, profile.length / 2f); }
            else if (type == 1) { c = new Vector3(0, baseY, -profile.length / 2f); fwd = Vector3.back; rht = Vector3.right; dx = profile.doorOffsetFromCenter; }
            else { c = new Vector3(profile.width / 2f + profile.wingLength, baseY, profile.wingOffset * Mathf.Max(0, profile.length / 2f - profile.wingWidth / 2f)); fwd = Vector3.right; rht = Vector3.back; len = profile.wingWidth; dx = 0; }

            float pw = Mathf.Clamp(profile.porchWidth, 0f, len); Vector3 dC = c + rht * dx;
            Vector3 lPil = dC + rht * (-pw / 2f + ps / 2f) + fwd * (pd - ps / 2f) + Vector3.up * (pH / 2f);
            Vector3 rPil = dC + rht * (pw / 2f - ps / 2f) + fwd * (pd - ps / 2f) + Vector3.up * (pH / 2f);

            md.AddCube(MatLayer.Walls, lPil - new Vector3(ps / 2f, pH / 2f, ps / 2f), lPil + new Vector3(ps / 2f, pH / 2f, ps / 2f), uvScale, false, false);
            md.AddCube(MatLayer.Walls, rPil - new Vector3(ps / 2f, pH / 2f, ps / 2f), rPil + new Vector3(ps / 2f, pH / 2f, ps / 2f), uvScale, false, false);

            float o = 0.1f;
            Vector3 rBL = dC + rht * (-pw / 2f - o) - fwd * o; Vector3 rBR = dC + rht * (pw / 2f + o) - fwd * o;
            Vector3 rTL = rBL + fwd * (pd + o * 2f); Vector3 rTR = rBR + fwd * (pd + o * 2f);

            md.AddFlatQuad(MatLayer.Roof, new Vector3(Mathf.Min(rBL.x, rTR.x), pTop, Mathf.Min(rBL.z, rTR.z)), new Vector3(Mathf.Min(rBL.x, rTR.x), pTop, Mathf.Max(rBL.z, rTR.z)), new Vector3(Mathf.Max(rBL.x, rTR.x), pTop, Mathf.Max(rBL.z, rTR.z)), new Vector3(Mathf.Max(rBL.x, rTR.x), pTop, Mathf.Min(rBL.z, rTR.z)), Vector3.up, uvScale);
            md.AddFlatQuad(MatLayer.Roof, new Vector3(Mathf.Min(rBL.x, rTR.x), pTop - profile.detailOffsetBias, Mathf.Min(rBL.z, rTR.z)), new Vector3(Mathf.Min(rBL.x, rTR.x), pTop - profile.detailOffsetBias, Mathf.Max(rBL.z, rTR.z)), new Vector3(Mathf.Max(rBL.x, rTR.x), pTop - profile.detailOffsetBias, Mathf.Max(rBL.z, rTR.z)), new Vector3(Mathf.Max(rBL.x, rTR.x), pTop - profile.detailOffsetBias, Mathf.Min(rBL.z, rTR.z)), Vector3.down, uvScale);

            if (accents && profile.addFasciaTrim)
            {
                rBL.y = pTop; rTL.y = pTop; rTR.y = pTop; rBR.y = pTop;
                AddFasciaQuad(md, rBL, rTL, profile.fasciaHeight, rht, profile.detailOffsetBias, true, uvScale);
                AddFasciaQuad(md, rTL, rTR, profile.fasciaHeight, fwd, profile.detailOffsetBias, true, uvScale);
                AddFasciaQuad(md, rTR, rBR, profile.fasciaHeight, -rht, profile.detailOffsetBias, true, uvScale);
            }
        }

        private void GenerateChimney(MeshData md, BuildingProfile profile, float wallTopY, float uvScale)
        {
            float posX = profile.width * 0.2f; float posZ = -profile.length * 0.25f; float R = profile.roofHeight;
            float h = profile.roofType == BuildingProfile.RoofType.Gabled ? (R - (Mathf.Abs(posX) * (R / (profile.width / 2)))) : 0;
            float bY = profile.roofType == BuildingProfile.RoofType.Gabled ? wallTopY : wallTopY - 0.1f;
            md.AddCube(MatLayer.Masonry, new Vector3(posX - profile.chimneySize.x / 2, bY, posZ - profile.chimneySize.z / 2), new Vector3(posX + profile.chimneySize.x / 2, wallTopY + h + profile.chimneySize.y, posZ + profile.chimneySize.z / 2), uvScale, true, false);
        }

        private void GenerateWindowsAndDoors(MeshData md, BuildingProfile profile, List<FootprintNode> nodes, int floor, float floorY, bool glass, bool frames, bool sDoor, bool accents, float uvScale)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                Vector3 start = nodes[i].position;
                Vector3 end = nodes[(i + 1) % nodes.Count].position;
                start.y = floorY; end.y = floorY;

                Vector3 dir = (end - start).normalized;
                Vector3 norm = Vector3.Cross(Vector3.up, dir).normalized;

                int baseWinCount = 0; bool isDoorActive = false;
                float globalFacadeLength = 1f;

                float dF = Vector3.Dot(norm, Vector3.forward); float dB = Vector3.Dot(norm, Vector3.back);
                float dL = Vector3.Dot(norm, Vector3.left); float dR = Vector3.Dot(norm, Vector3.right);

                // Dynamically detects if normal resides on the main body or the mirrored building body
                if (dF > 0.5f)
                {
                    baseWinCount = profile.windowsFront; globalFacadeLength = profile.width;
                    if (start.x >= profile.width / 2f) globalFacadeLength = profile.mirroredWidth;
                    if (end.x < 0f && profile.addDoor) isDoorActive = sDoor;
                }
                else if (dB > 0.5f)
                {
                    baseWinCount = profile.windowsBack; globalFacadeLength = profile.width;
                    if (start.x >= profile.width / 2f) globalFacadeLength = profile.mirroredWidth;
                    if (start.x < 0f && profile.addBackDoor) isDoorActive = sDoor;
                }
                else if (dL > 0.5f)
                {
                    baseWinCount = profile.windowsLeft; globalFacadeLength = profile.length;
                    if (start.x >= profile.width / 2f) globalFacadeLength = profile.mirroredLength;
                }
                else if (dR > 0.5f)
                {
                    baseWinCount = profile.windowsRight; globalFacadeLength = profile.length;
                    if (start.x >= profile.width / 2f) globalFacadeLength = profile.mirroredLength;
                    if (nodes[i].isWingPorchWall && profile.addWingDoor && !profile.addMirroredBuilding) isDoorActive = sDoor;
                }

                float wallSegmentLength = Vector3.Distance(start, end);
                float targetSpacing = globalFacadeLength / (baseWinCount + 1f);
                int adaptiveWinCount = Mathf.Max(0, Mathf.RoundToInt((wallSegmentLength / targetSpacing) - 1));

                float physicalWindowWidth = profile.windowWidth + (profile.windowFrameWidth * 2f) + 0.2f;
                int maxPhysicalWindows = Mathf.FloorToInt(wallSegmentLength / physicalWindowWidth);
                adaptiveWinCount = Mathf.Min(adaptiveWinCount, maxPhysicalWindows);

                PlaceDecals(md, profile, start, end, norm, dir, floor, adaptiveWinCount, glass, frames, isDoorActive, accents, uvScale);
            }
        }

        private void PlaceDecals(MeshData md, BuildingProfile p, Vector3 start, Vector3 end, Vector3 norm, Vector3 right, int floor, int winC, bool sGlass, bool sFrame, bool doorActive, bool sAccents, float uvScale)
        {
            float len = Vector3.Distance(start, end); float doorMin = 999f; float doorMax = -999f;

            if (doorActive && floor == 0)
            {
                float doorLocalX = len / 2f;

                if (Vector3.Dot(norm, Vector3.forward) > 0.5f) doorLocalX = start.x - p.doorOffsetFromCenter;
                else if (Vector3.Dot(norm, Vector3.back) > 0.5f) doorLocalX = p.doorOffsetFromCenter - start.x;

                Vector3 dC = start + right * doorLocalX + Vector3.up * (p.doorHeight / 2f);
                doorMin = doorLocalX - p.doorWidth / 2f - p.doorFrameWidth; doorMax = doorLocalX + p.doorWidth / 2f + p.doorFrameWidth;

                if (sFrame && p.addDoorFrame) AddDecalQuad(md, MatLayer.Frames, dC + Vector3.up * (p.doorFrameWidth / 2f), right, Vector3.up, norm, p.doorWidth + p.doorFrameWidth * 2f, p.doorHeight + p.doorFrameWidth, p.detailOffsetBias, false, uvScale);
                AddDecalQuad(md, MatLayer.Door, dC, right, Vector3.up, norm, p.doorWidth, p.doorHeight, p.detailOffsetBias * 2f, false, uvScale);
            }

            if (winC > 0 && (sGlass || sFrame))
            {
                float spacing = len / (winC + 1);
                for (int i = 0; i < winC; i++)
                {
                    float locX = spacing * (i + 1);
                    if (doorActive && floor == 0 && (locX + p.windowWidth / 2f + p.windowFrameWidth > doorMin && locX - p.windowWidth / 2f - p.windowFrameWidth < doorMax)) continue;
                    Vector3 wC = start + right * locX + Vector3.up * (p.windowSillHeight + p.windowHeight / 2f);

                    if (sFrame && p.addWindowFrames) AddDecalQuad(md, MatLayer.Frames, wC, right, Vector3.up, norm, p.windowWidth + p.windowFrameWidth * 2f, p.windowHeight + p.windowFrameWidth * 2f, p.detailOffsetBias, false, uvScale);
                    if (sGlass) AddDecalQuad(md, MatLayer.Glass, wC, right, Vector3.up, norm, p.windowWidth, p.windowHeight, p.detailOffsetBias * 2f, false, uvScale);
                }
            }

            if (sAccents && p.addVerticalTrim && winC > 1)
            {
                float spacing = len / (winC + 1);
                for (int i = 0; i < winC - 1; i++)
                {
                    float locX = spacing * (i + 1.5f);
                    if (doorActive && floor == 0 && (locX + p.trimWidth / 2f > doorMin && locX - p.trimWidth / 2f < doorMax)) continue;
                    AddDecalQuad(md, MatLayer.Accents, start + right * locX + Vector3.up * (p.wallHeight / 2f), right, Vector3.up, norm, p.trimWidth, p.wallHeight, p.detailOffsetBias * 1.2f, true, uvScale);
                }
            }
        }

        private void GenerateHorizontalTrims(MeshData md, BuildingProfile profile, float baseY, float topY)
        {
            // PHASE 2 FIX: Calling the Footprint routine multiplied by 1.5 pushes the entire framework slightly out
            // over the corner trims mathematically without complex offsetting required.
            float zBias = profile.detailOffsetBias * 1.5f;
            List<FootprintNode> fNodes = GetFootprint(profile, zBias);

            for (int floor = 1; floor <= profile.floorCount; floor++)
            {
                float cy = baseY + floor * profile.wallHeight;
                if (floor == profile.floorCount) cy = topY - profile.trimWidth / 2f;

                for (int i = 0; i < fNodes.Count; i++)
                {
                    Vector3 p1 = fNodes[i].position; Vector3 p2 = fNodes[(i + 1) % fNodes.Count].position; p1.y = cy; p2.y = cy;
                    Vector3 dir = (p2 - p1).normalized;

                    // Passing 0f for the zBias argument because p1/p2 mathematically inherited the offset trace bounds natively
                    AddDecalQuad(md, MatLayer.Accents, (p1 + p2) / 2f, dir, Vector3.up, Vector3.Cross(Vector3.up, dir).normalized, Vector3.Distance(p1, p2), profile.trimWidth, 0f, true, profile.textureScale);
                }
            }
        }

        private void GenerateCornerTrims(MeshData md, BuildingProfile p, float bY, float tY)
        {
            List<FootprintNode> fNodes = GetFootprint(p, p.detailOffsetBias);
            for (int i = 0; i < fNodes.Count; i++)
            {
                Vector3 pt = fNodes[i].position; pt.y = bY + (tY - bY) / 2f;
                Vector3 dIn = (pt - fNodes[(i - 1 + fNodes.Count) % fNodes.Count].position).normalized;
                Vector3 dOut = (fNodes[(i + 1) % fNodes.Count].position - pt).normalized;

                Vector3 nIn = Vector3.Cross(Vector3.up, dIn).normalized;
                Vector3 nOut = Vector3.Cross(Vector3.up, dOut).normalized;

                AddDecalQuad(md, MatLayer.Accents, pt - dIn * (p.trimWidth / 2f), dIn, Vector3.up, nIn, p.trimWidth, tY - bY, 0f, true, p.textureScale);
                AddDecalQuad(md, MatLayer.Accents, pt + dOut * (p.trimWidth / 2f), dOut, Vector3.up, nOut, p.trimWidth, tY - bY, 0f, true, p.textureScale);
            }
        }

        // PHASE 2 FIX: Optional tileUv applies precise un-stretched dimensional UV scale
        private void AddDecalQuad(MeshData md, MatLayer layer, Vector3 c, Vector3 rDir, Vector3 uDir, Vector3 n, float w, float h, float zBias, bool tileUv = false, float uvScale = 1f)
        {
            Vector3 pos = c + n * zBias;
            float uEnd = tileUv ? w * uvScale : 1f;
            md.AddQuad(layer, pos - rDir * (w / 2) - uDir * (h / 2), pos - rDir * (w / 2) + uDir * (h / 2), pos + rDir * (w / 2) + uDir * (h / 2), pos + rDir * (w / 2) - uDir * (h / 2), n, new Vector2(0, 0), new Vector2(0, 1), new Vector2(uEnd, 1), new Vector2(uEnd, 0));
        }

        private void AddFasciaQuad(MeshData md, Vector3 tL, Vector3 tR, float h, Vector3 n, float bias, bool tileUv = false, float uvScale = 1f)
        {
            float w = Vector3.Distance(tL, tR);
            float uEnd = tileUv ? w * uvScale : 1f;
            md.AddQuad(MatLayer.Accents, tL - Vector3.up * h + (n * bias), tL + (n * bias), tR + (n * bias), tR - Vector3.up * h + (n * bias), n, new Vector2(0, 0), new Vector2(0, 1), new Vector2(uEnd, 1), new Vector2(uEnd, 0));
        }
    }
}