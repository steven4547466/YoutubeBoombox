using GameNetcodeStuff;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.UIElements;

namespace YoutubeBoombox
{
    internal class YoutubeBoomboxGUI : MonoBehaviour
    {
        private float menuWidth;
        private float menuHeight;
        private float menuX;
        private float menuY;

        private string url = "Youtube URL";

        void Awake()
        {
            menuWidth = Screen.width / 3;
            menuHeight = Screen.width / 4;
            menuX = (Screen.width / 2) - (menuWidth / 2);
            menuY = (Screen.height / 2) - (menuHeight / 2);
        }

        public void OnGUI()
        {
            UnityEngine.Cursor.visible = true;
            UnityEngine.Cursor.lockState = CursorLockMode.Confined;
            GUI.Box(new Rect(menuX, menuY, menuWidth, menuHeight), "Youtube Boombox");
            url = GUI.TextField(new Rect(menuX + 25, menuY + 20, menuWidth - 50, 50), url);

            if (GUI.Button(new Rect(menuX + 25, menuY + 50 + 50, menuWidth - 50, 50), "Play"))
            {
                if (gameObject.TryGetComponent(out BoomboxController controller))
                {
                    controller.DestroyGUI();
                    controller.PlaySong(url);
                }

                UnityEngine.Cursor.visible = false;
                //Cursor.lockState = CursorLockMode.Locked;

                Destroy(this);
            }

            if (GUI.Button(new Rect(menuX + 25, menuY + 50 + 50 + 50, menuWidth - 50, 50), "Close"))
            {

                UnityEngine.Cursor.visible = false;
                //Cursor.lockState = CursorLockMode.Locked;

                if (gameObject.TryGetComponent(out BoomboxController controller))
                {
                    controller.DestroyGUI();
                }

                Destroy(this);
            }
        }
    }
}
