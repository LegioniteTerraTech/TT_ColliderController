using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace TT_ColliderController
{
    public class ModuleRemoveColliders : Module
    {
        //This is shoehorned into every block to control enabling of existing colliders
        //  MINIMISE UPDATE CYCLES FOR THIS AS IT CAN AND WILL LAG GREATLY IF USED WRONG!
        //     MANUAL ACTIONS REQUIRED
        // Will ignore:  
        //   - Colliders set the WheelCollider because if it touches those then CRASH
        //   - Essential blocks like cabs, anchors, moving parts, etc.
        //   - Blocks with ModuleRemoveColliders.DoNotDisableColliders = true as those are important to keep colliders for
        //   - All other player blocks when the player in any gamemode MP (no exploit-y)


        //Variables
        public RemoveColliderTank rCTank;
        public TankBlock TankBlock;

        public bool mouseHoverStat = false;     // Has the mouse previously enabled the colliders on this?

        private bool disabledAllColBlock = false;    // Have all of the colliders been disabled on this before?
        private bool pendingScan = true;                            // 
        private bool UpdateNow = true;                              // Force update scane to make sure everything is in check
        private bool DenyColliderChange = false;                    // Deny collider change if the block was flagged from spawn
        private int BlockCanBeToggled = 0;                          // Is this block one of the special exceptions?
                                                                    // 0 is disabled,  1 is BF,  2 is GC,  3 is HE,  4 is GSO

        // Player-Changable collidos
        public bool DoNotDisableColliders = false;//set this to "true" through your JSON to deny collider disabling
        /*
         * "TT_ColliderController.ColliderCommander.ModuleRemoveColliders": 
         * {
         *      "DoNotDisableColliders": true,  // set this to "true" through your JSON to deny collider disabling
         * }
         */


        public void ForceUpdateThis()
        {   // Outside request to force-update
            UpdateNow = true;
        }

        public void OnPool()
        {
            //Inititate Collider Cleansing on first pool
            TankBlock = gameObject.GetComponent<TankBlock>();
            string CB = gameObject.name;
            //Debug.Log("Processing " + gameObject + " " + CB);
            TankBlock.AttachEvent.Subscribe(AddBlock);
            TankBlock.DetachEvent.Subscribe(RemoveBlock);

            pendingScan = false;

            // Filtering Modules
            bool thisIsACab = gameObject.transform.GetComponent<ModuleTechController>();
            if (thisIsACab)
            {
                //Debug.Log("CAB DETECTED IN " + gameObject + " DENYING CHANGES!");
                DenyColliderChange = true;
                return;//End it NOW!
            }
            bool thisIsAWheel = gameObject.transform.GetComponent<ModuleWheels>();
            if (thisIsAWheel)
            {
                //Debug.Log("WHEEL DETECTED IN " + gameObject + " DENYING CHANGES!");
                DenyColliderChange = true;
                return;//End it NOW!
            }
            bool thisIsABubble = gameObject.transform.GetComponent<ModuleShieldGenerator>();
            if (thisIsABubble)
            {
                //Debug.Log("SHIELD DETECTED IN " + gameObject + " DENYING CHANGES!");
                DenyColliderChange = true;
                return;//End it NOW!
            }
            bool thisIsAnAnchor = gameObject.transform.GetComponent<ModuleAnchor>();
            if (thisIsAnAnchor)
            {
                //Debug.Log("ANCHOR DETECTED IN " + gameObject + " DENYING CHANGES!");
                DenyColliderChange = true;
                return;//End it NOW!
            }

            // Now filter MT-related parts
            if (CB == "EXP_TowSet_1_Hook_111" || CB == "EXP_TowSet_1_Ring_111" || CB == "EXP_TowSet_2_Hook_223" || CB == "EXP_TowSet_2_Ring_222" || CB == "EXP_TowSet_3_Hook_223" ||
                CB == "EXP_TowSet_3_Ring_222" || CB == "EXP_TowRing_332" || CB == "EXP_TowSet_4_Hook_322" || CB == "EXP_TowSet_4_Ring_322" || CB == "EXP_TowSet_4_Lock_311" ||
                CB == "EXP_JointSet_1_Bearing_111" || CB == "EXP_JointSet_1_Axle_111" || CB == "EXP_JointSet_1_Cap_1_111" || CB == "EXP_JointSet_1_Cap_2_111" || CB == "EXP_JointSet_2_Bearing_212" ||
                CB == "EXP_JointSet_2_Axle_222" || CB == "EXP_JointSet_Pole_121" || CB == "EXP_JointSet_Pole_111" || CB == "EXP_JointSet_3_Ball_111" || CB == "EXP_JointSet_3_Socket_111" ||
                CB == "EXP_JointSet_4_Ball_332" || CB == "EXP_JointSet_4_Socket_332")
            { //The RR Multi-Tech parts are to remain uneffected.
              //Debug.Log("MULTI-TECH BLOCK " + gameObject + " DENYING CHANGES!");
                DenyColliderChange = true;
                return;//End it NOW!
            }
            if (CB == "EXP_Platform_Ramp_214" || CB == "EXP_Platform_Ramp_213" || CB == "EXP_Platform_Ramp_224" || CB == "EXP_Platform_414")
            { //The RR Ramps are to remain uneffected.
              //Debug.Log("TECH PLATFORM BLOCK " + gameObject + " DENYING CHANGES!");
                DenyColliderChange = true;
                return;//End it NOW!
            }

            // Now filter Fusion Blocks
            if (CB == "_C_BLOCK:98341" || CB == "_C_BLOCK:98342" || CB == "_C_BLOCK:98343" || CB == "_C_BLOCK:98344" || CB == "_C_BLOCK:98345")
            { //GC Small Friction Pad and Non Slip-A<Tron 3000  |  Every MTMag.
              //Debug.Log("FUSION BLOCK DETECTED IN " + gameObject + " DENYING CHANGES!");
                DenyColliderChange = true;
                return;//End it NOW!
            }

            // Now filter Control Blocks
            if (CB == "_C_BLOCK:1293831" || CB == "_C_BLOCK:1293830" || CB == "_C_BLOCK:1293700" || CB == "_C_BLOCK:1293701" || CB == "_C_BLOCK:1293702" || CB == "_C_BLOCK:1293703")
            { //GC Small Friction Pad and Non Slip-A<Tron 3000  |  Every MTMag.
              //Debug.Log("UNEDITABLE CONTROL BLOCK DETECTED IN " + gameObject + " DENYING CHANGES!");
                DenyColliderChange = true;
                return;//End it NOW!
            }
            if (CB == "_C_BLOCK:1293838" || CB == "_C_BLOCK:129380" || CB == "_C_BLOCK:129381" || CB == "_C_BLOCK:6194710" || CB == "_C_BLOCK:1293834" || CB == "_C_BLOCK:1293837" ||
                CB == "_C_BLOCK:1980325" || CB == "_C_BLOCK:1293835" || CB == "_C_BLOCK:1393838" || CB == "_C_BLOCK:1393837" || CB == "_C_BLOCK:1393836" || CB == "_C_BLOCK:1393835" ||
                CB == "_C_BLOCK:29571436")
            { //EVERY PISTON AND SWIVEL
              //Debug.Log("CLUSTERBODY CONTROL BLOCK DETECTED IN " + gameObject + "!  Will hand off future operations to RemoveColliderTank!");
                DenyColliderChange = true;
                return;//End it NOW!
            }

            // NOW we move onwards to the blocks that are kept based on player intentions
            if (CB == "BF_Block_Faired_111" || CB == "BF_Block_Smooth_111" || CB == "BF_Block_Smooth_Edge_111" || CB == "BF_Block_Smooth_Corner_111" || CB == "BF_Block_Smooth_213")
            { //BF Face Blocks
              //Debug.Log("BF FACE BLOCK " + gameObject + " MARKING FOR FUTURE REFERENCE!");
                BlockCanBeToggled = 1;
                return;//End it NOW!
            }
            if (CB == "GC_Armour_Plate_221" || CB == "GC_Armour_Plate_421" || CB == "GC_Armour_Plate_121")
            { //GC Shock Plates
              //Debug.Log("GC SHOCK PLATE " + gameObject + " MARKING FOR FUTURE REFERENCE!");
                BlockCanBeToggled = 2;
                return;//End it NOW!
            }
            if (CB == "HE_ArmouredBlock_111" || CB == "HE_ArmouredBlock_112" || CB == "HE_ArmouredBlock_113" || CB == "HE_ArmouredBlock_114")
            { //HE Armour Slopes
              //Debug.Log("HE ARMOR SLOPE " + gameObject + " MARKING FOR FUTURE REFERENCE!");
                BlockCanBeToggled = 3;
                return;//End it NOW!
            }
            if (CB == "GSO_ArmourPlate_Small_111" || CB == "GSO_ArmourPlate_Medium_211" || CB == "GSO_ArmourPlate_Large_222" || CB == "GSO_ArmourPlate_Cab_111" ||
                CB == "GSO_Plough_311" || CB == "GSO_Plough_CowCatcher_321" || CB == "GSO_Plough_211")
            { //GSO Armour Slopes and Ploughs
              //Debug.Log("GSO ARMOR PLATE " + gameObject + " MARKING FOR FUTURE REFERENCE!");
                BlockCanBeToggled = 4;
                return;//End it NOW!
            }

        }
        public void AddBlock()
        {
            rCTank = transform.root.GetComponent<RemoveColliderTank>();
            //CompareExtremeBlocks(tankblock.transform.localPosition);
            rCTank.RequestRecalcFurthest();
        }
        public void RemoveBlock()
        {
            if ((bool)rCTank)
            {
                rCTank.RequestRecalcFurthest();
                rCTank = null;
            }
        }

        private bool IsLowestBlock()
        {
            if (!TankBlock.IsAttached)
                return false;
            rCTank = transform.root.GetComponent<RemoveColliderTank>();
            Vector3 rBU = rCTank.tank.trans.InverseTransformVector(rCTank.tank.rootBlockTrans.up);
            if (!rBU.y.Approximately(0)) 
            {
                if (rBU.y > 0)
                {
                    if (TankBlock.cachedLocalPosition.y.Approximately(rCTank.lowPos))
                        return true;
                }
                else
                {
                    if (TankBlock.cachedLocalPosition.y.Approximately(rCTank.higPos))
                        return true;
                }
            }
            else if (!rBU.x.Approximately(0))
            {
                if (rBU.x > 0)
                {
                    if (TankBlock.cachedLocalPosition.x.Approximately(rCTank.lefPos))
                        return true;
                }
                else
                {
                    if (TankBlock.cachedLocalPosition.x.Approximately(rCTank.rigPos))
                        return true;
                }
            }
            else // z
            {
                if (rBU.z > 0)
                {
                    if (TankBlock.cachedLocalPosition.z.Approximately(rCTank.reaPos))
                        return true;
                }
                else
                {
                    if (TankBlock.cachedLocalPosition.z.Approximately(rCTank.forPos))
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Disables colliders on block, if ColliderCommander's "Thorough" is true, then every collider
        ///   The ultimate exception are the lowest blocks on the Tech
        /// </summary>
        /// <returns>true if it is lowest</returns>
        private bool DisableColliders()
        {
            // The Cleanup Procedure 
            //   - Non-recursive as most colliders on a huge Tech are normal block colliders
            UpdateNow = false;
            if (IsLowestBlock())
                return true; //CANNOT DISABLE
            try//Sometimes there are colliders in the very base
            {
                if (ColliderCommander.Thorough)
                {
                    var Swapper = GetComponent<ColliderSwapper>();
                    if ((bool)Swapper)
                    {
                        Swapper.EnableCollision(false);
                        foreach (Collider col in GetComponentsInChildren<Collider>())
                        {
                            if (col.gameObject.layer == 20)
                                col.enabled = true;
                        }
                        disabledAllColBlock = true;
                        return false;
                    }
                }
                foreach (Collider col in GetComponents<Collider>())
                {
                    if (col.enabled)
                        KickStart.lastDisabledColliderCount++;
                    col.enabled = false;
                }
                //Debug.Log("Disabled Collider(s) in " + gameObject);
            }
            catch
            {
                //Debug.Log("Could not find Collider in " + gameObject);
            }
            try
            {
                //Try to cycle through EVERY GameObject on this block to disable EVERY COLLIDER
                int child = gameObject.transform.childCount;
                for (int v = 0; v < child; ++v)
                {
                    Transform grabbedGameObject = gameObject.transform.GetChild(v);
                    try
                    {
                        if (grabbedGameObject.gameObject.layer != 20)
                        {   //DON'T DISABLE WHEELS IT WILL CRASH THE GAME!
                            //   Also let specialized hoverbug-causing blocks continue working like intended.
                            foreach (Collider col in grabbedGameObject.GetComponents<Collider>())
                            {
                                if (col.enabled)
                                    KickStart.lastDisabledColliderCount++;
                                col.enabled = false;
                            }
                            //Debug.Log("Disabled Collider(s) in " + grabbedGameObject);
                        }
                        else
                        {
                            //Debug.Log("Skipped over Wheel Collider in " + grabbedGameObject);
                        }
                        //Debug.Log("Dee");
                    }
                    catch
                    {
                        //Debug.Log("Could not find Collider in " + grabbedGameObject);
                    }
                }
                /*
                try
                {
                    //This no work
                    gameObject.transform.GetComponent<ModuleWeapon>().enabled = false;
                    Debug.Log("Disarmed " + gameObject);
                }
                catch { }
                */

            }
            catch
            {
                Debug.Log("EoB error");//END OF BLOCK
            }
            disabledAllColBlock = true;
            return false;
        }


        private void EnableColliders()
        {
            UpdateNow = false;
            try//Sometimes there are colliders in the very base
            {
                if (ColliderCommander.Thorough)
                {
                    var Swapper = GetComponent<ColliderSwapper>();
                    if ((bool)Swapper)
                    {
                        Swapper.EnableCollision(true);
                        disabledAllColBlock = false;
                        return;
                    }
                }
                foreach (Collider col in GetComponents<Collider>())
                {
                    col.enabled = true;
                }
                //Debug.Log("Enabled Collider(s) in " + gameObject);
            }
            catch
            {
                //Debug.Log("Could not find Collider in " + gameObject);
            }
            try
            {
                //Try to cycle through EVERY GameObject on this block to enable EVERY COLLIDER
                int child = gameObject.transform.childCount;
                for (int v = 0; v < child; ++v)
                {
                    Transform grabbedGameObject = gameObject.transform.GetChild(v);
                    try
                    {
                        if (grabbedGameObject.gameObject.layer != 20)
                        {
                            foreach (Collider col in grabbedGameObject.GetComponents<Collider>())
                            {
                                col.enabled = true;
                            }
                            //Debug.Log("Enabled Collider(s) on " + grabbedGameObject);
                        }
                        else
                        {
                            //Debug.Log("Skipped over Wheel Collider in " + grabbedGameObject);
                        }
                    }
                    catch
                    {
                        //Debug.Log("Could not find Collider in " + grabbedGameObject);
                    }
                }
            }
            catch
            {
                Debug.Log("EoB error");//END OF BLOCK
            }
            disabledAllColBlock = false;
        }

        public bool DestroyCollidersOnBlock()
        {
            return DestroyCollidersOnBlock(out _);
        }
        public bool DestroyCollidersOnBlock(out bool IsBottom)
        {
            IsBottom = false;
            //Inititate Collider Cleaning
            //string CB = gameObject.name;
            //Debug.Log("Processing " + gameObject + " " + CB);

            if (pendingScan)
                OnPool();

            if (DenyColliderChange)
                return false;//END


            // NOW we move onwards to the blocks that are kept based on player intentions
            if (KickStart.KeepBFFaceBlocks && BlockCanBeToggled == 1)
            { //BF Face Blocks
              //Debug.Log("BF FACE BLOCK " + gameObject + " CANCELING!");
                EnableColliders();
                return false;//End it NOW!
            }
            if (KickStart.KeepGCArmorPlates && BlockCanBeToggled == 2)
            { //GC Shock Plates
              //Debug.Log("GC SHOCK PLATE " + gameObject + " CANCELING!");
                EnableColliders();
                return false;//End it NOW!
            }
            if (KickStart.KeepHESlopes && BlockCanBeToggled == 3)
            { //HE Armour Slopes
              //Debug.Log("HE ARMOR SLOPE " + gameObject + " CANCELING!");
                EnableColliders();
                return false;//End it NOW!
            }
            if (KickStart.KeepGSOArmorPlates && BlockCanBeToggled == 4)
            { //GSO Armour Slopes and Ploughs
              //Debug.Log("GSO ARMOR PLATE " + gameObject + " CANCELING!");
                EnableColliders();
                return false;//End it NOW!
            }

            if (DoNotDisableColliders != true && disabledAllColBlock == false || UpdateNow)
            {
                IsBottom = DisableColliders();
                return true;
            }
            //Otherwise take no action
            return false;
        }

        public bool ReturnCollidersOnBlock()
        {
            //Same as destroying colliders
            //string CB = gameObject.name;
            //Debug.Log("Processing " + gameObject + " " + CB);
            if (pendingScan)
                OnPool();

            if (DenyColliderChange)
                return false;//END


            // We don't disable the returning colliders to allow those to return even when toggled on when they were not here previously
            if (DoNotDisableColliders != true && disabledAllColBlock == true)
            {
                EnableColliders();
                return true;
            }
            //Otherwise take no action
            return false;
        }
    }
}
