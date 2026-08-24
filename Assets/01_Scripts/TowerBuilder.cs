using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Torre de Jenga: registra los bloques ya colocados a mano (hijos) o los genera
/// desde un prefab. Crea un suelo físico para que las piezas caídas aterrizen.
/// Vive en el objeto raíz de la torre (el prefab "Jenga").
/// </summary>
public class TowerBuilder : MonoBehaviour
{
    [Header("Prefab del bloque (solo si se genera la torre en runtime)")]
    public GameObject blockPrefab;

    [Header("Dimensiones del bloque en metros (proporción real de Jenga)")]
    public float blockLength = 0.075f;
    public float blockWidth = 0.025f;
    public float blockHeight = 0.015f;

    [Header("Suelo (collider invisible sobre la imagen)")]
    public Vector2 groundSize = new Vector2(0.13f, 0.13f);
    public float groundThickness = 0.006f;
    public bool showGround = false;
    public Color groundColor = new Color(0.28f, 0.22f, 0.16f, 1f);

    [Header("Configuración de la torre")]
    public int totalLayers = 18;

    readonly List<JengaBlock> allBlocks = new List<JengaBlock>();
    public IReadOnlyList<JengaBlock> AllBlocks => allBlocks;

    public struct PlacementSlot
    {
        public int layerIndex;
        public int slotIndex;
        public Vector3 localPosition;
        public Quaternion localRotation;
    }

    PhysicsMaterial woodMaterial;
    Transform groundTransform;

    /// <summary>
    /// Escala del bloque: el lado largo va en el eje Z local, igual que las
    /// piezas colocadas a mano en la escena.
    /// </summary>
    public Vector3 BlockScale => new Vector3(blockWidth, blockHeight, blockLength);

    void Start()
    {
        JengaPhysics.ConfigureWorld();
        EnsurePhysicsMaterial();
        RegisterExistingBlocks();
        if (allBlocks.Count == 0)
            BuildTower();
        EnsureGround();
    }

    void EnsurePhysicsMaterial()
    {
        if (woodMaterial != null) return;
        woodMaterial = JengaPhysics.CreateWoodMaterial();
    }

    void RegisterExistingBlocks()
    {
        allBlocks.Clear();
        JengaBlock[] children = GetComponentsInChildren<JengaBlock>(true);
        for (int i = 0; i < children.Length; i++)
        {
            JengaBlock block = children[i];
            AssignLayerAndSlot(block);
            block.ApplyPhysicsMaterial(woodMaterial);
            block.SetKinematic(true);
            allBlocks.Add(block);
        }
    }

    /// <summary>
    /// Dirección, en espacio de la torre, del lado largo del bloque.
    /// Se deduce de la escala real, así funciona tanto con la torre montada a
    /// mano en la escena como con la generada desde el prefab.
    /// </summary>
    public bool IsLongAxisAlongX(Transform blockTransform)
    {
        Vector3 scale = blockTransform.lossyScale;
        Vector3 longLocal = Mathf.Abs(scale.z) >= Mathf.Abs(scale.x) ? Vector3.forward : Vector3.right;
        Vector3 local = transform.InverseTransformDirection(blockTransform.rotation * longLocal);
        return Mathf.Abs(local.x) >= Mathf.Abs(local.z);
    }

    void AssignLayerAndSlot(JengaBlock block)
    {
        Vector3 local = transform.InverseTransformPoint(block.transform.position);
        block.layerIndex = Mathf.Max(0, Mathf.RoundToInt(local.y / Mathf.Max(blockHeight, 0.0001f)));

        // Las piezas se reparten en el eje perpendicular a su lado largo.
        bool alongX = IsLongAxisAlongX(block.transform);
        float lateral = alongX ? local.z : local.x;
        block.slotIndex = Mathf.Clamp(Mathf.RoundToInt(lateral / Mathf.Max(blockWidth, 0.0001f)) + 1, 0, 2);
    }

    public void BuildTower()
    {
        if (blockPrefab == null) return;

        for (int layer = 0; layer < totalLayers; layer++)
        {
            bool alongX = layer % 2 == 1;
            float y = blockHeight * layer + blockHeight / 2f;

            for (int slot = 0; slot < 3; slot++)
            {
                GameObject go = Instantiate(blockPrefab, transform);
                float offset = (slot - 1) * blockWidth;

                Vector3 localPos = alongX
                    ? new Vector3(0f, y, offset)
                    : new Vector3(offset, y, 0f);

                go.transform.localPosition = localPos;
                go.transform.localRotation = alongX ? Quaternion.Euler(0, 90, 0) : Quaternion.identity;
                go.transform.localScale = BlockScale;

                JengaBlock block = go.GetComponent<JengaBlock>();
                block.layerIndex = layer;
                block.slotIndex = slot;
                block.ApplyPhysicsMaterial(woodMaterial);
                allBlocks.Add(block);
            }
        }
    }

    void EnsureGround()
    {
        if (groundTransform != null) return;

        Transform existing = transform.Find("Ground");
        GameObject ground = existing != null ? existing.gameObject : GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Ground";
        ground.layer = 0;
        ground.transform.SetParent(transform, false);

        float firstBottom = 0f;
        if (allBlocks.Count > 0)
        {
            firstBottom = float.MaxValue;
            for (int i = 0; i < allBlocks.Count; i++)
            {
                Vector3 local = transform.InverseTransformPoint(allBlocks[i].transform.position);
                float bottom = local.y - blockHeight * 0.5f;
                if (bottom < firstBottom) firstBottom = bottom;
            }
        }
        else
        {
            firstBottom = -blockHeight * 0.5f;
        }

        ground.transform.localRotation = Quaternion.identity;
        ground.transform.localScale = new Vector3(groundSize.x, groundThickness, groundSize.y);
        ground.transform.localPosition = new Vector3(0f, firstBottom - groundThickness * 0.5f - 0.0002f, 0f);

        Collider col = ground.GetComponent<Collider>();
        if (col == null) col = ground.AddComponent<BoxCollider>();
        col.material = woodMaterial;
        col.contactOffset = 0.0004f;

        Rigidbody rb = ground.GetComponent<Rigidbody>();
        if (rb == null) rb = ground.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.None;
        rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
        rb.Sleep();

        MeshRenderer rend = ground.GetComponent<MeshRenderer>();
        if (rend != null)
        {
            rend.enabled = showGround;
            if (showGround)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                if (shader != null)
                {
                    Material mat = new Material(shader);
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", groundColor);
                    if (mat.HasProperty("_Color")) mat.SetColor("_Color", groundColor);
                    rend.material = mat;
                }
            }
        }

        groundTransform = ground.transform;
    }

    public float TopY
    {
        get
        {
            float maxY = 0f;
            for (int i = 0; i < allBlocks.Count; i++)
            {
                if (!IsInTower(allBlocks[i])) continue;
                Vector3 local = transform.InverseTransformPoint(allBlocks[i].transform.position);
                maxY = Mathf.Max(maxY, local.y + blockHeight * 0.5f);
            }
            return maxY;
        }
    }

    public int TopLayerIndex
    {
        get
        {
            int top = 0;
            for (int i = 0; i < allBlocks.Count; i++)
            {
                if (!IsInTower(allBlocks[i])) continue;
                if (allBlocks[i].layerIndex > top)
                    top = allBlocks[i].layerIndex;
            }
            return top;
        }
    }

    public static bool IsInTower(JengaBlock block)
    {
        return block != null && !block.isRemoved && !block.isHeld;
    }

    public bool IsTopLayerBlock(JengaBlock block)
    {
        return IsInTower(block) && block.layerIndex == TopLayerIndex;
    }

    public List<PlacementSlot> GetAvailablePlacementSlots()
    {
        var slots = new List<PlacementSlot>();
        int top = TopLayerIndex;
        bool[] used = new bool[3];
        int occupied = 0;
        bool topAlongX = top % 2 == 1;
        bool foundOrientation = false;
        float layerY = 0f;
        int layerSamples = 0;

        for (int i = 0; i < allBlocks.Count; i++)
        {
            JengaBlock block = allBlocks[i];
            if (!IsInTower(block) || block.layerIndex != top) continue;

            occupied++;
            if (block.slotIndex >= 0 && block.slotIndex <= 2)
                used[block.slotIndex] = true;

            if (!foundOrientation)
            {
                topAlongX = IsLongAxisAlongX(block.transform);
                foundOrientation = true;
            }

            Vector3 local = transform.InverseTransformPoint(block.transform.position);
            layerY += local.y;
            layerSamples++;
        }

        int placeLayer;
        bool alongX;
        float y;

        if (occupied >= 3)
        {
            // Capa nueva: siempre cruzada respecto a la de abajo.
            placeLayer = top + 1;
            alongX = !topAlongX;
            y = TopY + blockHeight * 0.5f;
        }
        else
        {
            // Se completa la capa actual, así que comparte su orientación.
            placeLayer = top;
            alongX = topAlongX;
            y = layerSamples > 0 ? layerY / layerSamples : blockHeight * placeLayer + blockHeight * 0.5f;
        }

        for (int slot = 0; slot < 3; slot++)
        {
            if (occupied < 3 && used[slot]) continue;
            slots.Add(MakeSlot(placeLayer, slot, alongX, y));
        }

        return slots;
    }

    PlacementSlot MakeSlot(int layer, int slot, bool alongX, float y)
    {
        float offset = (slot - 1) * blockWidth;
        return new PlacementSlot
        {
            layerIndex = layer,
            slotIndex = slot,
            localPosition = alongX ? new Vector3(0f, y, offset) : new Vector3(offset, y, 0f),
            localRotation = alongX ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity
        };
    }

    public bool TryGetNearestSlot(Vector3 worldPoint, float maxDistance, out PlacementSlot slot)
    {
        List<PlacementSlot> available = GetAvailablePlacementSlots();
        slot = default;
        float best = maxDistance;
        bool found = false;

        for (int i = 0; i < available.Count; i++)
        {
            Vector3 world = transform.TransformPoint(available[i].localPosition);
            Vector3 delta = Vector3.ProjectOnPlane(worldPoint - world, transform.up);
            float distance = delta.magnitude;
            if (distance <= best)
            {
                best = distance;
                slot = available[i];
                found = true;
            }
        }

        return found;
    }

    public void PlaceBlock(JengaBlock block, PlacementSlot slot)
    {
        if (block == null) return;

        block.isHeld = false;
        block.isRemoved = false;
        block.layerIndex = slot.layerIndex;
        block.slotIndex = slot.slotIndex;
        block.transform.SetParent(transform, true);
        block.transform.localRotation = slot.localRotation;
        // Apoyo exacto sobre la capa: un hueco aquí se traduce en un golpe al
        // activar la física y suele tumbar la torre.
        block.transform.localPosition = slot.localPosition;
        block.transform.localScale = BlockScale;
        block.SetColliderEnabled(true);
        block.SetKinematic(true);
    }
}
