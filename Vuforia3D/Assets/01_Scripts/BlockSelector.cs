using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

/// <summary>
/// Selección y arrastre de bloques con el New Input System (Enhanced Touch).
/// Un dedo: toca para seleccionar (cambia de color y muestra flechas) y
/// arrastra para mover, siguiendo el dedo sobre el plano de la torre.
/// </summary>
public class BlockSelector : MonoBehaviour
{
    public Camera arCamera;
    public TowerBuilder towerBuilder;
    public StabilityMonitor stabilityMonitor;
    public LayerMask blockLayerMask;

    [Header("Extracción")]
    public float extractThreshold = 0.06f;
    public float moveStep = 0.012f;

    [Header("Selección visual")]
    public Color selectedColor = new Color(1f, 0.55f, 0.1f);
    public Color arrowColor = new Color(1f, 0.85f, 0.15f);
    public float arrowWorldLength = 0.028f;
    public float arrowWorldThickness = 0.01f;

    const float TapPixelSlop = 24f;

    JengaBlock selectedBlock;
    Vector3 originalWorldPos;
    GameObject posArrow;
    GameObject negArrow;
    Material arrowMaterial;

    Vector2 pointerDownPos;
    Vector3 lastDragWorld;
    bool isDragging;
    bool pointerHeld;

    void OnEnable()
    {
        EnhancedTouchSupport.Enable();
    }

    void OnDisable()
    {
        EnhancedTouchSupport.Disable();
        pointerHeld = false;
        isDragging = false;
    }

    void Update()
    {
        if (TurnManager.Instance != null && TurnManager.Instance.IsGameOver) return;
        if (arCamera == null) return;

        if (Touch.activeTouches.Count > 0)
        {
            Touch touch = Touch.activeTouches[0];
            HandlePointer(touch.screenPosition, touch.phase);
            return;
        }

        if (Mouse.current == null) return;

        Vector2 mousePos = Mouse.current.position.ReadValue();
        if (Mouse.current.leftButton.wasPressedThisFrame)
            HandlePointer(mousePos, UnityEngine.InputSystem.TouchPhase.Began);
        else if (Mouse.current.leftButton.wasReleasedThisFrame && pointerHeld)
            HandlePointer(mousePos, UnityEngine.InputSystem.TouchPhase.Ended);
        else if (Mouse.current.leftButton.isPressed && pointerHeld)
            HandlePointer(mousePos, UnityEngine.InputSystem.TouchPhase.Moved);
    }

    void HandlePointer(Vector2 screenPos, UnityEngine.InputSystem.TouchPhase phase)
    {
        switch (phase)
        {
            case UnityEngine.InputSystem.TouchPhase.Began:
                BeginPointer(screenPos);
                break;
            case UnityEngine.InputSystem.TouchPhase.Moved:
            case UnityEngine.InputSystem.TouchPhase.Stationary:
                if (pointerHeld)
                    DragPointer(screenPos);
                break;
            case UnityEngine.InputSystem.TouchPhase.Ended:
            case UnityEngine.InputSystem.TouchPhase.Canceled:
                EndPointer(screenPos);
                break;
        }
    }

    void BeginPointer(Vector2 screenPos)
    {
        pointerHeld = true;
        isDragging = false;
        pointerDownPos = screenPos;

        if (!RaycastScene(screenPos, out RaycastHit hit))
            return;

        BlockMoveArrow arrow = hit.collider.GetComponentInParent<BlockMoveArrow>();
        if (arrow != null && selectedBlock != null)
            return;

        JengaBlock block = GetBlock(hit.collider);
        if (block != null && CanSelect(block))
        {
            if (selectedBlock != block)
            {
                ClearSelection(keepPosition: true);
                SelectBlock(block);
            }

            lastDragWorld = ProjectOnDragPlane(screenPos, selectedBlock);
            isDragging = true;
        }
    }

    void DragPointer(Vector2 screenPos)
    {
        if (!isDragging || selectedBlock == null) return;
        if (Vector2.Distance(screenPos, pointerDownPos) < TapPixelSlop) return;

        Vector3 worldPoint = ProjectOnDragPlane(screenPos, selectedBlock);
        Vector3 delta = worldPoint - lastDragWorld;
        selectedBlock.transform.position += delta;
        lastDragWorld = worldPoint;
    }

    void EndPointer(Vector2 screenPos)
    {
        if (!pointerHeld) return;
        pointerHeld = false;

        bool wasDrag = isDragging && Vector2.Distance(screenPos, pointerDownPos) > TapPixelSlop;
        isDragging = false;

        if (wasDrag)
            return;

        if (!RaycastScene(screenPos, out RaycastHit hit))
        {
            ClearSelection(keepPosition: true);
            return;
        }

        BlockMoveArrow arrow = hit.collider.GetComponentInParent<BlockMoveArrow>();
        if (arrow != null && selectedBlock != null)
        {
            NudgeSelected(arrow.direction);
            return;
        }

        JengaBlock block = GetBlock(hit.collider);
        if (block != null && CanSelect(block))
        {
            if (selectedBlock == block)
            {
                ClearSelection(keepPosition: true);
                return;
            }

            ClearSelection(keepPosition: true);
            SelectBlock(block);
            return;
        }

        ClearSelection(keepPosition: true);
    }

    Vector3 ProjectOnDragPlane(Vector2 screenPos, JengaBlock block)
    {
        Vector3 planeNormal = block.transform.parent != null
            ? block.transform.parent.up
            : Vector3.up;
        Plane plane = new Plane(planeNormal, block.transform.position);
        Ray ray = arCamera.ScreenPointToRay(screenPos);
        if (plane.Raycast(ray, out float distance))
            return ray.GetPoint(distance);

        return block.transform.position;
    }

    bool RaycastScene(Vector2 screenPos, out RaycastHit hit)
    {
        Ray ray = arCamera.ScreenPointToRay(screenPos);
        return Physics.Raycast(ray, out hit, 8f, ~0, QueryTriggerInteraction.Collide);
    }

    static JengaBlock GetBlock(Collider col)
    {
        if (col == null) return null;
        JengaBlock block = col.GetComponent<JengaBlock>();
        if (block != null) return block;
        return col.GetComponentInParent<JengaBlock>();
    }

    bool CanSelect(JengaBlock block)
    {
        if (block == null || block.isRemoved) return false;
        if (towerBuilder == null || !towerBuilder.isActiveAndEnabled)
            return true;

        int topLayer = towerBuilder.TopLayerIndex;
        return block.layerIndex <= topLayer - 1;
    }

    void SelectBlock(JengaBlock block)
    {
        selectedBlock = block;
        selectedBlock.SetKinematic(true);
        originalWorldPos = block.transform.position;
        selectedBlock.SetSelectedVisual(true, selectedColor);
        ShowArrows(block);
    }

    void NudgeSelected(int direction)
    {
        Vector3 axis = selectedBlock.transform.right;
        selectedBlock.transform.position += axis * (moveStep * direction);

        float distanceMoved = Vector3.Distance(selectedBlock.transform.position, originalWorldPos);
        if (stabilityMonitor != null && distanceMoved >= extractThreshold)
        {
            JengaBlock block = selectedBlock;
            HideArrows();
            block.SetSelectedVisual(false, selectedColor);
            selectedBlock = null;
            PlaceOnTop(block);
        }
    }

    void ClearSelection(bool keepPosition)
    {
        if (selectedBlock == null)
        {
            HideArrows();
            return;
        }

        JengaBlock block = selectedBlock;
        HideArrows();
        block.SetSelectedVisual(false, selectedColor);
        selectedBlock = null;

        if (!keepPosition)
        {
            block.transform.position = originalWorldPos;
            block.SetKinematic(true);
        }
    }

    void PlaceOnTop(JengaBlock block)
    {
        block.isRemoved = true;

        if (towerBuilder == null || !towerBuilder.isActiveAndEnabled)
            return;

        int newLayer = towerBuilder.TopLayerIndex + 1;
        bool rotated = newLayer % 2 == 1;
        float y = towerBuilder.TopY + 0.002f;

        block.transform.SetParent(towerBuilder.transform);
        block.transform.localPosition = new Vector3(0f, y, 0f);
        block.transform.localRotation = rotated ? Quaternion.Euler(0, 90, 0) : Quaternion.identity;
        block.SetKinematic(true);

        stabilityMonitor.SettleTower(() => TurnManager.Instance?.NextTurn());
    }

    void ShowArrows(JengaBlock block)
    {
        HideArrows();
        posArrow = CreateArrow("ArrowPositive", block.transform, new Vector3(0.62f, 0f, 0f), Quaternion.identity, 1);
        negArrow = CreateArrow("ArrowNegative", block.transform, new Vector3(-0.62f, 0f, 0f), Quaternion.Euler(0f, 180f, 0f), -1);
    }

    void HideArrows()
    {
        if (posArrow != null) Destroy(posArrow);
        if (negArrow != null) Destroy(negArrow);
        posArrow = null;
        negArrow = null;
    }

    GameObject CreateArrow(string name, Transform parent, Vector3 localPos, Quaternion localRot, int direction)
    {
        GameObject root = new GameObject(name);
        root.transform.SetParent(parent, false);
        root.transform.localPosition = localPos;
        root.transform.localRotation = localRot;

        Vector3 lossy = parent.lossyScale;
        root.transform.localScale = new Vector3(
            arrowWorldLength / Mathf.Max(Mathf.Abs(lossy.x), 0.0001f),
            arrowWorldThickness / Mathf.Max(Mathf.Abs(lossy.y), 0.0001f),
            arrowWorldThickness / Mathf.Max(Mathf.Abs(lossy.z), 0.0001f));

        BlockMoveArrow marker = root.AddComponent<BlockMoveArrow>();
        marker.direction = direction;

        BoxCollider col = root.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.center = new Vector3(0.5f, 0f, 0f);
        col.size = new Vector3(1.15f, 1.4f, 1.4f);

        BuildArrowVisual(root.transform);
        return root;
    }

    void BuildArrowVisual(Transform root)
    {
        Material mat = GetArrowMaterial();

        GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        shaft.name = "Shaft";
        shaft.transform.SetParent(root, false);
        shaft.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        shaft.transform.localPosition = new Vector3(0.32f, 0f, 0f);
        shaft.transform.localScale = new Vector3(0.28f, 0.32f, 0.28f);
        ApplyArrowVisual(shaft, mat);

        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Cube);
        head.name = "Head";
        head.transform.SetParent(root, false);
        head.transform.localPosition = new Vector3(0.78f, 0f, 0f);
        head.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        head.transform.localScale = new Vector3(0.42f, 0.42f, 0.42f);
        ApplyArrowVisual(head, mat);
    }

    void ApplyArrowVisual(GameObject go, Material mat)
    {
        Collider col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);

        MeshRenderer rend = go.GetComponent<MeshRenderer>();
        if (rend != null) rend.sharedMaterial = mat;
    }

    Material GetArrowMaterial()
    {
        if (arrowMaterial != null) return arrowMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        arrowMaterial = new Material(shader);
        if (arrowMaterial.HasProperty("_BaseColor"))
            arrowMaterial.SetColor("_BaseColor", arrowColor);
        if (arrowMaterial.HasProperty("_Color"))
            arrowMaterial.SetColor("_Color", arrowColor);
        return arrowMaterial;
    }

    void OnDestroy()
    {
        HideArrows();
        if (arrowMaterial != null) Destroy(arrowMaterial);
    }
}
