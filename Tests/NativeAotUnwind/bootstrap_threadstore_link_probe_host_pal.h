#pragma once

// Hosted-only services for the NativeAOT bootstrap link probe.
// They are not part of the guideXOS production PAL.
extern "C" bool HostPalPrepareThreadServices();
extern "C" void HostPalRejectHijackCallbackRegistration(bool reject);
extern "C" void* HostPalGetAttachedThread();
