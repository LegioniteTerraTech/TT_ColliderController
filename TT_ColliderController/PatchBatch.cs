using System;
using System.Reflection;
using HarmonyLib;
using ModHelper.Config;
using UnityEngine;
using Nuterra.NativeOptions;

namespace TT_ColliderController
{
    class PatchBatch
    {
    }
    internal class Patches
    {
        //SHOVE IN ModuleRemoveCollider in EVERYTHING!
        [HarmonyPatch(typeof(TankBlock))]
        [HarmonyPatch("OnPool")]//On Block Creation
        private class PatchBlock
        {
            private static void Postfix(TankBlock __instance)
            {
                var target = __instance.gameObject.AddComponent<ModuleRemoveColliders>();
                target.TankBlock = __instance;
            }
        }


        [HarmonyPatch(typeof(Tank))]
        [HarmonyPatch("OnPool")]
        private class PatchTank
        {
            private static void Postfix(Tank __instance)
            {
                var target = __instance.gameObject.AddComponent<RemoveColliderTank>();
                target.Subscribe(__instance);
            }
        }

        [HarmonyPatch(typeof(Tank))]
        [HarmonyPatch("NotifyDamage")]
        private class PatchTankDamage
        {
            private static void Postfix(Tank __instance)
            {
                var target = __instance.gameObject.GetComponent<RemoveColliderTank>();
                target.WarnCollisionDamage();
            }
        }
        /*
        [HarmonyPatch(typeof(ManPointer))]
        [HarmonyPatch("TryGrabVisible")]
        private class PatchManPointer
        {
            private static void Postfix(ManPointer __instance)
            {
                var wEffect = __instance.gameObject.AddComponent<ColliderCommander.RemoveColliderTank>();
                wEffect.Subscribe(__instance);
            }
        }
        */
    }
}
