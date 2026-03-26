cmake_minimum_required(VERSION 4.1)
set(CMAKE_MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>")
project(PhysX34Bridge)

set(CMAKE_CXX_STANDARD 20)

set(PHYSX_SDK_DIR "F:/dev0/prjk/PhysX-3.4/PhysX_3.4")
set(PHYSX_LIB_DIR "${PHYSX_SDK_DIR}/Lib/vc15win64")
set(PHYSX_SHARED_LIB_DIR "${PHYSX_SDK_DIR}/../PxShared/lib/vc15win64")

file(GLOB PHYSX_LIBRARIES
        "${PHYSX_LIB_DIR}/*.lib"
        "${PHYSX_SHARED_LIB_DIR}/*.lib"
)

get_filename_component(PXSHARED_ROOT "${PHYSX_SDK_DIR}/.." ABSOLUTE)
find_path(PHYSX_INCLUDE_DIR NAMES PxPhysicsAPI.h HINTS "${PHYSX_SDK_DIR}" PATH_SUFFIXES include Include)
find_path(PXSHARED_INCLUDE_DIR NAMES foundation/Px.h HINTS "${PXSHARED_ROOT}" PATH_SUFFIXES PxShared/include)
find_path(PHYSX_VEHICLE_INCLUDE_DIR NAMES vehicle/PxVehicleSDK.h HINTS "${PHYSX_SDK_DIR}" PATH_SUFFIXES include Include)

add_library(PhysX34Bridge SHARED library.cpp
        ErrorCallback.h
        ErrorCallback.cpp)

target_include_directories(PhysX34Bridge PRIVATE
        "${PHYSX_INCLUDE_DIR}"
        "${PXSHARED_INCLUDE_DIR}"
        "${PHYSX_VEHICLE_INCLUDE_DIR}"
)

target_link_libraries(PhysX34Bridge PRIVATE ${PHYSX_LIBRARIES})