using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace TT_ColliderController
{

    public class RemoveColliderTank : MonoBehaviour
    {
        //Attach this to sync all collider removals on a Tech
        public Tank tank;

        // TECH Variables
        public bool lastLocalState = false;             // Has there been a collider-disable check done before on this Tank?
        private bool ForceUpdate = false;               // Do we force update all colliders to be checked an all levels?
        private int lastBlockState = 0;                 // 
        private bool lastStateAffectingAllTechs = false;// If we were affecting all techs previously
        private float totalFlote = 1f;                  // The maximum float the Tech has predicted based off of indexable volume
        private float roughTankTotalVolume = 1f;        // The total volume of the tech, excluding modifiers
        private float SubmergeProx = 1f;                // The smallest predicted band that can fit around the Tech for float averaging
        private string lastName = "N/A";                // Last name of teh tech to perform updates where OnRecycle fails
        public float localDensity = 8;                  // Can't get the actual value as Watermod blocks get info off an internal class (inaccessable)

        private Vector3 FloteBias;          // Center of Buoyency first grab
        private Vector3 FloteBiasFinal;     // Center of Buoyency second grab
        private Vector3 FloteBiasExtremes;  // Extremes of the bands around the Techto calc SubmergeProx

        private GameObject FloteBiasActual; //  Center of Buoyency Final calc

        //AUTOCALC
        private Vector3 lastDetectScale;
        private int lastColEnabledTime = ColliderCommander.ColliderCooldown;
        private bool queuedColliderCheck = false;
        internal bool isTempColliderEnabled = false;
        private bool boxExists = false;
        private bool needsFurthestCheck = false;
        private List<Tank> closeTechs = new List<Tank>();
        internal IntVector3 lastblockEnabledPos = Vector3.back;
        internal BoxCollider BoxCol;

        // Direction Maximums
        internal sbyte lowPos = 0;
        internal sbyte higPos = 0;
        internal sbyte lefPos = 0;
        internal sbyte rigPos = 0;
        internal sbyte forPos = 0;
        internal sbyte reaPos = 0;


        // Was previously likely needed for density calculations but that was actually just a simple variable
        //private int blockCounter = 0;     // Count of blocks on Tech (OBSOLETE)
        //private float savedMass = 1f;     // Saving the tech's mass for calculations (OBSOLETE)
        //private float tankDensity = 1f;   // Density of the entire Tech (OBSOLETE)

        // Initation
        public void Subscribe(Tank tank)
        {
            //tank.AttachEvent.Subscribe(AddBlock);
            //tank.DetachEvent.Subscribe(RemoveBlock);
            this.tank = tank;
        }
        public bool CompareExtremeBlocks(IntVector3 localPos)
        {
            if (localPos.y >= higPos)
            {
                higPos = (sbyte)localPos.y;
                return true;
            }
            else if (localPos.y <= lowPos)
            {
                lowPos = (sbyte)localPos.y;
                return true;
            }
            if (localPos.x <= lefPos)
            {
                lefPos = (sbyte)localPos.x;
                return true;
            }
            else if (localPos.x >= rigPos)
            {
                rigPos = (sbyte)localPos.x;
                return true;
            }
            if (localPos.z >= forPos)
            {
                forPos = (sbyte)localPos.z;
                return true;
            }
            else if (localPos.z <= reaPos)
            {
                reaPos = (sbyte)localPos.z;
                return true;
            }
            return false;
        }


        public float GetExtremist(Vector3 input)
        {   //but not really as it grabs the smallest extreme
            float final = Mathf.Max(input.y, input.x);
            final = Mathf.Max(final, input.z);
            return final;
        }

        // Collision
        private void OnTriggerEnter(Collider collider)
        {
            //collider = GetComponent<Tank>().dragSphere
            if (ColliderCommander.areAllPossibleCollidersDisabled && lastColEnabledTime > ColliderCommander.ColliderCooldown - 5)
            {
                if (KickStart.AutoToggleOnAlly)
                {
                    try
                    {
                        /*
                        var projectileIn = collider.gameObject.GetComponent<Projectile>();
                        if (projectileIn != null)
                        {
                            //Debug.Log("OnEnemyProjectileEnter");
                            if (projectileIn.Shooter.IsFriendly(tank.Team))
                                WarnCollision();
                        }
                        else
                        { */
                        var tankIn = collider.transform.root.GetComponent<Tank>();
                        if (tankIn != null && tankIn != tank)
                        {
                            //Debug.Log("OnAlliedTankEnter");
                            if (tankIn.IsFriendly(tank.Team) && !(tank.IsAnchored && tankIn.IsAnchored))
                            {   //Two anchored techs shouldn't be able to collide - base walls
                                closeTechs.Add(tankIn);
                                WarnCollision();
                            }
                        }
                        //}
                    }
                    catch { }
                }

                if (KickStart.AutoToggleOnEnemy)
                {
                    try
                    {
                        var projectileIn = collider.gameObject.GetComponent<Projectile>();
                        if (projectileIn != null)
                        {
                            //Debug.Log("OnEnemyProjectileEnter");
                            if (projectileIn.Shooter.IsEnemy(tank.Team))
                                WarnCollision();
                        }
                        else
                        {
                            var tankIn = collider.transform.root.GetComponent<Tank>();
                            if (tankIn != null)
                            {
                                //Debug.Log("OnEnemyTankEnter");
                                if (tankIn.IsEnemy(tank.Team))
                                {
                                    closeTechs.Add(tankIn);
                                    WarnCollision();
                                }
                            }
                        }
                    }
                    catch { }
                }
            }
        }
        public void WarnCollisionDamage()
        {
            //Debug.Log("Tank direct hit");
            if (KickStart.AutoToggleOnEnemy)
                WarnCollision();
        }
        public void WarnCollision()
        {
            lastColEnabledTime = 0;
        }

        private void ReactToIncomingCollision()
        {
            if (KickStart.AutoToggleOnEnemy)
            {
                try
                {
                    //tank.GetComponent<TechVision>().GetFirstVisibleTechIsEnemy(tank.Team)
                    var AimSys = tank.GetComponent<TechWeapon>().GetFirstWeapon().gameObject.GetComponent<TargetAimer>();
                    if (AimSys.HasTarget)
                    {
                        AimSys.Target.tank.gameObject.GetComponent<RemoveColliderTank>().WarnCollision();
                    }
                }
                catch { }
            }
        }


        public void RecursiveClusterBodyHandlerDestroy(Transform grabbedGameObject, ref int numBottom)
        {
            //All temp variables used in the creation of the water-based clusterbody floatation summary
            float combinedFlote = 0f;
            bool isClusterBody = false;
            int blockCountCB = 0;
            int blockVolumeCountCB = 0;
            float mostExtreme = 0f;
            Vector3 FloteBiasCB = Vector3.zero;
            Vector3 FloteBiasExtremesCB = Vector3.zero;
            int childCB = grabbedGameObject.transform.childCount;
            for (int vCB = 0; vCB < childCB; ++vCB)
            {
                Transform grabbedGameObjectCB = grabbedGameObject.transform.GetChild(vCB);
                try
                {
                    //Debug.Log("\n(CB) Child " + grabbedGameObjectCB + " child of " + grabbedGameObject);
                    if (ForceUpdate == true && tank.PlayerFocused == true)
                    {
                        grabbedGameObjectCB.GetComponent<ModuleRemoveColliders>().ForceUpdateThis();
                        //Debug.Log("FORCE-UPDATED!");
                    }
                    try// Check to make sure there's a marker there
                    {
                        grabbedGameObjectCB.gameObject.GetComponent<ModuleClusterBodyMarker>();
                    }
                    catch
                    {
                        grabbedGameObjectCB.gameObject.AddComponent<ModuleClusterBodyMarker>();
                    }
                    bool isValid = grabbedGameObjectCB.GetComponent<ModuleRemoveColliders>().DestroyCollidersOnBlock(out bool isBottom);
                    if (isBottom)
                        numBottom++;
                    if (KickStart.isWaterModPresent && isValid)
                    {
                        blockCountCB++;
                        isClusterBody = true;
                        var currentBlock = grabbedGameObjectCB.GetComponent<TankBlock>();
                        Vector3 currentCase = grabbedGameObjectCB.transform.localPosition;
                        // Set the variables
                        IntVector3[] intVector = currentBlock.filledCells;
                        for (int CellIndex = 0; CellIndex < currentBlock.filledCells.Length; CellIndex++)
                        {
                            FloteBiasCB += grabbedGameObjectCB.TransformPoint(intVector[CellIndex].x, intVector[CellIndex].y, intVector[CellIndex].z);
                        }
                        FloteBiasExtremesCB.x = Mathf.Max(FloteBiasExtremesCB.x, Mathf.Abs(currentCase.x));
                        FloteBiasExtremesCB.y = Mathf.Max(FloteBiasExtremesCB.y, Mathf.Abs(currentCase.y));
                        FloteBiasExtremesCB.z = Mathf.Max(FloteBiasExtremesCB.z, Mathf.Abs(currentCase.z));
                        mostExtreme = GetExtremist(FloteBiasExtremesCB);
                        blockVolumeCountCB += currentBlock.filledCells.Length;
                        combinedFlote += localDensity * currentBlock.filledCells.Length;
                    }
                }
                catch
                {
                    //Debug.Log("(CB) Object " + grabbedGameObjectCB + " in " + grabbedGameObject + " is slippery!");
                    if (grabbedGameObjectCB.transform.childCount >= 1)
                    {
                        //Debug.Log("(CB) Performing Recursive Action on " + grabbedGameObjectCB + "!  Confirmed Children " + grabbedGameObjectCB.transform.childCount);
                        RecursiveClusterBodyHandlerDestroy(grabbedGameObjectCB, ref numBottom);
                    }
                }
            }
            if (isClusterBody)
            {
                // Final calculations before export and creation/updating of custom-tooled floater
                if (grabbedGameObject.GetComponent<ModuleClusterBodySubTech>() == false)
                    grabbedGameObject.gameObject.AddComponent<ModuleClusterBodySubTech>();
                var insert = grabbedGameObject.GetComponent<ModuleClusterBodySubTech>();
                Vector3 FinalFloatCenter;

                FinalFloatCenter = FloteBiasCB / blockVolumeCountCB;

                insert.FloteForceCenter = FinalFloatCenter;
                insert.FloteForce = combinedFlote;
                insert.FloteForceRange = FloteBiasExtremesCB;
                insert.FloteExtreme = mostExtreme;
                //Debug.Log("CB has " + blockCountCB + " blocks");
            }
        }

        public void RecursiveClusterBodyHandlerReturn(Transform grabbedGameObject)
        {
            int childCB = grabbedGameObject.transform.childCount;
            for (int vCB = 0; vCB < childCB; ++vCB)
            {
                Transform grabbedGameObjectCB = grabbedGameObject.transform.GetChild(vCB);
                try
                {
                    //Debug.Log("\n(CB) Child " + grabbedGameObjectCB + " child of " + grabbedGameObject);
                    if (ForceUpdate == true && tank.PlayerFocused == true)
                    {
                        grabbedGameObjectCB.GetComponent<ModuleRemoveColliders>().ForceUpdateThis();
                        //Debug.Log("FORCE-UPDATED!");
                    }
                    grabbedGameObjectCB.GetComponent<ModuleRemoveColliders>().ReturnCollidersOnBlock();
                }
                catch
                {
                    //Debug.Log("(CB) Object " + grabbedGameObjectCB + " in " + grabbedGameObject + " is slippery!");
                    if (grabbedGameObjectCB.transform.childCount >= 1)
                    {
                        //Debug.Log("(CB) Performing Recursive Action on " + grabbedGameObjectCB + "!  Confirmed Children " + grabbedGameObjectCB.transform.childCount);
                        RecursiveClusterBodyHandlerReturn(grabbedGameObjectCB);
                    }
                }
            }
        }




        // Water mod support
        /*
        // Obsolete
        private void CalcApproxForWaterMod()
        {
            //it's not perfect but it will help with water-based Techs
            var thisInst = tank.GetComponent<RemoveColliderTank>();
            tankDensity = savedMass / roughTankTotalVolume;
        }
        */
        private float CalcBlockForWaterMod(Transform target)
        {
            //it's not perfect but it will help with water-based Techs
            Vector3 currentCase = target.transform.localPosition + target.GetComponent<TankBlock>().CentreOfMass;
            IntVector3[] intVector = target.GetComponent<TankBlock>().filledCells;
            for (int CellIndex = 0; CellIndex < target.GetComponent<TankBlock>().filledCells.Length; CellIndex++)
            {
                FloteBias += target.TransformPoint(intVector[CellIndex].x, intVector[CellIndex].y, intVector[CellIndex].z);
            }
            FloteBiasExtremes.x = Mathf.Max(FloteBiasExtremes.x, Mathf.Abs(currentCase.x));
            FloteBiasExtremes.y = Mathf.Max(FloteBiasExtremes.y, Mathf.Abs(currentCase.y));
            FloteBiasExtremes.z = Mathf.Max(FloteBiasExtremes.z, Mathf.Abs(currentCase.z));
            SubmergeProx = GetExtremist(FloteBiasExtremes);
            //return localDensity; //WAIT what the heck was I thinking - mass is still active even after collider disabling!
            return target.GetComponent<TankBlock>().filledCells.Length * localDensity;
        }

        private void ApplySummaryUpForce()
        {
            //Run the same code as the Water mod for consistancy's sake, but add in some spice to make it work with re-sizable object
            Vector3 vector = GetFloteBiasActual();

            float Submerge = WaterMod.QPatch.WaterHeight - vector.y;
            Submerge = (Submerge * Mathf.Abs(Submerge) / SubmergeProx) + 0.50f;
            //CalcApproxForWaterMod(); // Obsolete density calc
            if (Submerge >= -0.5f && totalFlote != 0)
            {
                if (Submerge > 1.5f)
                {
                    tank.rbody.AddForceAtPosition(Vector3.up * (1.5f * totalFlote * 5f), vector);
                    //Debug.Log("COLLIDER CONTROLLER: MAXED! " + 1.5f * totalFlote * 5f);
                    return;
                }
                else if (Submerge < -0.2f)
                {
                    Submerge = -0.2f;
                }
                tank.rbody.AddForceAtPosition(Vector3.up * (Submerge * totalFlote * 5f), vector);
                //Debug.Log("COLLIDER CONTROLLER: " + Submerge * totalFlote * 5f);
            }
        }

        private Vector3 GetFloteBiasActual()
        {
            if (FloteBiasActual == null)
            {
                FloteBiasActual = GameObject.Instantiate(new GameObject(), transform, false);
                FloteBiasActual.name = "FloteForceCentreMain";
            }
            return FloteBiasActual.transform.position;
        }

        public void FixedUpdate()
        {
            if (ColliderCommander.areAllPossibleCollidersDisabled)
            {
                if (KickStart.AutoToggleOnEnemy || KickStart.AutoToggleOnAlly)
                {
                    if (lastDetectScale != tank.blockBounds.size)
                    {
                        if (BoxCol != null)
                        {
                            //float farthest = Mathf.Max(tank.blockBounds.extents.x, Mathf.Max(tank.blockBounds.extents.y, tank.blockBounds.extents.z));
                            BoxCol.size = tank.blockBounds.size;
                            BoxCol.center = tank.trans.InverseTransformPoint(tank.boundsCentreWorld);
                            lastDetectScale = tank.blockBounds.size;
                            //Debug.Log("COLLIDER CONTROLLER: SizeChanged to " + ColliderThis.radius);
                        }
                        else
                        {
                            BoxCol = tank.gameObject.AddComponent<BoxCollider>();
                            BoxCol.isTrigger = true;
                            //float farthest = Mathf.Max(tank.blockBounds.extents.x, Mathf.Max(tank.blockBounds.extents.y, tank.blockBounds.extents.z));
                            BoxCol.size = tank.blockBounds.size;
                            BoxCol.center = tank.trans.InverseTransformPoint(tank.boundsCentreWorld);
                            tank.gameObject.layer = Globals.inst.layerSceneryCoarse;//danger
                            lastDetectScale = tank.blockBounds.size;
                            Debug.Log("COLLIDER CONTROLLER: newCollider for " + tank.name + " set to " + BoxCol.size);
                            boxExists = true;
                        }
                    }
                }
                else
                {
                    Destroy(BoxCol);
                    BoxCol = null;
                }
            }
            if (BoxCol != null)
            {
                if (lastColEnabledTime > ColliderCommander.ColliderCooldown - 5)
                {
                    int techCount = closeTechs.Count;
                    for (int step = 0; step < techCount; )
                    {
                        Tank tech = closeTechs.ElementAt(step);
                        var RCT = tech.GetComponent<RemoveColliderTank>();
                        float exts = GetExtremist(RCT.lastDetectScale) + GetExtremist(lastDetectScale);
                        if (!(bool)RCT)
                        {
                            Debug.Log("COLLIDER CONTROLLER: RemoveColliderTank NULL ON " + tank.name);
                        }
                        else if ((tech.boundsCentreWorldNoCheck - tank.boundsCentreWorldNoCheck).sqrMagnitude - (exts * exts) < 0)
                        {
                            BoxCol.enabled = true;
                            break;
                        }
                        else
                        {
                            Debug.Log("COLLIDER CONTROLLER: tech " + tech.name + " left collision rad of " + tank.name);
                            closeTechs.RemoveAt(step);
                            techCount--;
                            continue;
                        }
                        step++;
                    }
                    if (BoxCol.enabled == false)
                        lastColEnabledTime = 0;
                }
                else
                    BoxCol.enabled = false;
            }
            if (lastColEnabledTime < ColliderCommander.ColliderCooldown)
                lastColEnabledTime++;
            if (KickStart.isWaterModPresent && lastLocalState)
            {
                //RUN WATER CALCS!
                ApplySummaryUpForce();
                //Debug.Log("COLLIDER CONTROLLER: COMPENSATING FLOAT for " + tank.name + " at " + tank.rbody.position);
            }
        }


        // Collider enabling and disabling
        public void RunColliderCheck()
        {
            if (ManNetwork.inst.IsMultiplayer())
            {
                if (lastLocalState || ForceUpdate)
                {
                    EnableCollidersTank();
                    ForceUpdate = false;
                    lastLocalState = false;
                }
                return;//No action for MP
            }
            //GetComponent<Tank>().BoundsIntersectSphere()
            //Debug.Log("COLLIDER CONTROLLER: Launched Collider Controller for " + gameObject + "!");
            if (KickStart.AutoToggleOnEnemy && isTempColliderEnabled && (tank.PlayerFocused || ColliderCommander.AFFECT_ALL_TECHS))
            {
                EnableCollidersTank();
            }
            else if (ColliderCommander.areAllPossibleCollidersDisabled && (tank.PlayerFocused || ColliderCommander.AFFECT_ALL_TECHS))
            {
                DisableCollidersTank();
            }
            else
            {
                EnableCollidersTank();
            }
            ForceUpdate = false;
            lastLocalState = ColliderCommander.areAllPossibleCollidersDisabled;
        }

        /// <summary>
        /// Disables all non-wheel colliders on the tech
        /// </summary>
        private void DisableCollidersTank()
        {
            int numBottom = 0;
            //Debug.Log("COLLIDER CONTROLLER: Collider Disabling!");
            try
            {
                //blockCounter = 0;
                totalFlote = 0;
                roughTankTotalVolume = 0;
                //savedMass = tank.GetComponent<Rigidbody>().mass;
                FloteBias = Vector3.zero;
                FloteBiasExtremes = Vector3.zero;
                int child = gameObject.transform.childCount;
                for (int v = 0; v < child; ++v)
                {
                    Transform grabbedGameObject = gameObject.transform.GetChild(v);
                    try
                    {
                        //Debug.Log("\nChild " + grabbedGameObject);
                        if (ForceUpdate || KickStart.updateToggle)
                        {
                            grabbedGameObject.GetComponent<ModuleRemoveColliders>().ForceUpdateThis();
                            //Debug.Log("FORCE-UPDATED!");
                        }
                        bool isValid = grabbedGameObject.GetComponent<ModuleRemoveColliders>().DestroyCollidersOnBlock(out bool isBottom);
                        if (isBottom)
                            numBottom++;
                        if (KickStart.isWaterModPresent && isValid)
                        {
                            //blockCounter++;
                            roughTankTotalVolume += (float)grabbedGameObject.GetComponent<TankBlock>().filledCells.Length;
                            totalFlote += CalcBlockForWaterMod(grabbedGameObject);
                        }
                    }
                    catch
                    {
                        //Debug.Log("Oop slippery object " + grabbedGameObject + "! Confirmed Children " + grabbedGameObject.transform.childCount);
                        if (grabbedGameObject.transform.childCount >= 1)
                        {
                            //Debug.Log("Multiple GameObjects detected from within! - Will Attempt ClusterBodyDecoder!");
                            RecursiveClusterBodyHandlerDestroy(grabbedGameObject, ref numBottom);
                        }
                    }
                }
                //Debug.Log("COLLIDER CONTROLLER: SET ALL POSSIBLE COLLIDERS ON " + gameObject + " DISABLED!");
                if (KickStart.isWaterModPresent)
                {
                    FloteBiasFinal = FloteBias / roughTankTotalVolume;

                    if (FloteBiasActual == null)
                    {
                        FloteBiasActual = Instantiate(new GameObject(), transform, false);
                        FloteBiasActual.name = "FloteForceCentreMain";
                    }
                    if (FloteBiasFinal.IsNaN())
                        FloteBiasActual.transform.position = Vector3.zero;
                    else
                        FloteBiasActual.transform.position = FloteBiasFinal;
                    FloteBiasActual.transform.localEulerAngles = Vector3.zero;
                    FloteBiasActual.transform.localScale = Vector3.one;
                    //Debug.Log("COLLIDER CONTROLLER: CONSTRUCTED FLOAT for " + tank.name + " of strength " + totalFlote + " at " + FloteBiasActual.transform.localPosition);
                }
            }
            catch
            {
                Debug.Log("COLLIDER CONTROLLER: FetchFailiure on " + gameObject + " disable");
            }
            if (numBottom == 0)
            {
                RecalcFurthest();
                Debug.Log("COLLIDER CONTROLLER: COULD NOT FETCH extents on " + gameObject);
            }
            else
            {
                //Debug.Log("COLLIDER CONTROLLER: kept " + numBottom + " colliders as they were on the outskirts on " + gameObject);
            }
        }
        public void RequestRecalcFurthest()
        {
            needsFurthestCheck = true;
        }
        private void RecalcFurthest()
        {
            lowPos = 0;
            higPos = 0;
            lefPos = 0;
            rigPos = 0;
            forPos = 0;
            reaPos = 0;
            List<TankBlock> blocks = new List<TankBlock>();
            try
            {
                foreach (TankBlock bloc in tank.blockman.IterateBlocks())
                {
                    if (CompareExtremeBlocks(bloc.trans.localPosition))
                        blocks.Add(bloc);
                }
                foreach (TankBlock bloc2 in blocks)
                {
                    if (CompareExtremeBlocks(bloc2.trans.localPosition))
                        bloc2.GetComponent<ModuleRemoveColliders>().ReturnCollidersOnBlock();
                }
            }
            catch { }
            /*
            Debug.Log("COLLIDER CONTROLLER: Recalc Extents - " + gameObject.name +" | " + lowPos + " | " +
            higPos + " | " +
            lefPos + " | " +
            rigPos + " | " +
            forPos + " | " +
            reaPos);*/
        }

        /// <summary>
        /// Enables all colliders on the tech
        /// </summary>
        private void EnableCollidersTank()
        {
            //Debug.Log("COLLIDER CONTROLLER: Collider Enabling!");
            KickStart.lastDisabledColliderCount = 0;
            try
            {
                int child = gameObject.transform.childCount;
                for (int v = 0; v < child; ++v)
                {
                    Transform grabbedGameObject = gameObject.transform.GetChild(v);
                    try
                    {
                        //Debug.Log("\nChild " + grabbedGameObject);
                        if (ForceUpdate == true && tank.PlayerFocused == true)
                        {
                            grabbedGameObject.GetComponent<ModuleRemoveColliders>().ForceUpdateThis();
                            //Debug.Log("FORCE-UPDATED!");
                        }
                        grabbedGameObject.GetComponent<ModuleRemoveColliders>().ReturnCollidersOnBlock();
                    }
                    catch
                    {
                        //Debug.Log("Oop slippery object " + grabbedGameObject + "! Confirmed Children " + grabbedGameObject.transform.childCount);
                        if (grabbedGameObject.transform.childCount >= 1)
                        {
                            //Debug.Log("Multiple GameObjects detected from within! - Will Attempt ClusterBodyDecoder!");
                            RecursiveClusterBodyHandlerReturn(grabbedGameObject);
                        }
                    }
                }
                //Debug.Log("COLLIDER CONTROLLER: SET ALL POSSIBLE COLLIDERS ON " + gameObject + " ENABLED!");
            }
            catch
            {
                //Debug.Log("COLLIDER CONTROLLER: FetchFailiure on " + gameObject + " enable");
            }
        }


        public void Update()
        {   // This is the BIG one, handles all the major operations!
            if (tank.PlayerFocused && KickStart.noColliderModeMouse)
                ColliderCommander.AttemptAutoToggleGrab(this);
            if (needsFurthestCheck)
            {
                RecalcFurthest();
                needsFurthestCheck = false;
            }

            // Check if in combat or on ally
            if (ColliderCommander.areAllPossibleCollidersDisabled && (KickStart.AutoToggleOnAlly || KickStart.AutoToggleOnEnemy))
            {   //Collison update
                //if (boxExists)
                //    BoxCol.enabled = true;
                ReactToIncomingCollision();
                if (!isTempColliderEnabled && lastColEnabledTime < ColliderCommander.ColliderCooldown)
                {
                    //Debug.Log("COLLIDER CONTROLLER: UPDATE REQUEST - ColliderInUpdate!");
                    isTempColliderEnabled = true;
                    queuedColliderCheck = true;
                }
                else if (isTempColliderEnabled && lastColEnabledTime >= ColliderCommander.ColliderCooldown)
                {
                    //Debug.Log("COLLIDER CONTROLLER: UPDATE REQUEST - ColliderOutUpdate!");
                    isTempColliderEnabled = false;
                    queuedColliderCheck = true;
                }
            }
            else
            {
                if (boxExists)
                    BoxCol.enabled = false;
                isTempColliderEnabled = false;
            }
            if (KickStart.updateToggle)
            {
                //Debug.Log("COLLIDER CONTROLLER: UPDATE REQUEST - manual intervention!");
                if (ManTechs.inst.Count > ColliderCommander.globalTechCount)
                    ColliderCommander.globalTechCount++;
                else
                {
                    KickStart.updateToggle = false;
                    ColliderCommander.globalTechCount = 0;
                }
                ForceUpdate = true;
                queuedColliderCheck = true;
            }
            /*
            // IN DEVELOPMENT
            else if (KickStart.getAllPossibleObjects)
            {
                if (KickStart.getAllPossibleObjects == false)
                    KickStart.AllCount = 0;
                KickStart.AllCount += tank.transform.childCount;
                if (ManTechs.inst.Count > globalTechCount)
                    globalTechCount++;
                else
                {
                    KickStart.AllCount += gameObject.scene.rootCount - ManTechs.inst.Count;
                    KickStart.getAllPossibleObjects = false;
                    globalTechCount = 0;
                }
            }
            */
            else if (ColliderCommander.AFFECT_ALL_TECHS != lastStateAffectingAllTechs)
            {   //Update all techs collider states
                //Debug.Log("COLLIDER CONTROLLER: UPDATE REQUEST - toggled setting!");
                lastStateAffectingAllTechs = ColliderCommander.AFFECT_ALL_TECHS;
                queuedColliderCheck = true;
            }

            else if (tank.FirstUpdateAfterSpawn || lastName != tank.name)
            {   //Tech update
                //Debug.Log("COLLIDER CONTROLLER: UPDATE REQUEST - TechTankUpdate! " + tank.FirstUpdateAfterSpawn);
                lastName = tank.name;
                queuedColliderCheck = true;
            }

            //Disableable to reduce lag
            else if (KickStart.enableBlockUpdate && lastBlockState != tank.blockman.IterateBlocks().Count())
            {   //Block update
                //Debug.Log("COLLIDER CONTROLLER: UPDATE REQUEST - TechBlockUpdate! " + lastBlockState + " | " + lastBlockState);
                lastBlockState = tank.blockman.IterateBlocks().Count();
                queuedColliderCheck = true;
            }
            // Run the main determine of operations
            if (ColliderCommander.areAllPossibleCollidersDisabled != lastLocalState || queuedColliderCheck)
            {
                RunColliderCheck();
                queuedColliderCheck = false;
            }
        }
    }
}
