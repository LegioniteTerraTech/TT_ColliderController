using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TT_ColliderController
{
    class BlockScanner
    {
        internal class GUIBlockGrabber : MonoBehaviour
        {
            //OBSOLETE!
            //public static GUIBlockGrabber inst;

            //private ColliderCommander.ModuleRemoveColliders targetBlock;
            public static Transform[] AllBlocks;
            public int ActiveColliders = 20;

            public static void Initiate()
            {
                IM
                var BlockHandle = new GameObject();
                BlockHandle.AddComponent<GUIBlockGrabber>();
                BlockHandle.SetActive(true);
            }

            private void Update()
            {
                //Debug.Log("ScaleTechs - LocalGUI: RUNNING!");
                if (!KickStart.collidersEnabled && !Singleton.Manager<ManPointer>.inst.DraggingItem && Input.GetMouseButtonDown(0))
                {
                    try
                    {
                        if (Singleton.Manager<ManTechBuilder>.inst.)
                        {
                            var thatTank = Singleton.Manager<ManTechs>.inst.;
                            Debug.Log("ScaleTechs: AutoGrab locked on " + thatTank.name);
                            Vector3 pos = Camera.main.transform.TransformPoint(Singleton.Manager<ManPointer>.inst.GetEmulatedCursorPos().ToVector3XY());
                            //.block.GetComponent<ColliderCommander.ModuleRemoveColliders>();
                            //BlockManager.  .BlockIterator<ColliderCommander.ModuleRemoveColliders>
                            //AllObj = FindObjectsOfType<Transform>();
                            int step = 0;
                            Debug.Log("ScaleTechs: AutoGrab found " + FindObjectsOfType<ColliderCommander.ModuleRemoveColliders>());
                            AllBlocks = new Transform[FindObjectsOfType<ColliderCommander.ModuleRemoveColliders>().Length];
                            foreach (ColliderCommander.ModuleRemoveColliders bloc in thatTank.transform)
                            {
                                AllBlocks[step] = bloc.transform;
                                if (bloc.mouseHoverStat)
                                {
                                    bloc.DestroyCollidersOnBlock();
                                    bloc.mouseHoverStat = false;
                                }
                                step++;
                            }
                            Debug.Log("ScaleTechs: AutoGrab stepped " + step + " times!");

                            AllBlocks = AllBlocks.OrderBy((d) => (d.position - pos).sqrMagnitude).ToArray();
                            for (int count = 0; ActiveColliders > count; count++)
                            {
                                var bloc = AllBlocks[count].GetComponent<ColliderCommander.ModuleRemoveColliders>();
                                if (!bloc.mouseHoverStat)
                                {
                                    bloc.ReturnCollidersOnBlock();
                                    bloc.mouseHoverStat = true;
                                }
                                /*
                                try
                                {
                                    Singleton.Manager<ManPointer>.inst.SetGrabbedTarget.
                                }
                                catch { }
                                */
                            }
                            //KickStart.AllCollidersCount = FindObjectsOfType<ColliderCommander.ModuleRemoveColliders>();
                            Debug.Log("ScaleTechs: AutoGrab Success");
                        }
                    }
                    catch (Exception e)
                    {
                        //targetBlock = null;
                        Debug.Log("ScaleTechs: AutoGrab Failiure");
                        Debug.Log(e);
                    }
                }
            }
        }
    }
}
