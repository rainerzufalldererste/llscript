#include "llshost.h"
#include "llshost_opcodes.h"

#include <stdbool.h>

#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <intrin.h>

#pragma warning (push, 0)
#include <winternl.h>

__forceinline void llshost_EvaluateCode(llshost_state_t *pState)
{
  uint64_t *pStackPtr = pState->pStack;
  lls_code_t *pCodePtr = pState->pCode;

  uint64_t iregister[8];
  double fregister[8];
  bool cmp;

  while (1)
  {
    switch (*pCodePtr)
    {
    case LLS_OP_EXIT:
      return;

    default:
      __debugbreak();
    }
  }
}

__forceinline void llshost_AllocateStack(llshost_state_t *pState)
{
  if (pState->pStack != NULL)
    return;

  if (pState->stackSize == 0)
    pState->stackSize = LLS_DEFUALT_STACK_SIZE;

  {
    // Load Process Environment Block.
    PEB *pProcessEnvironmentBlock = (PEB *)__readgsqword(0x60);

    // `pProcessEnvironmentBlock->Ldr->InMemoryOrderModuleList` contains a double linked list.
    // `Flink` and `Blink` are pointers to the next and previous element.
    //
    // All Windows executables should have the following module order.
    //  1. The module of the current executable.
    //  2. `ntdll.dll` (`%windir%\System32\ntdll.dll`)
    //  3. `kernel32.dll` (`%windir%\System32\kernel32.dll`)
    //
    //  ... followed by other modules.
    //
    // In order to get the `GetProcAddress` function we need to therefore get the third item (`Flink->Flink->Flink`).
    // We use the `CONTAINING_RECORD` macro to retrieve the associated table entry.
    LDR_DATA_TABLE_ENTRY *pKernel32TableEntry = CONTAINING_RECORD(pProcessEnvironmentBlock->Ldr->InMemoryOrderModuleList.Flink->Flink->Flink, LDR_DATA_TABLE_ENTRY, InMemoryOrderLinks);

    // We've ended up at the base address of `kernel32.dll`.
    IMAGE_DOS_HEADER *pDosHeader = (IMAGE_DOS_HEADER *)pKernel32TableEntry->DllBase;

    // In order to get the exported functions we need to go to the NT PE header.
    IMAGE_NT_HEADERS *pNtHeader = (IMAGE_NT_HEADERS *)((uint8_t *)pDosHeader + pDosHeader->e_lfanew);

    // From the NtHeader we can extract the virtual address of the export directory of this module.
    IMAGE_EXPORT_DIRECTORY *pExports = (IMAGE_EXPORT_DIRECTORY *)((uint8_t *)pDosHeader + pNtHeader->OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_EXPORT].VirtualAddress);

    // The exports directory contains both a list of function _names_ of this module and the associated _addresses_ of the functions.
    const int32_t *pNameOffsets = (const int32_t *)((uint8_t *)pDosHeader + pExports->AddressOfNames);

    // We will use this struct to store strings.
    // We are using a struct to make sure strings don't end up in another section of the executable where we wouldn't be able to address them in a different process.
    struct
    {
      uint64_t text0, text1;
    } x;

    // We're now looking for the `GetProcAddress` function. Since there's no other function starting with `GetProcA` we'll just find that instead.
    x.text0 = 0x41636F7250746547; // `GetProcA`

    int32_t i = 0;

    // We're just extracting the first 8 bytes of the strings and compare them to `GetProcA`. We'll find it eventually.
    while (*(uint64_t *)((char *)pDosHeader + pNameOffsets[i]) != x.text0)
      ++i;

    // We have found the index of `GetProcAddress`.

    // Not let's get the function offsets in order to retrieve the location of `GetProcAddress` in memory.
    const int32_t *pFunctionOffsets = (const int32_t *)((uint8_t *)pDosHeader + pExports->AddressOfFunctions);

    typedef FARPROC(*GetProcAddressFunc)(HMODULE, const char *);
    GetProcAddressFunc pGetProcAddress = (GetProcAddressFunc)(const void *)((uint8_t *)pDosHeader + pFunctionOffsets[i]);

    pState->pGetProcAddress = pGetProcAddress;

    // A HMODULE is just a pointer to the base address of a module.
    HMODULE kernel32Dll = (HMODULE)pDosHeader;

    // Get `LoadLibraryA`.
    x.text0 = 0x7262694C64616F4C; // `LoadLibr`
    x.text1 = 0x0000000041797261; // `aryA\0\0\0\0`

    pState->pLoadLibrary = pGetProcAddress(kernel32Dll, (const char *)&x.text0);

    // Get `VirtualAlloc`.
    x.text0 = 0x416C617574726956; // `VirtualA`
    x.text1 = 0x00000000636F6C6C; // `lloc\0\0\0\0`

    typedef LPVOID(*VirtualAllocFunc)(LPVOID lpAddress, SIZE_T dwSize, DWORD flAllocationType, DWORD flProtect);
    VirtualAllocFunc pVirtualAlloc = (VirtualAllocFunc)pGetProcAddress(kernel32Dll, (const char *)&x.text0);

    // Allocate Stack.
    pState->pStack = pVirtualAlloc(NULL, pState->stackSize, MEM_COMMIT, PAGE_EXECUTE_READWRITE);

    // Get `VirtualFree`.
    x.text0 = 0x466C617574726956; // `VirtualF`
    x.text1 = 0x0000000000656572; // `ree\0\0\0\0\0`

    pState->pVirtualFree = pGetProcAddress(kernel32Dll, (const char *)&x.text0);
  }
}

__forceinline void llshost_FreeStack(llshost_state_t *pState)
{
  if (pState->pVirtualFree == NULL || pState->pStack == NULL)
    return;

  typedef BOOL (* VirtualFreeFUNC)(LPVOID lpAddress, SIZE_T dwSize, DWORD  dwFreeType);
  VirtualFreeFUNC pVirtualFree = pState->pVirtualFree;

  pVirtualFree(pState->pStack, pState->stackSize, MEM_RELEASE);
}

__forceinline void llshost_FindCode(llshost_state_t *pState)
{
  uint8_t *pCode = __readgsqword(0); // Replace with `lea (register or whatever pCode ends up in), [rip]`. (for rax: 48 8D 05 00 00 00 00), fill gaps with 0x90 (nop)

  while (pState->pCallFuncShellcode == NULL)
  {
    if (*(uint64_t *)pCode == 0x0F1F840000000000)
      pState->pCallFuncShellcode = pCode + 8;

    ++pCode;
  }

  while (pState->pCode == NULL)
  {
    if (*(uint64_t *)pCode == LLS_CODE_START_PATTERN)
      pState->pCode = pCode + 8;

    ++pCode;
  }
}
#pragma warning (pop)

void llshost_position_independent(const lls_code_t *pCode)
{
  llshost_state_t state;

  for (size_t i = 0; i < sizeof(state); i++)
    ((uint8_t *)&state)[i] = 0;

  state.pCode = pCode;

  llshost_FindCode(&state);
  llshost_AllocateStack(&state);
  llshost_EvaluateCode(&state);
  llshost_FreeStack(&state);
}
