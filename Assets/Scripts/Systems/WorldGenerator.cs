using UnityEngine;
using Unity.Jobs;
using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using Unity.Collections;
using Unity.Mathematics;
using Random = UnityEngine.Random;

[BurstCompile]
public partial struct WorldGenerator : ISystem, ISystemStartStop
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<WorldGenerationSettings>();
    }

    [BurstCompile]
    public void OnStartRunning(ref SystemState state)
    {
        var settings = SystemAPI.GetSingleton<WorldGenerationSettings>();

        if (settings.noise.seed == 0)
        {
            settings.noise.seed = Random.Range(int.MinValue, int.MaxValue);
        }

        for (var chunkIndex = 0; chunkIndex < settings.chunkCount; chunkIndex++)
        {
            #region Chunk Generator
            var blocks = new NativeArray<BlockType>(settings.chunkSize.x * settings.chunkSize.y * settings.chunkSize.z, Allocator.Persistent);
            var generateChunk = new GenerateChunk
            {
                settings = settings,
                blocks = blocks,
                chunkIndex = chunkIndex
            };

            var chunkGenerator = generateChunk.Schedule(blocks.Length, settings.chunkSize.y);
            chunkGenerator.Complete();
            #endregion

            #region Voxel Generator
            var ecb = new EntityCommandBuffer(Allocator.TempJob);

            var generateVoxel = new GenerateVoxel
            {
                settings = settings,
                blocks = blocks,
                ecb = ecb.AsParallelWriter(),
                chunkIndex = chunkIndex
            };

            var voxelGenerator = generateVoxel.Schedule(blocks.Length, settings.chunkSize.y);
            voxelGenerator.Complete();

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            #endregion

            blocks.Dispose();
        }
    }

    [BurstCompile]
    public void OnStopRunning(ref SystemState state)
    {
        
    }

    [BurstCompile]
    public struct GenerateChunk : IJobParallelForBatch
    {
        [ReadOnly]
        public WorldGenerationSettings settings;

        [WriteOnly]
        public NativeArray<BlockType> blocks;

        [ReadOnly]
        public int chunkIndex;

        [BurstCompile]
        public void Execute(int startIndex, int count)
        {
            var position = GetBlockPosition(startIndex, settings);
            var chunkOffset = GetChunkOffset(chunkIndex, settings);
            int terrainHeight = math.clamp(ComputeTerrainHeight(position.x + chunkOffset.x, position.z + chunkOffset.y, settings.noise.seed), 0, settings.chunkSize.y);

            for (var y = 0; y < terrainHeight; y++)
            {
                blocks[startIndex + y] = BlockType.Grass;
            }

            for (var y = terrainHeight; y < count; y++)
            {
                blocks[startIndex + y] = BlockType.Air;
            }
        }

        private int ComputeTerrainHeight(int x, int z, int seed)
        {
            var n = noise.snoise(new float2(x, z) * settings.noise.frequency);
            n = math.remap(-1.0f, 1.0f, settings.noise.min, settings.noise.max, n);
            var height = math.mul(n, settings.noise.amplitude);

            return (int)(height * (settings.chunkSize.y - 1));
        }
    }

    [BurstCompile]
    public struct GenerateVoxel : IJobParallelFor
    {
        [ReadOnly]
        public WorldGenerationSettings settings;

        [ReadOnly]
        public NativeArray<BlockType> blocks;

        [WriteOnly]
        public EntityCommandBuffer.ParallelWriter ecb;

        [ReadOnly]
        public int chunkIndex;

        [BurstCompile]
        public void Execute(int index)
        {
            if (blocks[index] == BlockType.Grass)
            {
                var position = GetBlockPosition(index, settings);

                // Culling method
                // TODO: implement greedy meshing algorithm
                if (position.x == 0 || (position.x == settings.chunkSize.x - 1) ||
                    position.y == 0 || (position.y == settings.chunkSize.y - 1) ||
                    position.z == 0 || (position.z == settings.chunkSize.z - 1) ||
                    blocks[GetBlockIndex(new int3(position.x + 1, position.y, position.z), settings.chunkSize)] == BlockType.Air ||
                    blocks[GetBlockIndex(new int3(position.x - 1, position.y, position.z), settings.chunkSize)] == BlockType.Air ||
                    blocks[GetBlockIndex(new int3(position.x, position.y + 1, position.z), settings.chunkSize)] == BlockType.Air ||
                    blocks[GetBlockIndex(new int3(position.x, position.y - 1, position.z), settings.chunkSize)] == BlockType.Air ||
                    blocks[GetBlockIndex(new int3(position.x, position.y, position.z + 1), settings.chunkSize)] == BlockType.Air ||
                    blocks[GetBlockIndex(new int3(position.x, position.y, position.z - 1), settings.chunkSize)] == BlockType.Air)
                {
                    var chunkOffset = GetChunkOffset(chunkIndex, settings);
                    var entity = ecb.Instantiate(index, settings.grassPrefab);
                    ecb.SetComponent(index, entity, new LocalTransform
                    {
                        Position = position + new int3(chunkOffset.x, 0, chunkOffset.y),
                        Scale = 1.0f
                    });
                }
            }
        }
    }

    private static int2 GetChunkOffset(int chunkIndex, WorldGenerationSettings settings)
    {
        int chunksForAxis = (int)math.sqrt(settings.chunkCount);
        return new int2((chunkIndex / chunksForAxis) * settings.chunkSize.x, (chunkIndex % chunksForAxis) * settings.chunkSize.z);
    }

    private static int3 GetBlockPosition(int blockIndex, WorldGenerationSettings settings)
    {
        int x = blockIndex / (settings.chunkSize.y * settings.chunkSize.z);
        int y = blockIndex % settings.chunkSize.y;
        int z = (blockIndex / settings.chunkSize.y) % settings.chunkSize.z;

        return new int3(x, y, z);
    }

    private static int GetBlockIndex(int3 position, int3 chunkSize)
    {
        return position.x * (chunkSize.y * chunkSize.z) + position.z * chunkSize.y + position.y;
    }
}

public enum BlockType
{
    Grass,
    Air
}