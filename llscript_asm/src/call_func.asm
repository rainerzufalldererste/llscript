public __lls__call_func

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
; Macros

M_JUMP_TO_DO_CALL_IF_LAST MACRO
  ; Load type of param.
  mov rbx, [rax]
  
  ; Move to value.
  sub rax, 8
  
  ; if no param: jump to do_call.
  cmp rbx, 0
  je do_call
ENDM

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
; Code

.code
dq 0F1F840000000000H ; this is a nop statement that will be looked for.

; this actually is:
;  uint64_t __lls__call_func(const uint64_t *pStack);
;
; The required stack layout (in reverse):
;
; PARAMETER:
;
;   Type of Parameter. (8 bytes)
;      if 0: last param, goto `AFTER_PARAMETERS`
;      if 1: an 8 byte integer parameter.
;      else: an 8 byte floating point parameter.
;   
;   Parameter Value. (8 bytes)
;      the value to pass to the function.
;   
;   goto `PARAMETER`.
;
; AFTER_PARAMETERS:
;
;   Type of return value. (8 bytes)
;      if 0: void / uint64_t
;      else: float
;
;   Address of function. (8 bytes)
;      the address of the function that will be called.
;
__lls__call_func:
; Reserve Stack Space.
sub rsp, 8

; Move to the beginning of the stack.
sub rax, 8

; Move script stack ptr to rax.
mov rax, rcx

; Param 1
M_JUMP_TO_DO_CALL_IF_LAST

; set param 1 to rcx.
mov rcx, qword ptr [rax]

; if it's an integer param, we've loaded it to the correct register already. jump to after_param1.
cmp rbx, 1
je after_param1

; it's a float param: set to xmm0.
movsd xmm0, qword ptr [rcx]

after_param1:
; move to next param.
sub rax, 8



; Param 2
M_JUMP_TO_DO_CALL_IF_LAST

; set param 2 to rdx.
mov rdx, qword ptr [rax]

; if it's an integer param, we've loaded it to the correct register already. jump to after_param2.
cmp rbx, 1
je after_param2

; it's a float param: set to xmm1.
movsd xmm1, qword ptr [rdx]

after_param2:
; move to next param.
sub rax, 8



; Param 3
M_JUMP_TO_DO_CALL_IF_LAST

; set param 3 to r8.
mov r8, qword ptr [rax]

; if it's an integer param, we've loaded it to the correct register already. jump to after_param3.
cmp rbx, 1
je after_param3

; it's a float param: set to xmm2.
movsd xmm2, qword ptr [r8]

after_param3:
; move to next param.
sub rax, 8



; Param 4
M_JUMP_TO_DO_CALL_IF_LAST

; set param 4 to r9.
mov r9, qword ptr [rax]

; if it's an integer param, we've loaded it to the correct register already. jump to after_param4.
cmp rbx, 1
je after_param4

; it's a float param: set to xmm2.
movsd xmm3, qword ptr [r9]

after_param4:
; move to next param.
sub rax, 8



; Remaining Params
remaining_params:
M_JUMP_TO_DO_CALL_IF_LAST

; Push all remaining params to the stack.
push [rax]
sub rax, 8
jmp remaining_params


; TODO: Do we need to pop the values from the stack as well?


do_call:
; Let's figure out the return type!

; Get type from stack.
mov rbx, [rax]

; Move to function ptr.
sub rax, 8

; if it returns a float: jump to call_float.
cmp rbx, 1
je call_float

; call int/void func.
call qword ptr [rax]
jmp end_func

call_float:
; call float func.
call qword ptr [rax]

; move result to rax to pretend it's a uint64_t.
movsd qword ptr [rsp], xmm0
mov rax, qword ptr [rsp]



end_func:
; Free Stack Space.
add rsp, 8

; return.
ret

end
