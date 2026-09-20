; native_stubs.asm
; Minimal native implementations to satisfy ILCompiler link on Windows.
; NOTE: These are STUBS (many are no-ops) to unblock the build.
; Proper implementations should replace these for a working kernel.

[BITS 64]

default rel

section .text

; Module pointer accessor for UEFI boot
; Returns the address of the __Module symbol (module table)
; This is used by KMain to initialize the NativeAOT runtime
extern __Module
global __modules_a
__modules_a:
    lea rax, [rel __Module]
    ret

; === KERNEL ENTRY WRAPPER ===
; Retained as the native entry symbol used by the NativeAOT link configuration.
; Windows x64 ABI: RCX = bootInfo pointer
; The boot handoff prepares the stack and passes RCX through unchanged.
extern KMain
global KMainWrapper
KMainWrapper:
    ; The boot handoff already supplies the Microsoft x64 ABI frame.
    jmp KMain

; Retained for generated/native startup linkage; diagnostics are intentionally
; emitted by BootConsole and the fault handler instead.
global SerialDebugMarker
SerialDebugMarker:
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

; Return the caller's RSP before the CALL instruction.
global ReadRSP
ReadRSP:
    lea rax, [rsp + 8]
    ret

; Return the caller's return address. This is a cheap code-site breadcrumb.
global ReadCallSite
ReadCallSite:
    mov rax, [rsp]
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

global Load_TR
Load_TR:
    mov ax, cx
    ltr ax
    ret

global Read_TR
Read_TR:
    str ax
    movzx eax, ax
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

; Windows x64 arguments: RCX=RIP, RDX=CS, R8=RFLAGS, R9=RSP,
; [RSP+28h]=SS.  The iretq frame is the only path into CPL3.
global iret_to_user
iret_to_user:
    mov r10, [rsp + 28h]
    cli
    push r10
    push r9
    push r8
    push rdx
    push rcx
    iretq

; Tiny freestanding native payloads used only by the Phase 13 diagnostic.
; They make no managed/runtime/libc calls and use the one int 0x80 ABI.
global GetR3PayloadStart
GetR3PayloadStart:
    lea rax, [rel R3PayloadStart]
    ret
global GetR3PayloadSize
GetR3PayloadSize:
    mov eax, R3PayloadEnd - R3PayloadStart
    ret
global GetR3InvalidPayloadStart
GetR3InvalidPayloadStart:
    lea rax, [rel R3InvalidPayloadStart]
    ret
global GetR3InvalidPayloadSize
GetR3InvalidPayloadSize:
    mov eax, R3InvalidPayloadEnd - R3InvalidPayloadStart
    ret
global GetR3FaultPayloadStart
GetR3FaultPayloadStart:
    lea rax, [rel R3FaultPayloadStart]
    ret
global GetR3FaultPayloadSize
GetR3FaultPayloadSize:
    mov eax, R3FaultPayloadEnd - R3FaultPayloadStart
    ret

; Direct diagnostic fixture continuation. The Phase 13 proof runs before the
; normal desktop loop, so it returns through the original kernel caller rather
; than preemptively resuming the bootstrap context.
section .data align=8
r3_resume_rsp: dq 0

section .text
global GetR3ResumeStack
GetR3ResumeStack:
    mov rax, [rel r3_resume_rsp]
    ret

global GetR3ResumeStub
GetR3ResumeStub:
    lea rax, [rel Ring3ResumeStub]
    ret

global EnterR3AndReturn
EnterR3AndReturn:
    ; RCX = user RIP, RDX = user RSP, R8 = user RFLAGS.
    ; Preserve nonvolatile registers below the caller's return address.
    sub rsp, 80h
    mov [rsp + 00h], rbx
    mov [rsp + 08h], rbp
    mov [rsp + 10h], rsi
    mov [rsp + 18h], rdi
    mov [rsp + 20h], r12
    mov [rsp + 28h], r13
    mov [rsp + 30h], r14
    mov [rsp + 38h], r15
    lea rax, [rsp + 80h]
    mov [rel r3_resume_rsp], rax

    cli
    push qword 0x23
    push rdx
    push r8
    push qword 0x1B
    push rcx
    iretq

global Ring3ResumeStub
Ring3ResumeStub:
    ; RSP points at the caller's original return address.
    mov rbx, [rsp - 80h]
    mov rbp, [rsp - 78h]
    mov rsi, [rsp - 70h]
    mov rdi, [rsp - 68h]
    mov r12, [rsp - 60h]
    mov r13, [rsp - 58h]
    mov r14, [rsp - 50h]
    mov r15, [rsp - 48h]
    ret

%define R3_USER_CODE 0x0000400000000000

R3PayloadStart:
    mov eax, 1                  ; Ping
    int 0x80
    cmp eax, 1                  ; ABI version 1
    jnz .success_fail
    mov eax, 2                  ; Exit(0)
    xor edi, edi
    int 0x80
    ud2
.success_fail:
    mov eax, 2
    mov edi, 1
    int 0x80
    ud2
R3PayloadEnd:

R3InvalidPayloadStart:
    mov eax, 3                  ; ValidateRead(null, 1)
    xor edi, edi
    mov esi, 1
    int 0x80
    mov eax, 3                  ; ValidateRead(kernel pointer, 1)
    mov rdi, 0xFFFF800000000000
    mov esi, 1
    int 0x80
    mov eax, 3                  ; ValidateRead(code end, 2)
    mov rdi, R3_USER_CODE + 0xFFF
    mov esi, 2
    int 0x80
    mov eax, 3                  ; ValidateRead(overflow, 0x100)
    mov rdi, 0x7FFFFFFFFFFFFFF0
    mov rsi, 0x100
    int 0x80
    mov eax, 3                  ; ValidateRead(over maximum)
    mov rdi, R3_USER_CODE
    mov rsi, 0x10001
    int 0x80
    mov eax, 4                  ; ValidateWrite(read-only code, 1)
    mov rdi, R3_USER_CODE
    mov esi, 1
    int 0x80
    mov eax, 99                 ; invalid operation
    int 0x80
    mov eax, 2                  ; Exit(0)
    xor edi, edi
    int 0x80
    ud2
R3InvalidPayloadEnd:

R3FaultPayloadStart:
    mov rax, 0x00007FFF00010000 ; one byte above the mapped user stack
    mov rax, [rax]
    ud2
R3FaultPayloadEnd:

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
; On entry: the per-vector stub has reserved a native-only vector slot at
; [RSP] without clobbering any interrupted register.
;
; IDTStackGeneric layout in C# (with one native-only vector slot):
;   struct IDTStackGeneric {
;       RegistersStack rs;        // 15 * 8 = 120 bytes (rax, rcx, rdx, rbx, rbp, rsi, rdi, r8-r15)
;       ulong errorCode;          // 8 bytes
;       ulong vectorSlot;         // native-only, not part of the C# struct
;       InterruptReturnStack irs; // 5 * 8 = 40 bytes (rip, cs, rflags, rsp, ss) - pushed by CPU
;   }
;
; So we need to push GPRs FIRST (so they're at lowest address), then error code.
; The CPU already pushed the interrupt return frame.
;
global isr_common
isr_common:
    ; Push dummy error code FIRST (it comes AFTER RegistersStack in memory, but we push it first
    ; because stack grows down, so it ends up at higher address)
    ; Actually no - we need the MEMORY LAYOUT to match the struct.
    ; Stack grows DOWN, so what we push LAST is at the LOWEST address.
; C# struct has RegistersStack at offset 0 (lowest), errorCode at offset 120.
; The native-only vector slot is at offset 128 and the CPU frame starts at 136.
    ; 
; The vector slot is above the CPU frame. The CPU frame remains immediately
; below the slot and is either 3 qwords (no error code) or 4 qwords (error).
    ;
    ; We need to build the struct so that when we pass RSP to managed code:
    ;   [RSP+0..119] = RegistersStack (15 regs)
    ;   [RSP+120] = errorCode
;   [RSP+128] = native-only vector slot
;   [RSP+136..175] = InterruptReturnStack (CPU frame)
    ;
    ; The CPU's frame is already at the right place if we push:
    ;   - errorCode (8 bytes)
    ;   - GPRs (120 bytes, pushed in reverse order so first reg is at lowest addr)
    ;
; Final layout:
;   [RSP+0] = rax (first of RegistersStack)
    ;   ...
    ;   [RSP+112] = r15 (last of RegistersStack)  
    ;   [RSP+120] = errorCode
;   [RSP+128] = vectorSlot
;   [RSP+136] = RIP (irs.rip)
;   [RSP+144] = CS
;   [RSP+152] = RFLAGS
;   [RSP+160] = RSP
;   [RSP+168] = SS
    ;
    ; So we push errorCode first (goes above irs), then GPRs (go above errorCode)
; Total pushed by common: 1 (errorCode) + 15 (GPRs) = 128 bytes. The
; per-vector stub's native-only slot is already at the next qword.
    
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
    ; Save RAX last; the vector remains in the native-only slot.
    push rax

    ; Now RSP points to IDTStackGeneric:
    ;   [RSP+0] = rax ... [RSP+112] = r15 (RegistersStack, 120 bytes but r15 is last so +112)
    ; Wait, let me recalculate:
    ;   [RSP+0] = rax, [RSP+8] = rcx, ..., [RSP+112] = r15
    ;   [RSP+120] = errorCode
;   [RSP+128] = vectorSlot (native-only)
;   [RSP+136] = RIP (irs.rip from CPU)
    ;   ...
; That's 15 regs * 8 = 120 bytes for GPRs, + 8 for error code = 128 bytes,
; plus the native-only vector slot and CPU frame.
    
    ; Load vector number from the native-only slot.
    mov ecx, dword [rsp + 128]
    mov rdx, rsp            ; RDX = pointer to IDTStackGeneric (second param)

    ; Align the managed call independently of the interrupted RSP. Reserve
    ; shadow space plus a recovery slot below the saved register frame. A
    ; 0x20-byte allocation would place the recovery slot at [B] when B is
    ; already aligned, overwriting the saved RAX; 0x30 avoids that edge case.
    mov r11, rsp            ; struct base
    and rsp, ~0Fh
    sub rsp, 30h            ; MS x64 shadow space plus recovery slot
    mov [rsp + 20h], r11    ; recovery pointer, below the saved frame

    call intr_handler

    mov rax, [rsp + 20h]    ; Recover struct base
    add rsp, 30h
    mov rsp, rax

    ; A ring-0 return frame does not contain a hardware RSP/SS pair.  The
    ; managed scheduler nevertheless supplies one in its synthetic frame, so
    ; switch the stack explicitly and resume the selected kernel context.
    ; This is required before reclaiming a user process's RSP0 stack.
    mov r11, rsp
    test qword [r11 + 144], 3
    jnz .return_from_interrupt
    mov rax, [r11 + 128]
    mov r10, 052494E473352304Ch
    cmp rax, r10
    jne .return_from_interrupt

    ; Keep the selected frame in R12 while loading the target register set.
    ; Do not place a frame pointer in the target stack: that stack may be the
    ; bootstrap stack captured from an interrupted desktop context.
    mov r12, r11
    mov r10, [r12 + 160]       ; selected kernel RSP
    mov rax, [r12 + 136]       ; selected RIP
    mov [r10 - 8], rax         ; scratch below the return address
    mov rax, [r12 + 152]       ; selected RFLAGS
    push rax
    popfq
    mov rsp, r10
    mov rax, [r12 + 0]
    mov rcx, [r12 + 8]
    mov rdx, [r12 + 16]
    mov rbx, [r12 + 24]
    mov rbp, [r12 + 32]
    mov rsi, [r12 + 40]
    mov rdi, [r12 + 48]
    mov r8,  [r12 + 56]
    mov r9,  [r12 + 64]
    mov r10, [r12 + 72]
    mov r11, [r12 + 80]
    mov r13, [r12 + 96]
    mov r14, [r12 + 104]
    mov r15, [r12 + 112]
    mov r12, [r12 + 88]
    jmp [rsp - 8]

.return_from_interrupt:
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
    
    ; Pop error code and the native-only vector slot
    add rsp, 8
    add rsp, 8

    ; Now RSP points to interrupt return frame: RIP, CS, RFLAGS, RSP, SS
    iretq

; Generate stubs for 0..255. Record the vector in a native-only stack slot
; without clobbering any interrupted general-purpose register.
%macro DEFINE_ISR 1
global isr%1
isr%1:
    sub rsp, 8
    mov dword [rsp], %1
    mov dword [rsp + 4], 0
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
