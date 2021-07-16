using System;
using System.Reflection;
using HarmonyLib;
using ModHelper.Config;
using UnityEngine;
using Nuterra.NativeOptions;
using System.Collections.Generic;

namespace TT_ColliderController
{
    //This Mod exists solely to increase frames under load of a ton of blocks, and nothing more.  DO NOT USE WHILE BUILDING!
    //  We take no responsability for the following:
    //  Invincible Techs, unlimited power, collider abuse, crash spam.

    public class KickStart
    {
        //This kickstarts the whole mod.  
        // We add in multiple things including makeshift colliders that only enable when the mod is active.

        //Let hooks happen i guess
        const string ModName = "ColliderController";

        //Make a Config File to store user preferences
        public static ModConfig _thisModConfig;

        //Variables
        public static bool colliderGUIActive = false;       //Is the display up
        public static bool collidersEnabled = true;         //do we enable all colliders in the worldup
        //public static bool getAllPossibleObjects = false; //do we find all objects in the world
        public static bool getAllColliders = false;         //do we GET all colliders in the world
        public static bool updateToggle = false;            //Just update the darn thing; //do we obliterate all colliders in the world
        public static bool enableBlockUpdate = false;       //Update on EVERY block placement?
        public static bool noColliderModeMouse = true;      //Can the mouse take extra calculations to grab no-collider blocks?
        public static bool AutoToggleOnEnemy = true;        //Update on combat invoking?
        public static bool AutoToggleOnAlly = true;        //Update on combat invoking?
        public static int ActiveColliders = 60;//60         //How many active colliders we want on AutoGrab (when CB is enabled)

        //Detection Variables
        public static int AllCollidersCount = 0;
        //public static int AllCount = 0;
        public static int lastDisabledColliderCount = 0;   
        public static bool isWaterModPresent = false;
        public static bool isScaleTechsPresent = false;
        public static bool isControlBlocksPresent = false;

        //More toggles for goggles  (Catered for internals)
        public static bool KeepBFFaceBlocks = false;     //Do we keep BF Face Block collsions active?
        public static bool KeepGCArmorPlates = false;    //Do we keep GC Armor Plate collsions active?
        public static bool KeepHESlopes = false;         //Do we keep all HE Slopes collsions active?
        public static bool KeepGSOArmorPlates = false;   //Do we keep all GSO Armor Plate blocks active?

        public static KeyCode hotKey;
        public static int keyInt = 93;//default to be ]
        public static OptionKey GUIMenuHotKey;
        public static OptionToggle UIActive;
        public static OptionToggle KeepBF;
        public static OptionToggle KeepGC;
        public static OptionToggle KeepHE;
        public static OptionToggle KeepGSO;
        public static OptionToggle blockUpdate;
        public static OptionToggle mouseModeWithNoColliders;
        public static OptionToggle autoHandleCombat;//EARLY!
        public static OptionToggle autoHandleAlly;//EARLY!
        public static OptionRange activeColliderGrab;

        public static void Main()
        {
            //Where the fun begins
            
            //Initiate the madness
            Harmony harmonyInstance = new Harmony("legioniteterratech.collidercontroller");
            harmonyInstance.PatchAll(Assembly.GetExecutingAssembly());
            GUIColliderController.Initiate();
            //ColliderCommander.EarlySubscribe();
            //BlockScanner.GUIBlockGrabber.Initiate();

            if (LookForMod("WaterMod"))
            {
                Debug.Log("COLLIDER CONTROLLER: Found Water Mod!  Will compensate for missing buoyency colliders!");
                isWaterModPresent = true;
                if (LookForMod("ScaleableTechs"))
                {
                    Debug.Log("COLLIDER CONTROLLER: Found ScaleTechs!  Will update post rescale update!");
                    isScaleTechsPresent = true;
                }
            }

            if (LookForMod("Control Block"))
            {
                Debug.Log("COLLIDER CONTROLLER: Found Control blocks!  Will switch no-collider block fetch system to Control-Block Supported!");
                isControlBlocksPresent = true;
            } 


            Debug.Log("\nCOLLIDER CONTROLLER: Config Loading");
            ModConfig thisModConfig = new ModConfig();
            Debug.Log("\nCOLLIDER CONTROLLER: Config Loaded.");

            thisModConfig.BindConfig<KickStart>(null, "keyInt");
            hotKey = (KeyCode)keyInt;

            //thisModConfig.BindConfig<KickStart>(null, "colliderGUIActive");

            thisModConfig.BindConfig<KickStart>(null, "KeepBFFaceBlocks");
            thisModConfig.BindConfig<KickStart>(null, "KeepGCArmorPlates");
            thisModConfig.BindConfig<KickStart>(null, "KeepHESlopes");
            thisModConfig.BindConfig<KickStart>(null, "KeepGSOArmorPlates");

            thisModConfig.BindConfig<KickStart>(null, "enableBlockUpdate");
            thisModConfig.BindConfig<KickStart>(null, "ActiveColliders");
            thisModConfig.BindConfig<KickStart>(null, "AutoToggleOnEnemy");
            thisModConfig.BindConfig<KickStart>(null, "AutoToggleOnAlly");
            thisModConfig.BindConfig<KickStart>(null, "noColliderModeMouse");
            _thisModConfig = thisModConfig;

            //Nativeoptions
            var ColliderProperties = ModName + " - Collider Menu Settings";
            GUIMenuHotKey = new OptionKey("GUI Menu button", ColliderProperties, hotKey);
            GUIMenuHotKey.onValueSaved.AddListener(() => { keyInt = (int)(hotKey = GUIMenuHotKey.SavedValue); thisModConfig.WriteConfigJsonFile(); });

            //UIActive = new OptionToggle("Collider GUI Active", ColliderProperties, colliderGUIActive);
            //UIActive.onValueSaved.AddListener(() => {colliderGUIActive = UIActive.SavedValue; });

            KeepBF = new OptionToggle("Keep BF Face Blocks", ColliderProperties, KeepBFFaceBlocks);
            KeepBF.onValueSaved.AddListener(() => { KeepBFFaceBlocks = KeepBF.SavedValue; thisModConfig.WriteConfigJsonFile(); });
            KeepGC = new OptionToggle("Keep GC Shock Plates", ColliderProperties, KeepGCArmorPlates);
            KeepGC.onValueSaved.AddListener(() => { KeepGCArmorPlates = KeepGC.SavedValue; thisModConfig.WriteConfigJsonFile(); });
            KeepHE = new OptionToggle("Keep HE Fort Slopes", ColliderProperties, KeepHESlopes);
            KeepHE.onValueSaved.AddListener(() => { KeepHESlopes = KeepHE.SavedValue; thisModConfig.WriteConfigJsonFile(); });
            KeepGSO = new OptionToggle("Keep GSO Armour Plates and Ploughs", ColliderProperties, KeepGSOArmorPlates);
            KeepGSO.onValueSaved.AddListener(() => { KeepGSOArmorPlates = KeepGSO.SavedValue; thisModConfig.WriteConfigJsonFile(); });

            mouseModeWithNoColliders = new OptionToggle("Allow Grab of Disabled Colliders", ColliderProperties, noColliderModeMouse);
            mouseModeWithNoColliders.onValueSaved.AddListener(() => { noColliderModeMouse = mouseModeWithNoColliders.SavedValue; thisModConfig.WriteConfigJsonFile(); });
            autoHandleCombat = new OptionToggle("Toggle Colliders on Combat", ColliderProperties, AutoToggleOnEnemy);
            autoHandleCombat.onValueSaved.AddListener(() => { AutoToggleOnEnemy = autoHandleCombat.SavedValue; thisModConfig.WriteConfigJsonFile(); });
            autoHandleAlly = new OptionToggle("Toggle Colliders on Allied Collision", ColliderProperties, AutoToggleOnAlly);
            autoHandleAlly.onValueSaved.AddListener(() => { AutoToggleOnAlly = autoHandleAlly.SavedValue; thisModConfig.WriteConfigJsonFile(); });
            blockUpdate = new OptionToggle("BlockUpdate (INCREASES LAG ON HUGE)", ColliderProperties, enableBlockUpdate);
            blockUpdate.onValueSaved.AddListener(() => { enableBlockUpdate = blockUpdate.SavedValue; thisModConfig.WriteConfigJsonFile(); });

            activeColliderGrab = new OptionRange("Non-Collider Grabber (More means more lag)", ColliderProperties, ActiveColliders, 20f, 100f, 10f);
            activeColliderGrab.onValueSaved.AddListener(() => { ActiveColliders = (int)activeColliderGrab.SavedValue; });
        }

        public static bool LookForMod(string name)
        {
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.FullName.StartsWith(name))
                {
                    return true;
                }
            }
            return false;
        }
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
                var target = __instance.gameObject.AddComponent<ColliderCommander.ModuleRemoveColliders>();
                target.TankBlock = __instance;
            }
        }


        [HarmonyPatch(typeof(Tank))]
        [HarmonyPatch("OnPool")]
        private class PatchTank
        {
            private static void Postfix(Tank __instance)
            {
                var target = __instance.gameObject.AddComponent<ColliderCommander.RemoveColliderTank>();
                target.Subscribe(__instance);
            }
        }

        [HarmonyPatch(typeof(Tank))]
        [HarmonyPatch("NotifyDamage")]
        private class PatchTankDamage
        {
            private static void Postfix(Tank __instance)
            {
                var target = __instance.gameObject.GetComponent<ColliderCommander.RemoveColliderTank>();
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
    public class GUIColliderController : MonoBehaviour
    {
        //We handle the GUI for the ColliderController system here, toggle when to run real colliders or not.

        static private bool GUIIsActive = false;
        static private Rect MainWindow = new Rect(300, 0, 200, 170);
        static public GameObject GUIWindow;

        static private void GUIHandler(int ID)
        {
            //Toggle if the colliders be gone
            KickStart.collidersEnabled = GUI.Toggle(new Rect(15, 40, 100, 20), KickStart.collidersEnabled, "Colliders Default");
            KickStart.updateToggle = GUI.Toggle(new Rect(15, 60, 100, 20), KickStart.updateToggle, "Update Colliders");
            KickStart.getAllColliders = GUI.Toggle(new Rect(15, 80, 100, 20), KickStart.getAllColliders, "Update Count");
            if (KickStart.getAllColliders)
            {
                KickStart.AllCollidersCount = FindObjectsOfType<Collider>().Length; //- KickStart.lastDisabledColliderCount;
                KickStart.getAllColliders = false;
            }
            GUI.Label(new Rect(20, 105, 120, 20), "Count : " + KickStart.AllCollidersCount);
            GUI.Label(new Rect(20, 120, 120, 20), "Below is only SP");
            ColliderCommander.AFFECT_ALL_TECHS = GUI.Toggle(new Rect(15, 140, 100, 20), ColliderCommander.AFFECT_ALL_TECHS, "ALL TECHS");
            GUI.DragWindow();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KickStart.hotKey))
            {
                GUIIsActive = !GUIIsActive;
                GUIWindow.SetActive(GUIIsActive);
                if (!GUIIsActive)
                {
                    Debug.Log("\nCOLLIDER CONTROLLER: Writing to Config...");
                    KickStart._thisModConfig.WriteConfigJsonFile();
                }
            }
            if (KickStart.collidersEnabled)
            {
                ColliderCommander.areAllPossibleCollidersDisabled = false;
            }
            else
            {
                ColliderCommander.areAllPossibleCollidersDisabled = true;
            }
        }

        public static void Initiate()
        {
            new GameObject("GUIColliderController").AddComponent<GUIColliderController>();
            GUIWindow = new GameObject();
            GUIWindow.AddComponent<GUIDisplay>();
            GUIWindow.SetActive(false);
        }
        internal class GUIDisplay : MonoBehaviour
        {
            private void OnGUI()
            {
                if (GUIIsActive)
                {
                    MainWindow = GUI.Window(2199, MainWindow, GUIHandler, "Player Collider Control");
                }
            }
        }
    }
}
