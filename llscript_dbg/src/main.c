#include <stdio.h>
#include <malloc.h>

#include <conio.h>

#include "llshost.h"

uint64_t __lls__call_func(const uint64_t *pStack);

int32_t main(const int32_t argc, const char **pArgv)
{
  if (argc != 2)
  {
    puts("Invalid Parameter.\n\nUsage: llscript_dbg <Filename>");
    return -1;
  }

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

  uint8_t *pByteCode = (uint8_t *)malloc(fileSize);

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

#pragma warning(push)
#pragma warning(disable: 4054)
  llshost(pByteCode, (void *)&__lls__call_func);
#pragma warning(pop)

  puts("\n\nEnd Of Execution.\nPress any key to exit.");
  _getch();

  return 0;
}
