﻿using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

public class WorldGenerationSettingsMono : MonoBehaviour
{
    public WorldGenerationNoiseSettings noise;
    public GameObject grassPrefab;
    public int3 chunkSize;
    public int chunkCount;
}

public class WorldGenerationSettingsBaker : Baker<WorldGenerationSettingsMono>
{
    public override void Bake(WorldGenerationSettingsMono authoring)
    {
        var entity = GetEntity(TransformUsageFlags.None);
        AddComponent(entity, new WorldGenerationSettings
        {
            noise = authoring.noise,
            chunkSize = authoring.chunkSize,
            chunkCount = authoring.chunkCount,
            grassPrefab = GetEntity(authoring.grassPrefab, TransformUsageFlags.Dynamic)
        });
    }
}
