#pragma once
#include "ErrorCallback.h"

#ifdef PHYSX_WRAPPER_EXPORTS
#define API __declspec(dllexport)
#else
#define API __declspec(dllimport)
#endif

#include <stdint.h>
#include <PxPhysicsAPI.h>

struct PxBCookedMeshResult
{
    uint8_t* data;
    uint32_t size;
};

static physx::PxFoundation* gFoundation = nullptr;
static physx::PxCooking* gCooking = nullptr;

static physx::PxDefaultAllocator gAllocator;

extern "C"
{
    // Lifetime
    API bool PxBInit();
    API void PxBDestroy();

    API void PxBFreeBuffer(void* buffer);

    // Operations
    API PxBCookedMeshResult PxBCookTriangleMesh(
        const float* vertices,
        uint32_t vertexCount,
        const uint32_t* indices,
        uint32_t indexCount);
    API PxBCookedMeshResult PxBCookConvexMesh(
        const float* vertices,
        uint32_t vertexCount);
}