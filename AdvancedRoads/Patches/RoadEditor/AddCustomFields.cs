namespace AdvancedRoads.Patches.RoadEditor {
    using HarmonyLib;
    using System;
    using System.Reflection;
    using KianCommons;

    /// <summary>
    /// changeing types confuses AddCustomFields.
    /// this patch resolves that confusion by using the replaced types.
    /// TODO move this pacth to prefab indeces mod.
    /// </summary>
    [HarmonyPatch(typeof(RoadEditorPanel), "AddCustomFields" )]
    public static class AddCustomFields {
        public static void Postfix(RoadEditorPanel __instance) {
            object target = __instance.m_Target();
            Log.Debug($"AddCustomFields.PostFix() target={target}\n" + Environment.StackTrace);
            if (target is NetInfo.Segment) {
                __instance.AddCrossImportField();
                __instance.AddModelImportField(true);
            } else if (target is NetInfo.Node) {
                __instance.AddCrossImportField();
                __instance.AddModelImportField(false);
            } else if (target is NetInfo.Lane) {
                __instance.AddLanePropFields();
            } else if (target is NetLaneProps.Prop) {
                __instance.AddLanePropSelectField();
            }
        }
    }
}

