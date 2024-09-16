using UnityEngine;

public class DottedLineRenderer : MonoBehaviour
{
    public LineRenderer lineRenderer;
    public Material dottedMaterial;

    void Start()
    {
        if (lineRenderer != null && dottedMaterial != null)
        {
            lineRenderer.material = dottedMaterial;
            lineRenderer.textureMode = LineTextureMode.Tile;
        }
    }
}
