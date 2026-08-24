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

    [Header("Arrastre")]
    [Tooltip("Escala el desplazamiento del dedo (menor = más control).")]
    public float dragSensitivity = 0.85f;
    [Tooltip("Máximo desplazamiento por frame para evitar saltos bruscos.")]
    public float maxDragStep = 0.014f;
    public float tapPixelSlop = 28f;

    [Header("Colocación")]
    public float snapDistance = 0.09f;
    [Tooltip("Margen por debajo de la ranura que aún cuenta como estar en la cima.")]
    public float placeHeightTolerance = 0.03f;
    [Tooltip("Desplazamiento máximo por frame al guiar la pieza hacia la cima.")]
    public float maxHeldDragStep = 0.03f;
    [Tooltip("Cuánto se imanta la pieza a la ranura al pasar sobre la cima.")]
    [Range(0f, 1f)] public float placeMagnetStrength = 0.3f;

    [Header("Selección visual")]
    public Color selectedColor = new Color(1f, 0.55f, 0.1f);
    public Color arrowColor = new Color(1f, 0.85f, 0.15f);
    public Color ghostColor = new Color(0.25f, 0.95f, 0.4f, 0.55f);
    public Color ghostHighlightColor = new Color(1f, 0.9f, 0.2f, 0.75f);
    public float arrowWorldLength = 0.028f;
    public float arrowWorldThickness = 0.01f;

    const float OverlapTolerance = 0.0002f;

    JengaBlock selectedBlock;
    JengaBlock heldBlock;
    Vector3 originalWorldPos;
    readonly List<BlockMoveArrow> arrows = new List<BlockMoveArrow>();
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

        UpdateArrows();

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
            lastDragWorld = ProjectOnCameraPlane(screenPos, heldBlock.transform.position);
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
            ClearSelection();
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
            ClearSelection();
            return;
        }

        BlockMoveArrow arrow = hit.collider.GetComponentInParent<BlockMoveArrow>();
        if (arrow != null && selectedBlock != null)
        {
            NudgeSelected(arrow);
            return;
        }

        JengaBlock block = GetBlock(hit.collider);
        if (block != null && CanSelect(block))
        {
            if (selectedBlock == block)
            {
                ClearSelection();
                return;
            }

            ClearSelection();
            SelectBlock(block);
            return;
        }

        if (block != null && towerBuilder != null && towerBuilder.IsTopLayerBlock(block))
            TurnManager.Instance?.NotifyInvalidMove("No se pueden retirar bloques del nivel superior.");

        ClearSelection();
    }

    void DragHeldBlock(Vector2 screenPos)
    {
        if (heldBlock == null || towerBuilder == null) return;

        // Arrastre en el plano de la cámara: el dedo también sube y baja la pieza.
        Vector3 worldPoint = ProjectOnCameraPlane(screenPos, heldBlock.transform.position);
        ApplyDragDelta(heldBlock.transform, worldPoint - lastDragWorld, maxHeldDragStep);
        lastDragWorld = worldPoint;

        bool hasSlot = TryGetPlacement(out TowerBuilder.PlacementSlot slot, out bool atHeight);
        ConstrainHeldBlock(heldBlock, hasSlot, slot);

        if (hasSlot && atHeight)
        {
            HighlightGhost(true, slot);
            heldBlock.transform.localRotation = slot.localRotation;
            ApplyPlacementMagnet(slot);
        }
        else
        {
            HighlightGhost(false, default);
        }
    }

    /// <summary>
    /// Busca la ranura más cercana e informa si la pieza ya alcanzó la cima.
    /// Basta con estar a la altura del hueco o por encima: una vez arriba, la
    /// pieza cae sola en la ranura.
    /// </summary>
    bool TryGetPlacement(out TowerBuilder.PlacementSlot slot, out bool atHeight)
    {
        slot = default;
        atHeight = false;
        if (heldBlock == null || towerBuilder == null) return false;

        if (!towerBuilder.TryGetNearestSlot(heldBlock.transform.position, snapDistance, out slot))
            return false;

        float blockY = towerBuilder.transform.InverseTransformPoint(heldBlock.transform.position).y;
        atHeight = blockY >= slot.localPosition.y - placeHeightTolerance;
        return true;
    }

    /// <summary>
    /// Acerca la pieza a la ranura mientras la sostienes, para no tener que
    /// afinar la posición a mano.
    /// </summary>
    void ApplyPlacementMagnet(TowerBuilder.PlacementSlot slot)
    {
        if (placeMagnetStrength <= 0f || heldBlock == null) return;

        Vector3 target = towerBuilder.transform.TransformPoint(slot.localPosition);
        heldBlock.transform.position = Vector3.Lerp(
            heldBlock.transform.position, target, Mathf.Clamp01(placeMagnetStrength));
    }

    /// <summary>
    /// Evita que la pieza guiada atraviese la torre. Si hay una ranura cerca
    /// puede bajar hasta ella; si no, se queda por encima de la cima.
    /// </summary>
    void ConstrainHeldBlock(JengaBlock block, bool hasSlot, TowerBuilder.PlacementSlot slot)
    {
        if (block == null || towerBuilder == null) return;

        Transform tower = towerBuilder.transform;
        Vector3 local = tower.InverseTransformPoint(block.transform.position);

        float footprint = towerBuilder.blockLength * 0.5f + towerBuilder.blockHeight;
        float minOverTower = hasSlot
            ? slot.localPosition.y
            : towerBuilder.TopY + towerBuilder.blockHeight * 0.5f;
        bool overTower = Mathf.Abs(local.x) < footprint && Mathf.Abs(local.z) < footprint;

        if (overTower && local.y < minOverTower)
            local.y = minOverTower;

        float floor = towerBuilder.blockHeight * 0.5f;
        if (local.y < floor) local.y = floor;

        block.transform.position = tower.TransformPoint(local);
    }

    void TryPlaceHeldBlock(Vector2 screenPos)
    {
        if (heldBlock == null || towerBuilder == null) return;

        bool snapped = TryGetPlacement(out TowerBuilder.PlacementSlot slot, out bool atHeight);

        if (!snapped && RaycastScene(screenPos, out RaycastHit hit))
        {
            PlacementGhost ghost = hit.collider.GetComponentInParent<PlacementGhost>();
            if (ghost != null)
            {
                slot = ghost.slot;
                snapped = true;
                float blockY = towerBuilder.transform.InverseTransformPoint(heldBlock.transform.position).y;
                atHeight = blockY >= slot.localPosition.y - placeHeightTolerance;
            }
        }

        if (!snapped)
        {
            TurnManager.Instance?.NotifyInvalidMove("Lleva el bloque sobre una ranura de la cima. Sigues en tu turno.");
            return;
        }

        if (!atHeight)
        {
            TurnManager.Instance?.NotifyInvalidMove("Sube el bloque hasta la altura de la cima. Sigues en tu turno.");
            return;
        }

        ConfirmPlacement(slot);
    }

    Vector3 ProjectOnCameraPlane(Vector2 screenPos, Vector3 reference)
    {
        if (arCamera == null) return reference;

        Plane plane = new Plane(-arCamera.transform.forward, reference);
        Ray ray = arCamera.ScreenPointToRay(screenPos);
        if (plane.Raycast(ray, out float distance))
            return ray.GetPoint(distance);

        return reference;
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
        selectedBlock.SetSelectedVisual(true, selectedColor);
        ShowArrows(block);
    }

    void ApplyDragDelta(Transform target, Vector3 delta, float maxDelta)
    {
        delta *= dragSensitivity;
        float maxStep = Mathf.Max(maxDelta, 0.0001f);
        if (delta.sqrMagnitude > maxStep * maxStep)
            delta = delta.normalized * maxStep;

        target.position += delta;
    }

    Vector3 TowerUp => towerBuilder != null ? towerBuilder.transform.up : Vector3.up;

    void ApplyExtractDrag(JengaBlock block, Vector3 delta)
    {
        if (block == null) return;

        // Sin eje fijo: el bloque se puede sacar hacia cualquier lado del plano.
        delta = Vector3.ProjectOnPlane(delta, TowerUp) * dragSensitivity;

        float maxStep = Mathf.Max(maxDragStep, 0.0001f);
        if (delta.sqrMagnitude > maxStep * maxStep)
            delta = delta.normalized * maxStep;

        block.transform.position += delta;
        ResolveHorizontalOverlaps(block);
    }

    /// <summary>
    /// El bloque arrastrado es kinemático, así que PhysX no lo detiene solo.
    /// Se expulsa de cualquier pieza con la que se solape, únicamente en el
    /// plano horizontal: así choca con sus vecinas pero puede deslizarse
    /// libremente hacia los lados abiertos.
    /// </summary>
    void ResolveHorizontalOverlaps(JengaBlock block)
    {
        BoxCollider self = block != null ? block.Collider : null;
        if (self == null || !self.enabled) return;

        Vector3 up = TowerUp;
        int mask = blockLayerMask.value != 0 ? blockLayerMask.value : Physics.AllLayers;
        Vector3 halfExtents = Vector3.Scale(self.size, block.transform.lossyScale) * 0.5f;

        for (int iteration = 0; iteration < 4; iteration++)
        {
            Vector3 center = block.transform.TransformPoint(self.center);
            Collider[] hits = Physics.OverlapBox(
                center, halfExtents, block.transform.rotation, mask, QueryTriggerInteraction.Ignore);

            Vector3 push = Vector3.zero;
            for (int i = 0; i < hits.Length; i++)
            {
                Collider other = hits[i];
                if (other == self) continue;

                JengaBlock otherBlock = GetBlock(other);
                if (otherBlock == null || otherBlock == block) continue;
                if (otherBlock.isRemoved || otherBlock.isHeld) continue;

                if (!Physics.ComputePenetration(
                        self, block.transform.position, block.transform.rotation,
                        other, other.transform.position, other.transform.rotation,
                        out Vector3 direction, out float distance))
                    continue;

                Vector3 horizontal = Vector3.ProjectOnPlane(direction * distance, up);
                if (horizontal.sqrMagnitude > push.sqrMagnitude)
                    push = horizontal;
            }

            if (push.sqrMagnitude <= OverlapTolerance * OverlapTolerance) break;
            block.transform.position += push;
        }
    }

    float GetExtractDistance(JengaBlock block)
    {
        if (block == null) return 0f;
        return Vector3.ProjectOnPlane(block.transform.position - originalWorldPos, TowerUp).magnitude;
    }

    void NudgeSelected(BlockMoveArrow arrow)
    {
        if (selectedBlock == null || arrow == null) return;

        Vector3 worldDir = selectedBlock.transform.TransformDirection(arrow.localDirection).normalized;
        ApplyExtractDrag(selectedBlock, worldDir * moveStep);

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
            ClearSelection();
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

    void ClearSelection()
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

        block.transform.position = originalWorldPos;
        block.SetKinematic(true);
    }

    void BeginExtractedPlacement(JengaBlock block)
    {
        int removedLayer = block.layerIndex;

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
                },
                removedLayer);
        }
        else
        {
            settling = false;
            ShowPlacementGhosts();
            TurnManager.Instance?.EnterPlacePhase();
        }
    }

    /// <summary>
    /// La pieza se queda a la altura a la que se extrajo, solo apartada de la
    /// torre. Subirla hasta la cima es tarea del jugador.
    /// </summary>
    void ParkHeldBlock(JengaBlock block)
    {
        if (block == null || towerBuilder == null) return;

        Transform tower = towerBuilder.transform;
        Vector3 local = tower.InverseTransformPoint(block.transform.position);

        float clearance = towerBuilder.blockLength * 0.5f + towerBuilder.blockWidth;
        Vector2 planar = new Vector2(local.x, local.z);
        if (planar.sqrMagnitude < 1e-8f) planar = Vector2.right;
        if (planar.magnitude < clearance) planar = planar.normalized * clearance;

        local.x = planar.x;
        local.z = planar.y;

        block.transform.position = tower.TransformPoint(local);
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
                () => { settling = false; },
                slot.layerIndex,
                block);
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

        // Cuatro salidas: el bloque puede empujarse hacia cualquiera de sus lados.
        arrows.Add(CreateArrow("ArrowRight", block, Vector3.right));
        arrows.Add(CreateArrow("ArrowLeft", block, Vector3.left));
        arrows.Add(CreateArrow("ArrowForward", block, Vector3.forward));
        arrows.Add(CreateArrow("ArrowBack", block, Vector3.back));

        UpdateArrows();
    }

    void HideArrows()
    {
        for (int i = 0; i < arrows.Count; i++)
        {
            if (arrows[i] != null) Destroy(arrows[i].gameObject);
        }

        arrows.Clear();
    }

    /// <summary>
    /// Las flechas viven fuera del bloque (su escala es muy irregular y las
    /// deformaría), así que hay que recolocarlas mientras se arrastra.
    /// </summary>
    void UpdateArrows()
    {
        if (selectedBlock == null) return;

        Transform block = selectedBlock.transform;
        Vector3 scale = block.lossyScale;
        Vector3 up = TowerUp;

        for (int i = 0; i < arrows.Count; i++)
        {
            BlockMoveArrow arrow = arrows[i];
            if (arrow == null) continue;

            Vector3 local = arrow.localDirection;
            float halfExtent = Mathf.Abs(local.x) > 0.5f
                ? Mathf.Abs(scale.x) * 0.5f
                : Mathf.Abs(scale.z) * 0.5f;

            Vector3 worldDir = block.TransformDirection(local).normalized;
            Vector3 planar = Vector3.ProjectOnPlane(worldDir, up).normalized;
            if (planar.sqrMagnitude < 0.001f) planar = worldDir;

            arrow.transform.position = block.position + planar * (halfExtent + arrowWorldLength * 0.4f);

            Vector3 forward = Vector3.Cross(planar, up);
            if (forward.sqrMagnitude > 0.001f)
                arrow.transform.rotation = Quaternion.LookRotation(forward.normalized, up);
        }
    }

    BlockMoveArrow CreateArrow(string name, JengaBlock block, Vector3 localDirection)
    {
        GameObject root = new GameObject(name);
        Transform host = towerBuilder != null ? towerBuilder.transform : block.transform.parent;
        root.transform.SetParent(host, false);

        Vector3 lossy = host != null ? host.lossyScale : Vector3.one;
        root.transform.localScale = new Vector3(
            arrowWorldLength / Mathf.Max(Mathf.Abs(lossy.x), 0.0001f),
            arrowWorldThickness / Mathf.Max(Mathf.Abs(lossy.y), 0.0001f),
            arrowWorldThickness / Mathf.Max(Mathf.Abs(lossy.z), 0.0001f));

        BlockMoveArrow marker = root.AddComponent<BlockMoveArrow>();
        marker.localDirection = localDirection;

        BoxCollider col = root.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.center = new Vector3(0.5f, 0f, 0f);
        col.size = new Vector3(1.15f, 1.4f, 1.4f);

        BuildArrowVisual(root.transform);
        return marker;
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
