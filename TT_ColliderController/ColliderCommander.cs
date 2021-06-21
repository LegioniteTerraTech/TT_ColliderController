using System;
using System.Linq;
using cakeslice;
using Rewired;
using UnityEngine;

namespace TT_ColliderController
{ 
    class ColliderCommander
    {
        //The baseline controller for all colliders on a Tech.
        //  This mod is not intended for use in MP (will still let other players hit you fine) but if there is significant demand for it, I guess I can try netcode for this.

        // GLOBAL Variables
        public static bool areAllPossibleCollidersDisabled = false;//Are all colliders disabled with current constraints? (switch)
        public static bool AFFECT_ALL_TECHS = false;    // Should we disable all Techs colliders for MT-related reasons? - togg for lols - also breaks the game if active at startup
        public static int globalTechCount = 0;          // Last Count of all Techs in the world
        public static int lastBlockCount = 0;           // Last count of blocks on player tech
        public static int enabledByCursor = 0;          // Last count of blocks enabled by the cursor in disabled mode

        public static bool clicked = false;     // Has the player done a mouse-down movement? (prevents over-updates)
        public static bool grabQueue = false;   // Is 2-step process needed to grab a block (with colliders off) queued?
        public static int camQueue = 0;         // The 3-step process needed to refresh the tech move cam (with colliders off)


        // Fixed Variables
        public static int ColliderCooldown = 200;  //Cooldown (in fixedUpdates - which is roughly 50 a sec)

        // Store in user preferences - WIP
        //int blockNamesCount = 0;
        //string[] loaded;


        public static Transform[] AllBlocks;


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
            private int lastColEnabledTime = ColliderCooldown;
            private bool queuedColliderCheck = false;
            private bool isTempColliderEnabled = false;
            private bool sphereExists = false;
            private IntVector3 lastblockEnabledPos = Vector3.back;


            // Was previously likely needed for density calculations but that was actually just a simple variable
            //private int blockCounter = 0;     // Count of blocks on Tech (OBSOLETE)
            //private float savedMass = 1f;     // Saving the tech's mass for calculations (OBSOLETE)
            //private float tankDensity = 1f;   // Density of the entire Tech (OBSOLETE)


            public void Subscribe(Tank tank)
            {
                tank.AttachEvent.Subscribe(AddBlock);
                tank.DetachEvent.Subscribe(RemoveBlock);
                this.tank = tank;
            }
            public void AddBlock(TankBlock tankblock, Tank tank)
            {
                tankblock.GetComponent<ModuleRemoveColliders>().removeColliderTank = this;
            }

            public void RemoveBlock(TankBlock tankblock, Tank tank)
            {
                tankblock.GetComponent<ModuleRemoveColliders>().removeColliderTank = null;
            }

            public float GetExtremist(Vector3 input)
            {   //but not really as it grabs the smallest extreme
                float final = Mathf.Min(input.y, input.x);
                final = Mathf.Max(final, input.z);
                return final;
            }

            public void WarnCollisionDamage()
            {
                //Debug.Log("Tank direct hit");
                if (KickStart.AutoToggleOnEnemy)
                    WarnCollision();
            }

            public void WarnCollision()
            {
                var thisInst = tank.GetComponent<RemoveColliderTank>();
                thisInst.lastColEnabledTime = 0;
            }

            private void reactToIncomingCollision()
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
            private void OnTriggerStay(Collider collider)
            {
                var thisInst = tank.GetComponent<RemoveColliderTank>();
                //collider = thisInst.GetComponent<Tank>().dragSphere
                if (areAllPossibleCollidersDisabled && thisInst.lastColEnabledTime > ColliderCooldown / 2)
                {
                    if (KickStart.AutoToggleOnAlly)
                    {
                        try
                        {
                            var projectileIn = collider.gameObject.GetComponent<Projectile>();
                            if (projectileIn != null)
                            {
                                //Debug.Log("OnEnemyProjectileEnter");
                                if (projectileIn.Shooter.IsFriendly (tank.Team))
                                    WarnCollision();
                            }
                            else
                            {
                                var tankIn = collider.transform.root.GetComponent<Tank>();
                                if (tankIn != null)
                                {
                                    //Debug.Log("OnAlliedTankEnter");
                                    if (tankIn.IsFriendly(tank.Team) && !(tank.IsAnchored && tankIn.IsAnchored))
                                    {   //Two anchored techs shouldn't be able to collide - base walls
                                        WarnCollision();
                                    }
                                }
                            }
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
                                        WarnCollision();
                                }
                            }
                        }
                        catch { }
                    }
                }
            }


            public void RecursiveClusterBodyHandlerDestroy(Transform grabbedGameObject)
            {
                //All temp variables used in the creation of the water-based clusterbody floatation summary
                var thisInst = tank.GetComponent<RemoveColliderTank>();
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
                        if (thisInst.ForceUpdate == true && tank.PlayerFocused == true)
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
                        bool isValid = grabbedGameObjectCB.GetComponent<ModuleRemoveColliders>().DestroyCollidersOnBlock();
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
                            Debug.Log("(CB) Performing Recursive Action on " + grabbedGameObjectCB + "!  Confirmed Children " + grabbedGameObjectCB.transform.childCount);
                            RecursiveClusterBodyHandlerDestroy(grabbedGameObjectCB);
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
                    Debug.Log("CB has " + blockCountCB + " blocks");
                }
            }

            public void RecursiveClusterBodyHandlerReturn(Transform grabbedGameObject)
            {
                var thisInst = tank.GetComponent<RemoveColliderTank>();
                int childCB = grabbedGameObject.transform.childCount;
                for (int vCB = 0; vCB < childCB; ++vCB)
                {
                    Transform grabbedGameObjectCB = grabbedGameObject.transform.GetChild(vCB);
                    try
                    {
                        //Debug.Log("\n(CB) Child " + grabbedGameObjectCB + " child of " + grabbedGameObject);
                        if (thisInst.ForceUpdate == true && tank.PlayerFocused == true)
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
                            Debug.Log("(CB) Performing Recursive Action on " + grabbedGameObjectCB + "!  Confirmed Children " + grabbedGameObjectCB.transform.childCount);
                            RecursiveClusterBodyHandlerReturn(grabbedGameObjectCB);
                        }
                    }
                }
            }


            /*
            // Obsolete
            private void CalcApproxForWaterMod()
            {
                //it's not perfect but it will help with water-based Techs
                var thisInst = tank.GetComponent<RemoveColliderTank>();
                thisInst.tankDensity = thisInst.savedMass / thisInst.roughTankTotalVolume;
            }
            */


            private float CalcBlockForWaterMod(Transform target)
            {
                //it's not perfect but it will help with water-based Techs
                var thisInst = tank.GetComponent<RemoveColliderTank>();
                Vector3 currentCase = target.transform.localPosition + target.GetComponent<TankBlock>().CentreOfMass;
                IntVector3[] intVector = target.GetComponent<TankBlock>().filledCells;
                for (int CellIndex = 0; CellIndex < target.GetComponent<TankBlock>().filledCells.Length; CellIndex++)
                {
                    FloteBias += target.TransformPoint(intVector[CellIndex].x, intVector[CellIndex].y, intVector[CellIndex].z);
                }
                thisInst.FloteBiasExtremes.x = Mathf.Max(thisInst.FloteBiasExtremes.x, Mathf.Abs(currentCase.x));
                thisInst.FloteBiasExtremes.y = Mathf.Max(thisInst.FloteBiasExtremes.y, Mathf.Abs(currentCase.y));
                thisInst.FloteBiasExtremes.z = Mathf.Max(thisInst.FloteBiasExtremes.z, Mathf.Abs(currentCase.z));
                thisInst.SubmergeProx = GetExtremist(thisInst.FloteBiasExtremes);
                //return localDensity; //WAIT what the heck was I thinking - mass is still active even after collider disabling!
                return target.GetComponent<TankBlock>().filledCells.Length * localDensity;
            }


            public int RecursiveToggleGrab(Transform thisTank)
            {
                int step = 0;
                Debug.Log("ScaleTechs: AutoGrab block count changed or cursor colliders exceeded!");

                foreach (ModuleRemoveColliders bloc in thisTank.GetComponentsInChildren<ModuleRemoveColliders>())
                {
                    AllBlocks[step] = bloc.transform;
                    if (bloc.mouseHoverStat)
                    {
                        bloc.DestroyCollidersOnBlock();
                        bloc.mouseHoverStat = false;
                    }
                    step++;
                }
                lastBlockCount = thisTank.GetComponentsInChildren<ModuleClusterBodyMarker>().Length;
                foreach (ModuleClusterBodyMarker ClusterB in thisTank.GetComponentsInChildren<ModuleClusterBodyMarker>())
                {
                    step += RecursiveToggleGrab(ClusterB.transform);
                }
                return step;
            }


            // A unholy mess, but not the biggest of them all 
            //  - I present to you, 
            //   THE NON-COLLIDERGRABBINATOR!
            public void AttemptAutoToggleGrab()
            {
                //Very early attempt, very limited, need help pls...
                //  I know the AP system could be used for this matter but I have no idea how to grab that info without making a mess
                //Debug.Log("ScaleTechs - AutoGrab: RUNNING!");

                if (grabQueue)
                {
                    grabQueue = false;
                    try
                    {
                        Singleton.Manager<ManPointer>.inst.MouseEvent.Send(ManPointer.Event.LMB, true, false);
                        Debug.Log("ScaleTechs: AutoGrab GRABBED " + Singleton.Manager<ManPointer>.inst.targetVisible.name + "!");
                    }
                    catch
                    {
                        Debug.Log("ScaleTechs: AutoGrab Grabbed no object");
                    }
                    return;
                }
                else if (camQueue == 2)
                {
                    camQueue = 0;
                    try { Singleton.Manager<ManPointer>.inst.MouseEvent.Send(ManPointer.Event.RMB, false, false); }
                    catch { }
                    return;
                }
                else if (camQueue == 1)
                {
                    camQueue = 2;
                    try { Singleton.Manager<ManPointer>.inst.MouseEvent.Send(ManPointer.Event.RMB, true, false); }
                    catch { }
                    return;
                }

                //Singleton.Manager<ManInput>.inst.GetButtonDown() //< will need to do more research to grab info on custom keybinds! 
                if (!clicked && !KickStart.collidersEnabled && (Input.GetMouseButton(0) && !Input.GetKeyDown(KeyCode.T) || Input.GetMouseButton(1) && this.GetComponent<Tank>().beam.IsActive) && !Singleton.Manager<ManPointer>.inst.IsInteractionBlocked)
                {
                    clicked = true;
                    GameObject ProbeDirect;
                    GameObject EstScanPos;
                    try
                    {   //try get the tank
                        Vector3 pos = Camera.main.transform.position;
                        Vector3 posD = Singleton.camera.ScreenPointToRay(Input.mousePosition).direction.normalized;

                        //Debug.Log("ScaleTechs: Camera has " + Camera.main.transform.childCount + " children present already");

                        if (Camera.main.transform.childCount == 0)
                        {
                            ProbeDirect = Instantiate(new GameObject(), Camera.main.transform, false);
                            ProbeDirect.name = "ProbeDirect";
                            Debug.Log("ScaleTechs: AutoGrab probe launcher created");
                        }
                        if (Camera.main.transform.GetChild(0).childCount == 0)
                        {
                            EstScanPos = Instantiate(new GameObject(), Camera.main.transform.GetChild(0), false);
                            EstScanPos.name = "Probe";
                            Debug.Log("ScaleTechs: AutoGrab scanner created");
                        }

                        // PREP before probe launch
                        var ProbeLauncher = Camera.main.transform.GetChild(0);
                        ProbeLauncher.localPosition = Vector3.zero;
                        ProbeLauncher.transform.rotation = Quaternion.LookRotation(posD, Vector3.up);
                        var scanner = Camera.main.transform.GetChild(0).GetChild(0);
                        scanner.transform.position = pos + Vector3.forward;


                        int layerMask = Globals.inst.layerCosmetic.mask;
                        //int layerMask = -1;
                        //Ray FindCast = new Ray(ProbeLauncher.position, ProbeLauncher.forward);


                        RaycastHit rayman;
                        Physics.Raycast(scanner.position, scanner.forward, out rayman, 300, layerMask);
                        //FindCast, rayman, 5, layerMask);  , QueryTriggerInteraction.Ignore

                        var thisTank = GetComponent<Tank>();
                        if (rayman.collider.IsNull())
                        {
                            Debug.Log("ScaleTechs: Did not find target");
                        }
                        else
                        {
                            Debug.Log("ScaleTechs: target layer is " + rayman.collider.gameObject.layer.ToString() + " and targetname " + rayman.collider.gameObject.name);

                            if (rayman.collider.transform.root.GetComponent<Tank>() != null)
                            {
                                thisTank = rayman.collider.transform.root.GetComponent<Tank>();
                                if (thisTank.gameObject.GetComponent<RemoveColliderTank>().isTempColliderEnabled)
                                    return; //All colliders are active on this already!
                            }
                            else
                            {
                                //Debug.Log("ScaleTechs: AutoGrab tried grabbing tank but no such tank exists!  Defaulting to player Tech!"); 
                            }
                        }

                        try
                        {
                            bool scanned = false;
                            int zoom = 0;
                            Debug.Log("ScaleTechs: AutoGrab locked on " + thisTank.name);
                            //Debug.Log("ScaleTechs: AutoGrab cursor at " + (pos - thisTank.rbody.position) + " in relation to tank");
                            if (KickStart.isControlBlocksPresent)
                            {
                                // MK1
                                //   a freaking mess.  I believed blockman wasn't such a great superhero and didn't trust them.
                                //   at least this still works for Control Blocks, will work as a fallback method.
                                int step = 0;
                                int collidersEnabled = 0;
                                int col = thisTank.GetComponentsInChildren<ModuleRemoveColliders>().Length;
                                if (lastBlockCount != col || 250 < enabledByCursor || col / 2 < enabledByCursor)
                                {
                                    enabledByCursor = 0;
                                    lastBlockCount = thisTank.GetComponent<Tank>().blockman.blockCount;
                                    AllBlocks = new Transform[thisTank.GetComponent<Tank>().blockman.blockCount];
                                    step = RecursiveToggleGrab(thisTank.transform);
                                    //Debug.Log("ScaleTechs: AutoGrab stepped " + step + " times!");
                                }

                                AllBlocks = AllBlocks.OrderBy((d) => (d.position - pos).sqrMagnitude).ToArray();
                                if (AllBlocks.Length >= 1)
                                {
                                    var bloc = AllBlocks[0].GetComponent<ModuleRemoveColliders>();
                                    //Debug.Log("ScaleTechs: AutoGrab center block " + bloc.name);
                                }
                                else
                                {
                                    Debug.Log("ScaleTechs: AutoGrab COULD NOT FIND ANY TARGET!");
                                    return;
                                }
                                //Debug.Log("ScaleTechs: AutoGrab Launched scanner dumbfire-pseudo raycast");
                                // cause any colliders or raycasts won't do us any good
                                //   so we send a probe to search for the closest TankBlock-related *GameObject*



                                // Probe launch!
                                Vector3 change = scanner.localPosition;
                                change.z += (AllBlocks[0].position - scanner.position).magnitude - 4;
                                scanner.localPosition = change;

                                //Debug.Log("ScaleTechs: AutoGrab scanner - jumping to approx target at " + (AllBlocks[0].position - scanner.position).magnitude + " at tank");


                                float last = 256;
                                for (zoom = 0; 8 > zoom; zoom++)
                                {
                                    float test = (AllBlocks[0].position - scanner.position).sqrMagnitude;
                                    if (last + 4 < test)
                                        break;
                                    last = test;
                                    //Debug.Log("ScaleTechs: AutoGrab scanner closetarget = " + (AllBlocks[0].position - scanner.position).magnitude);
                                    if ((AllBlocks[0].position - scanner.position).sqrMagnitude < 25)//scan of radius 5
                                    {
                                        //Debug.Log("ScaleTechs: AutoGrab scanner has aquired range!  Adding colliders!");
                                        scanned = true;
                                        int toTimes = 0;
                                        for (int firedTimes = 0; (AllBlocks[firedTimes].position - scanner.position).sqrMagnitude < 25; firedTimes++)
                                        {
                                            var bloc = AllBlocks[firedTimes].GetComponent<ModuleRemoveColliders>();
                                            if (!bloc.mouseHoverStat)
                                            {
                                                bool y = bloc.ReturnCollidersOnBlock();
                                                bloc.mouseHoverStat = true;
                                                if (y)
                                                {
                                                    toTimes++;
                                                    enabledByCursor++;
                                                    collidersEnabled++;
                                                }
                                            }
                                            if (collidersEnabled >= KickStart.ActiveColliders)
                                                break;
                                        }
                                        //Debug.Log("ScaleTechs: AutoGrab added " +  grabbedtoTimes + " colliders this run.");
                                    }
                                    if (collidersEnabled >= KickStart.ActiveColliders)
                                        break;
                                    Vector3 changeStep = scanner.localPosition;
                                    changeStep.z += 5f;
                                    scanner.localPosition = changeStep;
                                    AllBlocks = AllBlocks.OrderBy((d) => (d.position - scanner.position).sqrMagnitude).ToArray();
                                }
                            }
                            else     // If there's no control blocks involved, we can do the faster processing
                            {
                                //MK2
                                //  BELIEVE in the almighty blockman!
                                //    not only do they minimise frame chugging, they also allow less sorting operations to take place resulting in less frame loss

                                scanner.localPosition = Vector3.zero;
                                layerMask = Globals.inst.layerTank.mask | Globals.inst.layerTankIgnoreTerrain.mask;

                                //Debug.Log("ScaleTechs: AutoGrab2 launching");
                                for (zoom = 0; 128 > zoom; zoom++)
                                {
                                    //float farthest = Mathf.Max(tank.blockBounds.extents.x, Mathf.Max(tank.blockBounds.extents.y, tank.blockBounds.extents.z));
                                    //thisTank.BoundsIntersectSphere.
                                    Vector3 toTankCoords = Vector3Int.RoundToInt(thisTank.transform.InverseTransformPoint(scanner.position));
                                    IntVector3 toTankCoords2 = toTankCoords;
                                    //Debug.Log("ScaleTechs: AutoGrab2 Attempting block fetch");
                                    var blockValid = thisTank.blockman.GetBlockAtPosition(toTankCoords2);
                                    if (blockValid != null)
                                    {
                                        scanned = true;
                                        var toProc = blockValid.gameObject.GetComponent<ModuleRemoveColliders>();
                                        toProc.ReturnCollidersOnBlock();
                                        toProc.mouseHoverStat = true;

                                        //Debug.Log("ScaleTechs: AutoGrab2 Confirming block");
                                        Vector3 AimedPos = scanner.TransformPoint(Vector3.back);
                                        Physics.Raycast(AimedPos, scanner.forward, out rayman, 1.5f, layerMask);
                                        //Debug.Log("ScaleTechs: AutoGrab2 Doing precheck");
                                        var checkExists = rayman.collider.IsNotNull();
                                        if (checkExists)
                                        {
                                            if (toTankCoords2 == thisTank.gameObject.GetComponent<RemoveColliderTank>().lastblockEnabledPos)
                                            {
                                                Debug.Log("ScaleTechs: AutoGrab2 -there's a block active already there!  at " + toTankCoords2 + " | " + thisTank.gameObject.GetComponent<RemoveColliderTank>().lastblockEnabledPos);
                                                return;
                                            }
                                            else
                                            {
                                                Debug.Log("ScaleTechs: AutoGrab2 Success!  Target " + checkExists);
                                                var blockThere = thisTank.blockman.GetBlockAtPosition(thisTank.gameObject.GetComponent<RemoveColliderTank>().lastblockEnabledPos);
                                                if (blockThere != null)
                                                {
                                                    blockThere.gameObject.GetComponent<ModuleRemoveColliders>().DestroyCollidersOnBlock();
                                                    Debug.Log("ScaleTechs: AutoGrab2 removed last enabled block on tech");
                                                }
                                                thisTank.gameObject.GetComponent<RemoveColliderTank>().lastblockEnabledPos = toTankCoords2;
                                                break;
                                            }
                                        }
                                        else
                                        {
                                            //Debug.Log("ScaleTechs: AutoGrab2 Fail on find attempt, trying again next step");
                                            thisTank.blockman.GetBlockAtPosition(toTankCoords2).gameObject.GetComponent<ModuleRemoveColliders>().DestroyCollidersOnBlock();
                                        }
                                    }
                                    Vector3 changeStep = scanner.localPosition;
                                    changeStep.z += 0.5f;
                                    scanner.localPosition = changeStep;
                                }
                            }

                            if (!scanned)
                            {
                                //Debug.Log("ScaleTechs: AutoGrab COULD NOT FIND TARGET!   error: OutOfBoundsException");
                                /*
                                // MK0
                                // Disabled as it adds to lag when just causally panning around
                                AllBlocks = AllBlocks.OrderBy((d) => (d.position - pos).sqrMagnitude).ToArray();
                                for (int count = 0; KickStart.ActiveColliders > count && AllBlocks.Length > count; count++)
                                {
                                    var bloc = AllBlocks[count].GetComponent<ModuleRemoveColliders>();
                                    if (!bloc.mouseHoverStat)
                                    {
                                        bloc.ReturnCollidersOnBlock();
                                        bloc.mouseHoverStat = true;
                                    }
                                }
                                */
                                return;
                            }
                            try
                            {
                                // We mind-control the in-game mouse to re-grab the block!
                                if (Input.GetMouseButton(0))
                                {
                                    Singleton.Manager<ManPointer>.inst.MouseEvent.Send(ManPointer.Event.LMB, false, false);
                                    //Debug.Log("ScaleTechs: AutoGrab GRABBED " + Singleton.Manager<ManPointer>.inst.targetVisible.name + "!");
                                    grabQueue = true;
                                }
                                else
                                {
                                    Singleton.Manager<ManPointer>.inst.MouseEvent.Send(ManPointer.Event.RMB, false, false);
                                    camQueue = 1;
                                }
                                return;
                            }
                            catch
                            {
                                Debug.Log("ScaleTechs: AutoGrab Failed to release!");
                            }
                        }
                        catch (Exception e)
                        {
                            Debug.Log("ScaleTechs: AutoGrab Failiure");
                            Debug.Log(e);
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.Log("ScaleTechs: CAMERA DOES NOT EXIST!");
                        Debug.Log(e);
                    }
                }
                else if (clicked && !Input.GetMouseButton(0) && !Input.GetMouseButton(1))
                    clicked = false; //re-enable when we have released the buttons
                return;
            }


            private void ApplySummaryUpForce()
            {   
                //Run the same code as the Water mod for consistancy's sake, but add in some spice to make it work with re-sizable object
                Vector3 vector = GetFloteBiasActual();

                var thisInst = tank.GetComponent<RemoveColliderTank>();
                float Submerge = WaterMod.QPatch.WaterHeight - vector.y;
                Submerge = (Submerge * Mathf.Abs(Submerge) / thisInst.SubmergeProx) + 0.50f;
                //CalcApproxForWaterMod(); // Obsolete density calc
                if (Submerge >= -0.5f && thisInst.totalFlote != 0)
                {
                    if (Submerge > 1.5f)
                    {
                        tank.rbody.AddForceAtPosition(Vector3.up * (1.5f * thisInst.totalFlote * 5f), vector);
                        //Debug.Log("COLLIDER CONTROLLER: MAXED! " + 1.5f * thisInst.totalFlote * 5f);
                        return;
                    }
                    else if (Submerge < -0.2f)
                    {
                        Submerge = -0.2f;
                    }
                    tank.rbody.AddForceAtPosition(Vector3.up * (Submerge * thisInst.totalFlote * 5f), vector);
                    //Debug.Log("COLLIDER CONTROLLER: " + Submerge * thisInst.totalFlote * 5f);
                }
            }

            private Vector3 GetFloteBiasActual()
            {
                var thisInst = tank.GetComponent<RemoveColliderTank>();
                if (FloteBiasActual == null)
                {
                    thisInst.FloteBiasActual = GameObject.Instantiate(new GameObject(), transform, false);
                    thisInst.FloteBiasActual.name = "FloteForceCentreMain";
                }
                return thisInst.FloteBiasActual.transform.position;
            }

            public void FixedUpdate()
            {
                var thisInst = tank.GetComponent<RemoveColliderTank>();
                var ColliderThis = tank.GetComponent<SphereCollider>();
                if (areAllPossibleCollidersDisabled && KickStart.AutoToggleOnEnemy && thisInst.lastDetectScale != tank.blockBounds.size)
                {
                    if (ColliderThis != null)
                    {
                        float farthest = Mathf.Max(tank.blockBounds.extents.x, Mathf.Max(tank.blockBounds.extents.y, tank.blockBounds.extents.z));
                        ColliderThis.radius = (farthest / 2) + 4;
                        thisInst.lastDetectScale = tank.blockBounds.size;
                        //Debug.Log("COLLIDER CONTROLLER: SizeChanged to " + ColliderThis.radius);
                    }
                    else
                    {
                        var newCol = tank.gameObject.AddComponent<SphereCollider>();
                        newCol.isTrigger = true;
                        float farthest = Mathf.Max(tank.blockBounds.extents.x, Mathf.Max(tank.blockBounds.extents.y, tank.blockBounds.extents.z));
                        newCol.radius = (farthest / 2) + 4;
                        tank.gameObject.layer = Globals.inst.layerTrigger;//danger
                        thisInst.lastDetectScale = tank.blockBounds.size;
                        Debug.Log("COLLIDER CONTROLLER: newCollider for " + tank.name + " set to " + newCol.radius);
                        sphereExists = true;
                    }
                }
                if (ColliderThis != null)
                {
                    if (thisInst.lastColEnabledTime > ColliderCooldown / 2)
                        ColliderThis.enabled = true;
                    else
                        ColliderThis.enabled = false;
                }
                if (thisInst.lastColEnabledTime < ColliderCooldown)
                    thisInst.lastColEnabledTime++;
                if (KickStart.isWaterModPresent && thisInst.lastLocalState)
                {
                    //RUN WATER CALCS!
                    ApplySummaryUpForce();
                    //Debug.Log("COLLIDER CONTROLLER: COMPENSATING FLOAT for " + tank.name + " at " + tank.rbody.position);
                }
            }


            public void RunColliderCheck()
            {
                var thisInst = tank.GetComponent<RemoveColliderTank>();
                if (ManNetwork.inst.IsMultiplayer())
                {
                    if (lastLocalState || thisInst.ForceUpdate)
                    {
                        EnableCollidersTank();
                        thisInst.ForceUpdate = false;
                        thisInst.lastLocalState = false;
                    }
                    return;//No action for MP
                }
                //thisInst.GetComponent<Tank>().BoundsIntersectSphere()
                //Debug.Log("COLLIDER CONTROLLER: Launched Collider Controller for " + gameObject + "!");
                if(KickStart.AutoToggleOnEnemy && thisInst.isTempColliderEnabled && (tank.PlayerFocused || AFFECT_ALL_TECHS))
                {
                    EnableCollidersTank();
                }
                else if (areAllPossibleCollidersDisabled && (tank.PlayerFocused || AFFECT_ALL_TECHS))
                {
                    DisableCollidersTank();
                }
                else
                {
                    EnableCollidersTank();
                }
                thisInst.ForceUpdate = false;
                thisInst.lastLocalState = areAllPossibleCollidersDisabled;
            }

            private void DisableCollidersTank()
            {
                var thisInst = tank.GetComponent<RemoveColliderTank>();
                //Debug.Log("COLLIDER CONTROLLER: Collider Disabling!");
                try
                {
                    //thisInst.blockCounter = 0;
                    thisInst.totalFlote = 0;
                    thisInst.roughTankTotalVolume = 0;
                    //thisInst.savedMass = tank.GetComponent<Rigidbody>().mass;
                    thisInst.FloteBias = Vector3.zero;
                    thisInst.FloteBiasExtremes = Vector3.zero;
                    int child = gameObject.transform.childCount;
                    for (int v = 0; v < child; ++v)
                    {
                        Transform grabbedGameObject = gameObject.transform.GetChild(v);
                        try
                        {
                            //Debug.Log("\nChild " + grabbedGameObject);
                            if (thisInst.ForceUpdate || KickStart.updateToggle)
                            {
                                grabbedGameObject.GetComponent<ModuleRemoveColliders>().ForceUpdateThis();
                                //Debug.Log("FORCE-UPDATED!");
                            }
                            bool isValid = grabbedGameObject.GetComponent<ModuleRemoveColliders>().DestroyCollidersOnBlock();
                            if (KickStart.isWaterModPresent && isValid)
                            {
                                //thisInst.blockCounter++;
                                thisInst.roughTankTotalVolume += (float)grabbedGameObject.GetComponent<TankBlock>().filledCells.Length;
                                thisInst.totalFlote += CalcBlockForWaterMod(grabbedGameObject);
                            }
                        }
                        catch
                        {
                            //Debug.Log("Oop slippery object " + grabbedGameObject + "! Confirmed Children " + grabbedGameObject.transform.childCount);
                            if (grabbedGameObject.transform.childCount >= 1)
                            {
                                //Debug.Log("Multiple GameObjects detected from within! - Will Attempt ClusterBodyDecoder!");
                                RecursiveClusterBodyHandlerDestroy(grabbedGameObject);
                            }
                        }
                    }
                    //Debug.Log("COLLIDER CONTROLLER: SET ALL POSSIBLE COLLIDERS ON " + gameObject + " DISABLED!");
                    if (KickStart.isWaterModPresent)
                    {
                        thisInst.FloteBiasFinal = thisInst.FloteBias / thisInst.roughTankTotalVolume;

                        if (FloteBiasActual == null)
                        {
                            thisInst.FloteBiasActual = Instantiate(new GameObject(), thisInst.transform, false);
                            thisInst.FloteBiasActual.name = "FloteForceCentreMain";
                        }
                        if (FloteBiasFinal.IsNaN())
                            thisInst.FloteBiasActual.transform.position = Vector3.zero;
                        else
                            thisInst.FloteBiasActual.transform.position = FloteBiasFinal;
                        thisInst.FloteBiasActual.transform.localEulerAngles = Vector3.zero;
                        thisInst.FloteBiasActual.transform.localScale = Vector3.one;
                        //Debug.Log("COLLIDER CONTROLLER: CONSTRUCTED FLOAT for " + tank.name + " of strength " + thisInst.totalFlote + " at " + thisInst.FloteBiasActual.transform.localPosition);
                    }
                }
                catch
                {
                    //Debug.Log("COLLIDER CONTROLLER: FetchFailiure on " + gameObject + " disable");
                }
            }

            private void EnableCollidersTank()
            {
                var thisInst = tank.GetComponent<RemoveColliderTank>();
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
                            if (thisInst.ForceUpdate == true && tank.PlayerFocused == true)
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
                var thisInst = tank.GetComponent<RemoveColliderTank>();
                if (tank.PlayerFocused && KickStart.noColliderModeMouse)
                    AttemptAutoToggleGrab();

                // Check if in combat or on ally
                if (areAllPossibleCollidersDisabled && (KickStart.AutoToggleOnAlly || KickStart.AutoToggleOnEnemy))
                {   //Collison update
                    if (sphereExists)
                        tank.GetComponent<SphereCollider>().enabled = true;
                    reactToIncomingCollision();
                    if (!thisInst.isTempColliderEnabled && thisInst.lastColEnabledTime < ColliderCooldown)
                    {
                        //Debug.Log("COLLIDER CONTROLLER: UPDATE REQUEST - ColliderInUpdate!");
                        thisInst.isTempColliderEnabled = true;
                        thisInst.queuedColliderCheck = true;
                    }
                    else if (thisInst.isTempColliderEnabled && thisInst.lastColEnabledTime >= ColliderCooldown)
                    {
                        //Debug.Log("COLLIDER CONTROLLER: UPDATE REQUEST - ColliderOutUpdate!");
                        thisInst.isTempColliderEnabled = false;
                        thisInst.queuedColliderCheck = true;
                    }
                }
                else
                {
                    if (sphereExists)
                        tank.GetComponent<SphereCollider>().enabled = false;
                    thisInst.isTempColliderEnabled = false;
                }
                if (KickStart.updateToggle)
                {
                    //Debug.Log("COLLIDER CONTROLLER: UPDATE REQUEST - manual intervention!");
                    if (ManTechs.inst.Count > globalTechCount)
                        globalTechCount++;
                    else
                    {
                        KickStart.updateToggle = false;
                        globalTechCount = 0;
                    }
                    thisInst.ForceUpdate = true;
                    thisInst.queuedColliderCheck = true;
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
                else if (AFFECT_ALL_TECHS != thisInst.lastStateAffectingAllTechs)
                {   //Update all techs collider states
                    //Debug.Log("COLLIDER CONTROLLER: UPDATE REQUEST - toggled setting!");
                    thisInst.lastStateAffectingAllTechs = AFFECT_ALL_TECHS;
                    thisInst.queuedColliderCheck = true;
                }

                else if (tank.FirstUpdateAfterSpawn || thisInst.lastName != tank.name)
                {   //Tech update
                    //Debug.Log("COLLIDER CONTROLLER: UPDATE REQUEST - TechTankUpdate! " + tank.FirstUpdateAfterSpawn);
                    thisInst.lastName = tank.name;
                    thisInst.queuedColliderCheck = true;
                }

                //Disableable to reduce lag
                else if (KickStart.enableBlockUpdate && thisInst.lastBlockState != tank.GetComponent<BlockManager>().blockCount)
                {   //Block update
                    //Debug.Log("COLLIDER CONTROLLER: UPDATE REQUEST - TechBlockUpdate! " + lastBlockState + " | " + thisInst.lastBlockState);
                    thisInst.lastBlockState = tank.GetComponent<BlockManager>().blockCount;
                    thisInst.queuedColliderCheck = true;
                }
                // Run the main determine of operations
                if (areAllPossibleCollidersDisabled != thisInst.lastLocalState || thisInst.queuedColliderCheck)
                {
                    RunColliderCheck();
                    thisInst.queuedColliderCheck = false;
                }
            }
        }

        public class ModuleClusterBodyMarker : MonoBehaviour
        {
            //only for Clusterbodies, nothing more.
        }

        public class ModuleClusterBodySubTech : MonoBehaviour
        {
            //This is only put in if it has to handle Clusterbodies with water mod
            public Vector3 FloteForceCenter;
            public Vector3 FloteForceRange;
            public float FloteForce = 0f;
            public float FloteExtreme = 0f;
            private GameObject FloteForceCenterTrans;

            private bool awaitingLoad = true;

            private void ApplySummaryUpForceCB()
            { //Run the same code as the Water mod for consistancy's sake, but add in some spice to make it work with re-sizable object
                try
                {
                    var tank = transform.root.GetComponent<Tank>();
                    var thisInst = gameObject.GetComponent<ModuleClusterBodySubTech>();


                    Vector3 vector = GetCurrentFloteForceCenter().position;
                    float Submerge = WaterMod.QPatch.WaterHeight - vector.y;
                    Submerge = ((Submerge * Mathf.Abs(Submerge)) / thisInst.FloteExtreme) + 0.5f;
                    if (Submerge >= -0.5f)
                    {
                        if (Submerge > 1.5f)
                        {
                            tank.rbody.AddForceAtPosition(Vector3.up * (1.5f * thisInst.FloteForce * 5f), vector);
                            return;
                        }
                        else if (Submerge < -0.2f)
                        {
                            Submerge = -0.2f;
                        }
                        tank.rbody.AddForceAtPosition(Vector3.up * (Submerge * thisInst.FloteForce * 5f), vector);
                    }
                }
                catch { }
            }

            private Transform GetCurrentFloteForceCenter()
            {
                var thisInst = gameObject.GetComponent<ModuleClusterBodySubTech>();
                Transform final = thisInst.FloteForceCenterTrans.transform;

                return final;
            }

            public void FixedUpdate()
            {
                var thisInst = gameObject.GetComponent<ModuleClusterBodySubTech>();
                if (thisInst.awaitingLoad)
                {
                    thisInst.FloteForceCenterTrans = Instantiate(new GameObject(), thisInst.transform, false);
                    thisInst.FloteForceCenterTrans.name = "FloteForceCentreCB";
                    thisInst.FloteForceCenterTrans.transform.position = FloteForceCenter;
                    thisInst.FloteForceCenterTrans.transform.localEulerAngles = Vector3.zero;
                    thisInst.FloteForceCenterTrans.transform.localScale = Vector3.one;
                    thisInst.awaitingLoad = false;
                    //Debug.Log("COLLIDER CONTROLLER: CONSTRUCTED FLOAT(CB) of strength " + thisInst.FloteForce + " at " + GetCurrentFloteForceCenter().localPosition);
                }

                if (transform.root.GetComponent<Tank>() == null)
                    Destroy(thisInst);//PREVENT CRASH!

                else if (transform.root.GetComponent<RemoveColliderTank>().lastLocalState)
                {
                    //RUN WATER CALCS!
                    ApplySummaryUpForceCB();
                }
            }
        }


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
            public RemoveColliderTank removeColliderTank;
            public TankBlock TankBlock;

            public bool mouseHoverStat = false;     // Has the mouse previously enabled the colliders on this?

            private bool areAllCollidersDisabledOnThisBlock = false;    // Have all of the colliders been disabled on this before?
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
                TankBlock.GetComponent<ModuleRemoveColliders>().UpdateNow = true;
            }

            public void OnPool()
            {
                //Inititate Collider Cleansing on first pool
                string CB = gameObject.name;
                //Debug.Log("Processing " + gameObject + " " + CB);

                var thisBlock = TankBlock.GetComponent<ModuleRemoveColliders>();
                thisBlock.pendingScan = false;

                // Filtering Modules
                bool thisIsACab = gameObject.transform.GetComponent<ModuleTechController>();
                if (thisIsACab)
                {
                    //Debug.Log("CAB DETECTED IN " + gameObject + " DENYING CHANGES!");
                    thisBlock.DenyColliderChange = true;
                    return;//End it NOW!
                }
                bool thisIsABubble = gameObject.transform.GetComponent<ModuleShieldGenerator>();
                if (thisIsABubble)
                {
                    //Debug.Log("SHIELD DETECTED IN " + gameObject + " DENYING CHANGES!");
                    thisBlock.DenyColliderChange = true;
                    return;//End it NOW!
                }
                bool thisIsAnAnchor = gameObject.transform.GetComponent<ModuleAnchor>();
                if (thisIsAnAnchor)
                {
                    //Debug.Log("ANCHOR DETECTED IN " + gameObject + " DENYING CHANGES!");
                    thisBlock.DenyColliderChange = true;
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
                    thisBlock.DenyColliderChange = true;
                    return;//End it NOW!
                }
                if (CB == "EXP_Platform_Ramp_214" || CB == "EXP_Platform_Ramp_213" || CB == "EXP_Platform_Ramp_224" || CB == "EXP_Platform_414")
                { //The RR Ramps are to remain uneffected.
                    //Debug.Log("TECH PLATFORM BLOCK " + gameObject + " DENYING CHANGES!");
                    thisBlock.DenyColliderChange = true;
                    return;//End it NOW!
                }

                // Now filter Fusion Blocks
                if (CB == "_C_BLOCK:98341" || CB == "_C_BLOCK:98342" || CB == "_C_BLOCK:98343" || CB == "_C_BLOCK:98344" || CB == "_C_BLOCK:98345")
                { //GC Small Friction Pad and Non Slip-A<Tron 3000  |  Every MTMag.
                    //Debug.Log("FUSION BLOCK DETECTED IN " + gameObject + " DENYING CHANGES!");
                    thisBlock.DenyColliderChange = true;
                    return;//End it NOW!
                }

                // Now filter Control Blocks
                if (CB == "_C_BLOCK:1293831" || CB == "_C_BLOCK:1293830" || CB == "_C_BLOCK:1293700" || CB == "_C_BLOCK:1293701" || CB == "_C_BLOCK:1293702" || CB == "_C_BLOCK:1293703")
                { //GC Small Friction Pad and Non Slip-A<Tron 3000  |  Every MTMag.
                    //Debug.Log("UNEDITABLE CONTROL BLOCK DETECTED IN " + gameObject + " DENYING CHANGES!");
                    thisBlock.DenyColliderChange = true;
                    return;//End it NOW!
                }
                if (CB == "_C_BLOCK:1293838" || CB == "_C_BLOCK:129380" || CB == "_C_BLOCK:129381" || CB == "_C_BLOCK:6194710" || CB == "_C_BLOCK:1293834" || CB == "_C_BLOCK:1293837" ||
                    CB == "_C_BLOCK:1980325" || CB == "_C_BLOCK:1293835" || CB == "_C_BLOCK:1393838" || CB == "_C_BLOCK:1393837" || CB == "_C_BLOCK:1393836" || CB == "_C_BLOCK:1393835" ||
                    CB == "_C_BLOCK:29571436")
                { //EVERY PISTON AND SWIVEL
                    //Debug.Log("CLUSTERBODY CONTROL BLOCK DETECTED IN " + gameObject + "!  Will hand off future operations to RemoveColliderTank!");
                    thisBlock.DenyColliderChange = true;
                    return;//End it NOW!
                }

                // NOW we move onwards to the blocks that are kept based on player intentions
                if (KickStart.KeepBFFaceBlocks && (CB == "BF_Block_Faired_111" || CB == "BF_Block_Smooth_111" || CB == "BF_Block_Smooth_Edge_111" || CB == "BF_Block_Smooth_Corner_111" || CB == "BF_Block_Smooth_213"))
                { //BF Face Blocks
                    //Debug.Log("BF FACE BLOCK " + gameObject + " MARKING FOR FUTURE REFERENCE!");
                    thisBlock.BlockCanBeToggled = 1;
                    return;//End it NOW!
                }
                if (KickStart.KeepGCArmorPlates && (CB == "GC_Armour_Plate_221" || CB == "GC_Armour_Plate_421" || CB == "GC_Armour_Plate_121"))
                { //GC Shock Plates
                    //Debug.Log("GC SHOCK PLATE " + gameObject + " MARKING FOR FUTURE REFERENCE!");
                    thisBlock.BlockCanBeToggled = 2;
                    return;//End it NOW!
                }
                if (KickStart.KeepHESlopes && (CB == "HE_ArmouredBlock_111" || CB == "HE_ArmouredBlock_112" || CB == "HE_ArmouredBlock_113" || CB == "HE_ArmouredBlock_114"))
                { //HE Armour Slopes
                    //Debug.Log("HE ARMOR SLOPE " + gameObject + " MARKING FOR FUTURE REFERENCE!");
                    thisBlock.BlockCanBeToggled = 3;
                    return;//End it NOW!
                }
                if (KickStart.KeepGSOArmorPlates && (CB == "GSO_ArmourPlate_Small_111" || CB == "GSO_ArmourPlate_Medium_211" || CB == "GSO_ArmourPlate_Large_222" || CB == "GSO_ArmourPlate_Cab_111" ||
                    CB == "GSO_Plough_311" || CB == "GSO_Plough_CowCatcher_321" || CB == "GSO_Plough_211"))
                { //GSO Armour Slopes and Ploughs
                    //Debug.Log("GSO ARMOR PLATE " + gameObject + " MARKING FOR FUTURE REFERENCE!");
                    thisBlock.BlockCanBeToggled = 4;
                    return;//End it NOW!
                }

            }

            private void DisableColliders()
            {
                // The Cleanup Procedure 
                //   - Non-recursive as most colliders on a huge Tech are normal block colliders
                var thisBlock = TankBlock.GetComponent<ModuleRemoveColliders>();
                thisBlock.UpdateNow = false;
                try//Sometimes there are colliders in the very base
                {
                    foreach (Collider col in thisBlock.GetComponents<Collider>())
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
                thisBlock.areAllCollidersDisabledOnThisBlock = true;
            }


            private void EnableColliders()
            {
                var thisBlock = TankBlock.GetComponent<ModuleRemoveColliders>();
                thisBlock.UpdateNow = false;
                try//Sometimes there are colliders in the very base
                {
                    foreach (Collider col in thisBlock.GetComponents<Collider>())
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
                    //Try to cycle through EVERY GameObject on this block to disable EVERY COLLIDER
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
                thisBlock.areAllCollidersDisabledOnThisBlock = false;

            }

            public bool DestroyCollidersOnBlock()
            {
                //Inititate Collider Cleaning
                //string CB = gameObject.name;
                //Debug.Log("Processing " + gameObject + " " + CB);

                var thisBlock = TankBlock.GetComponent<ModuleRemoveColliders>();
                if (thisBlock.pendingScan)
                    OnPool();

                if (thisBlock.DenyColliderChange)
                    return false;//END


                // NOW we move onwards to the blocks that are kept based on player intentions
                if (KickStart.KeepBFFaceBlocks && thisBlock.BlockCanBeToggled == 1)
                { //BF Face Blocks
                    //Debug.Log("BF FACE BLOCK " + gameObject + " CANCELING!");
                    EnableColliders();
                    return false;//End it NOW!
                }
                if (KickStart.KeepGCArmorPlates && thisBlock.BlockCanBeToggled == 2)
                { //GC Shock Plates
                    //Debug.Log("GC SHOCK PLATE " + gameObject + " CANCELING!");
                    EnableColliders();
                    return false;//End it NOW!
                }
                if (KickStart.KeepHESlopes && thisBlock.BlockCanBeToggled == 3)
                { //HE Armour Slopes
                    //Debug.Log("HE ARMOR SLOPE " + gameObject + " CANCELING!");
                    EnableColliders();
                    return false;//End it NOW!
                }
                if (KickStart.KeepGSOArmorPlates && thisBlock.BlockCanBeToggled == 4)
                { //GSO Armour Slopes and Ploughs
                    //Debug.Log("GSO ARMOR PLATE " + gameObject + " CANCELING!");
                    EnableColliders();
                    return false;//End it NOW!
                }

                if (thisBlock.DoNotDisableColliders != true && thisBlock.areAllCollidersDisabledOnThisBlock == false || thisBlock.UpdateNow)
                {
                    DisableColliders();
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

                var thisBlock = TankBlock.GetComponent<ModuleRemoveColliders>();
                if (thisBlock.pendingScan)
                    OnPool();

                if (thisBlock.DenyColliderChange)
                    return false;//END


                // We don't disable the returning colliders to allow those to return even when toggled on when they were not here previously
                if (thisBlock.DoNotDisableColliders != true && thisBlock.areAllCollidersDisabledOnThisBlock == true)
                {
                    EnableColliders();
                    return true;
                }
                //Otherwise take no action
                return false;
            }
        }
    }
}
