using UnityEngine;

namespace AudioRender
{
    [RequireComponent(typeof(MeshFilter))]
    public class WireframeObject : MonoBehaviour
    {
        [Header("Skinned mesh to bake into mesh filter. Leave empty if object is not animated.")]
        [SerializeField] SkinnedMeshRenderer skinnedMeshRenderer;

        [Header("Edges with angle below this threshold will not be rendered.")]
        [SerializeField] float edgeAngleLimit = 0.0f;

        [Header("Should we remove materials?\nThis is useful for Gfx rendering.")]
        [SerializeField] bool removeMaterials;

#if UNITY_EDITOR
        float lastEdgeAngleLimit;

        void LateUpdate()
        {
            if (!Mathf.Approximately(lastEdgeAngleLimit, edgeAngleLimit))
            {
                Debug.Log("Forced angle limit update");
                UpdateProperties(edgeAngleLimit);
            }
            lastEdgeAngleLimit = edgeAngleLimit;
        }
#endif

        MeshFilter meshFilter;
        MeshRenderer meshRenderer;

        private void OnEnable()
        {
            if (!meshFilter)
            {
                meshFilter = GetComponent<MeshFilter>();
            }
            if (!meshRenderer)
            {
                meshRenderer = GetComponent<MeshRenderer>();
            }
            if (removeMaterials)
            {
                MeshRenderer meshRenderer = meshFilter.GetComponent<MeshRenderer>();
                if (meshRenderer)
                {
                    meshRenderer.materials = new Material[0];
                }
            }

            AddToRenderer();
        }

        private void OnDisable()
        {
            RemoveFromRenderer();
        }

        private void AddToRenderer()
        {
            if (skinnedMeshRenderer)
            {
                if (WireframeRenderer.Instance)
                {
                    WireframeRenderer.Instance.AddSkinnedMesh(skinnedMeshRenderer, meshFilter, edgeAngleLimit, meshRenderer);
                }
            }
            else
            {
                if (WireframeRenderer.Instance)
                {
                    WireframeRenderer.Instance.AddMesh(WireframeRenderer.RenderType.Triangle, meshFilter, edgeAngleLimit, meshRenderer);
                }
            }
        }

        private void RemoveFromRenderer()
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
                    WireframeRenderer.Instance.RemoveMesh(WireframeRenderer.RenderType.Triangle, meshFilter);
                }
            }
        }

        public void UpdateProperties(float edgeAngleLimit)
        {
            RemoveFromRenderer();
            this.edgeAngleLimit = edgeAngleLimit;
            AddToRenderer();
        }
    }
}
