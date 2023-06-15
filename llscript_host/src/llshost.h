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
  const void *pCallFuncShellcode; // not required, will be looked for after the function otherwise.
  llshost_host_function_t *pHostFunctions; // null terminated list of available host functions.
  const lls_code_t *pCode;
  uint64_t registerValues[16];
  uint64_t stackSize;
  uint8_t *pStack;
  void *pLoadLibrary, *pGetProcAddress, *pHeapAlloc, *pHeapRealloc, *pHeapFree, *pHeapDestroy, *pHeapHandle;
#ifdef LS_DBG_MESSAGEBOX
  int32_t (*pMessageBoxA)(const void *hWnd, const char *pText, const char *pCaption, const uint32_t type);
#endif
} llshost_state_t;

// This function will look for `LLS_CODE_START_PATTERN` after the function.
void llshost_position_independent();

// Returns 0 on Error (code pointer is null or stack memory allocation failure).
uint8_t llshost(void *pCodePtr);

// Returns 0 on Error (code pointer is null or stack memory allocation failure).
uint8_t llshost_from_state(llshost_state_t *pState);

#define LLS_CODE_START_PATTERN (0x31719E1203636F37) // this equates to `37 6F 63 03 12 9E 71 31`.
#define LLS_DEFUALT_STACK_SIZE (0x6000) // 24 KB

#endif // llshost_h__
