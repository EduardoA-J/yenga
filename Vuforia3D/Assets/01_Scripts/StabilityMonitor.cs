using UnityEngine;
using System.Collections;

/// <summary>
/// Tras cada intento de extracción, "libera" brevemente la física de los bloques
/// restantes para ver si la torre se mantiene en pie o cae. Colócalo junto al
/// TowerBuilder (mismo GameObject "TowerAnchor").
/// </summary>
public class StabilityMonitor : MonoBehaviour
{
    public TowerBuilder towerBuilder;

    [Header("Umbrales de caída")]
    public float fallAngleThreshold = 35f; // grados de inclinación que cuentan como "cayó"
    public float settleTime = 1.2f;        // tiempo de simulación física por chequeo

    public void SettleTower(System.Action onStable)
    {
        StartCoroutine(SettleRoutine(onStable));
    }

    IEnumerator SettleRoutine(System.Action onStable)
    {
        foreach (var block in towerBuilder.AllBlocks)
        {
            if (!block.isRemoved)
                block.SetKinematic(false);
        }

        float timer = 0f;
        bool fell = false;

        while (timer < settleTime)
        {
            foreach (var block in towerBuilder.AllBlocks)
            {
                if (block.isRemoved) continue;

                float tiltAngle = Vector3.Angle(block.transform.up, Vector3.up);
                if (tiltAngle > fallAngleThreshold)
                {
                    fell = true;
                    break;
                }
            }

            if (fell) break;
            timer += Time.deltaTime;
            yield return null;
        }

        if (fell)
        {
            // Dejamos la física activa para que se vea la caída realista
            TurnManager.Instance?.EndGame("La torre perdió el equilibrio.");
        }
        else
        {
            foreach (var block in towerBuilder.AllBlocks)
            {
                if (!block.isRemoved)
                    block.SetKinematic(true);
            }
            onStable?.Invoke();
        }
    }
}