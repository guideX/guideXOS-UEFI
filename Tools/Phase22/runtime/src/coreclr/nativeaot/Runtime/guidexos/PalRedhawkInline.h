// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// GUIDEXOS NativeAOT inline PAL operations.
//
// These operations are compiler intrinsics or process-local state. They do not
// forward to the Windows PAL and do not introduce a target OS dependency.

#ifndef GUIDEXOS_PAL_REDHAWK_INLINE_INCLUDED
#define GUIDEXOS_PAL_REDHAWK_INLINE_INCLUDED

#include <intrin.h>

FORCEINLINE int32_t PalInterlockedIncrement(_Inout_ int32_t volatile *pDst)
{
    return _InterlockedIncrement((long volatile *)pDst);
}

FORCEINLINE int32_t PalInterlockedDecrement(_Inout_ int32_t volatile *pDst)
{
    return _InterlockedDecrement((long volatile *)pDst);
}

FORCEINLINE uint32_t PalInterlockedOr(_Inout_ uint32_t volatile *pDst, uint32_t iValue)
{
    return (uint32_t)_InterlockedOr((long volatile *)pDst, (long)iValue);
}

FORCEINLINE uint32_t PalInterlockedAnd(_Inout_ uint32_t volatile *pDst, uint32_t iValue)
{
    return (uint32_t)_InterlockedAnd((long volatile *)pDst, (long)iValue);
}

FORCEINLINE int32_t PalInterlockedExchange(_Inout_ int32_t volatile *pDst, int32_t iValue)
{
    return _InterlockedExchange((long volatile *)pDst, (long)iValue);
}

FORCEINLINE int32_t PalInterlockedCompareExchange(_Inout_ int32_t volatile *pDst, int32_t iValue, int32_t iComparand)
{
    return _InterlockedCompareExchange((long volatile *)pDst, (long)iValue, (long)iComparand);
}

FORCEINLINE int64_t PalInterlockedExchange64(_Inout_ int64_t volatile *pDst, int64_t iValue)
{
    return _InterlockedExchange64(pDst, iValue);
}

FORCEINLINE int64_t PalInterlockedCompareExchange64(_Inout_ int64_t volatile *pDst, int64_t iValue, int64_t iComparand)
{
    return _InterlockedCompareExchange64(pDst, iValue, iComparand);
}

#if defined(HOST_AMD64) || defined(HOST_ARM64)
FORCEINLINE uint8_t PalInterlockedCompareExchange128(_Inout_ int64_t volatile *pDst, int64_t iValueHigh, int64_t iValueLow, int64_t *pComparandAndResult)
{
    return _InterlockedCompareExchange128(pDst, iValueHigh, iValueLow, pComparandAndResult);
}
#endif

#ifdef HOST_64BIT
FORCEINLINE void *PalInterlockedExchangePointer(_Inout_ void * volatile *pDst, _In_ void *pValue)
{
    return _InterlockedExchangePointer(pDst, pValue);
}

FORCEINLINE void *PalInterlockedCompareExchangePointer(_Inout_ void * volatile *pDst, _In_ void *pValue, _In_ void *pComparand)
{
    return _InterlockedCompareExchangePointer(pDst, pValue, pComparand);
}
#else
#define PalInterlockedExchangePointer(_pDst, _pValue) \
    ((void *)PalInterlockedExchange((int32_t volatile *)(_pDst), (int32_t)(size_t)(_pValue)))
#define PalInterlockedCompareExchangePointer(_pDst, _pValue, _pComparand) \
    ((void *)PalInterlockedCompareExchange((int32_t volatile *)(_pDst), (int32_t)(size_t)(_pValue), (int32_t)(size_t)(_pComparand)))
#endif

static thread_local int32_t g_guidexos_last_error = 0;

FORCEINLINE int32_t PalGetLastError()
{
    return g_guidexos_last_error;
}

FORCEINLINE void PalSetLastError(int32_t error)
{
    g_guidexos_last_error = error;
}

#ifdef HOST_AMD64
FORCEINLINE void PalYieldProcessor()
{
    _mm_pause();
}

#pragma intrinsic(__faststorefence)
#define PalMemoryBarrier() __faststorefence()
#else
FORCEINLINE void PalYieldProcessor()
{
    __yield();
}
#define PalMemoryBarrier() __dmb(_ARM64_BARRIER_ISH)
#endif

#define PalDebugBreak() __debugbreak()

FORCEINLINE int32_t PalOsPageSize()
{
    return 0x1000;
}

#ifndef FireEtwYieldProcessorMeasurement
#define FireEtwYieldProcessorMeasurement(...) 0
#endif
#ifndef EventEnabledYieldProcessorMeasurement
#define EventEnabledYieldProcessorMeasurement() false
#endif

#endif // GUIDEXOS_PAL_REDHAWK_INLINE_INCLUDED
