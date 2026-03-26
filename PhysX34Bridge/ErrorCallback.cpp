#include "ErrorCallback.h"

ErrorCallback::ErrorCallback(bool *outError)
{
    m_outError = outError;
}

auto ErrorCallback::reportError(physx::PxErrorCode::Enum code, const char *message, const char *file, int line) -> void
{
    if (code == physx::PxErrorCode::eNO_ERROR ||
        code == physx::PxErrorCode::eDEBUG_INFO ||
        code == physx::PxErrorCode::eDEBUG_WARNING ||
        code == physx::PxErrorCode::eMASK_ALL)
    {
       *m_outError = false;
    }
    else
    {
        *m_outError = true;
    }
}


