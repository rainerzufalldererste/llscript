#include <Windows.h>
#include <intrin.h>

#include "llshost.h"

__forceinline void SetZero(void *pData, const size_t size)
{
  uint8_t *pData8 = (uint8_t *)(pData);
  size_t i = 0;

  if (size >= sizeof(__m128i))
    for (; i < size - (sizeof(__m128i) - 1); i += sizeof(__m128i))
      _mm_storeu_si128((__m128i *)(pData8 + i), _mm_setzero_si128());

  for (; i < size; i++)
    pData8[i] = 0;
}

#pragma function(memset)
extern void *memset(void *pDst, int data, size_t size)
{
  size_t i = 0;
  uint8_t *const pDst8 = (uint8_t *const)pDst;
  const __m128i data128 = _mm_set1_epi8((char)data);

  if (size >= sizeof(__m128i))
    for (; i < size - (sizeof(__m128i) - 1); i += sizeof(__m128i))
      _mm_storeu_si128((__m128i *)(pDst8 + i), data128);

  for (; i < size; i++)
    pDst8[i] = (uint8_t)data;

  return pDst;
}

#pragma function(memcpy)
extern void *memcpy(void *pDst, const void *pSrc, size_t size)
{
  size_t i = 0;
  const uint8_t *const pSrc8 = (const uint8_t *)pSrc;
  uint8_t *const pDst8 = (uint8_t *)pDst;

  if (size >= sizeof(__m128i))
    for (; i < size - (sizeof(__m128i) - 1); i += sizeof(__m128i))
      _mm_storeu_si128((__m128i *)(pDst8 + i), _mm_loadu_si128((const __m128i *)(pSrc8 + i)));

  for (; i < size; i++)
    pDst8[i] = pSrc8[i];

  return pDst;
}

#pragma comment (linker, "/ENTRY:entry_point")
void entry_point()
{
  int32_t argc = 0;
  const wchar_t **pArgv = CommandLineToArgvW(GetCommandLineW(), &argc);

  HANDLE stdOut = GetStdHandle(STD_OUTPUT_HANDLE);

  if (argc != 2)
  {
    const char out[] = "Invalid Parameter.\n\nUsage: llscript_exec <Filename>\nBuild Time: " __TIMESTAMP__ "\n";
    WriteFile(stdOut, out, sizeof(out), NULL, NULL);
    ExitProcess((UINT)-1);
  }

  uint8_t *pByteCode = NULL;

  // Read Bytecode from file.
  {
    HANDLE file = CreateFileW(pArgv[1], GENERIC_READ, FILE_SHARE_READ, NULL, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, NULL);

    if (file == NULL || file == INVALID_HANDLE_VALUE)
    {
      const char out[] = "Failed to open file.";
      WriteFile(stdOut, out, sizeof(out), NULL, NULL);
      ExitProcess((UINT)-1);
    }

    LARGE_INTEGER fileSize = { 0 };

    if (FALSE == GetFileSizeEx(file, &fileSize))
    {
      const char out[] = "Failed to retrieve file size.";
      WriteFile(stdOut, out, sizeof(out), NULL, NULL);
      ExitProcess((UINT)-1);
    }

    if (fileSize.QuadPart <= 0)
    {
      const char out[] = "Invalid file size.";
      WriteFile(stdOut, out, sizeof(out), NULL, NULL);
      ExitProcess((UINT)-1);
    }

    pByteCode = (uint8_t *)VirtualAlloc(NULL, (size_t)fileSize.QuadPart, MEM_COMMIT, PAGE_READWRITE);

    if (pByteCode == NULL)
    {
      const char out[] = "Memory Allocation Failure.";
      WriteFile(stdOut, out, sizeof(out), NULL, NULL);
      ExitProcess((UINT)-1);
    }

    if (FALSE == ReadFile(file, pByteCode, fileSize.LowPart, NULL, NULL))
    {
      const char out[] = "Failed to read file.";
      WriteFile(stdOut, out, sizeof(out), NULL, NULL);
      ExitProcess((UINT)-1);
    }

    CloseHandle(file);
  }

  llshost_state_t state;
  SetZero(&state, sizeof(state));
  
  state.pCode = pByteCode;

  static uint8_t stack[LLS_DEFUALT_STACK_SIZE];
  SetZero(stack, sizeof(stack));
  state.pStack = stack;
  state.stackSize = sizeof(stack);

  llshost_from_state(&state);

  ExitProcess((UINT)0);
  return;
}
