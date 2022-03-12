using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using ColossalFramework;
using ColossalFramework.Math;
using HarmonyLib;
using KianCommons;
using UnityEngine;
using static KianCommons.ReflectionHelpers;

namespace AdaptiveRoads.Patches.QuayRoads {
    [HarmonyPatch]
    [PreloadPatch]
    static class TerrainUpdatedPatch {
        static IEnumerable<MethodBase> TargetMethods() {
            yield return GetMethod(typeof(NetSegment), "TerrainUpdated");
            yield return GetMethod(typeof(NetNode), "TerrainUpdated");
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
            foreach (var instruction in instructions) {
                // remove the bounds on right and left
                if (instruction.Calls(typeof(Vector3).GetMethod("Lerp"))) {
                    yield return CodeInstruction.Call(typeof(Vector3), "LerpUnclamped");
                    continue;
                }

                yield return instruction;
            }
        }
    }
    [HarmonyPatch]
    [PreloadPatch]
    static class TerrainUpdatedReplace {
        static IEnumerable<MethodBase> TargetMethods() {    
            yield return GetMethod(typeof(NetNode), "TerrainUpdated");
        }

        public static void CheckHeightOffset(this NetNode instance, ushort nodeID) {
            typeof(NetNode).GetMethod("CheckHeightOffset", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(instance, new object[]{ nodeID});
        }
        static bool Prefix(ushort nodeID, float minX, float minZ, float maxX, float maxZ, NetNode __instance) {
            return PrefixImpl(nodeID, minX, minZ, maxX, maxZ, __instance);
        }
        static bool PrefixImpl(ushort nodeID, float minX, float minZ, float maxX, float maxZ, NetNode __instance) { 
            if ((__instance.m_flags & (global::NetNode.Flags.Created | global::NetNode.Flags.Deleted)) != global::NetNode.Flags.Created) {
                return false;
            }
            global::NetInfo info = __instance.Info;
            if (info == null) {
                return false;
            }
            byte b = (!Singleton<global::TerrainManager>.instance.HasDetailMapping(__instance.m_position) && info.m_requireSurfaceMaps) ? (byte)64 : (byte)0;
            if (b != __instance.m_heightOffset) {
                __instance.CheckHeightOffset(nodeID);
                global::NetManager instance = Singleton<global::NetManager>.instance;
                for (int i = 0; i < 8; i++) {
                    ushort segment = __instance.GetSegment(i);
                    if (segment != 0) {
                        ushort startNode = instance.m_segments.m_buffer[(int)segment].m_startNode;
                        ushort endNode = instance.m_segments.m_buffer[(int)segment].m_endNode;
                        if (startNode == nodeID) {
                            instance.m_nodes.m_buffer[(int)endNode].CheckHeightOffset(endNode);
                        } else {
                            instance.m_nodes.m_buffer[(int)startNode].CheckHeightOffset(startNode);
                        }
                    }
                }
            }
            bool flag;
            bool flag2;
            bool flag3;
            bool flag4;
            bool flag5;
            bool flag6;
            if ((__instance.m_flags & global::NetNode.Flags.Underground) != global::NetNode.Flags.None) {
                flag = false;
                flag2 = false;
                flag3 = false;
                flag4 = false;
                flag5 = false;
                flag6 = info.m_netAI.RaiseTerrain();
            } else {
                flag = (info.m_createPavement && (!info.m_lowerTerrain || (__instance.m_flags & global::NetNode.Flags.OnGround) != global::NetNode.Flags.None));
                flag2 = (info.m_createGravel && (!info.m_lowerTerrain || (__instance.m_flags & global::NetNode.Flags.OnGround) != global::NetNode.Flags.None));
                flag3 = (info.m_createRuining && (!info.m_lowerTerrain || (__instance.m_flags & global::NetNode.Flags.OnGround) != global::NetNode.Flags.None));
                flag4 = (info.m_clipTerrain && (!info.m_lowerTerrain || (__instance.m_flags & global::NetNode.Flags.OnGround) != global::NetNode.Flags.None) && info.m_netAI.CanClipNodes());
                flag5 = (info.m_flattenTerrain || (info.m_netAI.FlattenGroundNodes() && (__instance.m_flags & global::NetNode.Flags.OnGround) != global::NetNode.Flags.None));
                flag6 = false;
            }
            if (flag5 || info.m_lowerTerrain || flag6 || flag || flag2 || flag3 || flag4) {
                for (int j = 0; j < 8; j++) {
                    ushort segment2 = __instance.GetSegment(j);
                    if (segment2 != 0) {
                        if ((Singleton<global::NetManager>.instance.m_segments.m_buffer[(int)segment2].m_flags & (global::NetSegment.Flags.Created | global::NetSegment.Flags.Deleted)) != global::NetSegment.Flags.Created) {
                            return false;
                        }
                        ushort startNode2 = Singleton<global::NetManager>.instance.m_segments.m_buffer[(int)segment2].m_startNode;
                        ushort endNode2 = Singleton<global::NetManager>.instance.m_segments.m_buffer[(int)segment2].m_endNode;
                        if (startNode2 == nodeID) {
                            for (int k = 0; k < 8; k++) {
                                ushort segment3 = Singleton<global::NetManager>.instance.m_nodes.m_buffer[(int)endNode2].GetSegment(k);
                                if (segment3 != 0 && (Singleton<global::NetManager>.instance.m_segments.m_buffer[(int)segment3].m_flags & (global::NetSegment.Flags.Created | global::NetSegment.Flags.Deleted)) != global::NetSegment.Flags.Created) {
                                    return false;
                                }
                            }
                        } else {
                            for (int l = 0; l < 8; l++) {
                                ushort segment4 = Singleton<global::NetManager>.instance.m_nodes.m_buffer[(int)startNode2].GetSegment(l);
                                if (segment4 != 0 && (Singleton<global::NetManager>.instance.m_segments.m_buffer[(int)segment4].m_flags & (global::NetSegment.Flags.Created | global::NetSegment.Flags.Deleted)) != global::NetSegment.Flags.Created) {
                                    return false;
                                }
                            }
                        }
                    }
                }
                ushort num = 0;
                #region Junction
                if ((__instance.m_flags & global::NetNode.Flags.Junction) != global::NetNode.Flags.None) {
                    Vector3 vector = __instance.m_position;
                    int num2 = 0;
                    for (int m = 0; m < 8; m++) {
                        ushort segment5 = __instance.GetSegment(m);
                        if (segment5 != 0) {
                            global::NetSegment netSegment = Singleton<global::NetManager>.instance.m_segments.m_buffer[(int)segment5];
                            global::NetInfo info2 = netSegment.Info;
                            if (info2 != null && info2.m_netAI.GetSnapElevation() <= info.m_netAI.GetSnapElevation()) {
                                global::ItemClass connectionClass = info2.GetConnectionClass();
                                Vector3 a = (nodeID != netSegment.m_startNode) ? netSegment.m_endDirection : netSegment.m_startDirection;
                                float num3 = -1f;
                                for (int n = 0; n < 8; n++) {
                                    ushort segment6 = __instance.GetSegment(n);
                                    if (segment6 != 0 && segment6 != segment5) {
                                        global::NetSegment netSegment2 = Singleton<global::NetManager>.instance.m_segments.m_buffer[(int)segment6];
                                        global::NetInfo info3 = netSegment2.Info;
                                        if (info3 != null && info3.m_netAI.GetSnapElevation() <= info.m_netAI.GetSnapElevation()) {
                                            global::ItemClass connectionClass2 = info3.GetConnectionClass();
                                            if (connectionClass.m_service == connectionClass2.m_service) {
                                                Vector3 vector2 = (nodeID != netSegment2.m_startNode) ? netSegment2.m_endDirection : netSegment2.m_startDirection;
                                                num3 = Mathf.Max(num3, a.x * vector2.x + a.z * vector2.z);
                                            }
                                        }
                                    }
                                }
                                vector += a * (2f + num3 * 2f);
                                num2++;
                                num = segment5;
                            }
                        }
                    }
                    vector.y = __instance.m_position.y;
                    if (num2 > 1) {
                        num = 0;
                        for (int num4 = 0; num4 < 8; num4++) {
                            ushort segment7 = __instance.GetSegment(num4);
                            if (segment7 != 0) {
                                global::NetSegment netSegment3 = Singleton<global::NetManager>.instance.m_segments.m_buffer[(int)segment7];
                                global::NetInfo info4 = netSegment3.Info;
                                if (info4 != null && info4.m_netAI.GetSnapElevation() <= info.m_netAI.GetSnapElevation()) {
                                    Bezier3 bezier = default(Bezier3);
                                    Segment3 segment8 = default(Segment3);
                                    Vector3 zero = Vector3.zero;
                                    Vector3 zero2 = Vector3.zero;
                                    Vector3 a2 = Vector3.zero;
                                    Vector3 a3 = Vector3.zero;
                                    global::ItemClass connectionClass3 = info4.GetConnectionClass();
                                    Vector3 vector3 = (nodeID != netSegment3.m_startNode) ? netSegment3.m_endDirection : netSegment3.m_startDirection;
                                    float num5 = -4f;
                                    ushort num6 = 0;
                                    for (int num7 = 0; num7 < 8; num7++) {
                                        ushort segment9 = __instance.GetSegment(num7);
                                        if (segment9 != 0 && segment9 != segment7) {
                                            global::NetSegment netSegment4 = Singleton<global::NetManager>.instance.m_segments.m_buffer[(int)segment9];
                                            global::NetInfo info5 = netSegment4.Info;
                                            if (info5 != null && info5.m_netAI.GetSnapElevation() <= info.m_netAI.GetSnapElevation()) {
                                                global::ItemClass connectionClass4 = info5.GetConnectionClass();
                                                if (connectionClass3.m_service == connectionClass4.m_service) {
                                                    Vector3 vector4 = (nodeID != netSegment4.m_startNode) ? netSegment4.m_endDirection : netSegment4.m_startDirection;
                                                    float num8 = vector3.x * vector4.x + vector3.z * vector4.z;
                                                    if (vector4.z * vector3.x - vector4.x * vector3.z < 0f) {
                                                        if (num8 > num5) {
                                                            num5 = num8;
                                                            num6 = segment9;
                                                        }
                                                    } else {
                                                        num8 = -2f - num8;
                                                        if (num8 > num5) {
                                                            num5 = num8;
                                                            num6 = segment9;
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    bool start = netSegment3.m_startNode == nodeID;
                                    bool flag7;
                                    netSegment3.CalculateCorner(segment7, false, start, false, out bezier.a, out zero, out flag7);
                                    netSegment3.CalculateCorner(segment7, false, start, true, out segment8.a, out zero2, out flag7);
                                    if (num6 != 0) {
                                        global::NetSegment netSegment5 = Singleton<global::NetManager>.instance.m_segments.m_buffer[(int)num6];
                                        global::NetInfo info6 = netSegment5.Info;
                                        start = (netSegment5.m_startNode == nodeID);
                                        netSegment5.CalculateCorner(num6, false, start, true, out bezier.d, out a2, out flag7);
                                        netSegment5.CalculateCorner(num6, false, start, false, out segment8.b, out a3, out flag7);
                                        global::NetSegment.CalculateMiddlePoints(bezier.a, -zero, bezier.d, -a2, true, true, out bezier.b, out bezier.c);
                                        segment8.a = (bezier.a + segment8.a) * 0.5f;
                                        segment8.b = (bezier.d + segment8.b) * 0.5f;
                                        Vector3 vector5 = Vector3.Min(vector, Vector3.Min(bezier.Min(), segment8.Min()));
                                        Vector3 vector6 = Vector3.Max(vector, Vector3.Max(bezier.Max(), segment8.Max()));
                                        if (vector5.x <= maxX && vector5.z <= maxZ && minX <= vector6.x && minZ <= vector6.z) {
                                            float num9 = Vector3.Distance(bezier.a, bezier.b);
                                            float num10 = Vector3.Distance(bezier.b, bezier.c);
                                            float num11 = Vector3.Distance(bezier.c, bezier.d);
                                            Vector3 lhs = (bezier.a - bezier.b) * (1f / Mathf.Max(0.1f, num9));
                                            Vector3 vector7 = (bezier.c - bezier.b) * (1f / Mathf.Max(0.1f, num10));
                                            Vector3 rhs = (bezier.d - bezier.c) * (1f / Mathf.Max(0.1f, num11));
                                            float num12 = Mathf.Min(Vector3.Dot(lhs, vector7), Vector3.Dot(vector7, rhs));
                                            num9 += num10 + num11;
                                            int num13 = Mathf.Clamp(Mathf.CeilToInt(Mathf.Min(num9 * 0.125f, 50f - num12 * 50f)) * 2, 2, 16);
                                            Vector3 vector8 = bezier.a;
                                            Vector3 vector9 = segment8.a;
                                            for (int num14 = 1; num14 <= num13; num14++) {
                                                global::NetInfo netInfo = (num14 > num13 >> 1) ? info6 : info4;
                                                Vector3 vector10 = bezier.Position((float)num14 / (float)num13);
                                                Vector3 vector11;
                                                if (num14 <= num13 >> 1) {
                                                    vector11 = segment8.a + (vector - segment8.a) * ((float)num14 / (float)num13 * 2f);
                                                } else {
                                                    vector11 = vector + (segment8.b - vector) * ((float)num14 / (float)num13 * 2f - 1f);
                                                }
                                                bool flag8 = netInfo.m_createPavement && (!netInfo.m_lowerTerrain || (__instance.m_flags & global::NetNode.Flags.OnGround) != global::NetNode.Flags.None);
                                                bool flag9 = netInfo.m_createGravel && (!netInfo.m_lowerTerrain || (__instance.m_flags & global::NetNode.Flags.OnGround) != global::NetNode.Flags.None);
                                                bool flag10 = netInfo.m_createRuining && (!netInfo.m_lowerTerrain || (__instance.m_flags & global::NetNode.Flags.OnGround) != global::NetNode.Flags.None);
                                                bool flag11 = netInfo.m_clipTerrain && (!netInfo.m_lowerTerrain || (__instance.m_flags & global::NetNode.Flags.OnGround) != global::NetNode.Flags.None) && netInfo.m_netAI.CanClipNodes();
                                                bool flag12 = netInfo.m_flattenTerrain || (netInfo.m_netAI.FlattenGroundNodes() && (__instance.m_flags & global::NetNode.Flags.OnGround) != global::NetNode.Flags.None);
                                                Vector3 vector12 = vector8;
                                                Vector3 vector13 = vector10;
                                                Vector3 vector14 = vector11;
                                                Vector3 vector15 = vector9;
                                                global::TerrainModify.Heights heights = global::TerrainModify.Heights.None;
                                                global::TerrainModify.Surface surface = global::TerrainModify.Surface.None;
                                                if (flag6) {
                                                    heights = global::TerrainModify.Heights.SecondaryMin;
                                                } else {
                                                    if (flag5 || flag12) {
                                                        heights |= global::TerrainModify.Heights.PrimaryLevel;
                                                    }
                                                    if (info.m_lowerTerrain || netInfo.m_lowerTerrain) {
                                                        heights |= global::TerrainModify.Heights.PrimaryMax;
                                                    }
                                                    if (info.m_blockWater || netInfo.m_blockWater) {
                                                        heights |= global::TerrainModify.Heights.BlockHeight;
                                                    }
                                                    if (flag8) {
                                                        surface |= global::TerrainModify.Surface.PavementA;
                                                    }
                                                    if (flag2 || flag9) {
                                                        surface |= global::TerrainModify.Surface.Gravel;
                                                    }
                                                    if (flag3 || flag10) {
                                                        surface |= global::TerrainModify.Surface.Ruined;
                                                    }
                                                    if (flag4 || flag11) {
                                                        surface |= global::TerrainModify.Surface.Clip;
                                                    }
                                                }
                                                global::TerrainModify.Edges edges = global::TerrainModify.Edges.All;
                                                float num15 = 0f;
                                                float num16 = 1f;
                                                float num17 = 0f;
                                                float num18 = 0f;
                                                int num19 = 0;
                                                while (netInfo.m_netAI.NodeModifyMask(nodeID, ref __instance, segment7, num6, num19, ref surface, ref heights, ref edges, ref num15, ref num16, ref num17, ref num18)) {
                                                    if (num15 < 0.5f) {
                                                        global::TerrainModify.Edges edges2 = global::TerrainModify.Edges.AB;
                                                        if (num15 != 0f || num16 != 1f || num19 != 0) {
                                                            if (num15 != 0f) {
                                                                float t = 2f * num15 * netInfo.m_halfWidth / Vector3.Distance(vector12, vector15);
                                                                float t2 = 2f * num15 * netInfo.m_halfWidth / Vector3.Distance(vector13, vector14);
                                                                vector8 = Vector3.LerpUnclamped(vector12, vector15, t);
                                                                vector10 = Vector3.LerpUnclamped(vector13, vector14, t2);
                                                            } else {
                                                                vector8 = vector12;
                                                                vector10 = vector13;
                                                            }
                                                            if (num16 < 0.5f) {
                                                                edges2 |= global::TerrainModify.Edges.CD;
                                                                float t3 = 2f * num16 * netInfo.m_halfWidth / Vector3.Distance(vector12, vector15);
                                                                float t4 = 2f * num16 * netInfo.m_halfWidth / Vector3.Distance(vector13, vector14);
                                                                vector9 = Vector3.LerpUnclamped(vector12, vector15, t3);
                                                                vector11 = Vector3.LerpUnclamped(vector13, vector14, t4);
                                                            } else {
                                                                vector9 = vector15;
                                                                vector11 = vector14;
                                                            }
                                                        }
                                                        vector8.y += num17;
                                                        vector9.y += num18;
                                                        vector10.y += num17;
                                                        vector11.y += num18;
                                                        Vector3 zero3 = Vector3.zero;
                                                        Vector3 zero4 = Vector3.zero;
                                                        if (flag6) {
                                                            zero3.y += info.m_maxHeight;
                                                            zero4.y += info.m_maxHeight;
                                                        } else if (netInfo.m_lowerTerrain) {
                                                            if (!info.m_lowerTerrain) {
                                                                if (num14 == 1) {
                                                                    global::TerrainModify.Edges edges3 = edges2 | global::TerrainModify.Edges.DA;
                                                                    global::TerrainModify.ApplyQuad(vector8, vector10, vector11, vector9, edges3, global::TerrainModify.Heights.None, surface);
                                                                    surface = global::TerrainModify.Surface.None;
                                                                } else if (num14 == num13) {
                                                                    global::TerrainModify.Edges edges4 = edges2 | global::TerrainModify.Edges.BC;
                                                                    global::TerrainModify.ApplyQuad(vector8, vector10, vector11, vector9, edges4, global::TerrainModify.Heights.None, surface);
                                                                    surface = global::TerrainModify.Surface.None;
                                                                }
                                                                zero3.y += (float)Mathf.Abs(num14 - 1 - (num13 >> 1)) * (1f / (float)num13) * netInfo.m_netAI.GetTerrainLowerOffset();
                                                                zero4.y += (float)Mathf.Abs(num14 - (num13 >> 1)) * (1f / (float)num13) * netInfo.m_netAI.GetTerrainLowerOffset();
                                                            } else {
                                                                if ((__instance.m_flags & global::NetNode.Flags.OnGround) != global::NetNode.Flags.None) {
                                                                    if (num14 == 1) {
                                                                        edges2 |= global::TerrainModify.Edges.DA;
                                                                    } else if (num14 == num13) {
                                                                        edges2 |= global::TerrainModify.Edges.BC;
                                                                    }
                                                                }
                                                                zero3.y += netInfo.m_netAI.GetTerrainLowerOffset();
                                                                zero4.y += netInfo.m_netAI.GetTerrainLowerOffset();
                                                            }
                                                        }
                                                        edges2 &= edges;
                                                        global::TerrainModify.Surface surface2 = surface;
                                                        if ((surface2 & global::TerrainModify.Surface.PavementA) != global::TerrainModify.Surface.None) {
                                                            surface2 |= global::TerrainModify.Surface.Gravel;
                                                        }
                                                        global::TerrainModify.ApplyQuad(vector8 + zero3, vector10 + zero4, vector11 + zero4, vector9 + zero3, edges2, heights, surface2);
                                                    }
                                                    num19++;
                                                }
                                                vector8 = vector13;
                                                vector9 = vector14;
                                            }
                                        }
                                    } else {
                                        Vector3 vector16 = bezier.a;
                                        Vector3 vector17 = segment8.a;
                                        Vector3 vector18 = Vector3.zero;
                                        Vector3 vector19 = Vector3.zero;
                                        Vector3 a4 = vector16;
                                        Vector3 b2 = vector17;
                                        Vector3 a5 = vector18;
                                        Vector3 b3 = vector19;
                                        bool flag13 = info4.m_createPavement && (!info4.m_lowerTerrain || (__instance.m_flags & global::NetNode.Flags.OnGround) != global::NetNode.Flags.None);
                                        bool flag14 = info4.m_createGravel && (!info4.m_lowerTerrain || (__instance.m_flags & global::NetNode.Flags.OnGround) != global::NetNode.Flags.None);
                                        bool flag15 = info4.m_createRuining && (!info4.m_lowerTerrain || (__instance.m_flags & global::NetNode.Flags.OnGround) != global::NetNode.Flags.None);
                                        bool flag16 = info4.m_clipTerrain && (!info4.m_lowerTerrain || (__instance.m_flags & global::NetNode.Flags.OnGround) != global::NetNode.Flags.None) && info4.m_netAI.CanClipNodes();
                                        bool flag17 = info4.m_flattenTerrain || (info4.m_netAI.FlattenGroundNodes() && (__instance.m_flags & global::NetNode.Flags.OnGround) != global::NetNode.Flags.None);
                                        global::TerrainModify.Heights heights2 = global::TerrainModify.Heights.None;
                                        global::TerrainModify.Surface surface3 = global::TerrainModify.Surface.None;
                                        if (flag6) {
                                            heights2 = global::TerrainModify.Heights.SecondaryMin;
                                        } else {
                                            if (flag17) {
                                                heights2 |= global::TerrainModify.Heights.PrimaryLevel;
                                            }
                                            if (info4.m_lowerTerrain) {
                                                heights2 |= global::TerrainModify.Heights.PrimaryMax;
                                            }
                                            if (info4.m_blockWater) {
                                                heights2 |= global::TerrainModify.Heights.BlockHeight;
                                            }
                                            if (flag13) {
                                                surface3 |= global::TerrainModify.Surface.PavementA;
                                            }
                                            if (flag14) {
                                                surface3 |= global::TerrainModify.Surface.Gravel;
                                            }
                                            if (flag15) {
                                                surface3 |= global::TerrainModify.Surface.Ruined;
                                            }
                                            if (flag16) {
                                                surface3 |= global::TerrainModify.Surface.Clip;
                                            }
                                        }
                                        global::TerrainModify.Edges edges5 = global::TerrainModify.Edges.All;
                                        float num20 = 0f;
                                        float num21 = 1f;
                                        float num22 = 0f;
                                        float num23 = 0f;
                                        int num24 = 0;
                                        while (info4.m_netAI.NodeModifyMask(nodeID, ref __instance, segment7, segment7, num24, ref surface3, ref heights2, ref edges5, ref num20, ref num21, ref num22, ref num23)) {
                                            if (num20 != 0f || num21 != 1f || num24 != 0) {
                                                vector16 = Vector3.LerpUnclamped(a4, b2, num20);
                                                vector17 = Vector3.LerpUnclamped(a4, b2, num21);
                                                vector18 = Vector3.LerpUnclamped(a5, b3, num20);
                                                vector19 = Vector3.LerpUnclamped(a5, b3, num21);
                                            }
                                            vector16.y += num22;
                                            vector17.y += num23;
                                            vector18.y += num22;
                                            vector19.y += num23;
                                            if (info4.m_segments == null || info4.m_segments.Length == 0) {
                                                vector18 = vector16 - zero * (info4.m_halfWidth + 2f);
                                                vector19 = vector17 - zero2 * (info4.m_halfWidth + 2f);
                                                float num25 = Mathf.Min(new float[]
                                                {
                                                                                                Mathf.Min(Mathf.Min(vector16.x, vector17.x), Mathf.Min(vector18.x, vector19.x))
                                                });
                                                float num26 = Mathf.Max(new float[]
                                                {
                                                                                                Mathf.Max(Mathf.Max(vector16.x, vector17.x), Mathf.Max(vector18.x, vector19.x))
                                                });
                                                float num27 = Mathf.Min(new float[]
                                                {
                                                                                                Mathf.Min(Mathf.Min(vector16.z, vector17.z), Mathf.Min(vector18.z, vector19.z))
                                                });
                                                float num28 = Mathf.Max(new float[]
                                                {
                                                                                                Mathf.Max(Mathf.Max(vector16.z, vector17.z), Mathf.Max(vector18.z, vector19.z))
                                                });
                                                if (num25 <= maxX && num27 <= maxZ && minX <= num26 && minZ <= num28) {
                                                    global::TerrainModify.Edges edges6 = global::TerrainModify.Edges.AB | global::TerrainModify.Edges.BC | global::TerrainModify.Edges.CD;
                                                    if (info4.m_lowerTerrain && (__instance.m_flags & global::NetNode.Flags.OnGround) != global::NetNode.Flags.None) {
                                                        edges6 |= global::TerrainModify.Edges.DA;
                                                    }
                                                    edges6 &= edges5;
                                                    global::TerrainModify.Surface surface4 = surface3;
                                                    if ((surface4 & global::TerrainModify.Surface.PavementA) != global::TerrainModify.Surface.None) {
                                                        surface4 |= global::TerrainModify.Surface.Gravel;
                                                    }
                                                    Vector3 zero5 = Vector3.zero;
                                                    if (flag6) {
                                                        zero5.y += info4.m_maxHeight;
                                                    } else if (info4.m_lowerTerrain) {
                                                        zero5.y += info4.m_netAI.GetTerrainLowerOffset();
                                                    }
                                                    global::TerrainModify.ApplyQuad(vector16 + zero5, vector18 + zero5, vector19 + zero5, vector17 + zero5, edges6, heights2, surface4);
                                                }
                                            } else {
                                                vector18 = vector17;
                                                vector19 = vector16;
                                                a2 = zero2;
                                                a3 = zero;
                                                float d = info4.m_netAI.GetEndRadius() * 1.3333334f * 1.1f;
                                                Vector3 b4 = vector16 - zero * d;
                                                Vector3 c = vector18 - a2 * d;
                                                Vector3 vector20 = vector17 + zero2 * d;
                                                Vector3 vector21 = vector19 + a3 * d;
                                                float num29 = Mathf.Min(Mathf.Min(Mathf.Min(vector16.x, vector17.x), Mathf.Min(b4.x, vector20.x)), Mathf.Min(Mathf.Min(c.x, vector21.x), Mathf.Min(vector18.x, vector19.x)));
                                                float num30 = Mathf.Max(Mathf.Max(Mathf.Max(vector16.x, vector17.x), Mathf.Max(b4.x, vector20.x)), Mathf.Max(Mathf.Max(c.x, vector21.x), Mathf.Max(vector18.x, vector19.x)));
                                                float num31 = Mathf.Min(Mathf.Min(Mathf.Min(vector16.z, vector17.z), Mathf.Min(b4.z, vector20.z)), Mathf.Min(Mathf.Min(c.z, vector21.z), Mathf.Min(vector18.z, vector19.z)));
                                                float num32 = Mathf.Max(Mathf.Max(Mathf.Max(vector16.z, vector17.z), Mathf.Max(b4.z, vector20.z)), Mathf.Max(Mathf.Max(c.z, vector21.z), Mathf.Max(vector18.z, vector19.z)));
                                                if (num29 <= maxX && num31 <= maxZ && minX <= num30 && minZ <= num32) {
                                                    int num33 = Mathf.Clamp(Mathf.CeilToInt(info4.m_halfWidth * 0.4f), 2, 8);
                                                    Vector3 a6 = vector16;
                                                    Vector3 a7 = (vector16 + vector17) * 0.5f;
                                                    for (int num34 = 1; num34 <= num33; num34++) {
                                                        Vector3 a8 = Bezier3.Position(vector16, b4, c, vector18, ((float)num34 - 0.5f) / (float)num33);
                                                        Vector3 vector22 = Bezier3.Position(vector16, b4, c, vector18, (float)num34 / (float)num33);
                                                        global::TerrainModify.Edges edges7 = global::TerrainModify.Edges.AB | global::TerrainModify.Edges.BC;
                                                        edges7 &= edges5;
                                                        global::TerrainModify.Surface surface5 = surface3;
                                                        if ((surface5 & global::TerrainModify.Surface.PavementA) != global::TerrainModify.Surface.None) {
                                                            surface5 |= global::TerrainModify.Surface.Gravel;
                                                        }
                                                        Vector3 zero6 = Vector3.zero;
                                                        if (flag6) {
                                                            zero6.y += info4.m_maxHeight;
                                                        } else if (info4.m_lowerTerrain) {
                                                            zero6.y += info4.m_netAI.GetTerrainLowerOffset();
                                                        }
                                                        global::TerrainModify.ApplyQuad(a6 + zero6, a8 + zero6, vector22 + zero6, a7 + zero6, edges7, heights2, surface5);
                                                        a6 = vector22;
                                                    }
                                                }
                                            }
                                            num24++;
                                        }
                                    }
                                }
                            }
                        }
                        if (num2 == 8) {
                            Vector3 vector23 = vector + Vector3.left * 8f;
                            Vector3 vector24 = vector + Vector3.back * 8f;
                            Vector3 vector25 = vector + Vector3.right * 8f;
                            Vector3 vector26 = vector + Vector3.forward * 8f;
                            Vector3 vector27 = vector23;
                            Vector3 vector28 = vector24;
                            Vector3 vector29 = vector25;
                            Vector3 vector30 = vector26;
                            global::TerrainModify.Heights heights3 = global::TerrainModify.Heights.None;
                            global::TerrainModify.Surface surface6 = global::TerrainModify.Surface.None;
                            if (flag6) {
                                heights3 = global::TerrainModify.Heights.SecondaryMin;
                            } else {
                                if (flag5) {
                                    heights3 |= global::TerrainModify.Heights.PrimaryLevel;
                                }
                                if (info.m_lowerTerrain) {
                                    heights3 |= global::TerrainModify.Heights.PrimaryMax;
                                }
                                if (info.m_blockWater) {
                                    heights3 |= global::TerrainModify.Heights.BlockHeight;
                                }
                                if (flag) {
                                    surface6 |= global::TerrainModify.Surface.PavementA;
                                }
                                if (flag2) {
                                    surface6 |= global::TerrainModify.Surface.Gravel;
                                }
                                if (flag3) {
                                    surface6 |= global::TerrainModify.Surface.Ruined;
                                }
                                if (flag4) {
                                    surface6 |= global::TerrainModify.Surface.Clip;
                                }
                            }
                            global::TerrainModify.Edges edges8 = global::TerrainModify.Edges.All;
                            float num35 = 0f;
                            float num36 = 1f;
                            float num37 = 0f;
                            float num38 = 0f;
                            int num39 = 0;
                            while (info.m_netAI.NodeModifyMask(nodeID, ref __instance, 0, 0, num39, ref surface6, ref heights3, ref edges8, ref num35, ref num36, ref num37, ref num38)) {
                                if (num39 != 0) {
                                    vector23 = vector27;
                                    vector24 = vector28;
                                    vector25 = vector29;
                                    vector26 = vector30;
                                }
                                vector23.y += (num37 + num38) * 0.5f;
                                vector24.y += (num37 + num38) * 0.5f;
                                vector25.y += (num37 + num38) * 0.5f;
                                vector26.y += (num37 + num38) * 0.5f;
                                global::TerrainModify.Edges edges9 = global::TerrainModify.Edges.All;
                                edges9 &= edges8;
                                global::TerrainModify.Surface surface7 = surface6;
                                if ((surface7 & global::TerrainModify.Surface.PavementA) != global::TerrainModify.Surface.None) {
                                    surface7 |= global::TerrainModify.Surface.Gravel;
                                }
                                Vector3 zero7 = Vector3.zero;
                                if (flag6) {
                                    zero7.y += info.m_maxHeight;
                                } else if (info.m_lowerTerrain) {
                                    zero7.y += info.m_netAI.GetTerrainLowerOffset();
                                }
                                global::TerrainModify.ApplyQuad(vector23 + zero7, vector24 + zero7, vector25 + zero7, vector26 + zero7, edges9, heights3, surface7);
                                num39++;
                            }
                        }
                    }
                }
                #endregion
                #region Bend
                else if ((__instance.m_flags & global::NetNode.Flags.Bend) != global::NetNode.Flags.None) {
                    Bezier3 bezier2 = default(Bezier3);
                    Bezier3 bezier3 = default(Bezier3);
                    Vector3 zero8 = Vector3.zero;
                    Vector3 zero9 = Vector3.zero;
                    Vector3 zero10 = Vector3.zero;
                    Vector3 zero11 = Vector3.zero;
                    ushort segment10 = 0;
                    ushort num40 = 0;
                    int num41 = 0;
                    for (int num42 = 0; num42 < 8; num42++) {
                        ushort segment11 = __instance.GetSegment(num42);
                        if (segment11 != 0) {
                            global::NetSegment netSegment6 = Singleton<global::NetManager>.instance.m_segments.m_buffer[(int)segment11];
                            if (netSegment6.Info != null) {
                                bool start2 = netSegment6.m_startNode == nodeID;
                                if (++num41 == 1) {
                                    segment10 = segment11;
                                    bool flag18;
                                    netSegment6.CalculateCorner(segment11, false, start2, false, out bezier2.a, out zero8, out flag18);
                                    netSegment6.CalculateCorner(segment11, false, start2, true, out bezier3.a, out zero9, out flag18);
                                } else {
                                    num40 = segment11;
                                    bool flag18;
                                    netSegment6.CalculateCorner(segment11, false, start2, true, out bezier2.d, out zero10, out flag18);
                                    netSegment6.CalculateCorner(segment11, false, start2, false, out bezier3.d, out zero11, out flag18);
                                }
                            }
                        }
                    }
                    if (num40 == 0) {
                        return false;
                    }
                    Vector3 a9 = bezier2.a;
                    Vector3 a10 = bezier3.a;
                    Vector3 d2 = bezier2.d;
                    Vector3 d3 = bezier3.d;
                    global::TerrainModify.Heights heights4 = global::TerrainModify.Heights.None;
                    global::TerrainModify.Surface surface8 = global::TerrainModify.Surface.None;
                    if (flag6) {
                        heights4 = global::TerrainModify.Heights.SecondaryMin;
                    } else {
                        if (flag5) {
                            heights4 |= global::TerrainModify.Heights.PrimaryLevel;
                        }
                        if (info.m_lowerTerrain) {
                            heights4 |= global::TerrainModify.Heights.PrimaryMax;
                        }
                        if (info.m_blockWater) {
                            heights4 |= global::TerrainModify.Heights.BlockHeight;
                        }
                        if (flag) {
                            surface8 |= global::TerrainModify.Surface.PavementA;
                        }
                        if (flag2) {
                            surface8 |= global::TerrainModify.Surface.Gravel;
                        }
                        if (flag3) {
                            surface8 |= global::TerrainModify.Surface.Ruined;
                        }
                        if (flag4) {
                            surface8 |= global::TerrainModify.Surface.Clip;
                        }
                    }
                    global::TerrainModify.Edges edges10 = global::TerrainModify.Edges.All;
                    float num43 = 0f;
                    float num44 = 1f;
                    float num45 = 0f;
                    float num46 = 0f;
                    int num47 = 0;
                    while (info.m_netAI.NodeModifyMask(nodeID, ref __instance, segment10, num40, num47, ref surface8, ref heights4, ref edges10, ref num43, ref num44, ref num45, ref num46)) {
                        if (num43 != 0f || num44 != 1f || num47 != 0) {
                            bezier2.a = Vector3.LerpUnclamped(a9, a10, num43);
                            bezier3.a = Vector3.LerpUnclamped(a9, a10, num44);
                            bezier2.d = Vector3.LerpUnclamped(d2, d3, num43);
                            bezier3.d = Vector3.LerpUnclamped(d2, d3, num44);
                        }
                        bezier2.a.y = bezier2.a.y + num45;
                        bezier3.a.y = bezier3.a.y + num46;
                        bezier2.d.y = bezier2.d.y + num45;
                        bezier3.d.y = bezier3.d.y + num46;
                        global::NetSegment.CalculateMiddlePoints(bezier2.a, -zero8, bezier2.d, -zero10, true, true, out bezier2.b, out bezier2.c);
                        global::NetSegment.CalculateMiddlePoints(bezier3.a, -zero9, bezier3.d, -zero11, true, true, out bezier3.b, out bezier3.c);
                        Vector3 vector31 = Vector3.Min(bezier2.Min(), bezier3.Min());
                        Vector3 vector32 = Vector3.Max(bezier2.Max(), bezier3.Max());
                        if (vector31.x <= maxX && vector31.z <= maxZ && minX <= vector32.x && minZ <= vector32.z) {
                            float num48 = Vector3.Distance(bezier2.a, bezier2.b);
                            float num49 = Vector3.Distance(bezier2.b, bezier2.c);
                            float num50 = Vector3.Distance(bezier2.c, bezier2.d);
                            float num51 = Vector3.Distance(bezier3.a, bezier3.b);
                            float num52 = Vector3.Distance(bezier3.b, bezier3.c);
                            float num53 = Vector3.Distance(bezier3.c, bezier3.d);
                            Vector3 lhs2 = (bezier2.a - bezier2.b) * (1f / Mathf.Max(0.1f, num48));
                            Vector3 vector33 = (bezier2.c - bezier2.b) * (1f / Mathf.Max(0.1f, num49));
                            Vector3 rhs2 = (bezier2.d - bezier2.c) * (1f / Mathf.Max(0.1f, num50));
                            float num54 = Mathf.Min(Vector3.Dot(lhs2, vector33), Vector3.Dot(vector33, rhs2));
                            num48 += num49 + num50;
                            num51 += num52 + num53;
                            int num55 = Mathf.Clamp(Mathf.CeilToInt(Mathf.Min(Mathf.Max(num48, num51) * 0.25f, 100f - num54 * 100f)), 1, 16);
                            Vector3 a11 = bezier2.a;
                            Vector3 a12 = bezier3.a;
                            for (int num56 = 1; num56 <= num55; num56++) {
                                Vector3 vector34 = bezier2.Position((float)num56 / (float)num55);
                                Vector3 vector35 = bezier3.Position((float)num56 / (float)num55);
                                global::TerrainModify.Edges edges11 = global::TerrainModify.Edges.AB | global::TerrainModify.Edges.CD;
                                if (info.m_lowerTerrain && (__instance.m_flags & global::NetNode.Flags.OnGround) != global::NetNode.Flags.None) {
                                    if (num56 == 1) {
                                        edges11 |= global::TerrainModify.Edges.DA;
                                    } else if (num56 == num55) {
                                        edges11 |= global::TerrainModify.Edges.BC;
                                    }
                                }
                                edges11 &= edges10;
                                global::TerrainModify.Surface surface9 = surface8;
                                if ((surface9 & global::TerrainModify.Surface.PavementA) != global::TerrainModify.Surface.None) {
                                    surface9 |= global::TerrainModify.Surface.Gravel;
                                }
                                Vector3 zero12 = Vector3.zero;
                                if (flag6) {
                                    zero12.y += info.m_maxHeight;
                                } else if (info.m_lowerTerrain) {
                                    zero12.y += info.m_netAI.GetTerrainLowerOffset();
                                }
                                global::TerrainModify.ApplyQuad(a11 + zero12, vector34 + zero12, vector35 + zero12, a12 + zero12, edges11, heights4, surface9);
                                Log.Debug($"BendNode TerrainUpdated: nodeID: {nodeID}, section: {num47}\nApplyQuad({a11 + zero12}, {vector34 + zero12}, {vector35 + zero12}, {a12 + zero12}, {edges11}, {heights4}, {surface9})");
                                a11 = vector34;
                                a12 = vector35;
                            }
                        }
                        num47++;
                    }
                }
                #endregion
                if ((__instance.m_flags & global::NetNode.Flags.End) != global::NetNode.Flags.None || num != 0) {
                    Vector3 vector36 = Vector3.zero;
                    Vector3 vector37 = Vector3.zero;
                    Vector3 vector38 = Vector3.zero;
                    Vector3 vector39 = Vector3.zero;
                    Vector3 zero13 = Vector3.zero;
                    Vector3 zero14 = Vector3.zero;
                    Vector3 a13 = Vector3.zero;
                    Vector3 a14 = Vector3.zero;
                    ushort num57 = num;
                    int num58 = 0;
                    while (num58 < 8 && num57 == 0) {
                        ushort segment12 = __instance.GetSegment(num58);
                        if (segment12 != 0) {
                            num57 = segment12;
                        }
                        num58++;
                    }
                    if (num57 == 0) {
                        return false;
                    }
                    bool start3 = Singleton<global::NetManager>.instance.m_segments.m_buffer[(int)num57].m_startNode == nodeID;
                    bool flag19;
                    Singleton<global::NetManager>.instance.m_segments.m_buffer[(int)num57].CalculateCorner(num57, false, start3, false, out vector36, out zero13, out flag19);
                    Singleton<global::NetManager>.instance.m_segments.m_buffer[(int)num57].CalculateCorner(num57, false, start3, true, out vector37, out zero14, out flag19);
                    Vector3 a15 = vector36;
                    Vector3 b5 = vector37;
                    Vector3 a16 = vector38;
                    Vector3 b6 = vector39;
                    global::TerrainModify.Heights heights5 = global::TerrainModify.Heights.None;
                    global::TerrainModify.Surface surface10 = global::TerrainModify.Surface.None;
                    if (flag6) {
                        heights5 = global::TerrainModify.Heights.SecondaryMin;
                    } else {
                        if (flag5) {
                            heights5 |= global::TerrainModify.Heights.PrimaryLevel;
                        }
                        if (info.m_lowerTerrain) {
                            heights5 |= global::TerrainModify.Heights.PrimaryMax;
                        }
                        if (info.m_blockWater) {
                            heights5 |= global::TerrainModify.Heights.BlockHeight;
                        }
                        if (flag) {
                            surface10 |= global::TerrainModify.Surface.PavementA;
                        }
                        if (flag2) {
                            surface10 |= global::TerrainModify.Surface.Gravel;
                        }
                        if (flag3) {
                            surface10 |= global::TerrainModify.Surface.Ruined;
                        }
                        if (flag4) {
                            surface10 |= global::TerrainModify.Surface.Clip;
                        }
                    }
                    global::TerrainModify.Edges edges12 = global::TerrainModify.Edges.All;
                    float num59 = 0f;
                    float num60 = 1f;
                    float num61 = 0f;
                    float num62 = 0f;
                    int num63 = 0;
                    while (info.m_netAI.NodeModifyMask(nodeID, ref __instance, num57, num57, num63, ref surface10, ref heights5, ref edges12, ref num59, ref num60, ref num61, ref num62)) {
                        if (num59 != 0f || num60 != 1f || num63 != 0) {
                            vector36 = Vector3.LerpUnclamped(a15, b5, num59);
                            vector37 = Vector3.LerpUnclamped(a15, b5, num60);
                            vector38 = Vector3.LerpUnclamped(a16, b6, num59);
                            vector39 = Vector3.LerpUnclamped(a16, b6, num60);
                        }
                        vector36.y += num61;
                        vector37.y += num62;
                        vector38.y += num61;
                        vector39.y += num62;
                        if (info.m_segments == null || info.m_segments.Length == 0) {
                            vector38 = vector36 - zero13 * (info.m_halfWidth + 2f);
                            vector39 = vector37 - zero14 * (info.m_halfWidth + 2f);
                            float num64 = Mathf.Min(new float[]
                            {
                                                        Mathf.Min(Mathf.Min(vector36.x, vector37.x), Mathf.Min(vector38.x, vector39.x))
                            });
                            float num65 = Mathf.Max(new float[]
                            {
                                                        Mathf.Max(Mathf.Max(vector36.x, vector37.x), Mathf.Max(vector38.x, vector39.x))
                            });
                            float num66 = Mathf.Min(new float[]
                            {
                                                        Mathf.Min(Mathf.Min(vector36.z, vector37.z), Mathf.Min(vector38.z, vector39.z))
                            });
                            float num67 = Mathf.Max(new float[]
                            {
                                                        Mathf.Max(Mathf.Max(vector36.z, vector37.z), Mathf.Max(vector38.z, vector39.z))
                            });
                            if (num64 <= maxX && num66 <= maxZ && minX <= num65 && minZ <= num67) {
                                global::TerrainModify.Edges edges13 = global::TerrainModify.Edges.AB | global::TerrainModify.Edges.BC | global::TerrainModify.Edges.CD;
                                if (info.m_lowerTerrain && (__instance.m_flags & global::NetNode.Flags.OnGround) != global::NetNode.Flags.None) {
                                    edges13 |= global::TerrainModify.Edges.DA;
                                }
                                edges13 &= edges12;
                                global::TerrainModify.Surface surface11 = surface10;
                                if ((surface11 & global::TerrainModify.Surface.PavementA) != global::TerrainModify.Surface.None) {
                                    surface11 |= global::TerrainModify.Surface.Gravel;
                                }
                                Vector3 zero15 = Vector3.zero;
                                if (flag6) {
                                    zero15.y += info.m_maxHeight;
                                } else if (info.m_lowerTerrain) {
                                    zero15.y += info.m_netAI.GetTerrainLowerOffset();
                                }
                                global::TerrainModify.ApplyQuad(vector36 + zero15, vector38 + zero15, vector39 + zero15, vector37 + zero15, edges13, heights5, surface11);
                            }
                        } else {
                            vector38 = vector37;
                            vector39 = vector36;
                            a13 = zero14;
                            a14 = zero13;
                            float d4 = info.m_netAI.GetEndRadius() * 1.3333334f * 1.1f;
                            Vector3 b7 = vector36 - zero13 * d4;
                            Vector3 c2 = vector38 - a13 * d4;
                            Vector3 vector40 = vector37 + zero14 * d4;
                            Vector3 vector41 = vector39 + a14 * d4;
                            float num68 = Mathf.Min(Mathf.Min(Mathf.Min(vector36.x, vector37.x), Mathf.Min(b7.x, vector40.x)), Mathf.Min(Mathf.Min(c2.x, vector41.x), Mathf.Min(vector38.x, vector39.x)));
                            float num69 = Mathf.Max(Mathf.Max(Mathf.Max(vector36.x, vector37.x), Mathf.Max(b7.x, vector40.x)), Mathf.Max(Mathf.Max(c2.x, vector41.x), Mathf.Max(vector38.x, vector39.x)));
                            float num70 = Mathf.Min(Mathf.Min(Mathf.Min(vector36.z, vector37.z), Mathf.Min(b7.z, vector40.z)), Mathf.Min(Mathf.Min(c2.z, vector41.z), Mathf.Min(vector38.z, vector39.z)));
                            float num71 = Mathf.Max(Mathf.Max(Mathf.Max(vector36.z, vector37.z), Mathf.Max(b7.z, vector40.z)), Mathf.Max(Mathf.Max(c2.z, vector41.z), Mathf.Max(vector38.z, vector39.z)));
                            if (num68 <= maxX && num70 <= maxZ && minX <= num69 && minZ <= num71) {
                                int num72 = Mathf.Clamp(Mathf.CeilToInt(info.m_halfWidth * 0.4f), 2, 8);
                                Vector3 a17 = vector36;
                                Vector3 a18 = (vector36 + vector37) * 0.5f;
                                for (int num73 = 1; num73 <= num72; num73++) {
                                    Vector3 a19 = Bezier3.Position(vector36, b7, c2, vector38, ((float)num73 - 0.5f) / (float)num72);
                                    Vector3 vector42 = Bezier3.Position(vector36, b7, c2, vector38, (float)num73 / (float)num72);
                                    global::TerrainModify.Edges edges14 = global::TerrainModify.Edges.AB | global::TerrainModify.Edges.BC;
                                    edges14 &= edges12;
                                    global::TerrainModify.Surface surface12 = surface10;
                                    if ((surface12 & global::TerrainModify.Surface.PavementA) != global::TerrainModify.Surface.None) {
                                        surface12 |= global::TerrainModify.Surface.Gravel;
                                    }
                                    Vector3 zero16 = Vector3.zero;
                                    if (flag6) {
                                        zero16.y += info.m_maxHeight;
                                    } else if (info.m_lowerTerrain) {
                                        zero16.y += info.m_netAI.GetTerrainLowerOffset();
                                    }
                                    global::TerrainModify.ApplyQuad(a17 + zero16, a19 + zero16, vector42 + zero16, a18 + zero16, edges14, heights5, surface12);
                                    a17 = vector42;
                                }
                            }
                        }
                        num63++;
                    }
                }
                if (__instance.m_lane != 0U && info.m_halfWidth < 3.999f) {
                    Vector3 a20 = Singleton<global::NetManager>.instance.m_lanes.m_buffer[(int)((UIntPtr)__instance.m_lane)].CalculatePosition((float)__instance.m_laneOffset * 0.003921569f);
                    float num74 = 0f;
                    Vector3 vector43 = VectorUtils.NormalizeXZ(a20 - __instance.m_position, out num74);
                    if (num74 > 1f) {
                        Vector3 a21 = __instance.m_position - new Vector3(vector43.x + vector43.z * info.m_halfWidth, 0f, vector43.z - vector43.x * info.m_halfWidth);
                        Vector3 a22 = __instance.m_position - new Vector3(vector43.x - vector43.z * info.m_halfWidth, 0f, vector43.z + vector43.x * info.m_halfWidth);
                        Vector3 a23 = a20 + new Vector3(vector43.x - vector43.z * info.m_halfWidth, 0f, vector43.z + vector43.x * info.m_halfWidth);
                        Vector3 a24 = a20 + new Vector3(vector43.x + vector43.z * info.m_halfWidth, 0f, vector43.z - vector43.x * info.m_halfWidth);
                        float num75 = Mathf.Min(new float[]
                        {
                                                Mathf.Min(Mathf.Min(a21.x, a22.x), Mathf.Min(a23.x, a24.x))
                        });
                        float num76 = Mathf.Max(new float[]
                        {
                                                Mathf.Max(Mathf.Max(a21.x, a22.x), Mathf.Max(a23.x, a24.x))
                        });
                        float num77 = Mathf.Min(new float[]
                        {
                                                Mathf.Min(Mathf.Min(a21.z, a22.z), Mathf.Min(a23.z, a24.z))
                        });
                        float num78 = Mathf.Max(new float[]
                        {
                                                Mathf.Max(Mathf.Max(a21.z, a22.z), Mathf.Max(a23.z, a24.z))
                        });
                        if (num75 <= maxX && num77 <= maxZ && minX <= num76 && minZ <= num78) {
                            global::TerrainModify.Edges edges15 = global::TerrainModify.Edges.All;
                            global::TerrainModify.Heights heights6 = global::TerrainModify.Heights.None;
                            global::TerrainModify.Surface surface13 = global::TerrainModify.Surface.None;
                            if (flag) {
                                surface13 |= (global::TerrainModify.Surface.PavementA | global::TerrainModify.Surface.Gravel);
                            }
                            if (flag2) {
                                surface13 |= global::TerrainModify.Surface.Gravel;
                            }
                            if (flag3) {
                                surface13 |= global::TerrainModify.Surface.Ruined;
                            }
                            Vector3 zero17 = Vector3.zero;
                            global::TerrainModify.ApplyQuad(a21 + zero17, a23 + zero17, a24 + zero17, a22 + zero17, edges15, heights6, surface13);
                        }
                    }
                }
            }
            return false;
        }
    }
}
