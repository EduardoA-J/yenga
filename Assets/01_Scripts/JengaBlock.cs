using UnityEngine;

/// <summary>
/// Se coloca en el prefab del bloque (un Cube con BoxCollider y Rigidbody).
/// Mientras está kinematic sigue al ImageTarget; al soltarlo cae con gravedad
/// hacia el suelo de la torre.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(BoxCollider))]
[RequireComponent(typeof(Renderer))]
public class JengaBlock : MonoBehaviour
{
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId = Shader.PropertyToID("_Color");

    [HideInInspector] public int layerIndex;
    [HideInInspector] public int slotIndex;
    [HideInInspector] public bool isRemoved = false;
    [HideInInspector] public bool isHeld = false;

    Rigidbody rb;
    BoxCollider boxCollider;
    Renderer blockRenderer;
    MaterialPropertyBlock propertyBlock;
    Color originalColor = Color.white;
    Transform gravitySource;

    public Rigidbody Rigidbody => rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        boxCollider = GetComponent<BoxCollider>();
        blockRenderer = GetComponent<Renderer>();
        propertyBlock = new MaterialPropertyBlock();
        gravitySource = transform.parent;

        ConfigureRigidbody();
        CacheOriginalColor();
        SetKinematic(true);
    }

    void ConfigureRigidbody()
    {
        JengaPhysics.ApplyTo(rb, boxCollider);
        ApplyKinematicPhysics(true);
    }

    void ApplyKinematicPhysics(bool kinematic)
    {
        // Interpolation and CCD fight Vuforia: the ImageTarget moves the parent
        // every frame, but PhysX keeps the old world pose and the tower drifts
        // off the image. While kinematic, follow the transform exactly.
        rb.interpolation = kinematic ? RigidbodyInterpolation.None : RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
        rb.maxDepenetrationVelocity = 0.08f;
    }

    public void ApplyPhysicsMaterial(PhysicsMaterial material)
    {
        if (boxCollider != null && material != null)
            boxCollider.material = material;
    }

    public void SetColliderEnabled(bool enabled)
    {
        if (boxCollider == null) boxCollider = GetComponent<BoxCollider>();
        if (boxCollider != null) boxCollider.enabled = enabled;
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
        if (rb == null) rb = GetComponent<Rigidbody>();

        rb.isKinematic = value;
        rb.useGravity = false;
        ApplyKinematicPhysics(value);
        if (value)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.Sleep();
        }
    }

    public void ReleaseToPhysics()
    {
        gravitySource = transform.parent;
        SetKinematic(false);
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.WakeUp();
    }

    public bool HasLanded(float speedThreshold = 0.035f)
    {
        if (rb == null || rb.isKinematic) return true;
        return rb.linearVelocity.sqrMagnitude < speedThreshold * speedThreshold
            && rb.angularVelocity.sqrMagnitude < 0.1f;
    }

    void FixedUpdate()
    {
        if (rb == null || rb.isKinematic) return;

        Vector3 down = gravitySource != null ? -gravitySource.up : Vector3.down;
        rb.AddForce(down * JengaPhysics.Gravity, ForceMode.Acceleration);

        // Amortigua microimpulsos del solver para que la pila no se "camine".
        if (rb.linearVelocity.sqrMagnitude < 0.0004f)
            rb.linearVelocity *= 0.5f;
        if (rb.angularVelocity.sqrMagnitude < 0.01f)
            rb.angularVelocity *= 0.5f;
    }
}
