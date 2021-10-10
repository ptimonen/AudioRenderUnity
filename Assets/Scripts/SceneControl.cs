using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneControl : MonoBehaviour
{
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            int nextScene = SceneManager.GetActiveScene().buildIndex + 1;
            Debug.Log(SceneManager.GetActiveScene().buildIndex);
            Debug.Log(nextScene);
            Debug.Log(SceneManager.sceneCountInBuildSettings);
            if (nextScene >= SceneManager.sceneCountInBuildSettings)
            {
                nextScene = 0;
            }
            Debug.Log(nextScene);
            SceneManager.LoadScene(nextScene);
        }

        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            int prevScene = SceneManager.GetActiveScene().buildIndex - 1;
            if (prevScene < 0)
            {
                prevScene = SceneManager.sceneCountInBuildSettings - 1;
            }
            SceneManager.LoadScene(prevScene);
        }
    }
}
