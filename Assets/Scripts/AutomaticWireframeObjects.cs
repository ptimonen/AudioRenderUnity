using UnityEngine;

public class AutomaticWireframeObjects : MonoBehaviour
{
    void Start()
    {
        if (WireframeRenderer.Instance)
        {
            MeshFilter[] meshFilters = GetComponentsInChildren<MeshFilter>(false);
            foreach (MeshFilter meshFilter in meshFilters)
            {
                if (!meshFilter.gameObject.GetComponent<WireframeObject>())
                {
                    meshFilter.gameObject.AddComponent<WireframeObject>();
                }
            }
        }
        Destroy(this);
    }
}
