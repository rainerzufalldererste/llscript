
# llscript
## What is it?
- A basic custom low level scripting language, with a runtime-environment that can be injected as Shellcode.
- The compiled bytecode can simply be appended to the runtime-environment shellcode to be executed when injected.
- Includes a compiler (written in C#), a command line debugger (with custom debug information format) and a shellcode executer.
- Can also be used as a very small, easily integratable, embedded-friendly runtime-environment for scripts that needs low level access.
- Currently only works with Windows x64, but shouldn't be particularly hard to port.

## What can it do?
- Load arbitrary DLLs and extract symbols.
- Call C functions loaded from those DLLs
- Basic programming stuff.

## What can't it do?
- More advanced programming stuff that isn't absolutely essential.
- It can't even do `for` loops, only `while`, no `structs` etc. but the compiler is very hackable.
- Oh, and the shellcode isn't null free. One could hack the compiler to output null free byte code and spend the rest of eternity writing a null free interpreter, but that's not the point of this project at the moment.

## How to use it for shellcode?
### Step 1: Write Your Script
This is the example script. It only opens a `MessageBoxA`. But demonstrates how to load symbols from a DLL.
```c++
const text kernel32dll = "User32.dll"; // `text` maps to `ptr<i8>`.
const text messageBoxA = "MessageBoxA";

// `load_library` is provided by the compiler.
// other builtin functions include `alloc`, `free`, `realloc`, `get_proc_address`.
voidptr kernel32dll_handle = load_library(kernel32dll); // `voidptr` maps to `ptr<void>`.
voidptr messageBoxAAddr = get_proc_address(kernel32dll_handle, messageBoxA);

// this is how casts and pointers to external function work.
extern_func<i32 (const voidptr, const text, const text, u32)> messageBoxAFunc = 
  cast<extern_func<i32 (const voidptr, const text, const text, u32)>>(messageBoxAAddr);

messageBoxAFunc(null, "Hello from the other side!", "Very Important Message", 0);
```

### Step 2: Compile the Script.
```
> llsc example.lls
llsc - LLS Bytecode Compiler (Build Version: 1.0.8566.36389)

Parsing Succeeded. (88 Nodes parsed from 1 Files)

Warning (in 'example.lls', Line 13):
        lvalue call to 'extern_func<i32 (const ptr<void>, const ptr<const i8>, const ptr<const i8>, u32)> messageBoxAFunc' will discard the return value of type 'i32'.

Instruction Generation Succeeded. (69 Instructions & Pseudo-Instructions generated.)
Code Generation Succeeded. (393 Bytes)
Successfully wrote byte code to 'bytecode.lls'.

Compilation Succeeded.
```

### Step 3: Append the Bytecode to the Runtime-Environment Shellcode
- Open the compiler output file `bytecode.lls` and the runtime-environment shellcode `script_host.bin` in a hex editor like [HxD](https://mh-nexus.de/en/hxd/).
- Create a new hex-file and first paste in the contents of `script_host.bin`. This section should end with the magic constant `37 6F 63 03 12 9E 71 31`.
- Now paste in the contents of `bytecode.lls`. These should usually begin with `0E` (which is the op code `LLS_OP_STACK_INC_IMM`).
- Save the file.

### Step 4: Test the shellcode
Use `runsc` to test your shellcode.
```
runsc <YourShellcodeFileName>
```

## How to debug scripts?
Debugging such a hacked runtime environment is obviously not as easy as your normal programming languages, but there's a command line (low level) debugger.

### Step 1: Compile with Debug-Info
```
> llsc example.lls -dbgdb
llsc - LLS Bytecode Compiler (Build Version: 1.0.8566.36389)

Parsing Succeeded. (88 Nodes parsed from 1 Files)

Warning (in 'example.lls', Line 13):
        lvalue call to 'extern_func<i32 (const ptr<void>, const ptr<const i8>, const ptr<const i8>, u32)> messageBoxAFunc' will discard the return value of type 'i32'.

Instruction Generation Succeeded. (69 Instructions & Pseudo-Instructions generated.)
Code Generation Succeeded. (393 Bytes)
Successfully wrote byte code to 'bytecode.lls'.
Successfully wrote debug database to 'bytecode.lls.dbg'.

Compilation Succeeded.
```

### Step 2: Launch the Command Line Debugger
```
> llscript_dbg bytecode.lls bytecode.lls.dbg
llshost byte code interpreter

        'c' to run / continue execution
        'n' to step
        'l' to step a line (only available with debug info)
        'f' to step out
        'b' to set the breakpoint
        'r' for registers
        'p' for stack bytes
        'y' for advanced stack bytes
        'i' to inspect a value
        'm' to modify a value
        'v' show recent values (only available with debug info)
        'o' clear recent values (only available with debug info)
        'w' set value filter (only available with debug info)
        'W' break on a value filter match (only available with debug info)
        'F' continue to next function call/return
        's' toggle silent
        'S' toggle silent comments
        'q' to restart
        'x' to quit
        'z' to debug break


File: example.lls
   1: const text kernel32dll = "User32.dll"; // `text` maps to `ptr<i8>`.
>>
```

Now press <kbd>l</kbd> to step line by line, <kbd>c</kbd> to run.
Recently modified values and associated lines in the script will be displayed above the input line if debug information is available. 

```
kernel32dll @ code base offset 320  (array<i8>) : 0x7FF50BC00140
 --> 85, 115, 101, 114, 51, 50, 46, 100, 108, 108, 0, 77, 101, 115, 115, 97, 103, 101, 66, 111, 120, 65, 0, 72, ...
 --> 0x55, 0x73, 0x65, 0x72, 0x33, 0x32, 0x2E, 0x64, 0x6C, 0x6C, 0x0, 0x4D, 0x65, 0x73, 0x73, 0x61, 0x67, 0x65, 0x42, 0x6F, 0x78, 0x41, 0x0, 0x48, ...
 --> "User32.dll"
messageBoxA @ code base offset 331  (array<i8>) : 0x7FF50BC0014B
 --> 77, 101, 115, 115, 97, 103, 101, 66, 111, 120, 65, 0, 72, 101, 108, 108, 111, 32, 102, 114, 111, 109, 32, 116, ...
 --> 0x4D, 0x65, 0x73, 0x73, 0x61, 0x67, 0x65, 0x42, 0x6F, 0x78, 0x41, 0x0, 0x48, 0x65, 0x6C, 0x6C, 0x6F, 0x20, 0x66, 0x72, 0x6F, 0x6D, 0x20, 0x74, ...
 --> "MessageBoxA"
    6: voidptr kernel32dll_handle = load_library(kernel32dll); // `voidptr` maps to `ptr<void>`.
```

## How to integrate it with another application?
Simply include the corresponding header, link to `script_host.lib` and start the runtime-environment with a pointer to the bytecode.

```c
#include "llshost.h"

llshost_state_t  state = {};
state.pCode = your_compiled_byte_code;

// If you need to pass in additional values, simply set register values:
state.registerValues[0] = (uint64_t)value0;
state.registerValues[1] = (uint64_t)value1;

// Now, start the runtime-environment.
llshost_from_state(&state);
```

## How to build it?
- Clone the repo `git clone https://github.com/rainerzufalldererste/llscript.git`
- Run `create_project.bat`, select `Visual Studio 2015` if you're trying to build the script host for shellcode (everything newer will produce calls to `memcpy` even without the `crt`. If you're just planning to embed the script host in another application, any new Visual Studio version is fine. 

If you just want to play with the debugger or compiler, you can simply use `MSBuild` or `Visual Studio` to build the project solution.

If you want to create a shellcode version of a modified runtime-environment:

- Build `llscript_asm` then `llscript_host` then `llscript_host_bin`. (Visual Studio sometimes gets confused about `llscript_asm`, so not relying on dependencies is probably a good idea)
-   Extract the `.code` section via some dumping tool or simply open the binary in IDA, go to the last statement of the last symbol, and select everything upwards in the hex view, then paste that into a hex editor.
- Now we need to patch the assembly, because getting the current `rip` isn't something that can be expressed in a position independent or overly complicated way in msvc afaik, so we'll need to replace the assembly generated for `uint8_t *pCode = __readgsqword(0);` with `lea <whatever register the compiler chose>, [rip]`.
 - if the register was `rax`, replace `65 48 8B 04 25 00 00 00 00` (`mov rax,qword ptr gs:[0]`) with `48 8D 05 00 00 00 00 90 90`.
 - if the register was `rax`, replace `65 48 8B 0C 25 00 00 00 00` (`mov rcx,qword ptr gs:[0]`) with `48 8D 0D 00 00 00 00 90 90`.
 - if the register was anything else, use the [defuse.ca online x64 assembler](https://defuse.ca/online-x86-assembler.htm) and assemble `lea <whatever register the compiler chose>, [rip]` and replace the corresponding code with whatever that says. Pad with `0x90` (`nop`).
- Lastly, append the magic constant (`37 6F 63 03 12 9E 71 31`) to the shellcode. The runtime-environment will search for this pattern when launched without code to find it's input.

## License
- MIT
