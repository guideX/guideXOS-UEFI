; guideXOS Phase 25 freestanding bootstrap.
; Entry contract: RDI = user virtual address of ManagedImageStartupBlock.
; The kernel supplies a process-owned user RSP and installs GS before iretq.
; This file deliberately has no calls, imports, relocations, CRT, or runtime.

BITS 64

%define ABI_VERSION                 1
%define STARTUP_VERSION             1
%define MANAGED_IMAGE_BASE          0x0000401000000000
%define STARTUP_BLOCK               0x0000401100000000
%define BOOTSTRAP_BASE              0x0000401200000000
%define MANAGED_ENTRY               (MANAGED_IMAGE_BASE + 0x1420)
%define RUNTIME_STATE               0x0000401100010000
%define GS_BLOCK                    0x0000401100020000
%define TLS_VECTOR                  0x0000401100030000
%define FLS_STATE                   0x0000401100040000
%define USER_LIMIT                  0x0000800000000000
%define TLS_CURRENT_THREAD_RVA      0x97120
%define TLS_INDEX_RVA               0xa5ccc
%define MANAGED_ENTRY_BLOCKED       0x00000008
%define PHASE25_FAULT               0x00000100
%define RESULT_MAGIC                0x31524247       ; "GBR1"
%define RESULT_VERSION              1
%define RESULT_ENVIRONMENT          0x00000001
%define RESULT_PREEMPT_SENTINEL     0x00000002
%define RESULT_GS_TLS               0x00000004
%define RESULT_TLS_VECTOR           0x00000008
%define RESULT_FLS                  0x00000010
%define RESULT_PING                 0x00000020
%define RESULT_IPC                  0x00000040
%define RESULT_RESPONSE             0x00000080
%define RESULT_EXIT                 0x00000100
%define RESULT_SUCCESS_FLAGS        0x000001ff
%define RESULT_SETUP_FLAGS          (RESULT_ENVIRONMENT | RESULT_GS_TLS | RESULT_TLS_VECTOR | RESULT_FLS)
%define CONTEXT_SENTINEL            0x142e5a91c0de4711
%define STACK_SENTINEL              0x25a5c0de25a5c0de
%define FLS_SENTINEL                0x255f4c535f53454e
%define SERVICE_REQUEST_SIZE        32
%define SYSTEM_INFORMATION_SIZE     128

global phase25_bootstrap_start
phase25_bootstrap_start:
    ; Establish deterministic state before any user memory is trusted.
    mov r12, CONTEXT_SENTINEL
    mov rax, STACK_SENTINEL
    mov qword [rsp - 8], rax

    ; ManagedImageStartupBlock v1 and frozen Phase 23 identity.
    cmp dword [rdi + 0], ABI_VERSION
    jne .fail
    cmp dword [rdi + 4], STARTUP_VERSION
    jne .fail
    mov rax, MANAGED_IMAGE_BASE
    cmp qword [rdi + 8], rax
    jne .fail
    mov rax, BOOTSTRAP_BASE
    cmp qword [rdi + 24], rax
    jne .fail
    mov rax, MANAGED_ENTRY
    cmp qword [rdi + 32], rax
    jne .fail
    mov rax, GS_BLOCK
    cmp qword [rdi + 56], rax
    jne .fail
    mov rax, TLS_VECTOR
    cmp qword [rdi + 64], rax
    jne .fail
    cmp dword [rdi + 72], 0
    jne .fail
    mov rax, FLS_STATE
    cmp qword [rdi + 80], rax
    jne .fail
    mov rax, RUNTIME_STATE
    cmp qword [rdi + 88], rax
    jne .fail
    mov eax, [rdi + 136]
    test eax, MANAGED_ENTRY_BLOCKED
    jz .fail

    ; Actual X3 access: GS+0x58 is the TLS vector pointer.
    mov rax, [gs:0x58]
    cmp rax, [rdi + 64]
    jne .fail
    mov rbx, [rax]
    cmp rbx, [rdi + 88]
    jne .fail

    ; Validate the process-private runtime state and its generation/owner.
    mov r8, [rdi + 88]
    cmp dword [r8 + 0], STARTUP_VERSION
    jne .fail
    mov rax, [r8 + 8]
    cmp rax, r8
    jne .fail
    mov rax, [rdi + 8]
    mov r9, rax
    add r9, TLS_CURRENT_THREAD_RVA
    mov rax, USER_LIMIT
    cmp r9, rax
    jae .fail
    ; tls_CurrentThread is a frozen image location; before managed startup its
    ; process-private slot is deterministically zero, never a kernel pointer.
    cmp qword [r9], 0
    jne .fail
    mov r9, rax
    mov rax, [rdi + 8]
    add rax, TLS_INDEX_RVA
    cmp dword [rax], 0
    jne .fail

    ; Bounded FLS scaffold: observe, set, get, and clear one user-owned slot.
    mov r8, [rdi + 80]
    cmp dword [r8 + 0], STARTUP_VERSION
    jne .fail
    cmp dword [r8 + 4], 64
    jne .fail
    cmp qword [r8 + 24], 0
    jne .fail
    mov qword [r8 + 24], 1
    mov rax, FLS_SENTINEL
    mov qword [r8 + 32], rax
    cmp qword [r8 + 32], rax
    jne .fail
    mov qword [r8 + 32], 0
    mov qword [r8 + 24], 0

    ; Publish the environment checks before the optional controlled fault.
    mov dword [rdi + 0x300], RESULT_MAGIC
    mov dword [rdi + 0x304], RESULT_SETUP_FLAGS
    mov dword [rdi + 0x308], RESULT_VERSION
    mov rax, CONTEXT_SENTINEL
    cmp r12, rax
    jne .fail

    mov eax, [rdi + 136]
    test eax, PHASE25_FAULT
    jz .preempt
    ; Deliberate CPL3 page fault: immediately above the mapped user stack.
    mov rax, 0x00007fff00010000
    mov rax, [rax]
    ud2

.preempt:
    ; The loop is long enough for IRQ0 to preempt this CPL3 thread.  The
    ; kernel saves/restores R12 and the user stack on its trusted kernel stack.
    mov ecx, 0x02000000
.preempt_loop:
    dec ecx
    jnz .preempt_loop
    mov rax, CONTEXT_SENTINEL
    cmp r12, rax
    jne .fail
    mov rax, STACK_SENTINEL
    cmp qword [rsp - 8], rax
    jne .fail

    ; Re-read GS/TLS after the potential scheduler transition.
    mov rax, [gs:0x58]
    cmp rax, [rdi + 64]
    jne .fail
    mov rbx, [rax]
    cmp rbx, [rdi + 88]
    jne .fail
    mov dword [rdi + 0x304], (RESULT_SETUP_FLAGS | RESULT_PREEMPT_SENTINEL)

    ; Ping ABI/version query.
    mov eax, 1
    int 0x80
    cmp rax, ABI_VERSION
    jne .fail

    ; Existing bounded System Information service ABI.  Request and response
    ; live in unused space in the process-owned startup-block page.
    lea r8, [rdi + 0x100]
    mov dword [r8 + 0], 1
    mov dword [r8 + 4], 3
    mov dword [r8 + 8], 1
    mov dword [r8 + 12], SERVICE_REQUEST_SIZE
    lea rax, [rdi + 0x200]
    mov qword [r8 + 16], rax
    mov dword [r8 + 24], SYSTEM_INFORMATION_SIZE
    mov dword [r8 + 28], 0
    mov eax, 5
    mov rdi, r8
    mov esi, SERVICE_REQUEST_SIZE
    int 0x80
    test rax, rax
    jnz .fail

    ; The startup block is a fixed process-private mapping in the contract;
    ; recover it explicitly because the syscall transport owns argument regs.
    mov rdi, STARTUP_BLOCK
    mov r8, rdi
    lea r9, [rdi + 0x200]
    cmp dword [r9 + 0], 1
    jne .fail
    cmp dword [r9 + 4], SYSTEM_INFORMATION_SIZE
    jne .fail
    mov rax, [r9 + 16]
    cmp rax, [r9 + 24]
    jae .memory_ok
    jne .fail
.memory_ok:
    cmp word [r9 + 40], 32
    ja .fail
    cmp word [r9 + 42], 32
    ja .fail
    cmp word [r9 + 44], 16
    ja .fail

    ; Restore the startup-block pointer after the ABI call's argument setup.
    mov rdi, r8
    mov rax, CONTEXT_SENTINEL
    cmp r12, rax
    jne .fail
    mov rax, STACK_SENTINEL
    cmp qword [rsp - 8], rax
    jne .fail
    mov dword [rdi + 0x304], RESULT_SUCCESS_FLAGS
    mov dword [rdi + 0x308], RESULT_VERSION

    ; Existing Exit(0) ABI.  A terminal process must never return to wmain.
    mov eax, 2
    xor edi, edi
    int 0x80
    ud2

.fail:
    ; Fail closed through the bounded Exit ABI; no kernel pointer is exposed.
    mov eax, 2
    mov edi, 1
    int 0x80
    ud2

phase25_bootstrap_end:
