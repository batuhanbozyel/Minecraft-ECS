using UnityEngine;
using Unity.Entities;
using Unity.Collections;

[System.Serializable]
public struct WorldGenerationNoiseSettings
{
    public int seed;

    public float frequency;
    public float amplitude;

    public float min;
    public float max;
}

public struct WorldGenerationSettings : IComponentData
{
    public WorldGenerationNoiseSettings noise;
    public Entity grassPrefab;
    public int chunkSize;
    public int chunkCount;
}
