using UnityEngine;

/// <summary>
/// Se coloca en el prefab del bloque (un Cube con BoxCollider y Rigidbody).
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(BoxCollider))]
[RequireComponent(typeof(Renderer))]
public class JengaBlock : MonoBehaviour
{
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId = Shader.PropertyToID("_Color");

    [HideInInspector] public int layerIndex;
    [HideInInspector] public int slotIndex; // 0,1,2 dentro de la capa
    [HideInInspector] public bool isRemoved = false;

    private Rigidbody rb;
    private Renderer blockRenderer;
    private MaterialPropertyBlock propertyBlock;
    private Color originalColor = Color.white;
    public Rigidbody Rigidbody => rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        blockRenderer = GetComponent<Renderer>();
        propertyBlock = new MaterialPropertyBlock();
        CacheOriginalColor();
        SetKinematic(true);
    }

    void CacheOriginalColor()
    {
        if (blockRenderer == null || blockRenderer.sharedMaterial == null) return;

        Material mat = blockRenderer.sharedMaterial;
        if (mat.HasProperty(BaseColorId))
            originalColor = mat.GetColor(BaseColorId);
        else if (mat.HasProperty(ColorId))
            originalColor = mat.GetColor(ColorId);
    }

    public void SetSelectedVisual(bool selected, Color selectedColor)
    {
        if (blockRenderer == null) return;

        Color color = selected ? selectedColor : originalColor;
        blockRenderer.GetPropertyBlock(propertyBlock);
        if (blockRenderer.sharedMaterial != null && blockRenderer.sharedMaterial.HasProperty(BaseColorId))
            propertyBlock.SetColor(BaseColorId, color);
        propertyBlock.SetColor(ColorId, color);
        blockRenderer.SetPropertyBlock(propertyBlock);
    }

    public void SetKinematic(bool value)
    {
        rb.isKinematic = value;
        rb.useGravity = !value;
    }
}