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
    public float fallAngleThreshold = 28f;
    public float fallDropThreshold = 0.018f;
    public float minSettleTime = 0.45f;
    public float maxSettleTime = 2.2f;
    public float stableSpeed = 0.04f;

    public void SettleTower(System.Action onStable, System.Action onFell = null)
    {
        if (!isActiveAndEnabled || towerBuilder == null)
        {
            onStable?.Invoke();
            return;
        }

        StopAllCoroutines();
        StartCoroutine(SettleRoutine(onStable, onFell));
    }

    IEnumerator SettleRoutine(System.Action onStable, System.Action onFell)
    {
        JengaPhysics.ConfigureWorld();

        int previousIterations = Physics.defaultSolverIterations;
        int previousVelocity = Physics.defaultSolverVelocityIterations;
        Physics.defaultSolverIterations = 22;
        Physics.defaultSolverVelocityIterations = 14;

        foreach (var block in towerBuilder.AllBlocks)
        {
            if (!TowerBuilder.IsInTower(block)) continue;
            block.ReleaseToPhysics();
        }

        float timer = 0f;
        float fallenHold = 0f;
        bool fell = false;

        while (timer < maxSettleTime)
        {
            yield return new WaitForFixedUpdate();
            timer += Time.fixedDeltaTime;

            if (TowerHasFallen())
            {
                fallenHold += Time.fixedDeltaTime;
                if (fallenHold >= 0.12f)
                {
                    fell = true;
                    break;
                }
            }
            else
            {
                fallenHold = 0f;
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
