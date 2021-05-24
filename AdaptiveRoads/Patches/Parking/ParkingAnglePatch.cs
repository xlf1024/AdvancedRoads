namespace AdaptiveRoads.Patches.Parking {
    using HarmonyLib;
    using JetBrains.Annotations;
    using KianCommons;
    using System.Reflection;
    using UnityEngine;
    using AdaptiveRoads.Manager;
    using KianCommons.Patches;
    using System.Collections.Generic;
    using System.Reflection.Emit;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;

    [InGamePatch]
    [UsedImplicitly]
    [HarmonyPatch(typeof(PassengerCarAI), "FindParkingSpaceRoadSide")]
    static class ParkingAnglePatch {
        static MethodInfo mGetLaneID_ => typeof(PathManager).GetMethod(nameof(PathManager.GetLaneID), throwOnError: true);
        static MethodInfo mFixValues_ => typeof(ParkingAnglePatch).GetMethod(nameof(ParkingAnglePatch.FixValues), throwOnError: true);
        static float angle_;

        // this can be done via prefix by calling FindPathPosition and GetLaneID
        // but that would reduce performance in a performance critical part of the code.
        [SuppressMessage("Performance", "HAA0601:Value type to reference type conversion causing boxing allocation", Justification = "transpiler itself is not performance critocal")]
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase origin) {
            // inject a call after PathManager.GetLaneID() to fix values.
            foreach (var code in instructions) {
                yield return code;
                if (code.Calls(mGetLaneID_)) {
                    // laneID is already on the stack
                    var LoadRefPathPos = instructions.ToList().Find(c => c.IsLdLocA(typeof(PathUnit.Position), out _));
                    yield return LoadRefPathPos.Clone();
                    yield return new CodeInstruction(OpCodes.Ldarga_S, origin.GetArgLoc("width"));
                    yield return new CodeInstruction(OpCodes.Ldarga_S, origin.GetArgLoc("length"));
                    yield return new CodeInstruction(OpCodes.Call, mFixValues_); // FixValues(uint laneID, ref float width, ref float length)
                }
            }
        }

        static void Postfix(ref Quaternion parkRot) {
            if (angle_ != 0) {
                Rotate(ref parkRot, angle_);
            }
            angle_ = 0;
        }

        static uint FixValues(uint laneID, ref PathUnit.Position pathPos, ref float width, ref float length) {
            var info = pathPos.m_segment.ToSegment().Info;
            var net = info?.GetMetaData();
            angle_ = net?.ParkingAngleDegrees ?? 0;
            if (angle_ > 30) {
                var laneInfo = info.m_lanes[pathPos.m_lane];
                width = FixWidth(length, net.OneOverCosOfParkingAngle, laneInfo.m_width);
                length = FixLength(width, net.OneOverCosOfParkingAngle);
            }
            return laneID;
        }

        static void Rotate(ref Quaternion parkRot, float parkingAngleRad) =>
            parkRot *= Quaternion.Euler(0, parkingAngleRad, 0);

        static float FixWidth(float oneOverCosOfParkingAngle, float carLength, float laneWith) =>
            Mathf.Min(carLength* oneOverCosOfParkingAngle, laneWith);

        static float FixLength(float oneOverCosOfParkingAngle, float carWidth) =>
            carWidth * oneOverCosOfParkingAngle;

    }
}
