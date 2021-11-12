#include <stdio.h>
#include <malloc.h>
#include <string.h>

#include <conio.h>

#include "llshost.h"

int32_t main(const int32_t argc, const char **pArgv)
{
  if (argc != 2)
  {
    puts("Invalid Parameter.\n\nUsage: llscript_exec <Filename>");
    puts("Build Time: " __TIMESTAMP__);
    return -1;
  }

  uint8_t *pByteCode = NULL;

  // Read Bytecode from file.
  {
    FILE *pFile = fopen(pArgv[1], "rb");

    if (pFile == NULL)
    {
      fputs("Failed to open file.", stderr);
      return -1;
    }

    fseek(pFile, 0, SEEK_END);
    const int64_t fileSize = _ftelli64(pFile);
    fseek(pFile, 0, SEEK_SET);

    if (fileSize <= 0)
    {
      fputs("Invalid File.", stderr);
      return -1;
    }

    pByteCode = (uint8_t *)malloc(fileSize);

    if (pByteCode == NULL)
    {
      fputs("Memory Allocation Failure.", stderr);
      return -1;
    }

    if ((size_t)fileSize != fread(pByteCode, 1, (size_t)fileSize, pFile))
    {
      fputs("Failed to read file.", stderr);
      return -1;
    }

    fclose(pFile);
  }

  llshost_state_t state;
  memset(&state, 0, sizeof(state));
  
  state.pCode = pByteCode;

  static uint8_t stack[LLS_DEFUALT_STACK_SIZE];
  memset(stack, 0, sizeof(stack));
  state.pStack = stack;
  state.stackSize = sizeof(stack);

  llshost_from_state(&state);

  return 0;
}
