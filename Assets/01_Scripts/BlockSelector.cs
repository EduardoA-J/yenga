using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using System.Collections.Generic;

/// <summary>
/// Jenga AR: extraer un bloque (nunca el de la cima) y colocarlo arriba.
/// Solo se puede manipular con seguimiento Vuforia estable.
/// Un movimiento inválido no cambia el turno.
/// </summary>
public class BlockSelector : MonoBehaviour
{
    public Camera arCamera;
    public TowerBuilder towerBuilder;
    public StabilityMonitor stabilityMonitor;
    public LayerMask blockLayerMask;

    [Header("Extracción")]
    public float extractThreshold = 0.055f;
    public float moveStep = 0.012f;
    [Tooltip("Eleva un poco el bloque al seleccionarlo (separación visual).")]
    public float selectLiftOffset = 0.002f;

    [Header("Arrastre")]
    [Tooltip("Escala el desplazamiento del dedo (menor = más control).")]
    public float dragSensitivity = 0.85f;
    [Tooltip("Máximo desplazamiento por frame para evitar saltos bruscos.")]
    public float maxDragStep = 0.014f;
    public float tapPixelSlop = 28f;

    [Header("Colocación")]
    public float snapDistance = 0.035f;

    [Header("Selección visual")]
    public Color selectedColor = new Color(1f, 0.55f, 0.1f);
    public Color arrowColor = new Color(1f, 0.85f, 0.15f);
    public Color ghostColor = new Color(0.25f, 0.95f, 0.4f, 0.55f);
    public Color ghostHighlightColor = new Color(1f, 0.9f, 0.2f, 0.75f);
    public float arrowWorldLength = 0.028f;
    public float arrowWorldThickness = 0.01f;

    JengaBlock selectedBlock;
    JengaBlock heldBlock;
    Vector3 originalWorldPos;
    GameObject posArrow;
    GameObject negArrow;
    Material arrowMaterial;
    Material ghostMaterial;
    Material ghostHighlightMaterial;
    readonly List<GameObject> ghosts = new List<GameObject>();

    Vector2 pointerDownPos;
    Vector3 lastDragWorld;
    bool isDragging;
    bool pointerHeld;
    bool settling;

    void Awake()
    {
        if (GetComponent<ARTrackingGate>() == null)
            gameObject.AddComponent<ARTrackingGate>();

        BindTower();
    }

    void BindTower()
    {
        GameObject jenga = FindJenga();
        if (jenga == null) return;

        towerBuilder = jenga.GetComponent<TowerBuilder>();
        if (towerBuilder == null)
            towerBuilder = jenga.AddComponent<TowerBuilder>();

        stabilityMonitor = jenga.GetComponent<StabilityMonitor>();
        if (stabilityMonitor == null)
            stabilityMonitor = jenga.AddComponent<StabilityMonitor>();

        stabilityMonitor.towerBuilder = towerBuilder;
    }

    static GameObject FindJenga()
    {
        GameObject named = GameObject.Find("Jenga");
        if (named != null) return named;

        GameObject imageTarget = GameObject.Find("ImageTarget");
        if (imageTarget == null) return null;

        Transform child = imageTarget.transform.Find("Jenga");
        return child != null ? child.gameObject : null;
    }

    void OnEnable()
    {
        EnhancedTouchSupport.Enable();
        BindTower();
    }

    void OnDisable()
    {
        EnhancedTouchSupport.Disable();
        pointerHeld = false;
        isDragging = false;
    }

    bool CanManipulate
    {
        get
        {
            if (TurnManager.Instance != null && TurnManager.Instance.IsGameOver) return false;
            if (settling) return false;
            if (arCamera == null) return false;
            if (ARTrackingGate.Instance == null || !ARTrackingGate.Instance.IsStable) return false;
            return true;
        }
    }

    bool IsPlacing => heldBlock != null
                      && TurnManager.Instance != null
                      && TurnManager.Instance.Phase == TurnManager.TurnPhase.Place;

    void Update()
    {
        if (TurnManager.Instance != null && TurnManager.Instance.IsGameOver) return;
        if (settling) return;
        if (arCamera == null) return;
        if (towerBuilder == null) BindTower();
        if (!CanManipulate) return;

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

        if (IsPlacing)
        {
            lastDragWorld = ProjectOnTopPlane(screenPos);
            isDragging = true;
            return;
        }

        if (!RaycastScene(screenPos, out RaycastHit hit))
            return;

        BlockMoveArrow arrow = hit.collider.GetComponentInParent<BlockMoveArrow>();
        if (arrow != null && selectedBlock != null)
            return;

        JengaBlock block = GetBlock(hit.collider);
        if (block == null) return;

        if (!CanSelect(block))
        {
            if (towerBuilder != null && towerBuilder.IsTopLayerBlock(block))
                TurnManager.Instance?.NotifyInvalidMove("No se pueden retirar bloques del nivel superior.");
            return;
        }

        if (selectedBlock != block)
        {
            ClearSelection(keepPosition: false);
            SelectBlock(block);
        }

        lastDragWorld = ProjectOnDragPlane(screenPos, selectedBlock);
        isDragging = true;
    }

    void DragPointer(Vector2 screenPos)
    {
        if (!isDragging) return;
        if (Vector2.Distance(screenPos, pointerDownPos) < tapPixelSlop) return;

        if (IsPlacing)
        {
            DragHeldBlock(screenPos);
            return;
        }

        if (selectedBlock == null) return;

        Vector3 worldPoint = ProjectOnDragPlane(screenPos, selectedBlock);
        Vector3 delta = worldPoint - lastDragWorld;
        ApplyExtractDrag(selectedBlock, delta);
        lastDragWorld = worldPoint;

        if (GetExtractDistance(selectedBlock) >= extractThreshold)
            ExtractSelected();
    }

    void EndPointer(Vector2 screenPos)
    {
        if (!pointerHeld) return;
        pointerHeld = false;

        bool wasDrag = isDragging && Vector2.Distance(screenPos, pointerDownPos) > tapPixelSlop;
        isDragging = false;

        if (IsPlacing)
        {
            TryPlaceHeldBlock(screenPos);
            return;
        }

        if (wasDrag)
        {
            TryExtractOrRestore();
            return;
        }

        if (!RaycastScene(screenPos, out RaycastHit hit))
        {
            ClearSelection(keepPosition: false);
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
                ClearSelection(keepPosition: false);
                return;
            }

            ClearSelection(keepPosition: false);
            SelectBlock(block);
            return;
        }

        if (block != null && towerBuilder != null && towerBuilder.IsTopLayerBlock(block))
            TurnManager.Instance?.NotifyInvalidMove("No se pueden retirar bloques del nivel superior.");

        ClearSelection(keepPosition: false);
    }

    void DragHeldBlock(Vector2 screenPos)
    {
        if (heldBlock == null || towerBuilder == null) return;

        Vector3 worldPoint = ProjectOnTopPlane(screenPos);
        Vector3 delta = Vector3.ProjectOnPlane(worldPoint - lastDragWorld, towerBuilder.transform.up);
        ApplyDragDelta(heldBlock.transform, delta);
        lastDragWorld = worldPoint;

            if (towerBuilder.TryGetNearestSlot(heldBlock.transform.position, snapDistance, out TowerBuilder.PlacementSlot slot))
            {
                HighlightGhost(true, slot);
                heldBlock.transform.localRotation = slot.localRotation;
            }
            else
            {
                HighlightGhost(false, default);
            }
    }

    void TryPlaceHeldBlock(Vector2 screenPos)
    {
        if (heldBlock == null || towerBuilder == null) return;

        TowerBuilder.PlacementSlot slot;
        bool snapped = towerBuilder.TryGetNearestSlot(heldBlock.transform.position, snapDistance, out slot);

        if (!snapped && RaycastScene(screenPos, out RaycastHit hit))
        {
            PlacementGhost ghost = hit.collider.GetComponentInParent<PlacementGhost>();
            if (ghost != null)
            {
                slot = ghost.slot;
                snapped = true;
            }
        }

        if (!snapped)
        {
            TurnManager.Instance?.NotifyInvalidMove("Coloca el bloque en una ranura de la cima. Sigues en tu turno.");
            ParkHeldBlock(heldBlock);
            return;
        }

        ConfirmPlacement(slot);
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

    Vector3 ProjectOnTopPlane(Vector2 screenPos)
    {
        Vector3 normal = towerBuilder != null ? towerBuilder.transform.up : Vector3.up;
        Vector3 point = towerBuilder != null
            ? towerBuilder.transform.TransformPoint(new Vector3(0f, towerBuilder.TopY + towerBuilder.blockHeight * 0.5f, 0f))
            : Vector3.zero;
        Plane plane = new Plane(normal, point);
        Ray ray = arCamera.ScreenPointToRay(screenPos);
        if (plane.Raycast(ray, out float distance))
            return ray.GetPoint(distance);

        return heldBlock != null ? heldBlock.transform.position : point;
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
        if (!CanManipulate) return false;
        if (IsPlacing) return false;
        if (block == null || block.isRemoved || block.isHeld) return false;
        if (towerBuilder == null || !towerBuilder.isActiveAndEnabled)
            return false;

        return block.layerIndex <= towerBuilder.TopLayerIndex - 1;
    }

    void SelectBlock(JengaBlock block)
    {
        selectedBlock = block;
        originalWorldPos = block.transform.position;
        selectedBlock.SetKinematic(true);

        if (towerBuilder != null && selectLiftOffset > 0f)
            selectedBlock.transform.position += towerBuilder.transform.up * selectLiftOffset;

        selectedBlock.SetSelectedVisual(true, selectedColor);
        ShowArrows(block);
    }

    void ApplyDragDelta(Transform target, Vector3 delta)
    {
        delta *= dragSensitivity;
        float maxStep = Mathf.Max(maxDragStep, 0.0001f);
        if (delta.sqrMagnitude > maxStep * maxStep)
            delta = delta.normalized * maxStep;

        target.position += delta;
    }

    static Vector3 GetExtractAxis(JengaBlock block)
    {
        Vector3 scale = block.transform.lossyScale;
        return Mathf.Abs(scale.z) >= Mathf.Abs(scale.x)
            ? block.transform.forward
            : block.transform.right;
    }

    void ApplyExtractDrag(JengaBlock block, Vector3 delta)
    {
        if (block == null) return;

        Vector3 axis = GetExtractAxis(block);
        delta = axis * Vector3.Dot(delta, axis);
        delta *= dragSensitivity;

        float maxStep = Mathf.Max(maxDragStep, 0.0001f);
        if (delta.sqrMagnitude > maxStep * maxStep)
            delta = delta.normalized * maxStep;

        block.transform.position += delta;
    }

    float GetExtractDistance(JengaBlock block)
    {
        if (block == null) return 0f;
        Vector3 axis = GetExtractAxis(block);
        return Mathf.Abs(Vector3.Dot(block.transform.position - originalWorldPos, axis));
    }

    void NudgeSelected(int direction)
    {
        if (selectedBlock == null) return;

        Vector3 axis = GetExtractAxis(selectedBlock);
        ApplyExtractDrag(selectedBlock, axis * (moveStep * direction));

        if (GetExtractDistance(selectedBlock) >= extractThreshold)
            ExtractSelected();
    }

    void TryExtractOrRestore()
    {
        if (selectedBlock == null) return;

        float distanceMoved = GetExtractDistance(selectedBlock);
        if (distanceMoved >= extractThreshold)
            ExtractSelected();
        else
        {
            ClearSelection(keepPosition: false);
            TurnManager.Instance?.NotifyInvalidMove("El bloque no salió lo suficiente. Sigues en tu turno.");
        }
    }

    void ExtractSelected()
    {
        if (selectedBlock == null) return;

        AudioManager.Instance?.PlayBlockExtract();

        JengaBlock block = selectedBlock;
        HideArrows();
        block.SetSelectedVisual(false, selectedColor);
        selectedBlock = null;
        isDragging = false;
        pointerHeld = false;
        BeginExtractedPlacement(block);
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

    void BeginExtractedPlacement(JengaBlock block)
    {
        block.isRemoved = true;
        block.isHeld = true;
        block.SetKinematic(true);
        block.SetColliderEnabled(false);
        block.SetSelectedVisual(true, selectedColor);
        ParkHeldBlock(block);
        heldBlock = block;

        settling = true;
        if (stabilityMonitor != null)
        {
            stabilityMonitor.SettleTower(
                () =>
                {
                    settling = false;
                    if (TurnManager.Instance != null && TurnManager.Instance.IsGameOver) return;
                    ShowPlacementGhosts();
                    TurnManager.Instance?.EnterPlacePhase();
                },
                () =>
                {
                    settling = false;
                    HidePlacementGhosts();
                });
        }
        else
        {
            settling = false;
            ShowPlacementGhosts();
            TurnManager.Instance?.EnterPlacePhase();
        }
    }

    void ParkHeldBlock(JengaBlock block)
    {
        if (block == null || towerBuilder == null) return;

        Vector3 local = new Vector3(0.12f, towerBuilder.TopY + 0.05f, 0f);
        block.transform.position = towerBuilder.transform.TransformPoint(local);
        block.SetKinematic(true);
    }

    void ConfirmPlacement(TowerBuilder.PlacementSlot slot)
    {
        JengaBlock block = heldBlock;
        heldBlock = null;
        pointerHeld = false;
        isDragging = false;
        HidePlacementGhosts();

        towerBuilder.PlaceBlock(block, slot);
        block.SetSelectedVisual(false, selectedColor);

        settling = true;
        if (stabilityMonitor != null)
        {
            stabilityMonitor.SettleTower(
                () =>
                {
                    settling = false;
                    TurnManager.Instance?.CompleteTurn();
                },
                () => { settling = false; });
        }
        else
        {
            settling = false;
            TurnManager.Instance?.CompleteTurn();
        }
    }

    void ShowPlacementGhosts()
    {
        HidePlacementGhosts();
        if (towerBuilder == null) return;

        List<TowerBuilder.PlacementSlot> slots = towerBuilder.GetAvailablePlacementSlots();
        for (int i = 0; i < slots.Count; i++)
            ghosts.Add(CreateGhost(slots[i]));
    }

    void HidePlacementGhosts()
    {
        for (int i = 0; i < ghosts.Count; i++)
        {
            if (ghosts[i] != null) Destroy(ghosts[i]);
        }

        ghosts.Clear();
    }

    void HighlightGhost(bool hasSlot, TowerBuilder.PlacementSlot slot)
    {
        for (int i = 0; i < ghosts.Count; i++)
        {
            if (ghosts[i] == null) continue;
            PlacementGhost marker = ghosts[i].GetComponent<PlacementGhost>();
            MeshRenderer rend = ghosts[i].GetComponent<MeshRenderer>();
            if (rend == null) continue;

            bool match = hasSlot
                         && marker != null
                         && marker.slot.layerIndex == slot.layerIndex
                         && marker.slot.slotIndex == slot.slotIndex;
            rend.sharedMaterial = match ? GetGhostHighlightMaterial() : GetGhostMaterial();
        }
    }

    GameObject CreateGhost(TowerBuilder.PlacementSlot slot)
    {
        GameObject ghost = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ghost.name = $"PlacementGhost_{slot.layerIndex}_{slot.slotIndex}";
        ghost.transform.SetParent(towerBuilder.transform, false);
        ghost.transform.localPosition = slot.localPosition;
        ghost.transform.localRotation = slot.localRotation;
        ghost.transform.localScale = new Vector3(
            towerBuilder.blockLength * 1.04f,
            towerBuilder.blockHeight * 0.7f,
            towerBuilder.blockWidth * 1.04f);

        PlacementGhost marker = ghost.AddComponent<PlacementGhost>();
        marker.slot = slot;

        Collider col = ghost.GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }

        MeshRenderer rend = ghost.GetComponent<MeshRenderer>();
        if (rend != null) rend.sharedMaterial = GetGhostMaterial();
        return ghost;
    }

    void ShowArrows(JengaBlock block)
    {
        HideArrows();
        bool alongZ = Mathf.Abs(block.transform.lossyScale.z) >= Mathf.Abs(block.transform.lossyScale.x);
        if (alongZ)
        {
            posArrow = CreateArrow("ArrowPositive", block.transform, new Vector3(0f, 0f, 0.62f), Quaternion.Euler(0f, 90f, 0f), 1);
            negArrow = CreateArrow("ArrowNegative", block.transform, new Vector3(0f, 0f, -0.62f), Quaternion.Euler(0f, -90f, 0f), -1);
        }
        else
        {
            posArrow = CreateArrow("ArrowPositive", block.transform, new Vector3(0.62f, 0f, 0f), Quaternion.identity, 1);
            negArrow = CreateArrow("ArrowNegative", block.transform, new Vector3(-0.62f, 0f, 0f), Quaternion.Euler(0f, 180f, 0f), -1);
        }
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
        arrowMaterial = CreateUnlitMaterial(arrowColor);
        return arrowMaterial;
    }

    Material GetGhostMaterial()
    {
        if (ghostMaterial != null) return ghostMaterial;
        ghostMaterial = CreateUnlitMaterial(ghostColor);
        return ghostMaterial;
    }

    Material GetGhostHighlightMaterial()
    {
        if (ghostHighlightMaterial != null) return ghostHighlightMaterial;
        ghostHighlightMaterial = CreateUnlitMaterial(ghostHighlightColor);
        return ghostHighlightMaterial;
    }

    static Material CreateUnlitMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        Material mat = new Material(shader);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        return mat;
    }

    void OnDestroy()
    {
        HideArrows();
        HidePlacementGhosts();
        if (arrowMaterial != null) Destroy(arrowMaterial);
        if (ghostMaterial != null) Destroy(ghostMaterial);
        if (ghostHighlightMaterial != null) Destroy(ghostHighlightMaterial);
    }
}
