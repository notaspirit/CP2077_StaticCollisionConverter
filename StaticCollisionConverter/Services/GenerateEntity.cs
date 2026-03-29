using WolvenKit.Core.Extensions;
using WolvenKit.RED4.Archive.CR2W;
using WolvenKit.RED4.Types;

namespace StaticCollisionConverter.Services;

public enum dynCollMeshType
{
    Invalid,
    TriangleMesh,
    ConvexMesh

}

public class GenerateEntity
{
    private static Random random = new Random();
    public static CR2WFile Generate(string? CMeshPath, List<byte[]> dynCollMeshes, dynCollMeshType meshType)
    {
        var entity = new entEntityTemplate()
        {
            Components = new CArray<entIComponent>(),
            Entity = new gameObject()
        };
        
        if (CMeshPath != null)
            entity.Components.Add(
                new entMeshComponent()
                {
                    Mesh = new CResourceAsyncReference<CMesh>(CMeshPath),
                    Id = random.NextCRUID(),
                    Name = "mesh"
                });

        foreach (var dynCollMesh in dynCollMeshes)
        {
            var collComp = new entColliderComponent()
            {
                Id = random.NextCRUID(),
                Name = $"collision_mesh_{dynCollMeshes.IndexOf(dynCollMesh)}",
                Colliders = new CArray<CHandle<physicsICollider>>(),
                FilterData = new physicsFilterData()
                {
                    Preset = "World Static",
                    QueryFilter = new physicsQueryFilter()
                    {
                        Mask1 = 0,
                        Mask2 = 70107400
                    },
                    SimulationFilter = new physicsSimulationFilter()
                    {
                        Mask1 = 114696,
                        Mask2 = 23627
                    }
                },
                Volume = 1,
                Mass = 1
            };
        
            collComp.Colliders = new CArray<CHandle<physicsICollider>>();
            switch (meshType)
            {
                case dynCollMeshType.TriangleMesh:
                    collComp.Colliders.Add(new physicsColliderMesh()
                    {
                        CompiledGeometryBuffer = new DataBuffer(dynCollMesh)
                    });
                    break;
                case dynCollMeshType.ConvexMesh:
                    collComp.Colliders.Add(new physicsColliderConvex()
                    {
                        CompiledGeometryBuffer = new DataBuffer(dynCollMesh)
                    });
                    break;
                case dynCollMeshType.Invalid:
                default:
                    throw new InvalidOperationException($"Type {meshType} is not supported");
            }
        
            entity.Components.Add(collComp);
        }
        
        return new CR2WFile()
        {
            RootChunk = entity
        };
    }
}