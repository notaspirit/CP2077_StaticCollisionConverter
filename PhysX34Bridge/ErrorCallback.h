#pragma once
#include <foundation/PxErrorCallback.h>

class ErrorCallback : public physx::PxErrorCallback
{
public:
    ErrorCallback(bool* outError);
    auto reportError(physx::PxErrorCode::Enum code, const char* message,
                     const char* file, int line) -> void override;
private:
    bool* m_outError;
};
