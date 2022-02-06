using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AudioRender
{
    public class ForceSquareAspect : MonoBehaviour
    {
        void LateUpdate()
        {
            if (Screen.width != Screen.height)
            {
                int newSize = Mathf.Max(Screen.width, Screen.height);
                Screen.SetResolution(newSize, newSize, FullScreenMode.Windowed);
            }

            if (!Screen.fullScreen)
            {
                Screen.fullScreen = false;
            }
        }
    }
}
