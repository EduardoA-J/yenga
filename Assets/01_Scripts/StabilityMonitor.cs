using UnityEngine;
using System.Collections;

/// <summary>
/// Tras extraer o colocar un bloque, simula la torre y decide si se mantiene
/// o se ha caído. Espera a que las velocidades bajen antes de congelar.
/// </summary>
public class StabilityMonitor : MonoBehaviour
{
    public TowerBuilder towerBuilder;

    [Header("Umbrales de caída")]
    public float fallAngleThreshold = 45f;
    public float fallDropThreshold = 0.05f;
    [Tooltip("Espera antes de evaluar caída (evita falsos positivos al activar física).")]
    public float fallCheckDelay = 0.5f;
    [Tooltip("Tiempo continuo en estado de caída antes de confirmar game over.")]
    public float fallConfirmTime = 0.35f;
    public float minSettleTime = 0.6f;
    public float maxSettleTime = 2.6f;
    public float stableSpeed = 0.06f;

    [Header("Alcance de la simulación")]
    [Tooltip("Capas por debajo del movimiento que también se simulan. El resto queda fijo.")]
    public int layersBelowToSimulate = 1;

    /// <summary>
    /// Simula la torre. <paramref name="fromLayer"/> limita la simulación a las
    /// capas afectadas: las inferiores se mantienen fijas y hacen de base sólida.
    /// <paramref name="anchoredBlock"/> es la pieza recién colocada, que se deja
    /// quieta para que no golpee la pila al soltarla.
    /// </summary>
    public void SettleTower(System.Action onStable, System.Action onFell = null,
                            int fromLayer = 0, JengaBlock anchoredBlock = null)
    {
        if (!isActiveAndEnabled || towerBuilder == null)
        {
            onStable?.Invoke();
            return;
        }

        StopAllCoroutines();
        StartCoroutine(SettleRoutine(onStable, onFell, fromLayer, anchoredBlock));
    }

    IEnumerator SettleRoutine(System.Action onStable, System.Action onFell,
                              int fromLayer, JengaBlock anchoredBlock)
    {
        JengaPhysics.ConfigureWorld();

        int previousIterations = Physics.defaultSolverIterations;
        int previousVelocity = Physics.defaultSolverVelocityIterations;
        Physics.defaultSolverIterations = 34;
        Physics.defaultSolverVelocityIterations = 18;

        int minSimulatedLayer = Mathf.Max(0, fromLayer - Mathf.Max(0, layersBelowToSimulate));

        foreach (var block in towerBuilder.AllBlocks)
        {
            if (!TowerBuilder.IsInTower(block)) continue;
            if (block == anchoredBlock) continue;
            if (block.layerIndex < minSimulatedLayer) continue;
            block.ReleaseToPhysics();
        }

        float timer = 0f;
        float fallenHold = 0f;
        bool fell = false;
        float confirmTime = Mathf.Max(fallConfirmTime, Time.fixedDeltaTime);

        while (timer < maxSettleTime)
        {
            yield return new WaitForFixedUpdate();
            timer += Time.fixedDeltaTime;

            if (timer >= fallCheckDelay)
            {
                if (TowerHasFallen())
                {
                    fallenHold += Time.fixedDeltaTime;
                    if (fallenHold >= confirmTime)
                    {
                        fell = true;
                        break;
                    }
                }
                else
                {
                    fallenHold = 0f;
                }
            }

            if (timer >= minSettleTime && TowerIsSettled())
                break;
        }

        if (!fell)
            fell = TowerHasFallen();

        if (fell)
        {
            TurnManager.Instance?.EndGame("La torre perdió el equilibrio.");
            onFell?.Invoke();

            // Deja correr la animación de derrumbe con toda la torre suelta.
            foreach (var block in towerBuilder.AllBlocks)
            {
                if (!TowerBuilder.IsInTower(block)) continue;
                block.ReleaseToPhysics();
            }

            float extra = 0f;
            while (extra < 2.2f)
            {
                extra += Time.deltaTime;
                yield return null;
            }

            FreezeLandedBlocks();
            RestoreSolver(previousIterations, previousVelocity);
            yield break;
        }

        FreezeLandedBlocks();
        RestoreSolver(previousIterations, previousVelocity);
        onStable?.Invoke();
    }

    static void RestoreSolver(int iterations, int velocityIterations)
    {
        Physics.defaultSolverIterations = iterations;
        Physics.defaultSolverVelocityIterations = velocityIterations;
    }

    void FreezeLandedBlocks()
    {
        foreach (var block in towerBuilder.AllBlocks)
        {
            if (block == null || block.isHeld) continue;
            block.SetKinematic(true);
        }
    }

    bool TowerIsSettled()
    {
        foreach (var block in towerBuilder.AllBlocks)
        {
            if (!TowerBuilder.IsInTower(block)) continue;
            if (!block.HasLanded(stableSpeed)) return false;
        }

        return true;
    }

    bool TowerHasFallen()
    {
        Vector3 up = towerBuilder.transform.up;
        float blockHeight = Mathf.Max(towerBuilder.blockHeight, 0.0001f);

        foreach (var block in towerBuilder.AllBlocks)
        {
            if (!TowerBuilder.IsInTower(block)) continue;

            float tiltAngle = Vector3.Angle(block.transform.up, up);
            if (tiltAngle > fallAngleThreshold)
                return true;

            Vector3 local = towerBuilder.transform.InverseTransformPoint(block.transform.position);
            float expectedY = block.layerIndex * blockHeight + blockHeight * 0.5f;
            if (local.y < expectedY - fallDropThreshold)
                return true;
        }

        return false;
    }
}
