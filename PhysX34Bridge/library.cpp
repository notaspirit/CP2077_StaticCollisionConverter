#define PHYSX_WRAPPER_EXPORTS
#include "library.h"

#include <fstream>

extern "C"
{
    bool PxBInit()
    {
        if (gFoundation != nullptr || gCooking != nullptr)
            return true;

        bool hasError = false;
        auto errorCallback = ErrorCallback(&hasError);

        gFoundation = PxCreateFoundation(PX_FOUNDATION_VERSION, gAllocator, errorCallback);
        if (gFoundation == nullptr || hasError)
            return false;

        physx::PxTolerancesScale scale;
        scale.length = 1.0f;
        scale.speed = 9.81f;

        physx::PxCookingParams params(scale);
        params.meshPreprocessParams = physx::PxMeshPreprocessingFlag::eWELD_VERTICES;
        params.meshWeldTolerance = 0.05f;

        gCooking = PxCreateCooking(PX_PHYSICS_VERSION, *gFoundation, params);
        if (gCooking == nullptr || hasError)
            return false;

        return true;
    }

    void PxBDestroy()
    {
        if (gCooking != nullptr)
        {
            gCooking->release();
            gCooking = nullptr;
        }

        if (gFoundation != nullptr)
        {
            gFoundation->release();
            gFoundation = nullptr;
        }
    }

    void PxBFreeBuffer(void* buffer)
    {
        if (buffer == nullptr)
            return;

        delete[] reinterpret_cast<uint8_t*>(buffer);
    }

    PxBCookedMeshResult PxBCookTriangleMesh(
        const float* vertices,
        uint32_t vertexCount,
        const uint32_t* indices,
        uint32_t indexCount)
    {
        physx::PxDefaultMemoryOutputStream buf;

        physx::PxTriangleMeshDesc desc;
        desc.points.count = vertexCount / 3;
        desc.points.stride = sizeof(float) * 3;
        desc.points.data = vertices;
        desc.triangles.count = indexCount / 3;
        desc.triangles.stride = 3 * sizeof(physx::PxU32);
        desc.triangles.data = indices;
        gCooking->cookTriangleMesh(desc, buf);

        PxBCookedMeshResult result{};
        result.size = buf.getSize();
        result.data = new uint8_t[result.size];

        memcpy(result.data, buf.getData(), result.size);

        return result;
    }

    PxBCookedMeshResult PxBCookConvexMesh(
        const float* vertices,
        uint32_t vertexCount)
    {
        physx::PxDefaultMemoryOutputStream buf;

        physx::PxConvexMeshDesc desc;
        desc.points.count = vertexCount / 3;
        desc.points.stride = sizeof(float) * 3;
        desc.points.data = vertices;
        desc.flags = physx::PxConvexFlag::eCOMPUTE_CONVEX;
        desc.vertexLimit = 256; // it's what the blender addon uses too, not sure if it's the right count for this context
        gCooking->cookConvexMesh(desc, buf);

        PxBCookedMeshResult result{};
        result.size = buf.getSize();
        result.data = new uint8_t[result.size];

        memcpy(result.data, buf.getData(), result.size);

        return result;
    }
}


