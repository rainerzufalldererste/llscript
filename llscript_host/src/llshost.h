#ifndef llshost_h__
#define llshost_h__

#include <stdint.h>

typedef uint8_t lls_code_t;

typedef struct
{
  const uint64_t functionName; // Can be retrieved using `llshost_GetFunctionName`.
  const void *pFunctionAddr;
} llshost_host_function_t;

inline uint64_t llshost_GetFunctionName(const char *functionName)
{
  uint64_t ret = 0;
  size_t i = 0;

  while (1)
  {
    if (functionName[i] == 0)
      break;
    else
      ret ^= (uint64_t)(functionName[i]) << ((i % 8) * 8);

    ++i;
  }

  return ret;
}

typedef struct
{
  void *pCallFuncShellcode; // not required, will be looked for after the function otherwise.
  llshost_host_function_t *pHostFunctions; // null terminated list of available host functions.
  const lls_code_t *pCode;
  uint64_t stackSize;
  uint64_t *pStack;
  void *pVirtualFree; // should be NULL if you allocated the stack.
  void *pLoadLibrary, *pGetProcAddress, *pHeapAlloc, *pHeapFree, *pHeapDestroy, *pHeapHandle;
} llshost_state_t;

// If no code specified this function will look for `LLS_CODE_START_PATTERN` after the function and 
void llshost_position_independent(const lls_code_t *pCode);

#define LLS_CODE_START_PATTERN (0x31719E1203636F37)
#define LLS_DEFUALT_STACK_SIZE (0x1000000) // 16 MB

#endif // llshost_h__
