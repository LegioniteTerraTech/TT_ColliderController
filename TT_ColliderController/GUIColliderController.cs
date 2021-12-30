using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace TT_ColliderController
{
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
