#include <stdio.h>
#include <stdint.h>

#define WIN32_LEAN_AND_MEAN
#include <windows.h>

const size_t Megabyte = 1024 * 1024;

#pragma warning (push)
#pragma warning (disable: 4152)

int32_t main(const int32_t argc, const char **pArgv)
{
  if (argc != 2)
  {
    fputs("Invalid Parameter.\n\nUsage: runsc <filepath>\n\nrunsc will run arbitrary bytes as code. Handle with care.", stderr);
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

  void *pData = VirtualAlloc(0, (size_t)(fileSize + (Megabyte - 1)) & (Megabyte - 1), MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE);

  if (pData == NULL)
  {
    fputs("Memory Allocation Failure.", stderr);
    return -1;
  }

  if ((size_t)fileSize != fread(pData, 1, (size_t)fileSize, pFile))
  {
    fputs("Failed to read file.", stderr);
    return -1;
  }

  fclose(pFile);

  void (*Function)() = pData;
  Function();
}

#pragma warning (pop)
