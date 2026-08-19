using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Genera la torre de bloques como hijos de este mismo GameObject.
/// Este script debe ir en un objeto hijo del ImageTarget (ej: "TowerAnchor"),
/// posicionado en (0,0,0) local para que la torre nazca sobre la imagen.
/// </summary>
public class TowerBuilder : MonoBehaviour
{
    [Header("Prefab del bloque (Cube con JengaBlock + Rigidbody + BoxCollider)")]
    public GameObject blockPrefab;

    [Header("Dimensiones del bloque en metros (proporción real de Jenga)")]
    public float blockLength = 0.075f;
    public float blockWidth = 0.025f;
    public float blockHeight = 0.015f;

    [Header("Configuración de la torre")]
    public int totalLayers = 18;

    private List<JengaBlock> allBlocks = new List<JengaBlock>();
    public List<JengaBlock> AllBlocks => allBlocks;

    void Start()
    {
        BuildTower();
    }

    public void BuildTower()
    {
        for (int layer = 0; layer < totalLayers; layer++)
        {
            bool rotated = layer % 2 == 1;
            float y = blockHeight * layer + blockHeight / 2f;

            for (int slot = 0; slot < 3; slot++)
            {
                GameObject go = Instantiate(blockPrefab, transform);
                float offset = (slot - 1) * blockWidth;

                Vector3 localPos = rotated
                    ? new Vector3(offset, y, 0f)
                    : new Vector3(0f, y, offset);

                go.transform.localPosition = localPos;
                go.transform.localRotation = rotated ? Quaternion.Euler(0, 90, 0) : Quaternion.identity;
                go.transform.localScale = new Vector3(blockLength, blockHeight, blockWidth);

                JengaBlock block = go.GetComponent<JengaBlock>();
                block.layerIndex = layer;
                block.slotIndex = slot;
                allBlocks.Add(block);
            }
        }
    }

    public float TopY => blockHeight * totalLayers;
    public int TopLayerIndex => allBlocks.Count > 0 ? allBlocks[allBlocks.Count - 1].layerIndex : 0;
}