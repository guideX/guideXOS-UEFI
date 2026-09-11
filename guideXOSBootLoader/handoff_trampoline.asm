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

; Bounded ABI diagnostics.  These helpers have no net stack effect and do
; not issue CALL/RET, so each checkpoint reports the trampoline's real RSP at
; the named boundary.  The balanced push/pop in the byte emitter preserves
; the value while polling the UART line-status register.
%macro ABI_SERIAL_EMIT_AL 0
    push rax
    mov dx, 03FDh
%%wait:
    in al, dx
    test al, 20h
    jz %%wait
    pop rax
    mov dx, 03F8h
    out dx, al
%endmacro

%macro ABI_SERIAL_CHAR 1
    mov al, %1
    ABI_SERIAL_EMIT_AL
%endmacro

%macro ABI_SERIAL_HEX64 1
    mov r10, %1
    mov r11d, 16
%%hex:
    rol r10, 4
    mov al, r10b
    and al, 0Fh
    add al, '0'
    cmp al, '9'
    jbe %%digit
    add al, 7
%%digit:
    ABI_SERIAL_EMIT_AL
    dec r11d
    jnz %%hex
%endmacro

%macro ABI_SERIAL_NIBBLE 1
    mov r10, %1
    and r10d, 0Fh
    mov al, r10b
    add al, '0'
    cmp al, '9'
    jbe %%digit
    add al, 7
%%digit:
    ABI_SERIAL_EMIT_AL
%endmacro

%macro ABI_SERIAL_MOD32 1
    mov r10, %1
    and r10d, 1Fh
    mov al, r10b
    shr al, 4
    add al, '0'
    ABI_SERIAL_EMIT_AL
    mov al, r10b
    and al, 0Fh
    add al, '0'
    cmp al, '9'
    jbe %%digit
    add al, 7
%%digit:
    ABI_SERIAL_EMIT_AL
%endmacro

%macro ABI_RSP_CHECKPOINT 1
    ABI_SERIAL_CHAR 'A'
    ABI_SERIAL_CHAR 'B'
    ABI_SERIAL_CHAR 'I'
    ABI_SERIAL_CHAR '_'
    ABI_SERIAL_CHAR 'R'
    ABI_SERIAL_CHAR 'S'
    ABI_SERIAL_CHAR 'P'
    ABI_SERIAL_CHAR '_'
    ABI_SERIAL_CHAR %1
    ABI_SERIAL_CHAR ' '
    ABI_SERIAL_CHAR 'R'
    ABI_SERIAL_CHAR 'S'
    ABI_SERIAL_CHAR 'P'
    ABI_SERIAL_CHAR '='
    ABI_SERIAL_CHAR '0'
    ABI_SERIAL_CHAR 'x'
    ABI_SERIAL_HEX64 rsp
    ABI_SERIAL_CHAR ' '
    ABI_SERIAL_CHAR 'R'
    ABI_SERIAL_CHAR 'S'
    ABI_SERIAL_CHAR 'P'
    ABI_SERIAL_CHAR '_'
    ABI_SERIAL_CHAR 'M'
    ABI_SERIAL_CHAR 'O'
    ABI_SERIAL_CHAR 'D'
    ABI_SERIAL_CHAR '1'
    ABI_SERIAL_CHAR '6'
    ABI_SERIAL_CHAR '='
    ABI_SERIAL_NIBBLE rsp
    ABI_SERIAL_CHAR ' '
    ABI_SERIAL_CHAR 'R'
    ABI_SERIAL_CHAR 'S'
    ABI_SERIAL_CHAR 'P'
    ABI_SERIAL_CHAR '_'
    ABI_SERIAL_CHAR 'M'
    ABI_SERIAL_CHAR 'O'
    ABI_SERIAL_CHAR 'D'
    ABI_SERIAL_CHAR '3'
    ABI_SERIAL_CHAR '2'
    ABI_SERIAL_CHAR '='
    ABI_SERIAL_MOD32 rsp
    ABI_SERIAL_CHAR 0Dh
    ABI_SERIAL_CHAR 0Ah
%endmacro

; void BootHandoffTrampoline(void* kernelEntry, void* bootInfo, void* stackTop, void* pml4Phys);
BootHandoffTrampoline:
    ; Windows x64 calling convention on entry:
    ;   RCX = kernelEntry
    ;   RDX = bootInfo
    ;   R8  = stackTop
    ;   R9  = pml4Phys

    cli                         ; Disable interrupts - no going back

    ; === Stage 1: Save all parameters in non-volatile registers FIRST ===
    ; We must do this BEFORE touching stack, CR3, or the serial logger. The
    ; logger uses DX as a UART port register, so logging before this capture
    ; would corrupt the low 16 bits of the bootInfo argument in RDX.
    mov r12, rcx                ; r12 = kernelEntry
    mov r13, rdx                ; r13 = bootInfo
    mov r14, r8                 ; r14 = stackTop
    mov r15, r9                 ; r15 = pml4Phys

    ABI_RSP_CHECKPOINT 'T'       ; Exact trampoline-entry RSP

    ; --- Breadcrumb: 'T' = Trampoline entry ---
    mov dx, 03F8h
.wait_T:
    add dx, 5                   ; 0x3FD = line status
    in al, dx
    test al, 20h
    jz .wait_T
    sub dx, 5                   ; back to 0x3F8
    mov al, 'T'
    out dx, al

    ; === Stage 2: Load CR3 with new page tables ===
    ; Do this BEFORE stack switch! The trampoline code is executing from
    ; memory that must be identity-mapped in both old and new page tables.
    test r15, r15               ; Check if pml4Phys is NULL
    jz .skip_cr3                ; If NULL, keep current page tables

    mov rax, r15
    mov cr3, rax                ; Load new page tables (TLB flush)

    ; --- Breadcrumb: '3' = CR3 loaded ---
    mov dx, 03F8h
.wait_3:
    add dx, 5
    in al, dx
    test al, 20h
    jz .wait_3
    sub dx, 5
    mov al, '3'
    out dx, al

.skip_cr3:
    ; === Stage 3: Switch to new stack ===
    ABI_RSP_CHECKPOINT 'B'       ; RSP immediately before stack switch
    ; Now that we have new page tables, switch to the new stack
    ; (which must be mapped in the new page tables)
    mov rsp, r14                ; rsp = stackTop
    and rsp, ~0Fh               ; Ensure 16-byte alignment
    ABI_RSP_CHECKPOINT 'S'       ; RSP immediately after stack alignment

    ; --- Breadcrumb: 'S' = Stack switched ---
    mov dx, 03F8h
.wait_S:
    add dx, 5
    in al, dx
    test al, 20h
    jz .wait_S
    sub dx, 5
    mov al, 'S'
    out dx, al

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

    ; --- Breadcrumb: 'J' = About to Jump ---
    mov dx, 03F8h
.wait_J:
    add dx, 5
    in al, dx
    test al, 20h
    jz .wait_J
    sub dx, 5
    mov al, 'J'
    out dx, al

    ; --- Debug: Print the target address (r12) as hex ---
    ; Print low 32 bits (should be enough to identify virtual vs physical)
    mov r10, r12                ; Save r12 to r10 (we'll use this as the value to print)
    
    ; Print each nibble (8 hex digits for low 32 bits)
    mov r11d, 8                 ; Use r11 as loop counter (8 nibbles)
.print_addr:
    rol r10d, 4                 ; Rotate left, bringing high nibble to low position
    mov al, r10b
    and al, 0Fh
    add al, '0'
    cmp al, '9'
    jbe .digit_ok
    add al, 7                   ; 'A'-'9'-1 = 7
.digit_ok:
    ; Wait for serial ready
    mov dx, 03FDh
.wait_addr:
    push rax                    ; Save the digit
    in al, dx
    test al, 20h
    pop rax                     ; Restore the digit  
    jz .wait_addr
    ; Output the digit
    mov dx, 03F8h
    out dx, al
    dec r11d
    jnz .print_addr

    ; Print newline
.wait_nl:
    mov dx, 03FDh
    in al, dx
    test al, 20h
    jz .wait_nl
    mov dx, 03F8h
    mov al, 0Dh                 ; CR
    out dx, al
.wait_nl2:
    mov dx, 03FDh
    in al, dx
    test al, 20h
    jz .wait_nl2
    mov dx, 03F8h
    mov al, 0Ah                 ; LF
    out dx, al

    ; === Stage 5: Jump to kernel ===
    ; NOTE: Framebuffer writes removed - the actual framebuffer address comes
    ; from GOP and is passed in bootInfo. The hardcoded 0x80000000 was wrong.
    ; The kernel will handle framebuffer output after parsing bootInfo.
    
    ; Write 'F' to serial to indicate ready for jump
    mov dx, 03F8h
.wait_F:
    add dx, 5
    in al, dx
    test al, 20h
    jz .wait_F
    sub dx, 5
    mov al, 'F'
    out dx, al
    
    ; Write 'G' to serial to show we're about to jump to kernel
    mov dx, 03F8h
.wait_G:
    add dx, 5
    in al, dx
    test al, 20h
    jz .wait_G
    sub dx, 5
    mov al, 'G'
    out dx, al
    
    ; Write newline before jumping
    mov dx, 03F8h
.wait_nl3:
    add dx, 5
    in al, dx
    test al, 20h
    jz .wait_nl3
    sub dx, 5
    mov al, 0Dh
    out dx, al
.wait_nl4:
    mov dx, 03FDh
    in al, dx
    test al, 20h
    jz .wait_nl4
    mov dx, 03F8h
    mov al, 0Ah
    out dx, al
    
    ; === INLINE TEST: Try to execute at the kernel entry and see what happens ===
    ; Instead of jumping directly, let's manually execute the prologue
    ; and see if we can at least push to the stack
    
    ; Save the entry point for later
    push r12                    ; Save kernel entry on stack
    
    ; Write 'P' = about to test prologue manually
    mov dx, 03F8h
.wait_P:
    add dx, 5
    in al, dx
    test al, 20h
    jz .wait_P
    sub dx, 5
    mov al, 'P'
    out dx, al
    
    ; Read the first byte at the kernel entry point
    mov rax, r12                ; RAX = kernel entry
    mov al, [rax]               ; Try to READ from kernel entry
    
    ; Write 'R' = read succeeded
    mov dx, 03F8h
.wait_R:
    add dx, 5
    in al, dx
    test al, 20h
    jz .wait_R
    sub dx, 5
    mov al, 'R'
    out dx, al
    
    ; === Debug: Read and print the actual PTE for the kernel entry ===
    ; PML4 is in r15, kernel entry is in r12
    ; We need to walk the page tables to find the PTE
    ; Virtual address breakdown for r12:
    ;   PML4 index = bits 47:39 = (r12 >> 39) & 0x1FF
    ;   PDPT index = bits 38:30 = (r12 >> 30) & 0x1FF
    ;   PD index   = bits 29:21 = (r12 >> 21) & 0x1FF
    ;   PT index   = bits 20:12 = (r12 >> 12) & 0x1FF
    
    ; Get PML4 entry
    mov rax, r12
    shr rax, 39
    and rax, 0x1FF
    shl rax, 3                  ; multiply by 8 (entry size)
    add rax, r15                ; add PML4 base
    mov rbx, [rax]              ; rbx = PML4 entry
    
    ; Get PDPT base from PML4 entry
    mov rax, rbx
    and rax, 0x000FFFFFFFFFF000 ; mask to get physical address
    ; rax = PDPT physical base
    
    ; Get PDPT index
    mov rcx, r12
    shr rcx, 30
    and rcx, 0x1FF
    shl rcx, 3
    add rax, rcx
    mov rbx, [rax]              ; rbx = PDPT entry
    
    ; Get PD base from PDPT entry
    mov rax, rbx
    and rax, 0x000FFFFFFFFFF000
    
    ; Get PD index
    mov rcx, r12
    shr rcx, 21
    and rcx, 0x1FF
    shl rcx, 3
    add rax, rcx
    mov rbx, [rax]              ; rbx = PD entry
    
    ; Get PT base from PD entry
    mov rax, rbx
    and rax, 0x000FFFFFFFFFF000
    
    ; Get PT index
    mov rcx, r12
    shr rcx, 12
    and rcx, 0x1FF
    shl rcx, 3
    add rax, rcx
    mov rbx, [rax]              ; rbx = PT entry (the actual PTE!)
    
    ; Print 'E' then the full 64-bit PTE value
    mov dx, 03F8h
.wait_E:
    add dx, 5
    in al, dx
    test al, 20h
    jz .wait_E
    sub dx, 5
    mov al, 'E'
    out dx, al
    
    ; Print the PTE as 16 hex digits
    mov r10, rbx                ; r10 = PTE value to print
    mov r11d, 16                ; 16 nibbles for 64 bits
.print_pte:
    rol r10, 4                  ; Rotate left to get next nibble
    mov al, r10b
    and al, 0Fh
    add al, '0'
    cmp al, '9'
    jbe .pte_digit_ok
    add al, 7
.pte_digit_ok:
    mov dx, 03FDh
.wait_pte:
    push rax
    in al, dx
    test al, 20h
    pop rax
    jz .wait_pte
    mov dx, 03F8h
    out dx, al
    dec r11d
    jnz .print_pte
    
    ; Print newline
    mov dx, 03FDh
.wait_pte_nl:
    in al, dx
    test al, 20h
    jz .wait_pte_nl
    mov dx, 03F8h
    mov al, 0Dh
    out dx, al
.wait_pte_nl2:
    mov dx, 03FDh
    in al, dx
    test al, 20h
    jz .wait_pte_nl2
    mov dx, 03F8h
    mov al, 0Ah
    out dx, al
    
    ; Restore kernel entry
    pop r12
    
    ; === FINAL TEST: Use CALL instead of JMP ===
    ; If the kernel hangs immediately, the return address won't matter.
    ; But if it does something and crashes, we might see output.
    ; Also, let's write a marker RIGHT BEFORE the jump.
    
    ; Print '*' right before jumping
    mov dx, 03F8h
.wait_star:
    add dx, 5
    in al, dx
    test al, 20h
    jz .wait_star
    sub dx, 5
    mov al, '*'
    out dx, al
    
    ; === FINAL STACK TEST ===
    ; Test that we can push/pop on the current stack
    ; If this works, the stack is accessible
    push rax                    ; Test push
    pop rax                     ; Test pop
    
    ; Write '+' to show stack push/pop worked
    mov dx, 03F8h
.wait_plus:
    add dx, 5
    in al, dx
    test al, 20h
    jz .wait_plus
    sub dx, 5
    mov al, '+'
    out dx, al
    
    ; === DIRECT INSTRUCTION TEST ===
    ; Try to execute the first instruction of the kernel manually
    ; The kernel prologue starts with: 55 = push rbp
    ; Let's simulate this to see if it works
    push rbp                    ; Same as kernel's first instruction
    
    ; Write 'B' to show push rbp worked
    mov dx, 03F8h
.wait_B:
    add dx, 5
    in al, dx
    test al, 20h
    jz .wait_B
    sub dx, 5
    mov al, 'B'
    out dx, al
    
    pop rbp                     ; Restore rbp
    
    ; === PRINT RSP VALUE ===
    ; Print current RSP to see if it's valid
    mov r10, rsp
    mov r11d, 16
.print_rsp:
    rol r10, 4
    mov al, r10b
    and al, 0Fh
    add al, '0'
    cmp al, '9'
    jbe .rsp_digit_ok
    add al, 7
.rsp_digit_ok:
    mov dx, 03FDh
.wait_rsp:
    push rax
    in al, dx
    test al, 20h
    pop rax
    jz .wait_rsp
    mov dx, 03F8h
    out dx, al
    dec r11d
    jnz .print_rsp
    
    ; Newline
    mov dx, 03FDh
.wait_rsp_nl:
    in al, dx
    test al, 20h
    jz .wait_rsp_nl
    mov dx, 03F8h
    mov al, 0Dh
    out dx, al
.wait_rsp_nl2:
    mov dx, 03FDh
    in al, dx
    test al, 20h
    jz .wait_rsp_nl2
    mov dx, 03F8h
    mov al, 0Ah
    out dx, al
    
    ; Write '!' to show we're about to call
    mov dx, 03F8h
.wait_exclaim:
    add dx, 5
    in al, dx
    test al, 20h
    jz .wait_exclaim
    sub dx, 5
    mov al, '!'
    out dx, al
    
    ; === CHECK CR4 FOR SMEP/SMAP ===
    ; SMEP (bit 20) prevents supervisor from executing user-mode pages
    ; SMAP (bit 21) prevents supervisor from accessing user-mode pages
    mov rax, cr4
    mov r10, rax
    
    ; Print 'C' then CR4 value
    mov dx, 03F8h
.wait_C4:
    add dx, 5
    in al, dx
    test al, 20h
    jz .wait_C4
    sub dx, 5
    mov al, 'C'
    out dx, al
    
    ; Print CR4 as 8 hex digits
    mov r11d, 8
.print_cr4:
    rol r10d, 4
    mov al, r10b
    and al, 0Fh
    add al, '0'
    cmp al, '9'
    jbe .cr4_digit_ok
    add al, 7
.cr4_digit_ok:
    mov dx, 03FDh
.wait_cr4:
    push rax
    in al, dx
    test al, 20h
    pop rax
    jz .wait_cr4
    mov dx, 03F8h
    out dx, al
    dec r11d
    jnz .print_cr4
    
    ; Newline
    mov dx, 03FDh
.wait_cr4_nl:
    in al, dx
    test al, 20h
    jz .wait_cr4_nl
    mov dx, 03F8h
    mov al, 0Dh
    out dx, al
.wait_cr4_nl2:
    mov dx, 03FDh
    in al, dx
    test al, 20h
    jz .wait_cr4_nl2
    mov dx, 03F8h
    mov al, 0Ah
    out dx, al
    
    ; === CHECK IF PAGE IS USER-MODE (bit 2 of PTE) ===
    ; If PTE has U bit set AND CR4.SMEP is set, execution will fail
    ; Our PTE was 0x023 = Present | RW | Accessed, NO User bit
    ; So this shouldn't be the issue, but let's verify
    
    ; === TRY DISABLING SMEP/SMAP BEFORE CALL ===
    ; Clear bits 20 and 21 of CR4
    mov rax, cr4
    and rax, ~((1 << 20) | (1 << 21))  ; Clear SMEP and SMAP
    mov cr4, rax
    
    ; Write 'D' to show SMEP/SMAP disabled
    mov dx, 03F8h
.wait_D:
    add dx, 5
    in al, dx
    test al, 20h
    jz .wait_D
    sub dx, 5
    mov al, 'D'
    out dx, al
    
    ; === TRY A DIFFERENT APPROACH: JMP INSTEAD OF CALL ===
    ; Maybe the issue is with the CALL instruction specifically
    ; Let's try: push return_addr, then jmp r12
    ; First write '@' marker
    mov dx, 03F8h
.wait_at:
    add dx, 5
    in al, dx
    test al, 20h
    jz .wait_at
    sub dx, 5
    mov al, '@'
    out dx, al
    
    ; === FLUSH CACHES AND SERIALIZE ===
    ; The kernel was loaded by the bootloader, but the instruction cache
    ; might have stale data. Use wbinvd to write-back and invalidate caches.
    wbinvd
    
    ; Serialize the processor to ensure all previous instructions complete
    ; and instruction fetch uses fresh cache state
    ; Use cpuid as a serializing instruction (it's always available)
    push rax
    push rbx
    push rcx
    push rdx
    xor eax, eax
    cpuid
    pop rdx
    pop rcx
    pop rbx
    pop rax
    
    ; Write 'W' to show cache flush done
    mov dx, 03F8h
.wait_W:
    add dx, 5
    in al, dx
    test al, 20h
    jz .wait_W
    sub dx, 5
    mov al, 'W'
    out dx, al
    
    ; === TRY EXECUTING FIRST FEW INSTRUCTIONS INLINE ===
    ; Instead of jumping, let's manually execute what the kernel prologue does
    ; Prologue: 55 41 57 41 56 41 55 41 54 57 56 53 48 83 EC 48
    ; = push rbp; push r15; push r14; push r13; push r12; push rdi; push rsi; push rbx; sub rsp, 0x48
    
    ; Write 'I' to show we're doing inline test
    mov dx, 03F8h
.wait_I:
    add dx, 5
    in al, dx
    test al, 20h
    jz .wait_I
    sub dx, 5
    mov al, 'I'
    out dx, al
    
    ; Execute the exact same instructions as the kernel prologue
    push rbp                    ; 55
    push r15                    ; 41 57
    push r14                    ; 41 56
    push r13                    ; 41 55
    push r12                    ; 41 54
    push rdi                    ; 57
    push rsi                    ; 56
    push rbx                    ; 53
    sub rsp, 0x48               ; 48 83 EC 48
    
    ; Write 'Y' to show inline prologue succeeded!
    mov dx, 03F8h
.wait_Y:
    add dx, 5
    in al, dx
    test al, 20h
    jz .wait_Y
    sub dx, 5
    mov al, 'Y'
    out dx, al
    
    ; Undo the prologue
    add rsp, 0x48
    pop rbx
    pop rsi
    pop rdi
    pop r12
    pop r13
    pop r14
    pop r15
    pop rbp
    
    ; Write 'Z' to show cleanup succeeded
    mov dx, 03F8h
.wait_Z:
    add dx, 5
    in al, dx
    test al, 20h
    jz .wait_Z
    sub dx, 5
    mov al, 'Z'
    out dx, al
    
    ; Restore RCX = bootInfo for kernel (we clobbered it during page table walk)
    mov rcx, r13
    
    ; === FINAL ATTEMPT: Direct jump ===
    ; Write '>' to show we're doing final jump
    mov dx, 03F8h
.wait_gt:
    add dx, 5
    in al, dx
    test al, 20h
    jz .wait_gt
    sub dx, 5
    mov al, '>'
    out dx, al
    
    ; === CHECK CODE SEGMENT ===
    ; Print CS register to verify we're in a valid code segment
    xor rax, rax
    mov ax, cs
    mov r10, rax
    
    ; Print 'S' then CS value (4 hex digits)
    mov dx, 03F8h
.wait_S2:
    add dx, 5
    in al, dx
    test al, 20h
    jz .wait_S2
    sub dx, 5
    mov al, 'S'
    out dx, al
    
    mov r11d, 4
.print_cs:
    rol r10w, 4
    mov al, r10b
    and al, 0Fh
    add al, '0'
    cmp al, '9'
    jbe .cs_digit_ok
    add al, 7
.cs_digit_ok:
    mov dx, 03FDh
.wait_cs:
    push rax
    in al, dx
    test al, 20h
    pop rax
    jz .wait_cs
    mov dx, 03F8h
    out dx, al
    dec r11d
    jnz .print_cs
    
    ; === VERIFY TARGET ADDRESS ONE MORE TIME ===
    ; Print the actual r12 value we're about to jump to
    mov dx, 03F8h
.wait_T2:
    add dx, 5
    in al, dx
    test al, 20h
    jz .wait_T2
    sub dx, 5
    mov al, 'T'
    out dx, al
    
    mov r10, r12
    mov r11d, 16
.print_target:
    rol r10, 4
    mov al, r10b
    and al, 0Fh
    add al, '0'
    cmp al, '9'
    jbe .target_digit_ok
    add al, 7
.target_digit_ok:
    mov dx, 03FDh
.wait_target:
    push rax
    in al, dx
    test al, 20h
    pop rax
    jz .wait_target
    mov dx, 03F8h
    out dx, al
    dec r11d
    jnz .print_target
    
    ; Newline
    mov dx, 03FDh
.wait_target_nl:
    in al, dx
    test al, 20h
    jz .wait_target_nl
    mov dx, 03F8h
    mov al, 0Dh
    out dx, al
.wait_target_nl2:
    mov dx, 03FDh
    in al, dx
    test al, 20h
    jz .wait_target_nl2
    mov dx, 03F8h
    mov al, 0Ah
    out dx, al
    
    ; Write '!' right before final jump
    mov dx, 03F8h
.wait_final:
    add dx, 5
    in al, dx
    test al, 20h
    jz .wait_final
    sub dx, 5
    mov al, '!'
    out dx, al
    
    ; === ATTEMPT: Copy first few kernel instructions and execute them here ===
    ; This will tell us if there's something wrong with the actual bytes
    ; Read 32 bytes from kernel entry and print them
    mov rsi, r12                ; Source = kernel entry
    
    ; Print 'K' then first 8 bytes of kernel code
    mov dx, 03F8h
.wait_K:
    add dx, 5
    in al, dx
    test al, 20h
    jz .wait_K
    sub dx, 5
    mov al, 'K'
    out dx, al
    
    ; Read first 8 bytes
    mov rax, [rsi]              ; First 8 bytes of kernel
    mov r10, rax
    mov r11d, 16
.print_k8:
    rol r10, 4
    mov al, r10b
    and al, 0Fh
    add al, '0'
    cmp al, '9'
    jbe .k8_digit_ok
    add al, 7
.k8_digit_ok:
    mov dx, 03FDh
.wait_k8:
    push rax
    in al, dx
    test al, 20h
    pop rax
    jz .wait_k8
    mov dx, 03F8h
    out dx, al
    dec r11d
    jnz .print_k8
    
    ; Newline
    mov dx, 03FDh
.wait_k8_nl:
    in al, dx
    test al, 20h
    jz .wait_k8_nl
    mov dx, 03F8h
    mov al, 0Dh
    out dx, al
.wait_k8_nl2:
    mov dx, 03FDh
    in al, dx
    test al, 20h
    jz .wait_k8_nl2
    mov dx, 03F8h
    mov al, 0Ah
    out dx, al
    
    ; === TRY COPYING CODE AND EXECUTING IT ===
    ; Copy first 32 bytes of kernel to stack and execute there
    ; This tests if the problem is the location or the code itself
    
    ; Allocate 64 bytes on stack for code copy
    sub rsp, 64
    
    ; Copy 32 bytes from kernel entry to stack
    mov rsi, r12                ; Source = kernel entry (virtual address)
    mov rdi, rsp                ; Dest = stack
    mov rcx, 32
.copy_loop:
    mov al, [rsi]
    mov [rdi], al
    inc rsi
    inc rdi
    dec rcx
    jnz .copy_loop
    
    ; Write 'X' to show copy done
    mov dx, 03F8h
.wait_X:
    add dx, 5
    in al, dx
    test al, 20h
    jz .wait_X
    sub dx, 5
    mov al, 'X'
    out dx, al
    
    ; DON'T execute copied code - it won't work because of RIP-relative addressing
    ; Just restore stack
    add rsp, 64
    
    ; === FINAL: Try the actual jump ===
    ; Write '#' right before the jump
    mov dx, 03F8h
.wait_hash:
    add dx, 5
    in al, dx
    test al, 20h
    jz .wait_hash
    sub dx, 5
    mov al, '#'
    out dx, al
    
    ; Write newline
    mov dx, 03FDh
.wait_hash_nl:
    in al, dx
    test al, 20h
    jz .wait_hash_nl
    mov dx, 03F8h
    mov al, 0Dh
    out dx, al
.wait_hash_nl2:
    mov dx, 03FDh
    in al, dx
    test al, 20h
    jz .wait_hash_nl2
    mov dx, 03F8h
    mov al, 0Ah
    out dx, al
    
    ; === SIMPLIFIED KERNEL JUMP ===
    ; At this point we've done extensive validation:
    ; - Page tables are set up and verified
    ; - Stack is accessible
    ; - Kernel entry is readable
    ; - All debug markers printed
    ;
    ; Now just do a clean jump to the kernel.
    
    ; Restore RCX with bootInfo (we may have clobbered it)
    mov rcx, r13
    
    ; Write '^' before final jump
    mov dx, 03F8h
.wait_caret:
    add dx, 5
    in al, dx
    test al, 20h
    jz .wait_caret
    sub dx, 5
    mov al, '^'
    out dx, al
    
    ; Write newline
    mov dx, 03FDh
.wait_caret_nl:
    in al, dx
    test al, 20h
    jz .wait_caret_nl
    mov dx, 03F8h
    mov al, 0Dh
    out dx, al
.wait_caret_nl2:
    mov dx, 03FDh
    in al, dx
    test al, 20h
    jz .wait_caret_nl2
    mov dx, 03F8h
    mov al, 0Ah
    out dx, al

    ABI_RSP_CHECKPOINT 'J'       ; RSP immediately before the JMP entry
    
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
    ; If we get here, kernel returned (shouldn't happen)
    mov dx, 03F8h
.wait_ret:
    add dx, 5
    in al, dx
    test al, 20h
    jz .wait_ret
    sub dx, 5
    mov al, 'X'         ; 'X' = kernel returned
    out dx, al
    jmp .hang

    ; --- Breadcrumb: '?' = Jump didn't work (should never reach here) ---
    mov dx, 03F8h
.wait_Q:
    add dx, 5
    in al, dx
    test al, 20h
    jz .wait_Q
    sub dx, 5
    mov al, '?'
    out dx, al

    ; === PANIC: Should never reach here ===
.hang:
    mov dx, 03F8h
.wait_hang:
    add dx, 5
    in al, dx
    test al, 20h
    jz .wait_hang
    sub dx, 5
    mov al, '!'
    out dx, al
    
    hlt
    jmp .hang

; Stub functions for compatibility with trampoline_msvc.cpp interface
; (These are no-ops since the code is already in executable memory)
SetupTrampoline:
    ret

GetTrampolineCodeSize:
    mov rax, 256                ; Return a reasonable size estimate
    ret
