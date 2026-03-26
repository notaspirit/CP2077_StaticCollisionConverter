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
    public static entEntityTemplate Generate(string CMeshPath, byte[] dynCollMesh, dynCollMeshType meshType)
    {
        var entity = new entEntityTemplate();
        entity.Components = new CArray<entIComponent>();
        entity.Components.Add(
            new entMeshComponent()
            {
                Mesh = new CResourceAsyncReference<CMesh>(CMeshPath)
            }
            );

        var collComp = new entSimpleColliderComponent();
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
        return entity;
    }
}