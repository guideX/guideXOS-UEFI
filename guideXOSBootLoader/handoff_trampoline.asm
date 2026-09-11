; Minimal UEFI -> kernel handoff trampoline (x86_64, MS x64 ABI)
; Assembled with NASM: nasm -f win64 handoff_trampoline.asm -o handoff_trampoline.obj
;
; Extern C signature:
;   void BootHandoffTrampoline(void* kernelEntry, void* bootInfo, void* stackTop, void* pml4Phys);
;
; Behavior:
;   - cli
;   - Save parameters BEFORE any stack/CR3 changes
;   - Load cr3 = pml4Phys (BEFORE stack switch, since trampoline must be mapped)
;   - rsp = stackTop (16-byte aligned), then reserve 0x28 bytes
;     (32-byte home space plus an 8-byte synthetic CALL return slot)
;   - jmp kernelEntry(bootInfo) using MS x64 ABI (RCX=bootInfo), presenting
;     the same RSP%16==8 callee entry state as a normal CALL
;
; CRITICAL: The trampoline code AND the new stack must both be identity-mapped
;           in the new page tables before calling this function!

BITS 64
DEFAULT REL

global BootHandoffTrampoline
global SetupTrampoline
global GetTrampolineCodeSize

section .text

; void BootHandoffTrampoline(void* kernelEntry, void* bootInfo, void* stackTop, void* pml4Phys);
BootHandoffTrampoline:
    ; Windows x64 calling convention on entry:
    ;   RCX = kernelEntry
    ;   RDX = bootInfo
    ;   R8  = stackTop
    ;   R9  = pml4Phys

    cli                         ; Disable interrupts - no going back

    ; === Stage 1: Save all parameters in non-volatile registers FIRST ===
    ; Capture the parameters before changing the stack or page tables.
    mov r12, rcx                ; r12 = kernelEntry
    mov r13, rdx                ; r13 = bootInfo
    mov r14, r8                 ; r14 = stackTop
    mov r15, r9                 ; r15 = pml4Phys

    ; === Stage 2: Load CR3 with new page tables ===
    ; Do this BEFORE stack switch! The trampoline code is executing from
    ; memory that must be identity-mapped in both old and new page tables.
    test r15, r15               ; Check if pml4Phys is NULL
    jz .skip_cr3                ; If NULL, keep current page tables

    mov rax, r15
    mov cr3, rax                ; Load new page tables (TLB flush)

.skip_cr3:
    ; === Stage 3: Switch to new stack ===
    ; Now that we have new page tables, switch to the new stack
    ; (which must be mapped in the new page tables)
    mov rsp, r14                ; rsp = stackTop
    and rsp, ~0Fh               ; Ensure 16-byte alignment

    ; === Stage 4: Set up kernel call ===
    ; MS x64 ABI: RCX = first parameter (bootInfo)
    mov rcx, r13                ; rcx = bootInfo

    ; A normal MS x64 CALL presents the callee with RSP%16==8 because CALL
    ; pushes an 8-byte return address. This transition uses JMP, so reserve
    ; that slot explicitly, in addition to the required 32-byte home space.
    ; The slot is intentionally not populated: KMain is non-returning.
    ; Aligned stackTop (RSP%16==0) - 0x28 => NativeAOT callee entry RSP%16==8.
    sub rsp, 28h

    ; Clear other parameter registers (not strictly necessary but clean)
    xor rdx, rdx
    xor r8, r8
    xor r9, r9

    ; === FINAL JUMP TO KERNEL ===
    ; RCX = bootInfo pointer (MS x64 ABI first argument)
    ; RSP is 8 mod 16 at this exact boundary: the JMP supplies no return
    ; address, so the reserved 0x28 presents normal CALL-callee alignment.
    ; Page tables are loaded in CR3
    ;
    ; Use JMP instead of CALL because KMain is non-returning; the explicit
    ; 0x28 reservation above supplies the normal callee entry geometry.
    jmp r12
    
.kernel_returned:
.hang:
    hlt
    jmp .hang

; Stub functions for compatibility with trampoline_msvc.cpp interface
; (These are no-ops since the code is already in executable memory)
SetupTrampoline:
    ret

GetTrampolineCodeSize:
    mov rax, 256                ; Return a reasonable size estimate
    ret
