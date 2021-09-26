using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class WireframeObject : MonoBehaviour
{
    [Header("Skinned mesh to bake into mesh filter. Leave empty if object is not animated.")]
    [SerializeField] SkinnedMeshRenderer skinnedMeshRenderer;

    MeshFilter meshFilter;

    private void OnEnable()
    {
        if (!meshFilter)
        {
            meshFilter = GetComponent<MeshFilter>();
        }

        if (skinnedMeshRenderer)
        {
            if (WireframeRenderer.Instance)
            {
                WireframeRenderer.Instance.AddSkinnedMesh(skinnedMeshRenderer, meshFilter);
            }
        }
        else
        {
            if (WireframeRenderer.Instance)
            {
                WireframeRenderer.Instance.AddMesh(meshFilter);
            }
        }
    }

    private void OnDisable()
    {
        if (skinnedMeshRenderer)
        {
            if (WireframeRenderer.Instance)
            {
                WireframeRenderer.Instance.RemoveSkinnedMesh(skinnedMeshRenderer, meshFilter);
            }
        }
        else
        {
            if (WireframeRenderer.Instance)
            {
                WireframeRenderer.Instance.RemoveMesh(meshFilter);
            }
        }
    }
}
