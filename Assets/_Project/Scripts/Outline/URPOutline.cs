using System.Linq;
using UnityEngine;

namespace Game
{
    [RequireComponent(typeof(Renderer))]
    public class URPOutline : MonoBehaviour
    {
        [Header("Outline Settings")]
        [SerializeField] private Color outlineColor = Color.yellow;
        [SerializeField][Range(0, 5)] private float outlineWidth = 1f;

        private Renderer objectRenderer;
        private Material[] originalMaterials;
        private Material outlineMaterial;
        private bool isHighlighted;

        public Color OutlineColor
        {
            get => outlineColor;
            set
            {
                outlineColor = value;
                if (outlineMaterial != null)
                {
                    outlineMaterial.SetColor("_OutlineColor", value);
                }
            }
        }

        public float OutlineWidth
        {
            get => outlineWidth;
            set
            {
                outlineWidth = value;
                if (outlineMaterial != null)
                {
                    outlineMaterial.SetFloat("_OutlineWidth", value);
                }
            }
        }

        private void Awake()
        {
            InitializeOutline();
        }

        private void InitializeOutline()
        {
            objectRenderer = GetComponent<Renderer>();
            originalMaterials = objectRenderer.materials;

            Shader outlineShader = Shader.Find("URP/Outline");
            if (outlineShader == null)
            {
                outlineShader = Resources.FindObjectsOfTypeAll<Shader>()
                    .FirstOrDefault(shader => shader != null && shader.name == "URP/Outline");
            }

            if (outlineShader == null)
            {
                Debug.LogError("Outline shader not found! Make sure URP/Outline shader exists.");
                return;
            }

            outlineMaterial = new Material(outlineShader);
            outlineMaterial.SetColor("_OutlineColor", outlineColor);
            outlineMaterial.SetFloat("_OutlineWidth", outlineWidth);
        }

        public void SetHighlighted(bool enabled)
        {
            if (objectRenderer == null || outlineMaterial == null)
            {
                return;
            }

            if (isHighlighted == enabled)
            {
                return;
            }

            isHighlighted = enabled;

            if (enabled)
            {
                Material[] newMaterials = new Material[originalMaterials.Length + 1];
                for (int i = 0; i < originalMaterials.Length; i++)
                {
                    newMaterials[i] = originalMaterials[i];
                }

                newMaterials[newMaterials.Length - 1] = outlineMaterial;
                objectRenderer.materials = newMaterials;
            }
            else
            {
                objectRenderer.materials = originalMaterials;
            }
        }

        public void ForceHighlight(bool enabled)
        {
            SetHighlighted(enabled);
        }

        private void OnDestroy()
        {
            if (outlineMaterial != null)
            {
                Destroy(outlineMaterial);
            }
        }
    }
}
