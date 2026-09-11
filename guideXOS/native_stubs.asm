; native_stubs.asm
; Minimal native implementations to satisfy ILCompiler link on Windows.
; NOTE: These are STUBS (many are no-ops) to unblock the build.
; Proper implementations should replace these for a working kernel.

[BITS 64]

default rel

section .text

; Return the caller-visible RSP without creating a frame.  This is used only
; by bounded ABI diagnostics; keeping the helper frame-less makes the value
; the exact RSP at the managed call site after the helper returns.
global ReadRSP
ReadRSP:
    mov rax, rsp
    ret

; Module pointer accessor for UEFI boot
; Returns the address of the __Module symbol (module table)
; This is used by KMain to initialize the NativeAOT runtime
extern __Module
global __modules_a
__modules_a:
    lea rax, [rel __Module]
    ret

; === KERNEL ENTRY WRAPPER ===
; This is the REAL entry point that the bootloader should call.
; It writes debug markers BEFORE any managed code runs, proving the jump succeeded.
; Then it calls the managed KMain function.
;
; Windows x64 ABI: RCX = bootInfo pointer
; We preserve RCX and pass it to KMain.
extern KMain
global KMainWrapper
KMainWrapper:
    ; === IMMEDIATE SERIAL OUTPUT ===
    ; Write 'WRAP' to COM1 to prove we got here
    ; Save all registers we'll use
    push rcx                ; Save bootInfo pointer (will be restored before jmp)
    push rdx
    push rax
    push r10
    push r11
    
    ; Write 'W'
    mov dx, 0x3F8
    mov al, 'W'
    out dx, al
    
    ; Write 'R'
    mov dx, 0x3FD
.wait_r:
    in al, dx
    test al, 0x20
    jz .wait_r
    mov dx, 0x3F8
    mov al, 'R'
    out dx, al
    
    ; Write 'A'
    mov dx, 0x3FD
.wait_a:
    in al, dx
    test al, 0x20
    jz .wait_a
    mov dx, 0x3F8
    mov al, 'A'
    out dx, al
    
    ; Write 'P'
    mov dx, 0x3FD
.wait_p:
    in al, dx
    test al, 0x20
    jz .wait_p
    mov dx, 0x3F8
    mov al, 'P'
    out dx, al
    
    ; Write '2' to mark this is the NEW code
    mov dx, 0x3FD
.wait_2:
    in al, dx
    test al, 0x20
    jz .wait_2
    mov dx, 0x3F8
    mov al, '2'
    out dx, al
    
    ; Write newline
    mov dx, 0x3FD
.wait_nl1:
    in al, dx
    test al, 0x20
    jz .wait_nl1
    mov dx, 0x3F8
    mov al, 0x0D
    out dx, al
    
    mov dx, 0x3FD
.wait_nl2:
    in al, dx
    test al, 0x20
    jz .wait_nl2
    mov dx, 0x3F8
    mov al, 0x0A
    out dx, al
    
    ; === Print KMain address ===
    ; Write 'J='
    mov dx, 0x3FD
.wait_j:
    in al, dx
    test al, 0x20
    jz .wait_j
    mov dx, 0x3F8
    mov al, 'J'
    out dx, al
    
    mov dx, 0x3FD
.wait_eq:
    in al, dx
    test al, 0x20
    jz .wait_eq
    mov dx, 0x3F8
    mov al, '='
    out dx, al
    
    ; Get KMain address and print it
    lea r10, [rel KMain]
    mov r11d, 8             ; 8 hex digits (low 32 bits)
.print_addr:
    rol r10d, 4
    mov al, r10b
    and al, 0x0F
    add al, '0'
    cmp al, '9'
    jbe .digit_ok
    add al, 7
.digit_ok:
    push rax
    mov dx, 0x3FD
.wait_digit:
    in al, dx
    test al, 0x20
    jz .wait_digit
    pop rax
    mov dx, 0x3F8
    out dx, al
    dec r11d
    jnz .print_addr
    
    ; Newline
    mov dx, 0x3FD
.wait_nl3:
    in al, dx
    test al, 0x20
    jz .wait_nl3
    mov dx, 0x3F8
    mov al, 0x0D
    out dx, al
    
    mov dx, 0x3FD
.wait_nl4:
    in al, dx
    test al, 0x20
    jz .wait_nl4
    mov dx, 0x3F8
    mov al, 0x0A
    out dx, al
    
    ; === Try to read first byte of KMain ===
    lea r10, [rel KMain]
    
    ; Write 'T' before trying to read (Test)
    mov dx, 0x3FD
.wait_t:
    in al, dx
    test al, 0x20
    jz .wait_t
    mov dx, 0x3F8
    mov al, 'T'
    out dx, al
    
    mov al, [r10]           ; Try to read first byte of KMain
    mov r11b, al            ; Save the byte
    
    ; Write 'R' = Read succeeded
    mov dx, 0x3FD
.wait_read:
    in al, dx
    test al, 0x20
    jz .wait_read
    mov dx, 0x3F8
    mov al, 'R'
    out dx, al
    
    ; Print the byte as 2 hex digits
    mov al, r11b
    shr al, 4
    add al, '0'
    cmp al, '9'
    jbe .high_ok
    add al, 7
.high_ok:
    push rax
    mov dx, 0x3FD
.wait_high:
    in al, dx
    test al, 0x20
    jz .wait_high
    pop rax
    mov dx, 0x3F8
    out dx, al
    
    mov al, r11b
    and al, 0x0F
    add al, '0'
    cmp al, '9'
    jbe .low_ok
    add al, 7
.low_ok:
    push rax
    mov dx, 0x3FD
.wait_low:
    in al, dx
    test al, 0x20
    jz .wait_low
    pop rax
    mov dx, 0x3F8
    out dx, al
    
    ; Newline before jump
    mov dx, 0x3FD
.wait_nl5:
    in al, dx
    test al, 0x20
    jz .wait_nl5
    mov dx, 0x3F8
    mov al, 0x0D
    out dx, al
    
    mov dx, 0x3FD
.wait_nl6:
    in al, dx
    test al, 0x20
    jz .wait_nl6
    mov dx, 0x3F8
    mov al, 0x0A
    out dx, al
    
    ; Write 'GO' before jump
    mov dx, 0x3FD
.wait_g:
    in al, dx
    test al, 0x20
    jz .wait_g
    mov dx, 0x3F8
    mov al, 'G'
    out dx, al
    
    mov dx, 0x3FD
.wait_o:
    in al, dx
    test al, 0x20
    jz .wait_o
    mov dx, 0x3F8
    mov al, 'O'
    out dx, al
    
    mov dx, 0x3FD
.wait_nl7:
    in al, dx
    test al, 0x20
    jz .wait_nl7
    mov dx, 0x3F8
    mov al, 0x0D
    out dx, al
    
    mov dx, 0x3FD
.wait_nl8:
    in al, dx
    test al, 0x20
    jz .wait_nl8
    mov dx, 0x3F8
    mov al, 0x0A
    out dx, al
    
    ; Restore registers
    pop r11
    pop r10
    pop rax
    pop rdx
    pop rcx                 ; Restore bootInfo pointer
    
    ; === JUMP TO MANAGED KMain ===
    ; RCX = bootInfo pointer (MS x64 ABI first argument)
    jmp KMain
    
    ; If KMain returns (shouldn't happen), halt
.hang:
    hlt
    jmp .hang

; === BOOT DEBUG HELPER ===
; This is called at the VERY START of KMain to prove we entered the kernel.
; It writes directly to serial port - framebuffer writes removed since we
; don't know the framebuffer address here without the bootInfo parameter.
; SerialDebugMarker() - writes 'K!' to COM1 (0x3F8)
global SerialDebugMarker
SerialDebugMarker:
    push rax
    push rdx
    
    ; Write 'K' to COM1 (0x3F8)
    mov dx, 0x3FD           ; Line status register
.wait_serial:
    in al, dx
    test al, 0x20           ; Check THRE bit
    jz .wait_serial
    mov dx, 0x3F8           ; Data register
    mov al, 'K'
    out dx, al
    
    ; Write '!' to show we're still going
    mov dx, 0x3FD
.wait_serial2:
    in al, dx
    test al, 0x20
    jz .wait_serial2
    mov dx, 0x3F8
    mov al, '!'
    out dx, al
    
    ; NOTE: Framebuffer write removed - we don't have bootInfo here
    ; The managed KMain code will do framebuffer writes after validating bootInfo
    
    pop rdx
    pop rax
    ret

global Hlt
Hlt:
    hlt
    ret

global Cli
Cli:
    cli
    ret

global Sti
Sti:
    sti
    ret

global Nop
Nop:
    nop
    ret

global Rdtsc
Rdtsc:
    rdtsc
    shl rdx, 32
    or rax, rdx
    ret

global ReadCR2
ReadCR2:
    mov rax, cr2
    ret

global ReadCR3
ReadCR3:
    mov rax, cr3
    ret

global WriteCR3
WriteCR3:
    mov cr3, rcx
    ret

global Invlpg
Invlpg:
    invlpg [rcx]
    ret

; Port IO
; Signatures in C#: Out8(ushort port, byte value), etc.
; Windows x64 ABI: RCX=port, RDX=value
; IMPORTANT: Must save RDX before clobbering it with the port!

global Out8
Out8:
    ; RCX = port, RDX = value
    mov eax, edx    ; Save value to EAX FIRST (before we clobber DX)
    mov dx, cx      ; Now move port to DX
    out dx, al      ; Output AL (value) to port DX
    ret

global In8
In8:
    ; RCX = port, returns byte in AL (zero-extended to EAX)
    mov dx, cx
    xor eax, eax
    in al, dx
    ret

global Out16
Out16:
    ; RCX = port, RDX = value (16-bit)
    mov eax, edx    ; Save value to EAX FIRST
    mov dx, cx      ; Now move port to DX
    out dx, ax      ; Output AX (value) to port DX
    ret

global In16
In16:
    ; RCX = port, returns word in AX (zero-extended to EAX)
    mov dx, cx
    xor eax, eax
    in ax, dx
    ret

global Out32
Out32:
    ; RCX = port, RDX = value (32-bit)
    mov eax, edx    ; Save value to EAX FIRST
    mov dx, cx      ; Now move port to DX
    out dx, eax     ; Output EAX (value) to port DX
    ret

global In32
In32:
    ; RCX = port, returns dword in EAX
    mov dx, cx
    in eax, dx
    ret

; rep helpers
; Stosb(void* p, byte value, ulong count) => RCX=p, RDX=value, R8=count

global Stosb
Stosb:
    push rdi
    cld
    mov rdi, rcx
    mov al, dl
    mov rcx, r8
    rep stosb
    pop rdi
    ret

; Stosd(void* p, uint value, ulong count) => RCX=p, EDX=value, R8=count

global Stosd
Stosd:
    push rdi
    cld
    mov rdi, rcx
    mov eax, edx
    mov rcx, r8
    rep stosd
    pop rdi
    ret

; Movsb(void* dest, void* source, ulong count) => RCX=dest, RDX=src, R8=count

global Movsb
Movsb:
    push rdi
    push rsi
    cld
    mov rdi, rcx
    mov rsi, rdx
    mov rcx, r8
    rep movsb
    pop rsi
    pop rdi
    ret

; Movsd(uint* dest, uint* source, ulong count) => RCX=dest, RDX=src, R8=count

global Movsd
Movsd:
    push rdi
    push rsi
    cld
    mov rdi, rcx
    mov rsi, rdx
    mov rcx, r8
    rep movsd
    pop rsi
    pop rdi
    ret

; Descriptor table loaders. Expect pointer refs passed by ref in C#.

global Load_GDT
Load_GDT:
    lgdt [rcx]
    ret

; Reload segment registers after GDT change
; This is REQUIRED after loading a new GDT to actually use the new selectors
; Windows x64 ABI: no parameters
; Uses CS=0x08 (kernel code), DS/ES/SS=0x10 (kernel data)
global Reload_Segments
Reload_Segments:
    ; Reload data segment registers immediately
    mov ax, 0x10            ; Kernel data selector
    mov ds, ax
    mov es, ax
    mov ss, ax
    mov fs, ax
    mov gs, ax
    
    ; Reload CS via far return
    ; Push new CS and return address, then do retfq
    pop rax                 ; Get return address
    push qword 0x08         ; Push new CS (kernel code selector)
    push rax                ; Push return address
    retfq                   ; Far return - loads CS from stack

global Load_IDT
Load_IDT:
    lidt [rcx]
    ret

; ==========================================================
; IDT + Interrupt stubs (minimal)
;
; C# side exports: IDT.intr_handler(int irq, IDTStackGeneric* stack)
; We dispatch with Windows x64 ABI:
;   RCX = irq (vector number)
;   RDX = pointer to a stack struct compatible with IDTStackGeneric
;
; This implementation is intentionally simple:
; - It always presents an errorCode slot (0) to managed code.
; - It saves general purpose registers in the order expected by IDT.RegistersStack.
; - It passes the address of the saved register block as `stack`.
;
; IMPORTANT: This assumes the CPU pushed an interrupt-return frame (RIP,CS,RFLAGS,RSP,SS)
; and we do not modify it except via the managed handler editing values in memory.
;
; This is enough to avoid triple faults and to allow IRQ0 to be handled.
; ==========================================================

extern intr_handler

%macro PUSH_GPRS 0
    ; Order must match IDT.RegistersStack: rax rcx rdx rbx rbp rsi rdi r8 r9 r10 r11 r12 r13 r14 r15
    ; CRITICAL: Must save RBP! The interrupted code might be using it as frame pointer.
    push rax
    push rcx
    push rdx
    push rbx
    push rbp        ; Added RBP - was missing!
    push rsi
    push rdi
    push r8
    push r9
    push r10
    push r11
    push r12
    push r13
    push r14
    push r15
%endmacro

%macro POP_GPRS 0
    pop r15
    pop r14
    pop r13
    pop r12
    pop r11
    pop r10
    pop r9
    pop r8
    pop rdi
    pop rsi
    pop rbp         ; Added RBP - was missing!
    pop rbx
    pop rdx
    pop rcx
    pop rax
%endmacro

; A common ISR entry used by all vectors.
; On entry: AL contains vector number.
;
; IDTStackGeneric layout in C#:
;   struct IDTStackGeneric {
;       RegistersStack rs;        // 15 * 8 = 120 bytes (rax, rcx, rdx, rbx, rbp, rsi, rdi, r8-r15)
;       ulong errorCode;          // 8 bytes
;       InterruptReturnStack irs; // 5 * 8 = 40 bytes (rip, cs, rflags, rsp, ss) - pushed by CPU
;   }
;
; So we need to push GPRs FIRST (so they're at lowest address), then error code.
; The CPU already pushed the interrupt return frame.
;
global isr_common
isr_common:
    ; Save vector number temporarily
    push rax                ; Save vector (in AL, but push full RAX) [will be at highest addr]
    
    ; Push dummy error code FIRST (it comes AFTER RegistersStack in memory, but we push it first
    ; because stack grows down, so it ends up at higher address)
    ; Actually no - we need the MEMORY LAYOUT to match the struct.
    ; Stack grows DOWN, so what we push LAST is at the LOWEST address.
    ; C# struct has RegistersStack at offset 0 (lowest), errorCode at offset 120, irs at offset 128.
    ; So we need: [RSP+0]=GPRs, [RSP+120]=errorCode, [RSP+128]=irs
    ; 
    ; Currently on stack after 'push rax' for vector:
    ;   [RSP+0] = saved vector RAX
    ;   [RSP+8] = RIP (from CPU)
    ;   [RSP+16] = CS
    ;   [RSP+24] = RFLAGS  
    ;   [RSP+32] = RSP
    ;   [RSP+40] = SS
    ;
    ; We need to build the struct so that when we pass RSP to managed code:
    ;   [RSP+0..119] = RegistersStack (15 regs)
    ;   [RSP+120] = errorCode
    ;   [RSP+128..167] = InterruptReturnStack (5 qwords from CPU)
    ;
    ; The CPU's frame is already at the right place if we push:
    ;   - errorCode (8 bytes)
    ;   - GPRs (120 bytes, pushed in reverse order so first reg is at lowest addr)
    ;
    ; Wait, let's think again. After CPU interrupt:
    ;   [RSP] = RIP, [RSP+8] = CS, ... [RSP+32] = SS (the irs)
    ;
    ; We need final layout:
    ;   [RSP+0] = rax (first of RegistersStack)
    ;   ...
    ;   [RSP+112] = r15 (last of RegistersStack)  
    ;   [RSP+120] = errorCode
    ;   [RSP+128] = RIP (irs.rip)
    ;   [RSP+136] = CS
    ;   [RSP+144] = RFLAGS
    ;   [RSP+152] = RSP
    ;   [RSP+160] = SS
    ;
    ; So we push errorCode first (goes above irs), then GPRs (go above errorCode)
    ; Total pushed by us: 1 (errorCode) + 15 (GPRs) = 16 qwords = 128 bytes
    ; Plus 1 for saved vector = 136 bytes from CPU's frame
    
    ; Pop the vector we just saved (we'll save it differently)
    pop rax                 ; Get vector back into AL
    
    ; Now push in correct order to build IDTStackGeneric:
    ; First push dummy error code (will be at offset 120 relative to final RSP)
    push qword 0            ; errorCode placeholder
    
    ; Now push all GPRs in the order that puts rax at lowest address
    ; RegistersStack order: rax, rcx, rdx, rbx, rbp, rsi, rdi, r8-r15
    ; Push in REVERSE order so rax ends up at lowest address
    push r15
    push r14
    push r13
    push r12
    push r11
    push r10
    push r9
    push r8
    push rdi
    push rsi
    push rbp
    push rbx
    push rdx
    push rcx
    ; Save RAX last but we need to preserve the vector first
    movzx ecx, al           ; Save vector in ECX (it was in AL)
    push rax                ; Now push RAX (may have been modified, but we have vector in ECX)

    ; Now RSP points to IDTStackGeneric:
    ;   [RSP+0] = rax ... [RSP+112] = r15 (RegistersStack, 120 bytes but r15 is last so +112)
    ; Wait, let me recalculate:
    ;   [RSP+0] = rax, [RSP+8] = rcx, ..., [RSP+112] = r15
    ;   [RSP+120] = errorCode
    ;   [RSP+128] = RIP (irs.rip from CPU)
    ;   ...
    ; That's 15 regs * 8 = 120 bytes for GPRs, + 8 for error code = 128 bytes we pushed
    ; Plus CPU pushed 40 bytes (5 qwords for irs)
    
    ; RCX already has vector number (first param)
    mov rdx, rsp            ; RDX = pointer to IDTStackGeneric (second param)

    ; Align stack and add shadow space for MS x64 ABI
    sub rsp, 32             ; Shadow space

    call intr_handler

    add rsp, 32             ; Remove shadow space

    ; Restore GPRs (in reverse order of how we pushed them)
    pop rax
    pop rcx
    pop rdx
    pop rbx
    pop rbp
    pop rsi
    pop rdi
    pop r8
    pop r9
    pop r10
    pop r11
    pop r12
    pop r13
    pop r14
    pop r15
    
    ; Pop error code
    add rsp, 8

    ; Now RSP points to interrupt return frame: RIP, CS, RFLAGS, RSP, SS
    iretq

; Generate stubs for 0..255 that load AL=vector and jump to common.
%macro DEFINE_ISR 1
global isr%1
isr%1:
    mov al, %1
    jmp isr_common
%endmacro

%assign __i 0
%rep 256
    DEFINE_ISR __i
%assign __i __i+1
%endrep

; IDT entry builder
; rdi = idt base, esi = vector, rax = handler address
; Gate type: interrupt gate (0x8E), selector 0x08
%macro SET_IDT_ENTRY 0
    ; Compute &idt[vector] without using scale=16 (not supported in x86 addressing)
    mov r11, rsi
    shl r11, 4              ; *16
    add r11, rdi            ; base

    ; offset low
    mov word [r11 + 0], ax
    ; selector
    mov word [r11 + 2], 0x08
    ; reserved0
    mov byte [r11 + 4], 0
    ; type attributes
    mov byte [r11 + 5], 0x8E

    ; offset mid
    shr rax, 16
    mov word [r11 + 6], ax

    ; offset high
    shr rax, 16
    mov dword [r11 + 8], eax

    ; reserved1
    mov dword [r11 + 12], 0
%endmacro

; Replaces the previous stub.
; Signature: set_idt_entries(void* idt)
; Windows x64 ABI: RCX = idt pointer
global set_idt_entries
set_idt_entries:
    push rbx
    push rdi
    push rsi
    push r11

    mov rdi, rcx      ; idt base
    xor esi, esi      ; vector index

.fill_loop:
    lea rbx, [rel isr_table]
    mov rax, [rbx + rsi*8]
    SET_IDT_ENTRY

    inc esi
    cmp esi, 256
    jne .fill_loop

    pop r11
    pop rsi
    pop rdi
    pop rbx
    ret

; Jump table for ISR addresses
align 8
isr_table:
%assign __j 0
%rep 256
    dq isr%+__j
%assign __j __j+1
%endrep

; The following are currently stubbed as no-ops or trivial returns.
; They must be replaced with real implementations for a functional OS.

global enable_sse
enable_sse:
    ; enable SSE: set CR0/CR4 bits minimally
    mov rax, cr0
    and rax, 0xFFFFFFFFFFFFFFFB  ; clear EM
    or  rax, 0x2                 ; set MP
    mov cr0, rax
    mov rax, cr4
    or  rax, (1<<9) | (1<<10)    ; OSFXSR | OSXMMEXCPT
    mov cr4, rax
    ret

global vmware_send
vmware_send:
    xor eax, eax
    ret

global Rdmsr
Rdmsr:
    ; RCX = MSR index
    mov ecx, ecx
    rdmsr
    shl rdx, 32
    or rax, rdx
    ret

global Wrmsr
Wrmsr:
    ; RCX = MSR index, RDX = value
    mov r8, rdx          ; value
    mov ecx, ecx         ; index
    mov eax, r8d         ; low 32
    shr r8, 32
    mov edx, r8d         ; high 32
    wrmsr
    ret

; ----------------------------------------------------------
; Missing symbols required by managed code / runtime glue
; ----------------------------------------------------------

; Unsigned long conversion helper (stub)
; Signature expected by managed: mystrtoul(...)
; Provide a trivial implementation that returns 0.
global mystrtoul
mystrtoul:
    xor eax, eax
    ret

; LodePNG decode entrypoint (stub)
; int lodepng_decode_memory(...)
; Return nonzero to indicate failure (keeps callers from using output buffers).
global lodepng_decode_memory
lodepng_decode_memory:
    mov eax, 1
    ret

; PIO string ops for disk drivers
; Windows x64 ABI: RCX=port, RDX=data ptr, R8=count

global Insw
Insw:
    push rdi
    mov rdi, rdx
    mov rdx, rcx
    mov rcx, r8
    rep insw
    pop rdi
    ret

global Outsw
Outsw:
    push rsi
    mov rsi, rdx
    mov rdx, rcx
    mov rcx, r8
    rep outsw
    pop rsi
    ret

; Scheduler entrypoint used by ThreadPool (stub)
; Real implementation should switch to next thread context.
global Schedule_Next
Schedule_Next:
    ret
