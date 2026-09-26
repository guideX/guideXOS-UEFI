; Phase 26 entry: validate the loader-owned X3 state, call NativeAOT wmain,
; publish its integer result, then terminate through the bounded Ring 3 ABI.
BITS 64

%define ABI_VERSION             1
%define STARTUP_VERSION         1
%define MANAGED_IMAGE_BASE      0x0000401000000000
%define BOOTSTRAP_BASE          0x0000401200000000
%define RUNTIME_STATE            0x0000401100010000
%define GS_BLOCK                0x0000401100020000
%define TLS_VECTOR              0x0000401100030000
%define FLS_STATE               0x0000401100040000
%define TLS_BLOCK               0x0000401100060000
%define RESULT_MAGIC            0x31524247
%define RESULT_VERSION          2
%define RESULT_ENTRY_CALLED     0x00000200
%define RESULT_RETURNED         0x00000400
%define RESULT_SUCCESS_FLAGS    (RESULT_ENTRY_CALLED | RESULT_RETURNED)
%define CONTEXT_SENTINEL        0x142e5a91c0de4711

global phase26_bootstrap_start
phase26_bootstrap_start:
    mov r15, rdi

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
    ; The kernel has already validated the descriptor-derived managed entry
    ; and copied that address into the startup block.  Consume that validated
    ; value instead of duplicating a generated wmain RVA here, while retaining
    ; a bootstrap-side image-range check.
    mov rax, [rdi + 32]
    cmp rax, [rdi + 8]
    jb .fail
    mov rdx, [rdi + 8]
    add rdx, [rdi + 16]
    jc .fail
    cmp rax, rdx
    jae .fail
    mov rax, GS_BLOCK
    cmp qword [rdi + 56], rax
    jne .fail
    mov rax, TLS_VECTOR
    cmp qword [rdi + 64], rax
    jne .fail
    mov rax, FLS_STATE
    cmp qword [rdi + 80], rax
    jne .fail
    mov rax, RUNTIME_STATE
    cmp qword [rdi + 88], rax
    jne .fail

    mov rax, [gs:0x58]
    cmp rax, [rdi + 64]
    jne .fail
    mov rbx, [rax]
    mov rax, TLS_BLOCK
    cmp rbx, rax
    jne .fail
    mov rbx, RUNTIME_STATE
    cmp dword [rbx + 0], STARTUP_VERSION
    jne .fail
    mov rax, RUNTIME_STATE
    cmp qword [rbx + 8], rax
    jne .fail
    cmp dword [rdi + 72], 0
    jne .fail

    ; Microsoft x64 caller frame: 32-byte home area.  The loader supplies a
    ; 16-byte-aligned top, so retaining that alignment here gives wmain the
    ; required 8-mod-16 entry RSP after CALL.  NativeAOT's generated startup
    ; path expects the ordinary wmain contract, including argv[0], even when
    ; the managed Main method has no arguments.
    mov rbx, RUNTIME_STATE
    mov [rbx + 0xF8], rdi
    mov rax, CONTEXT_SENTINEL
    mov [rbx + 0xF0], rax
    sub rsp, 0x40
    lea rdx, [rsp + 0x20]
    lea rax, [rsp + 0x38]
    mov [rdx + 0x00], rax
    mov qword [rdx + 0x08], 0
    mov word [rax], 0
    mov ecx, 1
    mov rax, [r15 + 32]
    call rax
    add rsp, 0x40
    mov r14d, eax

    mov rbx, RUNTIME_STATE
    mov r15, [rbx + 0xF8]
    mov rax, CONTEXT_SENTINEL
    cmp qword [rbx + 0xF0], rax
    jne .fail
    mov dword [r15 + 0x300], RESULT_MAGIC
    mov dword [r15 + 0x304], RESULT_SUCCESS_FLAGS
    mov dword [r15 + 0x308], RESULT_VERSION
    mov dword [r15 + 0x30c], r14d

    mov eax, 2
    mov edi, r14d
    int 0x80
    ud2

.fail:
    mov dword [r15 + 0x300], RESULT_MAGIC
    mov dword [r15 + 0x304], 0
    mov dword [r15 + 0x308], RESULT_VERSION
    mov eax, 2
    mov edi, 1
    int 0x80
    ud2

phase26_bootstrap_end:
