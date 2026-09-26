; Phase 26 NativeAOT PAL syscall veneer.
; Microsoft x64 ABI in, guideXOS Ring 3 ABI out.

BITS 64

SECTION .text
GLOBAL guidexos_pal_syscall5
guidexos_pal_syscall5:
    ; The fifth and sixth C arguments are above the caller shadow space.
    mov r10, [rsp + 0x28]
    mov r11, [rsp + 0x30]

    ; RDI and RSI are nonvolatile under the Microsoft ABI.
    push rdi
    push rsi

    ; C signature: (operation, arg1, arg2, arg3, arg4, arg5).
    ; Ring 3 dispatch consumes RAX, RDI, RSI, RDX, R8, and R9.
    mov rax, rcx
    mov rdi, rdx
    mov rsi, r8
    mov rdx, r9
    mov r8, r10
    mov r9, r11
    int 0x80

    pop rsi
    pop rdi
    ret
