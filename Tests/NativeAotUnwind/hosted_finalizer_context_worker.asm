option casemap:none

.code
EXTERN HostedFinalizerFiberBody:PROC

; The fiber start routine records the incoming Windows x64 stack alignment,
; then tail-calls the C++ body without changing its ABI-visible entry frame.
HostedFinalizerFiberEntry PROC
    mov  rax, rsp
    and  eax, 0Fh
    mov  DWORD PTR [rcx], eax
    jmp  HostedFinalizerFiberBody
HostedFinalizerFiberEntry ENDP

END
