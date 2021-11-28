#include <stdio.h>
#include <malloc.h>
#include <string.h>

#include <conio.h>

#include "llshost.h"

#ifdef _WIN32
#include <inttypes.h>
#include <Windows.h>
#include <winnt.h>
#include <DbgHelp.h>

#pragma comment(lib, "Dbghelp.lib")

LONG WINAPI TopLevelExceptionHandler(IN EXCEPTION_POINTERS *pExceptionInfo)
{
  printf("Exception triggered: 0x%" PRIX32 ".\n", pExceptionInfo->ExceptionRecord->ExceptionCode);

  do
  {
    char filename[MAX_PATH] = "llscript_exec.exe"; // Already filled in case `GetModuleFileNameA` fails.
    GetModuleFileNameA(GetCurrentProcess(), filename, sizeof(filename));

    const char dmpFileExtension[] = ".dmp";
    strncat_s(filename, sizeof(filename), dmpFileExtension, sizeof(dmpFileExtension));

    HANDLE fileHandle = CreateFileA(filename, GENERIC_WRITE, FILE_SHARE_WRITE, NULL, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, NULL);

    if (fileHandle == INVALID_HANDLE_VALUE)
      break;

    MINIDUMP_EXCEPTION_INFORMATION exceptionInfo;
    memset(&exceptionInfo, 0, sizeof(exceptionInfo));

    exceptionInfo.ThreadId = exceptionInfo.ThreadId;
    exceptionInfo.ExceptionPointers = exceptionInfo.ExceptionPointers;
    exceptionInfo.ClientPointers = TRUE;

    printf("Attempting to write minidump to '%s'.", filename);

    if (TRUE != MiniDumpWriteDump(GetCurrentProcess(), GetCurrentProcessId(), fileHandle, MiniDumpWithFullMemory | MiniDumpWithThreadInfo, &exceptionInfo, NULL, NULL))
      printf("Failed to write crash dump with error code 0x%" PRIu32 ".\n", GetLastError());

  } while (0);

  fflush(stdout);

  return EXCEPTION_CONTINUE_SEARCH;
}

BOOL WINAPI SignalHandler(DWORD type)
{
  printf("Signal triggered: 0x%" PRIX32 ".\n", type);

  do
  {
    char filename[MAX_PATH] = "llscript_exec.exe"; // Already filled in case `GetModuleFileNameA` fails.
    GetModuleFileNameA(GetCurrentProcess(), filename, sizeof(filename));

    const char dmpFileExtension[] = ".dmp";
    strncat_s(filename, sizeof(filename), dmpFileExtension, sizeof(dmpFileExtension));

    HANDLE fileHandle = CreateFileA(filename, GENERIC_WRITE, FILE_SHARE_WRITE, NULL, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, NULL);

    if (fileHandle == INVALID_HANDLE_VALUE)
      break;

    printf("Attempting to write minidump to '%s'.", filename);

    if (TRUE != MiniDumpWriteDump(GetCurrentProcess(), GetCurrentProcessId(), fileHandle, MiniDumpWithFullMemory | MiniDumpWithThreadInfo, NULL, NULL, NULL))
      printf("Failed to write crash dump with error code 0x%" PRIu32 ".\n", GetLastError());

  } while (0);

  fflush(stdout);

  return TRUE;
}
#endif

int32_t main(const int32_t argc, const char **pArgv)
{
  if (argc != 2)
  {
    puts("Invalid Parameter.\n\nUsage: llscript_exec <Filename>");
    puts("Build Time: " __TIMESTAMP__);
    return -1;
  }

#ifdef _WIN32
  //CoInitialize(NULL);
  //SetUnhandledExceptionFilter(TopLevelExceptionHandler);
  //SetConsoleCtrlHandler(SignalHandler, TRUE);
  //
  //// Prevent anyone in the future to override our unhandled exception filter.
  //do
  //{
  //  HANDLE kernel32 = LoadLibraryA("kernel32.dll");
  //
  //  if (kernel32 == INVALID_HANDLE_VALUE || kernel32 == NULL)
  //    break;
  //
  //   void *pPosition = (void *)GetProcAddress(kernel32, "SetUnhandledExceptionFilter");
  //  
  //  if (pPosition == NULL)
  //    break;
  //
  //  const uint8_t shellcode[] = { 0x33, 0xC0, 0xC2, 0x04, 0x00 }; // xor eax, eax; ret 0x4; -> return 0;
  //  DWORD oldProtect;
  //  
  //  if (FALSE == VirtualProtect(pPosition, sizeof(shellcode), PAGE_EXECUTE_READWRITE, &oldProtect))
  //    break;
  //  
  //  if (FALSE == WriteProcessMemory(GetCurrentProcess(), pPosition, shellcode, sizeof(shellcode), NULL))
  //    break;
  //
  //  if (FALSE == VirtualProtect(pPosition, sizeof(shellcode), oldProtect, NULL))
  //    break;
  //
  //  FreeLibrary(kernel32);
  //} while (0);
#endif

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
