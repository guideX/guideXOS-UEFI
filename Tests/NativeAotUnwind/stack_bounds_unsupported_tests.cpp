#include <cstdio>

extern "C" bool PalGetMaximumStackBounds(void** ppStackLowOut,
                                         void** ppStackHighOut);

int main()
{
    int failures = 0;
    int cases = 0;
    auto check = [&](bool condition, const char* name) {
        ++cases;
        if (!condition) {
            ++failures;
            std::fprintf(stderr, "FAIL: %s\n", name);
        }
    };

    void* low = reinterpret_cast<void*>(static_cast<unsigned long long>(0x1111));
    void* high = reinterpret_cast<void*>(static_cast<unsigned long long>(0x2222));
    const bool bothReported = PalGetMaximumStackBounds(&low, &high);
    check(!bothReported && low == nullptr && high == nullptr,
          "unsupported provider clears both outputs and returns false");

    low = reinterpret_cast<void*>(static_cast<unsigned long long>(0x3333));
    const bool lowOnlyReported = PalGetMaximumStackBounds(&low, nullptr);
    check(!lowOnlyReported && low == nullptr,
          "unsupported provider clears the low output independently");

    high = reinterpret_cast<void*>(static_cast<unsigned long long>(0x4444));
    const bool highOnlyReported = PalGetMaximumStackBounds(nullptr, &high);
    check(!highOnlyReported && high == nullptr,
          "unsupported provider clears the high output independently");

    check(!PalGetMaximumStackBounds(nullptr, nullptr),
          "unsupported provider rejects absent output pointers");

    std::printf("stack_bounds_unsupported: %d passed, %d failed\n",
                cases - failures, failures);
    return failures == 0 ? 0 : 1;
}
