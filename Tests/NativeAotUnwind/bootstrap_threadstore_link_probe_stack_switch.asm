option casemap:none

.code

; void guidexos_call_on_stack(void* stackTop, void (*entry)(void*), void* arg)
; Windows x64 ABI: reserve 32-byte shadow space and call with RSP aligned.
guidexos_call_on_stack PROC
    push rbx
    mov  rbx, rsp
    mov  rax, rcx
    and  rax, -16
    sub  rax, 20h
    mov  rsp, rax
    mov  rcx, r8
    call rdx
    mov  rsp, rbx
    pop  rbx
    ret
guidexos_call_on_stack ENDP

END
