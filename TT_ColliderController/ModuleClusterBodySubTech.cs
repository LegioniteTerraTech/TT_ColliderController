using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace TT_ColliderController
{
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
}
