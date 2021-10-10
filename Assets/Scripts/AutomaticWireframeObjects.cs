using UnityEngine;

public class AutomaticWireframeObjects : MonoBehaviour
{
    [Header("Gathers all mesh filters from children and makes them wireframe objects.")]

    [Header("Edges with angle below this threshold will not be rendered.")]
    [SerializeField] float edgeAngleLimit;

    [Header("Should we override existing wireframe objects from children?")]
    [SerializeField] bool overrideExistingObjects;

    void Start()
    {
        if (WireframeRenderer.Instance)
        {
            MeshFilter[] meshFilters = GetComponentsInChildren<MeshFilter>(false);
            foreach (MeshFilter meshFilter in meshFilters)
            {
                WireframeObject wireFrameObject = meshFilter.gameObject.GetComponent<WireframeObject>();
                if (!wireFrameObject)
                {
                    wireFrameObject = meshFilter.gameObject.AddComponent<WireframeObject>();
                    wireFrameObject.UpdateProperties(edgeAngleLimit);
                }
                else if (overrideExistingObjects)
                {
                    wireFrameObject.UpdateProperties(edgeAngleLimit);
                }
            }
        }
        Destroy(this);
    }
}
