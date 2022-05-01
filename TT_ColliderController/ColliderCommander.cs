using System;
using System.Linq;
using cakeslice;
using Rewired;
using UnityEngine;

namespace TT_ColliderController
{
    public static class ColliderCommander
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

        public static bool Thorough = true;    // Disable EVERY possible Tech collider?

        // Fixed Variables
        public static int ColliderCooldown = 200;  //Cooldown (in fixedUpdates - which is roughly every 4 sec)

        // Store in user preferences - WIP
        //int blockNamesCount = 0;
        //string[] loaded;


        public static Transform[] AllBlocks;

        // Click-based block enabler

        // A unholy mess, but not the biggest of them all 
        //  - I present to you, 
        //   THE NON-COLLIDERGRABBINATOR!
        public static void AttemptAutoToggleGrab(RemoveColliderTank RCTank)
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

            Tank thisTank = RCTank.tank;
            //Singleton.Manager<ManInput>.inst.GetButtonDown() //< will need to do more research to grab info on custom keybinds! 
            if (!clicked && !KickStart.collidersEnabled && (Input.GetMouseButton(0) && !Input.GetKeyDown(KeyCode.T) || Input.GetMouseButton(1) && thisTank.beam.IsActive) && !Singleton.Manager<ManPointer>.inst.IsInteractionBlocked)
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
                        ProbeDirect = UnityEngine.Object.Instantiate(new GameObject(), Camera.main.transform, false);
                        ProbeDirect.name = "ProbeDirect";
                        Debug.Log("ScaleTechs: AutoGrab probe launcher created");
                    }
                    if (Camera.main.transform.GetChild(0).childCount == 0)
                    {
                        EstScanPos = UnityEngine.Object.Instantiate(new GameObject(), Camera.main.transform.GetChild(0), false);
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
                        Debug.Log("ScaleTechs: AutoGrab locked on " + thisTank.name);
                        //Debug.Log("ScaleTechs: AutoGrab cursor at " + (pos - thisTank.rbody.position) + " in relation to tank");
                        if (KickStart.isControlBlocksPresent)
                        {
                            TryGrabUniversal(thisTank, RCTank, pos, ref scanned, ref scanner);
                        }
                        else     // If there's no control blocks involved, we can do the faster processing
                        {
                            TryGrabBlockMan(thisTank, RCTank, ref rayman, ref scanned, ref layerMask, ref scanner);
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
                                if (Singleton.Manager<ManPointer>.inst.DraggingItem.IsNull())
                                {
                                    Singleton.Manager<ManPointer>.inst.MouseEvent.Send(ManPointer.Event.RMB, false, false);
                                    camQueue = 1;
                                }
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
            else if (ColliderCommander.clicked && !Input.GetMouseButton(0) && !Input.GetMouseButton(1))
                ColliderCommander.clicked = false; //re-enable when we have released the buttons
            return;
        }

        public static int RecursiveToggleGrab(Transform thisTank)
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


        // MK1
        //   a freaking mess.  I believed blockman wasn't such a great superhero and didn't trust them.
        //   at least this still works for Control Blocks, will work as a fallback method.
        private static void TryGrabUniversal(Tank thisTank, RemoveColliderTank RCTank, Vector3 pos, ref bool scanned, ref Transform scanner)
        {
            int step = 0;
            int collidersEnabled = 0;
            int col = thisTank.GetComponentsInChildren<ModuleRemoveColliders>().Length;
            if (lastBlockCount != col || 250 < enabledByCursor || col / 2 < enabledByCursor)
            {
                enabledByCursor = 0;
                lastBlockCount = thisTank.GetComponent<Tank>().blockman.IterateBlocks().Count();
                AllBlocks = new Transform[lastBlockCount];
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
            for (int zoom = 0; 8 > zoom; zoom++)
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
                Vector3 scanPos = scanner.position;
                AllBlocks = AllBlocks.OrderBy((d) => (d.position - scanPos).sqrMagnitude).ToArray();
            }
        }

        //MK2
        //  BELIEVE in the almighty blockman!
        //    not only do they minimize frame chugging, they also allow less sorting operations to take place resulting in less frame loss
        private static void TryGrabBlockMan(Tank thisTank, RemoveColliderTank RCTank, ref RaycastHit rayman, ref bool scanned, ref int layerMask, ref Transform scanner)
        {
            scanner.localPosition = Vector3.zero;
            layerMask = Globals.inst.layerTank.mask | Globals.inst.layerTankIgnoreTerrain.mask;

            //Debug.Log("ScaleTechs: AutoGrab2 launching");
            for (int zoom = 0; 128 > zoom; zoom++)
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
                        if (toTankCoords2 == RCTank.lastblockEnabledPos)
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

    }
}
