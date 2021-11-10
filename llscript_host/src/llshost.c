#include "llshost.h"
#include "llshost_opcodes.h"
#include "llshost_builtin_func.h"

#include <stdbool.h>

#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <intrin.h>

#pragma warning (push, 0)
#include <winternl.h>

#ifdef LLS_LOW_PERF_MODE
#define ASSERT_NO_ELSE else { __debugbreak(); return; }
#define IF_LAST_OPT(...) if (__VA_ARGS__)
#define ASSERT(x) do { if (!(x)) { __debugbreak(); return; } } while (0)
#else
#define ASSERT_NO_ELSE
#define IF_LAST_OPT(...)
#define ASSERT(x)
#endif

#define LLS_NOT_POSITION_INDEPENDENT

#ifdef LLS_DEBUG_MODE
// Debug Mode Is NOT POSITION INDEPENDENT!
#include <stdio.h>
#include <inttypes.h>
#include <stdint.h>

#include <crtdbg.h>

#define LOG_INSTRUCTION_NAME(x) do { if (!silent) printf("\r% 16" PRIX64 ": " #x " ", (uint64_t)(pCodePtr - 1 - pState->pCode)); } while (0)
#define LOG_ENUM(x) do { if (!silent) printf(#x); } while (0)
#define LOG_REGISTER(x) do { if (!silent) printf("r:%" PRIu8 "", (uint8_t)(x)); } while (0)
#define LOG_U8(x) do { if (!silent) printf("%" PRIu8 "", (uint8_t)(x)); } while (0)
#define LOG_U64(x) do { if (!silent) printf("%" PRIu64 " (0x%" PRIX64 ")", (uint64_t)(x), (uint64_t)(x)); } while (0)
#define LOG_X64(x) do { if (!silent) printf("0x%" PRIX64 "", (uint64_t)(x)); } while (0)
#define LOG_I64(x) do { if (!silent) printf("%" PRIi64 " (0x%" PRIX64 ")", (int64_t)(x), (int64_t)(x)); } while (0)
#define LOG_F64(x) do { if (!silent) printf("%f", (double)(x)); } while (0)
#define LOG_DELIMITER() do { if (!silent) fputs(", ", stdout); } while (0)
#define LOG_DETAILS() do { if (!silent) fputs("\n\t\t// ", stdout); } while (0)
#define LOG_STRING(x) do { if (!silent) fputs((x), stdout); } while (0)
#define LOG_INFO_START() do { if (!silent) fputs(" -> (", stdout); } while (0)
#define LOG_INFO_END() do { if (!silent) fputs(")", stdout); } while (0)
#define LOG_END() do { if (!silent) puts(""); } while (0)

__forceinline void LOG_U64_AS_STRING(uint64_t value)
{
  const char *pData = (const char *)&value;

  for (size_t i = 0; i < 8; i++)
    if (pData[i] > 0x20)
      printf("%c", pData[i]);
    else
      fputs("?", stdout);
}

__forceinline void LOG_U64_AS_BYTES(uint64_t value)
{
  const char *pData = (const char *)&value;

  for (size_t i = 0; i < 8; i++)
    printf("%02" PRIX8 " ", pData[i]);
}

__forceinline void LOG_INSPECT_INTEGER(const uint64_t param, llshost_state_t *pState)
{
  bool possiblePointer = false;

  if ((uint8_t *)param >= pState->pStack && (uint8_t *)param < pState->pStack + pState->stackSize)
  {
    puts("\t\t// \tCould be stack pointer:");
    possiblePointer = true;
  }
  else if (param > 0x00007FF000000000 && param < 0x00007FFFFFFFFFFF)
  {
    puts("\t\t// \tCould be heap pointer.");
  }

  if (possiblePointer)
  {
    const uint64_t value = *(const uint64_t *)param;
    fputs("\t\t// \t", stdout);

    LOG_U64_AS_BYTES(value);

    fputs("... \t", stdout);

    LOG_U64_AS_STRING(value);

    puts(" ...");
  }
}

void *pDebugDatabase = NULL;
HANDLE stdOutHandle = NULL;

typedef struct
{
  uint64_t instruction, startOffset;
} DebugDatabaseEntryHeader;

typedef struct
{
  uint64_t debugDatabaseVersion, entryCount;
  DebugDatabaseEntryHeader entries[];
} DebugDatabaseHeader;

typedef struct
{
  uint64_t codeCount, commentCount, variableLocationCount;
  uint64_t offsets[];
} DebugDatabaseEntry;

typedef enum
{
  DT_Other,
  DT_U8,
  DT_U16,
  DT_U32,
  DT_U64,
  DT_I8,
  DT_I16,
  DT_I32,
  DT_I64,
  DT_F32,
  DT_F64,
  DT_OtherPtr,
  DT_U8Ptr,
  DT_U16Ptr,
  DT_U32Ptr,
  DT_U64Ptr,
  DT_I8Ptr,
  DT_I16Ptr,
  DT_I32Ptr,
  DT_I64Ptr,
  DT_F32Ptr,
  DT_F64Ptr,
  DT_OtherArray,
  DT_U8Array,
  DT_U16Array,
  DT_U32Array,
  DT_U64Array,
  DT_I8Array,
  DT_I16Array,
  DT_I32Array,
  DT_I64Array,
  DT_F32Array,
  DT_F64Array
} DebugDatabaseVariableType;

#pragma pack(1)
typedef struct
{
  uint8_t type;
  bool inRegister;
  uint64_t position;
  char name[];
} DebugDatabaseVariableLocation;

typedef struct
{
  uint64_t age, lastDisplayAge;
  DebugDatabaseVariableLocation *pLocation;
} RecentVariableReference;

typedef enum
{
  CC_Black,
  CC_DarkRed,
  CC_DarkGreen,
  CC_DarkYellow,
  CC_DarkBlue,
  CC_DarkMagenta,
  CC_DarkCyan,
  CC_BrightGray,
  CC_DarkGray,
  CC_BrightRed,
  CC_BrightGreen,
  CC_BrightYellow,
  CC_BrightBlue,
  CC_BrightMagenta,
  CC_BrightCyan,
  CC_White
} ConsoleColour;

inline WORD GetWindowsConsoleColourFromConsoleColour(const ConsoleColour colour)
{
  switch (colour & 0xF)
  {
  default:
  case CC_Black: return 0;
  case CC_DarkBlue: return 1;
  case CC_DarkGreen: return 2;
  case CC_DarkCyan: return 3;
  case CC_DarkRed: return 4;
  case CC_DarkMagenta: return 5;
  case CC_DarkYellow: return 6;
  case CC_BrightGray: return 7;
  case CC_DarkGray: return 8;
  case CC_BrightBlue: return 9;
  case CC_BrightGreen: return 10;
  case CC_BrightCyan: return 11;
  case CC_BrightRed: return 12;
  case CC_BrightMagenta: return 13;
  case CC_BrightYellow: return 14;
  case CC_White: return 15;
  }
}

void SetConsoleColour(const ConsoleColour foregroundColour, const ConsoleColour backgroundColour)
{
  if (stdOutHandle)
  {
    const WORD fgColour = GetWindowsConsoleColourFromConsoleColour(foregroundColour);
    const WORD bgColour = GetWindowsConsoleColourFromConsoleColour(backgroundColour);

    SetConsoleTextAttribute(stdOutHandle, fgColour | (bgColour << 4));
  }
}

void ResetConsoleColour()
{
  if (stdOutHandle)
    SetConsoleTextAttribute(stdOutHandle, GetWindowsConsoleColourFromConsoleColour(CC_BrightGray) | (GetWindowsConsoleColourFromConsoleColour(CC_Black) << 8));
}

bool _IsBadReadPtr(const void *pPtr, const size_t size)
{
  if ((size_t)pPtr >= 0x00007FF000000000 && (size_t)pPtr < 0x00007FFFFFFFFFFF)
    return IsBadReadPtr(pPtr, size);

  return true;
}

void PrintVariableInfo(DebugDatabaseVariableLocation *pVariableInfo, bool isNewReference, const uint8_t *pStack, const uint64_t *pIRegister, const double *pFRegister);

#else
#define LOG_INSTRUCTION_NAME(x)
#define LOG_ENUM(x)
#define LOG_REGISTER(x)
#define LOG_U8(x)
#define LOG_U64(x)
#define LOG_X64(x)
#define LOG_I64(x)
#define LOG_F64(x)
#define LOG_DELIMITER()
#define LOG_DETAILS()
#define LOG_STRING(x)
#define LOG_INFO_START()
#define LOG_INFO_END()
#define LOG_END()
#endif

__forceinline void CopyBytes(void *pTarget, const void *pSource, size_t bytes)
{
  for (int64_t i = (int64_t)bytes - 1; i >= 0; i--)
    ((uint8_t *)pTarget)[i] = ((const uint8_t *)pSource)[i];
}

#define LLS_IREGISTER_COUNT (8)
#define LLS_FREGISTER_COUNT (8)

#if !defined(LLS_DEBUG_MODE) && !defined(LLS_NOT_POSITION_INDEPENDENT)
__forceinline
#endif
void llshost_EvaluateCode(llshost_state_t *pState)
{
  uint8_t *pStack = pState->pStack;
  lls_code_t *pCodePtr = pState->pCode;

  uint64_t iregister[LLS_IREGISTER_COUNT];
  double fregister[LLS_FREGISTER_COUNT];
  bool cmp = false;

  CopyBytes(iregister, pState->registerValues, sizeof(iregister));
  CopyBytes(fregister, pState->registerValues + 8, sizeof(fregister));

#ifdef LLS_DEBUG_MODE
  RecentVariableReference recentValues[6];

  for (size_t i = 0; i < ARRAYSIZE(recentValues); i++)
    recentValues[i].pLocation = NULL;

  bool silent = false;
  bool stepInstructions = true;
  bool stepOut = false;
  bool keepRecentValues = true;
  bool stepByLine = false;
  bool isLineEnd = false;
  uint64_t breakpoint = (uint64_t)-1;

  stdOutHandle = GetStdHandle(STD_OUTPUT_HANDLE);

  if (stdOutHandle == INVALID_HANDLE_VALUE)
    stdOutHandle = NULL;

  SetConsoleColour(CC_DarkGray, CC_Black);

  puts("llshost byte code interpreter.\n\n\t'c' to run / continue execution.\n\t'n' to step.\n\t'l' to step a line (only available with debug info).\n\t'f' to step out.\n\t'b' to set the breakpoint\n\t'r' for registers\n\t'p' for stack bytes\n\t'y' for advanced stack bytes\n\t'i' to inspect a value\n\t'm' to modify a value\n\t's' toggle silent.\n\t'q' to restart.\n\t'x' to quit.\n\t'z' to debug break.\n\n");

  ResetConsoleColour();
#endif

  while (1)
  {
#ifdef LLS_DEBUG_MODE
    const uint64_t address = pCodePtr - pState->pCode;

    DebugDatabaseEntry *pEntry = NULL;
    size_t entryDataOffset = 0;

    isLineEnd = false;

    if (pDebugDatabase)
    {
      DebugDatabaseHeader *pHeader = pDebugDatabase;

      if (pHeader->debugDatabaseVersion <= 0)
      {
        int64_t l = 0;
        int64_t r = pHeader->entryCount - 1;

        while (l <= r)
        {
          const int64_t i = (l + r) / 2;

          if (pHeader->entries[i].instruction < address)
          {
            l = i + 1;
          }
          else if (pHeader->entries[i].instruction > address)
          {
            r = i - 1;
          }
          else
          {
            pEntry = (uint8_t *)pHeader + sizeof(DebugDatabaseHeader) + pHeader->entryCount * sizeof(DebugDatabaseEntryHeader) + pHeader->entries[i].startOffset;
            break;
          }
        }
      }

      if (pEntry)
      {
        fputs("\r", stdout);
        entryDataOffset = (pEntry->codeCount + pEntry->commentCount + pEntry->variableLocationCount) * sizeof(uint64_t) + sizeof(DebugDatabaseEntry);

        if (pEntry->codeCount)
        {
          isLineEnd = true;

          fflush(stdout);
          SetConsoleColour(CC_BrightYellow, CC_Black);

          for (size_t i = 0; i < pEntry->codeCount; i++)
            fputs((char *)pEntry + entryDataOffset + pEntry->offsets[i], stdout);

          ResetConsoleColour();
        }

        if (pEntry->commentCount)
        {
          fflush(stdout);
          SetConsoleColour(CC_DarkGray, CC_Black);

          for (size_t i = 0; i < pEntry->commentCount; i++)
          {
            const char *comment = (const char *)pEntry + entryDataOffset + pEntry->offsets[pEntry->codeCount + i];

            if (comment[0] == '#')
            {
              fputs("\t\t", stdout);
              puts(comment + 1);
            }
            else // Colour Label Descriptors Differently.
            {
              fflush(stdout);
              SetConsoleColour(CC_DarkGreen, CC_Black);

              printf("% 16" PRIX64 ": %s", address, comment);
              
              fflush(stdout);
              SetConsoleColour(CC_DarkGray, CC_Black);
              puts("");
            }
          }

          ResetConsoleColour();
        }
      }
    }

    if (isLineEnd && stepByLine)
    {
      bool hasAny = false;

      for (size_t i = 0; i < ARRAYSIZE(recentValues); i++)
      {
        if (recentValues[i].pLocation == NULL)
          continue;

        hasAny = true;
        break;
      }

      if (hasAny)
      {
        puts(" - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -");

        for (size_t i = 0; i < ARRAYSIZE(recentValues); i++)
        {
          if (recentValues[i].pLocation == NULL)
            continue;

          PrintVariableInfo(recentValues[i].pLocation, recentValues[i].lastDisplayAge != recentValues[i].age, pStack, iregister, fregister);

          recentValues[i].age++;
          recentValues[i].lastDisplayAge = recentValues[i].age;
        }
      }
    }

    if (address == breakpoint)
    {
      printf("\n\tBreakpoint Hit (0x%" PRIX64 ")!\n\n", breakpoint);
      stepInstructions = true;
    }

    if (stepInstructions)
    {
      SetConsoleColour(CC_DarkGray, CC_Black);

      if (stepByLine && !isLineEnd)
        goto continue_execution;

      while (1)
      {
        fputs(">> ", stdout);
        const char c = _getch();

        switch (c)
        {
        case 'c':
          stepInstructions = false;
          goto continue_execution;

        case 'n':
          stepByLine = false;
          goto continue_execution;

        case 'l':
        {
          if (pDebugDatabase)
          {
            stepByLine = true;
            goto continue_execution;
          }
          else
          {
            puts("No Debug Information available.");
          }

          break;
        }

        case 'f':
          stepOut = true;
          stepInstructions = false;
          goto continue_execution;

        case 'b':
          fputs("Set breakpoint to: 0x", stdout);
          scanf("%" PRIX64 "", &breakpoint);
          printf("\nBreakpoint set at 0x%" PRIX64 ".\n", breakpoint);
          break;

        case 'r':
          puts("Registers:");
          for (size_t i = 0; i < 8; i++)
          {
            printf("\t% 3" PRIu64 ": %" PRIu64 " / %" PRIi64 " (0x%" PRIX64 ") \t", i, iregister[i], *(int64_t *)&iregister[i], iregister[i]);
            LOG_U64_AS_STRING(iregister[i]);
            puts("");
            LOG_INSPECT_INTEGER(iregister[i], pState);
          }

          for (size_t i = 0; i < 8; i++)
            printf("\t% 3" PRIu64 ": %d\n", i + 8, fregister[i]);

          printf("\tCMP: %" PRIu8 "\n", cmp);
          printf("\nStack Offset: %" PRIu64 "\n", (size_t)pStack - (size_t)pState->pStack);
          puts("");
          break;

        case 'p':
        {
          const size_t offset = (size_t)pStack - (size_t)pState->pStack;
          printf("Stack Offset: %" PRIu64 "\n", offset);

          uint8_t *pStackInspect = pStack - 64;

          if (pStackInspect < pState->pStack)
            pStackInspect = pState->pStack;

          for (size_t i = 0; i < 64; i += 8)
          {
            if (i >= offset)
              break;

            printf("\n -%02" PRIu64 ": ", (uint64_t)(pStack - (pStackInspect + i)));

            for (size_t j = 0; j < 8; j++)
            {
              if (i + j >= offset)
                fputs("   ", stdout);
              else
                printf("%02" PRIX8 " ", pStackInspect[i + j]);
            }

            fputs("\t", stdout);

            for (size_t j = 0; j < 8; j++)
            {
              if (i + j >= offset)
                break;

              const uint8_t value = pStackInspect[i + j];

              if (value >= 0x20)
                printf("%c", (char)value);
              else
                fputs("?", stdout);
            }
          }

          puts("\n");

          break;
        }

        case 'y':
        {
          size_t offset;
          fputs("Start Offset: ", stdout);
          scanf("%" PRIi64 "", &offset);

          size_t size;
          fputs("\nSize: ", stdout);
          scanf("%" PRIi64 "", &size);

          puts("");

          uint8_t *pStackInspect = pStack - size;

          if (pStackInspect < pState->pStack)
            pStackInspect = pState->pStack;

          for (size_t i = 0; i < size + offset; i += 8)
          {
            printf("\n %02" PRIi64 ": ", -(int64_t)(pStack - (pStackInspect + i)));

            for (size_t j = 0; j < 8; j++)
              printf("%02" PRIX8 " ", pStackInspect[i + j]);

            fputs("\t", stdout);

            for (size_t j = 0; j < 8; j++)
            {
              const uint8_t value = pStackInspect[i + j];

              if (value >= 0x20)
                printf("%c", (char)value);
              else
                fputs("?", stdout);
            }
          }

          puts("\n");

          break;
        }

        case 'i':
        {
          fputs("Start Offset: ", stdout);

          int64_t offset;
          scanf("%" PRIi64 "", &offset);

          uint8_t *pValue = &pStack[-offset];
          uint64_t ivalue = *(uint64_t *)pValue;

          printf("\nValue at Stack Offset %" PRIi64 ":\n", offset);
          printf("\t%" PRIu64 " / %" PRIi64 " (0x%" PRIX64 ") \t", ivalue, *(int64_t *)&ivalue, ivalue);
          LOG_U64_AS_BYTES(ivalue);
          fputs("\t ", stdout);
          LOG_U64_AS_STRING(ivalue);
          puts("");
          LOG_INSPECT_INTEGER(ivalue, pState);
          printf("\t%f / %f\n\n", *(double *)pValue, *(float *)pValue);

          break;
        }

        case 'm':
        {
          puts("[r]egister, [c]mp or [s]tack byte?");
          
          switch (_getch())
          {
          case 'r':
          {
            puts("Register Index:");
            int64_t registerIndex;
            scanf("%" PRIi64 "", &registerIndex);

            if (registerIndex < 8)
            {
              puts("\nValue: (64 bit uppercase hex integer)");

              uint64_t value;
              scanf("%" PRIX64 "", &value);

              iregister[registerIndex] = value;

              puts("\nSuccess!");
            }
            else if (registerIndex < 16)
            {
              puts("\nValue: (double)");

              double value;
              scanf("%f", &value);

              fregister[registerIndex - 8] = value;

              puts("\nSuccess!");
            }
            else
            {
              puts("Invalid Register Index.");
            }

            break;
          }

          case 's':
          {
            puts("\nStack Offset:");
            int64_t stackOffset;
            scanf("%" PRIi64 "", &stackOffset);

            puts("\nValue: (byte)");
            uint8_t value;
            scanf("%" PRIu8 "", &value);

            pStack[-stackOffset] = value;

            puts("\nSuccess!");

            break;
          }

          case 'c':
          {
            puts("\nValue: (byte)");
            uint8_t value;
            scanf("%" PRIu8 "", &value);

            cmp = value;

            puts("\nSuccess!");

            break;
          }

          default:
            puts("\nInvalid Option.");
            break;
          }
        }

        case 's':
          silent = !silent;
          break;

        case 'z':
          __debugbreak();
          break;

        case 'x':
          return;

        case 'q':
          CopyBytes(iregister, pState->registerValues, sizeof(iregister));
          CopyBytes(fregister, pState->registerValues + 8, sizeof(fregister));
          pCodePtr = pState->pCode;
          pStack = pState->pStack;
          break;

        default:;
        }
      }

    continue_execution:
      ;

      ResetConsoleColour();
    }

#endif

    const lls_code_t opcode = *pCodePtr;
    pCodePtr++;

    switch (opcode)
    {
    case LLS_OP_EXIT:
      LOG_INSTRUCTION_NAME(LLS_OP_EXIT);
      LOG_END();

      CopyBytes(pState->registerValues, iregister, sizeof(iregister));
      CopyBytes(pState->registerValues + 8, fregister, sizeof(fregister));

      return;

    case LLS_OP_MOV_IMM_REGISTER:
    {
      LOG_INSTRUCTION_NAME(LLS_OP_MOV_IMM_REGISTER);

      const lls_code_t target_register = *pCodePtr;
      pCodePtr++;

      LOG_REGISTER(target_register);
      LOG_DELIMITER();

      if (target_register < 8)
      {
        iregister[target_register] = *(uint64_t *)pCodePtr;
        LOG_U64(*(uint64_t *)pCodePtr);
        pCodePtr += 8;
      }
      else IF_LAST_OPT(target_register < 16)
      {
        fregister[target_register - 8] = *(double *)pCodePtr;
        LOG_F64(*(double *)pCodePtr);
        pCodePtr += 8;
      }
      ASSERT_NO_ELSE;

      LOG_END();
      break;
    }

    case LLS_OP_MOV_REGISTER_REGISTER:
    {
      LOG_INSTRUCTION_NAME(LLS_OP_MOV_REGISTER_REGISTER);

      const lls_code_t target_register = *pCodePtr;
      pCodePtr++;

      LOG_REGISTER(target_register);
      LOG_DELIMITER();

      const lls_code_t source_register = *pCodePtr;
      pCodePtr++;

      LOG_REGISTER(source_register);
      LOG_END();

      if (target_register < 8)
      {
        if (source_register < 8)
          iregister[target_register] = iregister[source_register];
        else IF_LAST_OPT(source_register < 16)
          ((int64_t *)iregister)[target_register] = (int64_t)fregister[source_register - 8];
        ASSERT_NO_ELSE;
      }
      else IF_LAST_OPT(target_register < 16)
      {
        if (source_register < 8)
          fregister[target_register - 8] = (double)(((int64_t *)iregister)[source_register]);
        else IF_LAST_OPT(source_register < 16)
          fregister[target_register - 8] = fregister[source_register - 8];
        ASSERT_NO_ELSE
      }
      ASSERT_NO_ELSE;

      break;
    }

    case LLS_OP_MOV_REGISTER_STACK:
    {
      LOG_INSTRUCTION_NAME(LLS_OP_MOV_REGISTER_STACK);

      uint64_t *pStackPtr = pStack - *(int64_t *)pCodePtr;
      LOG_I64(*(int64_t *)pCodePtr);
      pCodePtr += 8;

      LOG_DELIMITER();

      const lls_code_t source_register = *pCodePtr;
      pCodePtr++;

      LOG_REGISTER(source_register);
      LOG_END();

      if (source_register < 8)
        *pStackPtr = iregister[source_register];
      else IF_LAST_OPT(source_register < 16)
        *pStackPtr = *(uint64_t *)&fregister[source_register - 8];
      ASSERT_NO_ELSE;

      break;
    }

    case LLS_OP_MOV_REGISTER_STACK_N_BYTES:
    {
      LOG_INSTRUCTION_NAME(LLS_OP_MOV_REGISTER_STACK_N_BYTES);

      const uint8_t *pStackPtr = pStack - *(int64_t *)pCodePtr;
      LOG_I64(*(int64_t *)pCodePtr);
      pCodePtr += 8;

      const lls_code_t source_register = *pCodePtr;
      pCodePtr++;
      const uint8_t bytes = *pCodePtr;
      pCodePtr++;

      LOG_DELIMITER();
      LOG_REGISTER(source_register);
      LOG_DELIMITER();
      LOG_U8(bytes);
      LOG_END();

      if (source_register < 8)
        CopyBytes(pStackPtr, &iregister[source_register], bytes);
      else IF_LAST_OPT(source_register < 16)
        CopyBytes(pStackPtr, &fregister[source_register - 8], bytes);
      ASSERT_NO_ELSE;

      break;
    }

    case LLS_OP_MOV_STACK_REGISTER:
    {
      LOG_INSTRUCTION_NAME(LLS_OP_MOV_STACK_REGISTER);

      const lls_code_t target_register = *pCodePtr;
      pCodePtr++;

      LOG_REGISTER(target_register);
      LOG_DELIMITER();

      const uint8_t *pStackPtr = pStack - *(int64_t *)pCodePtr;
      LOG_I64(*(int64_t *)pCodePtr);
      pCodePtr += 8;

      LOG_END();

      if (target_register < 8)
        iregister[target_register] = *(const uint64_t *)pStackPtr;
      else IF_LAST_OPT(target_register < 16)
        fregister[target_register - 8] = *(const double *)pStackPtr;
      ASSERT_NO_ELSE;

      break;
    }

    case LLS_OP_MOV_STACK_STACK:
    {
      LOG_INSTRUCTION_NAME(LLS_OP_MOV_STACK_STACK);

      uint64_t *pTargetStackPtr = pStack - *(int64_t *)pCodePtr;
      LOG_I64(*(int64_t *)pCodePtr);
      pCodePtr += 8;

      LOG_DELIMITER();

      const uint64_t *pSourceStackPtr = pStack - *(int64_t *)pCodePtr;
      LOG_I64(*(int64_t *)pCodePtr);
      pCodePtr += 8;

      LOG_END();

      *pTargetStackPtr = *pSourceStackPtr;

      break;
    }

    case LLS_OP_MOV_STACK_STACK_N_BYTES:
    {
      LOG_INSTRUCTION_NAME(LLS_OP_MOV_STACK_STACK_N_BYTES);

      uint8_t *pTargetStackPtr = pStack - *(int64_t *)pCodePtr;
      LOG_I64(*(int64_t *)pCodePtr);
      pCodePtr += 8;

      LOG_DELIMITER();

      const uint8_t *pSourceStackPtr = pStack - *(int64_t *)pCodePtr;
      LOG_I64(*(int64_t *)pCodePtr);
      pCodePtr += 8;

      LOG_DELIMITER();

      const uint8_t bytes = *pCodePtr;
      pCodePtr++;
      LOG_U8(bytes);

      LOG_END();

      CopyBytes(pTargetStackPtr, pSourceStackPtr, bytes);

      break;
    }

    case LLS_OP_MOV_REGISTER__PTR_IN_REGISTER:
    {
      LOG_INSTRUCTION_NAME(LLS_OP_MOV_REGISTER__PTR_IN_REGISTER);

      const lls_code_t target_ptr_register = *pCodePtr;
      pCodePtr++;

      LOG_REGISTER(target_ptr_register);
      LOG_INFO_START();
      LOG_U64(iregister[target_ptr_register]);
      LOG_INFO_END();
      LOG_DELIMITER();

      const lls_code_t source_register = *pCodePtr;
      pCodePtr++;

      LOG_U8(source_register);
      LOG_END();

      void *pTarget;

      IF_LAST_OPT(target_ptr_register < 8)
        pTarget = (void *)iregister[target_ptr_register];
      ASSERT_NO_ELSE;

      if (source_register < 8)
        *(uint64_t *)pTarget = iregister[source_register];
      else IF_LAST_OPT(source_register < 16)
        *(double *)pTarget = fregister[source_register - 8];
      ASSERT_NO_ELSE;

      break;
    }

    case LLS_OP_MOV_REGISTER__PTR_IN_REGISTER_N_BYTES:
    {
      LOG_INSTRUCTION_NAME(LLS_OP_MOV_REGISTER__PTR_IN_REGISTER_N_BYTES);

      const lls_code_t target_ptr_register = *pCodePtr;
      pCodePtr++;

      LOG_REGISTER(target_ptr_register);
      LOG_INFO_START();
      LOG_U64(iregister[target_ptr_register]);
      LOG_INFO_END();
      LOG_DELIMITER();

      const lls_code_t source_register = *pCodePtr;
      pCodePtr++;

      LOG_U8(source_register);
      LOG_DELIMITER();

      const uint8_t bytes = *pCodePtr;
      pCodePtr++;

      LOG_U8(bytes);
      LOG_END();

      void *pTarget;

      IF_LAST_OPT(target_ptr_register < 8)
        pTarget = (void *)iregister[target_ptr_register];
      ASSERT_NO_ELSE;

      if (source_register < 8)
        CopyBytes(pTarget, &iregister[source_register], bytes);
      else IF_LAST_OPT(source_register < 16)
        CopyBytes(pTarget, &fregister[source_register - 8], bytes);
      ASSERT_NO_ELSE;

      break;
    }

    case LLS_OP_MOV_PTR_IN_REGISTER__REGISTER:
    {
      LOG_INSTRUCTION_NAME(LLS_OP_MOV_PTR_IN_REGISTER__REGISTER);

      const lls_code_t target_register = *pCodePtr;
      pCodePtr++;

      LOG_REGISTER(target_register);
      LOG_DELIMITER();

      const lls_code_t source_ptr_register = *pCodePtr;
      pCodePtr++;

      LOG_REGISTER(source_ptr_register);
      LOG_INFO_START();
      LOG_U64(iregister[source_ptr_register]);
      LOG_INFO_END();
      LOG_END();

      const void *pDestination;

      IF_LAST_OPT(source_ptr_register < 8)
        pDestination = (void *)iregister[source_ptr_register];
      ASSERT_NO_ELSE;

      if (target_register < 8)
        iregister[target_register] = *(const uint64_t *)pDestination;
      else IF_LAST_OPT(target_register < 16)
        fregister[target_register - 8] = *(const double *)pDestination;
      ASSERT_NO_ELSE;

      break;
    }

    case LLS_OP_LEA_STACK_TO_REGISTER:
    {
      LOG_INSTRUCTION_NAME(LLS_OP_LEA_STACK_TO_REGISTER);

      const lls_code_t target_register = *pCodePtr;
      pCodePtr++;

      LOG_REGISTER(target_register);
      LOG_DELIMITER();

      const uint64_t *pSourceStackPtr = pStack - *(int64_t *)pCodePtr;
      LOG_I64(*(int64_t *)pCodePtr);
      pCodePtr += 8;

      LOG_END();

      IF_LAST_OPT(target_register < 8)
        iregister[target_register] = (uint64_t)pSourceStackPtr;
      ASSERT_NO_ELSE;

      break;
    }

    case LLS_OP_PUSH_REGISTER:
    {
      LOG_INSTRUCTION_NAME(LLS_OP_PUSH_REGISTER);

      const lls_code_t source_register = *pCodePtr;
      pCodePtr++;

      LOG_REGISTER(source_register);
      LOG_END();

      if (source_register < 8)
      {
        *(uint64_t *)pStack = iregister[source_register];
        pStack += 8;
      }
      else IF_LAST_OPT(source_register < 16)
      {
        *(uint64_t *)pStack = *(uint64_t *)&fregister[source_register - 8];
        pStack += 8;
      }
      ASSERT_NO_ELSE;

      break;
    }

    case LLS_OP_POP_REGISTER:
    {
      LOG_INSTRUCTION_NAME(LLS_OP_POP_REGISTER);

      const lls_code_t target_register = *pCodePtr;
      pCodePtr++;

      LOG_REGISTER(target_register);
      LOG_END();

      if (target_register < 8)
      {
        pStack -= 8;
        iregister[target_register] = *(const uint64_t *)pStack;
      }
      else IF_LAST_OPT(target_register < 16)
      {
        pStack -= 8;
        fregister[target_register - 8] = *(const double *)pStack;
      }
      ASSERT_NO_ELSE;

        break;
    }

    case LLS_OP_STACK_INC_IMM:
    {
      LOG_INSTRUCTION_NAME(LLS_OP_STACK_INC_IMM);

      const int64_t imm = *(int64_t *)pCodePtr;
      pCodePtr += sizeof(int64_t);

      LOG_I64(imm);
      LOG_END();

      pStack += imm;

      break;
    }

    case LLS_OP_STACK_INC_REGISTER:
    {
      LOG_INSTRUCTION_NAME(LLS_OP_STACK_INC_REGISTER);

      const lls_code_t target_register = *pCodePtr;
      pCodePtr++;

      LOG_REGISTER(target_register);

      int64_t offset;

      IF_LAST_OPT(target_register < 8)
        offset = iregister[target_register];
      ASSERT_NO_ELSE;

      LOG_INFO_START();
      LOG_I64(offset);
      LOG_INFO_END();
      LOG_END();

      pStack += offset;

      break;
    }

    case LLS_OP_STACK_DEC_IMM:
    {
      LOG_INSTRUCTION_NAME(LLS_OP_STACK_DEC_IMM);

      const int64_t imm = *(int64_t *)pCodePtr;
      pCodePtr += sizeof(int64_t);

      LOG_I64(imm);
      LOG_END();

      pStack -= imm;

      break;
    }

    case LLS_OP_STACK_DEC_REGISTER:
    {
      LOG_INSTRUCTION_NAME(LLS_OP_STACK_DEC_REGISTER);

      const lls_code_t target_register = *pCodePtr;
      pCodePtr++;

      LOG_REGISTER(target_register);

      int64_t offset;

      IF_LAST_OPT(target_register < 8)
        offset = iregister[target_register];
      ASSERT_NO_ELSE;

      LOG_INFO_START();
      LOG_I64(offset);
      LOG_INFO_END();
      LOG_END();

      pStack -= offset;

      break;
    }

    case LLS_OP_ADD_IMM:
    {
      LOG_INSTRUCTION_NAME(LLS_OP_ADD_IMM);

      const lls_code_t target_register = *pCodePtr;
      pCodePtr++;

      LOG_REGISTER(target_register);
      LOG_DELIMITER();

      if (target_register < 8)
      {
        const uint64_t imm = *(uint64_t *)pCodePtr;
        pCodePtr += sizeof(uint64_t);

        LOG_U64(imm);

        iregister[target_register] += imm;
      }
      else IF_LAST_OPT(target_register < 16)
      {
        const double imm = *(double *)pCodePtr;
        pCodePtr += sizeof(double);

        LOG_F64(imm);

        fregister[target_register - 8] += imm;
      }
      ASSERT_NO_ELSE;

      LOG_END();

      break;
    }

    case LLS_OP_ADD_REGISTER:
    {
      LOG_INSTRUCTION_NAME(LLS_OP_ADD_REGISTER);

      const lls_code_t source_register = *pCodePtr;
      pCodePtr++;

      LOG_REGISTER(source_register);
      LOG_DELIMITER();

      const lls_code_t operand_register = *pCodePtr;
      pCodePtr++;

      LOG_REGISTER(operand_register);
      LOG_END();

      if (source_register < 8)
      {
        ASSERT(operand_register < 8);
        iregister[source_register] += iregister[operand_register];
      }
      else IF_LAST_OPT(source_register < 16)
      {
        ASSERT(operand_register >= 8 && operand_register < 16);
        fregister[source_register - 8] += fregister[operand_register - 8];
      }
      ASSERT_NO_ELSE;

      break;
    }

    case LLS_OP_MULI_IMM:
    {
      LOG_INSTRUCTION_NAME(LLS_OP_MULI_IMM);

      const lls_code_t target_register = *pCodePtr;
      pCodePtr++;

      LOG_REGISTER(target_register);
      LOG_DELIMITER();

      if (target_register < 8)
      {
        const int64_t imm = *(int64_t *)pCodePtr;
        pCodePtr += sizeof(int64_t);

        LOG_I64(imm);

        *(int64_t *)(&iregister[target_register]) *= imm;
      }
      else IF_LAST_OPT(target_register < 16)
      {
        const double imm = *(double *)pCodePtr;
        pCodePtr += sizeof(double);

        LOG_F64(imm);

        fregister[target_register - 8] *= imm;
      }
      ASSERT_NO_ELSE;

      LOG_END();

      break;
    }

    case LLS_OP_MULI_REGISTER:
    {
      LOG_INSTRUCTION_NAME(LLS_OP_MULI_REGISTER);

      const lls_code_t source_register = *pCodePtr;
      pCodePtr++;

      LOG_REGISTER(source_register);
      LOG_DELIMITER();

      const lls_code_t operand_register = *pCodePtr;
      pCodePtr++;

      LOG_REGISTER(operand_register);
      LOG_END();

      if (source_register < 8)
      {
        ASSERT(operand_register < 8);
        iregister[source_register] *= iregister[operand_register];
      }
      else IF_LAST_OPT(source_register < 16)
      {
        ASSERT(operand_register >= 8 && operand_register < 16);
        fregister[source_register - 8] *= fregister[operand_register - 8];
      }
      ASSERT_NO_ELSE;

      break;
    }

    case LLS_OP_MULU_IMM:
    {
      LOG_INSTRUCTION_NAME(LLS_OP_MULU_IMM);

      const lls_code_t target_register = *pCodePtr;
      pCodePtr++;

      LOG_REGISTER(target_register);
      LOG_DELIMITER();

      IF_LAST_OPT(target_register < 8)
      {
        const uint64_t imm = *(uint64_t *)pCodePtr;
        pCodePtr += sizeof(uint64_t);

        LOG_U64(imm);

        iregister[target_register] *= imm;
      }
      ASSERT_NO_ELSE;

      LOG_END();

      break;
    }

    case LLS_OP_MULU_REGISTER:
    {
      LOG_INSTRUCTION_NAME(LLS_OP_MULU_REGISTER);

      const lls_code_t source_register = *pCodePtr;
      pCodePtr++;

      LOG_REGISTER(source_register);
      LOG_DELIMITER();

      const lls_code_t operand_register = *pCodePtr;
      pCodePtr++;

      LOG_REGISTER(operand_register);
      LOG_END();

      IF_LAST_OPT(source_register < 8)
      {
        ASSERT(operand_register < 8);
        iregister[source_register] *= iregister[operand_register];
      }
      ASSERT_NO_ELSE;

      break;
    }

    case LLS_OP_AND_IMM:
    {
      LOG_INSTRUCTION_NAME(LLS_OP_AND_IMM);

      const lls_code_t target_register = *pCodePtr;
      pCodePtr++;

      LOG_REGISTER(target_register);
      LOG_DELIMITER();

      IF_LAST_OPT(target_register < 8)
      {
        const uint64_t imm = *(uint64_t *)pCodePtr;
        pCodePtr += sizeof(uint64_t);

        LOG_U64(imm);

        iregister[target_register] &= imm;
      }
      ASSERT_NO_ELSE;

      LOG_END();

      break;
    }

    case LLS_OP_AND_REGISTER:
    {
      LOG_INSTRUCTION_NAME(LLS_OP_AND_REGISTER);

      const lls_code_t source_register = *pCodePtr;
      pCodePtr++;

      LOG_REGISTER(source_register);
      LOG_DELIMITER();

      const lls_code_t operand_register = *pCodePtr;
      pCodePtr++;

      LOG_REGISTER(operand_register);
      LOG_END();

      IF_LAST_OPT(source_register < 8)
      {
        ASSERT(operand_register < 8);
        iregister[source_register] &= iregister[operand_register];
      }
      ASSERT_NO_ELSE;

      break;
    }

    case LLS_OP_OR_REGISTER:
    {
      LOG_INSTRUCTION_NAME(LLS_OP_OR_REGISTER);

      const lls_code_t source_register = *pCodePtr;
      pCodePtr++;

      LOG_REGISTER(source_register);
      LOG_DELIMITER();

      const lls_code_t operand_register = *pCodePtr;
      pCodePtr++;

      LOG_REGISTER(operand_register);
      LOG_END();

      IF_LAST_OPT(source_register < 8)
      {
        ASSERT(operand_register < 8);
        iregister[source_register] |= iregister[operand_register];
      }
      ASSERT_NO_ELSE;

      break;
    }

    case LLS_OP_XOR_REGISTER:
    {
      LOG_INSTRUCTION_NAME(LLS_OP_XOR_REGISTER);

      const lls_code_t source_register = *pCodePtr;
      pCodePtr++;

      LOG_REGISTER(source_register);
      LOG_DELIMITER();

      const lls_code_t operand_register = *pCodePtr;
      pCodePtr++;

      LOG_REGISTER(operand_register);
      LOG_END();

      IF_LAST_OPT(source_register < 8)
      {
        ASSERT(operand_register < 8);
        iregister[source_register] ^= iregister[operand_register];
      }
      ASSERT_NO_ELSE;

      break;
    }

    case LLS_OP_LOGICAL_AND_REGISTER:
    {
      LOG_INSTRUCTION_NAME(LLS_OP_LOGICAL_AND_REGISTER);

      const lls_code_t source_register = *pCodePtr;
      pCodePtr++;

      LOG_REGISTER(source_register);
      LOG_DELIMITER();

      const lls_code_t operand_register = *pCodePtr;
      pCodePtr++;

      LOG_REGISTER(operand_register);
      LOG_END();

      IF_LAST_OPT(source_register < 8)
      {
        ASSERT(operand_register < 8);
        iregister[source_register] = (iregister[source_register] && iregister[operand_register]) ? 1 : 0;
      }
      ASSERT_NO_ELSE;

      break;
    }

    case LLS_OP_LOGICAL_OR_REGISTER:
    {
      LOG_INSTRUCTION_NAME(LLS_OP_LOGICAL_OR_REGISTER);

      const lls_code_t source_register = *pCodePtr;
      pCodePtr++;

      LOG_REGISTER(source_register);
      LOG_DELIMITER();

      const lls_code_t operand_register = *pCodePtr;
      pCodePtr++;

      LOG_REGISTER(operand_register);
      LOG_END();

      IF_LAST_OPT(source_register < 8)
      {
        ASSERT(operand_register < 8);
        iregister[source_register] = (iregister[source_register] || iregister[operand_register]) ? 1 : 0;
      }
      ASSERT_NO_ELSE;

      break;
    }

    case LLS_OP_BSL_REGISTER:
    {
      LOG_INSTRUCTION_NAME(LLS_OP_BSL_REGISTER);

      const lls_code_t source_register = *pCodePtr;
      pCodePtr++;

      LOG_REGISTER(source_register);
      LOG_DELIMITER();

      const lls_code_t operand_register = *pCodePtr;
      pCodePtr++;

      LOG_REGISTER(operand_register);
      LOG_END();

      IF_LAST_OPT(source_register < 8)
      {
        ASSERT(operand_register < 8);
        iregister[source_register] = iregister[source_register] << iregister[operand_register];
      }
      ASSERT_NO_ELSE;

      break;
    }

    case LLS_OP_BSR_REGISTER:
    {
      LOG_INSTRUCTION_NAME(LLS_OP_BSR_REGISTER);

      const lls_code_t source_register = *pCodePtr;
      pCodePtr++;

      LOG_REGISTER(source_register);
      LOG_DELIMITER();

      const lls_code_t operand_register = *pCodePtr;
      pCodePtr++;

      LOG_REGISTER(operand_register);
      LOG_END();

      IF_LAST_OPT(source_register < 8)
      {
        ASSERT(operand_register < 8);
        iregister[source_register] = iregister[source_register] >> iregister[operand_register];
      }
      ASSERT_NO_ELSE;

      break;
    }

    case LLS_OP_NEGATE_REGISTER:
    {
      LOG_INSTRUCTION_NAME(LLS_OP_NEGATE_REGISTER);

      const lls_code_t source_register = *pCodePtr;
      pCodePtr++;

      LOG_REGISTER(source_register);
      LOG_END();

      if (source_register < 8)
        iregister[source_register] = -iregister[source_register];
      else IF_LAST_OPT(source_register < 16)
        fregister[source_register - 8] = -fregister[source_register - 8];
      ASSERT_NO_ELSE;

      break;
    }

    case LLS_OP_INV_REGISTER:
    {
      LOG_INSTRUCTION_NAME(LLS_OP_INV_REGISTER);

      const lls_code_t source_register = *pCodePtr;
      pCodePtr++;

      LOG_REGISTER(source_register);
      LOG_END();

      IF_LAST_OPT(source_register < 8)
        iregister[source_register] = ~iregister[source_register];
      ASSERT_NO_ELSE;

      break;
    }

    case LLS_OP_NOT_REGISTER:
    {
      LOG_INSTRUCTION_NAME(LLS_OP_NOT_REGISTER);

      const lls_code_t source_register = *pCodePtr;
      pCodePtr++;

      LOG_REGISTER(source_register);
      LOG_END();

      IF_LAST_OPT(source_register < 8)
        iregister[source_register] = !iregister[source_register];
      ASSERT_NO_ELSE;

      break;
    }

    case LLS_OP_EQ_REGISTER:
    {
      LOG_INSTRUCTION_NAME(LLS_OP_EQ_REGISTER);

      const lls_code_t source_register = *pCodePtr;
      pCodePtr++;

      LOG_REGISTER(source_register);
      LOG_DELIMITER();

      const lls_code_t operand_register = *pCodePtr;
      pCodePtr++;

      LOG_REGISTER(operand_register);
      LOG_END();

      IF_LAST_OPT(source_register < 8)
      {
        ASSERT(operand_register < 8);
        iregister[source_register] = (iregister[source_register] == iregister[operand_register]);
      }
      ASSERT_NO_ELSE;

      break;
    }

    case LLS_OP_LT_REGISTER:
    {
      LOG_INSTRUCTION_NAME(LLS_OP_LT_REGISTER);

      const lls_code_t source_register = *pCodePtr;
      pCodePtr++;

      LOG_REGISTER(source_register);
      LOG_DELIMITER();

      const lls_code_t operand_register = *pCodePtr;
      pCodePtr++;

      LOG_REGISTER(operand_register);
      LOG_END();

      IF_LAST_OPT(source_register < 8)
      {
        ASSERT(operand_register < 8);
        iregister[source_register] = (iregister[source_register] < iregister[operand_register]);
      }
      ASSERT_NO_ELSE;

      break;
    }

    case LLS_OP_GT_REGISTER:
    {
      LOG_INSTRUCTION_NAME(LLS_OP_GT_REGISTER);

      const lls_code_t source_register = *pCodePtr;
      pCodePtr++;

      LOG_REGISTER(source_register);
      LOG_DELIMITER();

      const lls_code_t operand_register = *pCodePtr;
      pCodePtr++;

      LOG_REGISTER(operand_register);
      LOG_END();

      IF_LAST_OPT(source_register < 8)
      {
        ASSERT(operand_register < 8);
        iregister[source_register] = (iregister[source_register] > iregister[operand_register]);
      }
      ASSERT_NO_ELSE;

      break;
    }

    case LLS_OP_CMP_NEQ_IMM_REGISTER:
    {
      LOG_INSTRUCTION_NAME(LLS_OP_CMP_NEQ_IMM_REGISTER);

      const lls_code_t value_register = *pCodePtr;
      pCodePtr++;

      LOG_REGISTER(value_register);
      LOG_DELIMITER();

      if (value_register < 8)
      {
        const uint64_t value = *(const uint64_t *)pCodePtr;
        pCodePtr += sizeof(uint64_t);
        LOG_U64(value);

        cmp = iregister[value_register] == value;
      }
      else IF_LAST_OPT(value_register < 16)
      {
        const double value = *(const double *)pCodePtr;
        pCodePtr += sizeof(double);
        LOG_F64(value);

        cmp = fregister[value_register - 8] == value;
      }
      ASSERT_NO_ELSE;

      LOG_INFO_START();
      LOG_U8(cmp);
      LOG_INFO_END();

      LOG_END();

      break;
    }

    case LLS_OP_JUMP_CMP_TRUE_RELATIVE_IMM:
    {
      LOG_INSTRUCTION_NAME(LLS_OP_JUMP_CMP_TRUE_RELATIVE_IMM);

      const int64_t value = *(const int64_t *)pCodePtr;
      pCodePtr += sizeof(int64_t);
      LOG_I64(value);
      LOG_INFO_START();
      LOG_U8(cmp);
      LOG_INFO_END();
      LOG_END();

      if (cmp)
        pCodePtr += value;

      break;
    }

    case LLS_OP_JMP_RELATIVE_IMM:
    {
      LOG_INSTRUCTION_NAME(LLS_OP_JMP_RELATIVE_IMM);

      const int64_t value = *(const int64_t *)pCodePtr;
      pCodePtr += sizeof(int64_t);
      LOG_I64(value);
      LOG_END();

      pCodePtr += value;

      break;
    }

    case LLS_OP_CALL_INTERNAL_RELATIVE_IMM:
    {
      LOG_INSTRUCTION_NAME(LLS_OP_CALL_INTERNAL_RELATIVE_IMM);

      const int64_t value = *(const int64_t *)pCodePtr;
      pCodePtr += sizeof(int64_t);

      LOG_I64(value);
      LOG_INFO_START();
      LOG_X64(pCodePtr - pState->pCode);

      ((uint64_t *)pStack)[0] = (uint64_t)pCodePtr;
      pCodePtr += value;

      LOG_STRING(" to ");
      LOG_X64(pCodePtr - pState->pCode);
      LOG_INFO_END();
      LOG_END();

      break;
    }

    case LLS_OP_RETURN_INTERNAL:
    {
      LOG_INSTRUCTION_NAME(LLS_OP_RETURN_INTERNAL);

      LOG_INFO_START();
      LOG_X64(pCodePtr - pState->pCode);

      pCodePtr = (lls_code_t *)(((uint64_t *)pStack)[0]);

      LOG_STRING(" to ");
      LOG_X64(pCodePtr - pState->pCode);
      LOG_INFO_END();
      LOG_END();

#ifdef LLS_DEBUG_MODE
      for (size_t i = 0; i < ARRAYSIZE(recentValues); i++)
        recentValues[i].pLocation = NULL;
#endif

      break;
    }

    case LLS_OP_CALL_EXTERNAL__RESULT_TO_REGISTER:
    {
      uint64_t(*__lls__call_func)(const uint64_t *pStack) = pState->pCallFuncShellcode;

      const lls_code_t target_register = *pCodePtr;
      pCodePtr++;

      LOG_INSTRUCTION_NAME(LLS_OP_CALL_EXTERNAL__RESULT_TO_REGISTER);
      LOG_REGISTER(target_register);

#ifdef LLS_DEBUG_MODE
      if (!silent)
      {
        LOG_DETAILS();
        LOG_STRING("Parameters: (in reverse)\n");
        uint64_t *pFunctionCallParams = pStack;
        pFunctionCallParams--;

        while (true)
        {
          const uint64_t paramType = *pFunctionCallParams;
          pFunctionCallParams--;

          if (paramType == 0)
          {
            puts("\t\t// End Of Parameters");
            break;
          }
          else if (paramType == 1)
          {
            const uint64_t param = *pFunctionCallParams;
            pFunctionCallParams--;

            printf("\t\t// - Integer: %" PRIu64 " / %" PRIi64 " (0x%" PRIX64 ")\n", param, (int64_t)param, param);

            LOG_INSPECT_INTEGER(param, pState);
          }
          else
          {
            const double param = *(const double *)pFunctionCallParams;
            pFunctionCallParams--;

            printf("\t\t// - Float: %f (0x%" PRIX64 ")\n", param, *(const uint64_t *)&param);
          }
        }

        fputs("\t\t// Return Type is ", stdout);

        const uint64_t returnType = *pFunctionCallParams;
        pFunctionCallParams--;

        if (returnType)
          puts("Float");
        else
          puts("Integer / Void");

        const uint64_t functionAddress = *pFunctionCallParams;
        pFunctionCallParams--;

        printf("\t\t// Function Address: 0x%" PRIX64 ".\n", functionAddress);
      }
#endif

      const uint64_t result = __lls__call_func((const uint64_t *)pStack);

      if (target_register < 8)
      {
        iregister[target_register] = result;
#ifdef LLS_DEBUG_MODE
        printf("\t\t// Return Value: %" PRIu64 " / %" PRIi64 " (0x%" PRIX64 ")\n", result, (int64_t)result, result);

        LOG_INSPECT_INTEGER(result, pState);
#endif
      }
      else IF_LAST_OPT(target_register < 16)
      {
        fregister[target_register - 8] = *(double *)&result;
#ifdef LLS_DEBUG_MODE
        printf("\t\t// Return Value: %f (0x%" PRIX64 ")\n", *(double *)result, result);
#endif
      }
      ASSERT_NO_ELSE;

      LOG_END();
      break;
    }

    case LLS_OP_CALL_BUILTIN__RESULT_TO_REGISTER__ID_FROM_REGISTER:
    {
      LOG_INSTRUCTION_NAME(LLS_OP_CALL_BUILTIN__RESULT_TO_REGISTER__ID_FROM_REGISTER);

      const lls_code_t id_register = *pCodePtr;
      pCodePtr++;

      LOG_REGISTER(id_register);
      LOG_DELIMITER();

      const lls_code_t target_register = *pCodePtr;
      pCodePtr++;

      LOG_REGISTER(target_register);

      IF_LAST_OPT(target_register < 8)
      {
        switch (iregister[id_register])
        {
        case LLS_BF_ALLOC:
          LOG_DETAILS();
          LOG_ENUM(LLS_BF_ALLOC);
          LOG_INFO_START();
          LOG_X64(iregister[1]);
          LOG_INFO_END();
          LOG_END();

          IF_LAST_OPT(target_register < 8)
          {
            LPVOID(*HeapAlloc)(HANDLE hHeap, DWORD dwFlags, SIZE_T dwBytes) = pState->pHeapAlloc;

            iregister[target_register] = (uint64_t)HeapAlloc(pState->pHeapHandle, 0, iregister[1]);

            if (iregister[target_register] == 0)
            {
              LOG_DETAILS();
              LOG_STRING("Failed! (Return Value was 0)");
              LOG_END();
            }
          }
          ASSERT_NO_ELSE;
          break;

        case LLS_BF_FREE:
          LOG_DETAILS();
          LOG_ENUM(LLS_BF_FREE);
          LOG_INFO_START();
          LOG_X64(iregister[1]);
          LOG_INFO_END();
          LOG_END();
          {
            BOOL(*HeapFree)(HANDLE hHeap, DWORD dwFlags, _Frees_ptr_opt_ LPVOID lpMem) = pState->pHeapFree;

            HeapFree(pState->pHeapHandle, 0, iregister[1]);

            break;
          }

        case LLS_BF_REALLOC:
          LOG_DETAILS();
          LOG_ENUM(LLS_BF_REALLOC);
          LOG_INFO_START();
          LOG_X64(iregister[1]);
          LOG_DELIMITER();
          LOG_X64(iregister[2]);
          LOG_INFO_END();
          LOG_END();

          IF_LAST_OPT(target_register < 8)
          {
            LPVOID(*HeapReAlloc)(HANDLE hHeap, DWORD dwFlags, _Frees_ptr_opt_ LPVOID lpMem, SIZE_T dwBytes) = pState->pHeapRealloc;

            iregister[target_register] = HeapReAlloc(pState->pHeapHandle, 0, iregister[1], iregister[2]);

            if (iregister[target_register] == 0)
            {
              LOG_DETAILS();
              LOG_STRING("Failed! (Return Value was 0)");
              LOG_END();
            }
          }
          ASSERT_NO_ELSE;
          break;

        case LLS_BF_LOAD_LIBRARY:
          LOG_DETAILS();
          LOG_ENUM(LLS_BF_LOAD_LIBRARY);
          LOG_INFO_START();
          LOG_X64(iregister[1]);
          LOG_INFO_END();
          LOG_END();

          IF_LAST_OPT(target_register < 8)
          {
            HMODULE(*LoadLibraryA)(LPCSTR lpLibFileName) = pState->pLoadLibrary;

            iregister[target_register] = LoadLibraryA(iregister[1]);

            if (iregister[target_register] == 0)
            {
              LOG_DETAILS();
              LOG_STRING("Failed! (Return Value was 0)");
              LOG_END();
            }
          }
          ASSERT_NO_ELSE;
          break;

        case LLS_BF_GET_PROC_ADDRESS:
          LOG_DETAILS();
          LOG_ENUM(LLS_BF_GET_PROC_ADDRESS);
          LOG_INFO_START();
          LOG_X64(iregister[1]);
          LOG_DELIMITER();
          LOG_X64(iregister[2]);
          LOG_INFO_END();
          LOG_END();

          IF_LAST_OPT(target_register < 8)
          {
            FARPROC(*GetProcAddress)(HMODULE hModule, LPCSTR lpProcName) = pState->pGetProcAddress;

            iregister[target_register] = GetProcAddress(iregister[1], iregister[2]);

            if (iregister[target_register] == 0)
            {
              LOG_DETAILS();
              LOG_STRING("Failed! (Return Value was 0)");
              LOG_END();
            }
          }
          ASSERT_NO_ELSE;
          break;

        default:
          LOG_DETAILS();
          LOG_ENUM(INVALID_BUILTIN_FUNCTION);
          LOG_END();
          __debugbreak();
        }
      }

      break;
    }

    default:
      LOG_INSTRUCTION_NAME(INVALID_INSTRUCTION);
      LOG_INFO_START();
      LOG_U8(*(pCodePtr - 1));
      LOG_INFO_END();
      LOG_END();
      __debugbreak();
    }

#ifdef LLS_DEBUG_MODE

    if (keepRecentValues && !stepByLine)
    {
      bool hasAny = false;

      for (size_t i = 0; i < ARRAYSIZE(recentValues); i++)
      {
        if (recentValues[i].pLocation == NULL)
          continue;

        hasAny = true;
        break;
      }

      if (hasAny)
      {
        puts(" - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -");

        for (size_t i = 0; i < ARRAYSIZE(recentValues); i++)
        {
          if (recentValues[i].pLocation == NULL)
            continue;

          PrintVariableInfo(recentValues[i].pLocation, false, pStack, iregister, fregister);

          recentValues[i].age++;
          recentValues[i].lastDisplayAge = recentValues[i].age;
        }
      }
    }

    if (pEntry && pEntry->variableLocationCount)
    {
      ResetConsoleColour();

      if (!stepByLine)
        puts(" - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -");

      if (pEntry->variableLocationCount)
      {
        for (size_t i = 0; i < pEntry->variableLocationCount; i++)
        {
          DebugDatabaseVariableLocation *pVariableInfo = (uint8_t *)pEntry + entryDataOffset + pEntry->offsets[pEntry->codeCount + pEntry->commentCount + i];

          if (!stepByLine)
            PrintVariableInfo(pVariableInfo, true, pStack, iregister, fregister);

          size_t replaceByMatch = (size_t)-1;
          size_t replaceByAge = (size_t)-1;
          size_t oldestAge = 0;

          for (size_t j = 0; j < ARRAYSIZE(recentValues); j++)
          {
            if (recentValues[j].pLocation == NULL)
            {
              replaceByAge = j;
              oldestAge = (size_t)-1;
            }
            else
            {
              if (recentValues[j].pLocation->inRegister && pVariableInfo->inRegister && recentValues[j].pLocation->position == pVariableInfo->position)
              {
                recentValues[j].pLocation = NULL;
                replaceByAge = j;
                oldestAge = (size_t)-1;
              }
              else
              {
                if (strcmp(recentValues[j].pLocation->name, pVariableInfo->name) == 0)
                {
                  replaceByMatch = j;
                }
                else if (recentValues[j].age > oldestAge)
                {
                  replaceByAge = j;
                  oldestAge = recentValues[j].age;
                }
              }
            }
          }

          const size_t replaceIndex = replaceByMatch != (size_t)-1 ? replaceByMatch : replaceByAge;
          
          recentValues[replaceIndex].pLocation = pVariableInfo;
          recentValues[replaceIndex].age = 0;
          recentValues[replaceIndex].lastDisplayAge = (size_t)-1;
        }

        ResetConsoleColour();
      }
    }
  
#endif
  }
}

#ifndef _DEBUG
__forceinline
#endif
void llshost_Setup(llshost_state_t *pState)
{
  if (pState->pStack == NULL && pState->stackSize == 0)
    pState->stackSize = LLS_DEFUALT_STACK_SIZE;

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

  // Get `GetProcessHeap`.
  x.text0 = 0x65636F7250746547; // `GetProce`
  x.text1 = 0x0000706165487373; // `ssHeap\0\0`

  typedef HANDLE(*GetProcessHeapFunc)();
  GetProcessHeapFunc pGetProcessHeap = (GetProcessHeapFunc)pGetProcAddress(kernel32Dll, (const char *)&x.text0);

  if (pState->pStack == NULL)
  {
    pState->pHeapHandle = pGetProcessHeap();

    if (pState->pHeapHandle == NULL)
    {
      // Get `HeapCreate`.
      x.text0 = 0x6165724370616548; // `HeapCrea`
      x.text1 = 0x0000000000006574; // `te\0\0\0\0\0\0`

      typedef HANDLE(*HeapCreateFunc)(DWORD flOptions, SIZE_T dwInitialSize, SIZE_T dwMaximumSize);
      HeapCreateFunc pHeapCreate = (HeapCreateFunc)pGetProcAddress(kernel32Dll, (const char *)&x.text0);

      pState->pHeapHandle = pHeapCreate(0, 0, 0);

      // Get `HeapDestroy`.
      x.text0 = 0x7473654470616548; // `HeapDest`
      x.text1 = 0x0000000000796F72; // `roy\0\0\0\0\0`

      pState->pHeapDestroy = pGetProcAddress(kernel32Dll, (const char *)&x.text0);
    }

    // Get `HeapAlloc`.
    x.text0 = 0x6F6C6C4170616548; // `HeapAllo`
    x.text1 = 0x0000000000000063; // `c\0\0\0\0\0\0\0`

    typedef LPVOID(*HeapAllocFunc)(HANDLE hHeap, DWORD dwFlags, SIZE_T dwBytes);
    HeapAllocFunc pHeapAlloc = pGetProcAddress(kernel32Dll, (const char *)&x.text0);;

    pState->pHeapAlloc = pHeapAlloc;

    // Get `HeapFree`.
    x.text0 = 0x6565724670616548; // `HeapFree`
    x.text1 = 0x0000000000000000; // `\0\0\0\0\0\0\0\0`

    pState->pHeapFree = pGetProcAddress(kernel32Dll, (const char *)&x.text0);

    // Get `HeapReAlloc`.
    x.text0 = 0x6C41655270616548; // `HeapReAl`
    x.text1 = 0x0000000000636F6C; // `loc\0\0\0\0\0`

    pState->pHeapRealloc = pGetProcAddress(kernel32Dll, (const char *)&x.text0);

    // Allocate Stack.
    pState->pStack = pHeapAlloc(pState->pHeapHandle, 0, pState->stackSize);
  }
}

__forceinline void llshost_Cleanup(llshost_state_t *pState)
{
  if (pState->pHeapHandle == NULL)
    return;

  if (pState->pHeapFree != NULL && pState->pStack != NULL)
  {
    typedef BOOL (*HeapFreeFunc)(HANDLE hHeap, DWORD dwFlags, _Frees_ptr_opt_ LPVOID lpMem);
    HeapFreeFunc pHeapFree = pState->pHeapFree;

    pHeapFree(pState->pHeapHandle, 0, pState->pStack);
  }

  if (pState->pHeapDestroy != NULL)
  {
    typedef BOOL (*HeapDestroyFunc)(HANDLE hHeap);
    HeapDestroyFunc pHeapDestroy = pState->pHeapDestroy;

    pHeapDestroy(pState->pHeapHandle);
  }
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

void llshost_position_independent()
{
  llshost_state_t state;

  for (size_t i = 0; i < sizeof(state); i++)
    ((uint8_t *)&state)[i] = 0;

  llshost_FindCode(&state);
  llshost_Setup(&state);
  llshost_EvaluateCode(&state);
  llshost_Cleanup(&state);
}


uint64_t __lls__call_func(const uint64_t *pStack);

uint8_t llshost(void *pCodePtr)
{
  llshost_state_t state;

  for (size_t i = 0; i < sizeof(state); i++)
    ((uint8_t *)&state)[i] = 0;

  state.pCode = pCodePtr;
#pragma warning(push)
#pragma warning(disable: 4054)
  state.pCallFuncShellcode = (const void *)__lls__call_func;
#pragma warning(pop)

  if (state.pStack == NULL || state.pCode == NULL)
    return 0;

  llshost_Setup(&state);

  if (state.pStack == NULL || state.pCode == NULL)
    return 0;

  llshost_EvaluateCode(&state);
  llshost_Cleanup(&state);

  return 1;
}

uint8_t llshost_from_state(llshost_state_t *pState)
{
  if (pState->pCallFuncShellcode == NULL)
#pragma warning(push)
#pragma warning(disable: 4054)
    pState->pCallFuncShellcode = (const void *)__lls__call_func;
#pragma warning(pop)

  llshost_Setup(pState);

  if (pState->pStack == NULL || pState->pCode == NULL)
    return 0;

  llshost_EvaluateCode(pState);
  llshost_Cleanup(pState);

  return 1;
}

#ifdef LLS_DEBUG_MODE

void PrintVariableInfo(DebugDatabaseVariableLocation *pVariableInfo, bool isNewReference, const uint8_t *pStack, const uint64_t *pIRegister, const double *pFRegister)
{
  fflush(stdout);
  SetConsoleColour(isNewReference ? CC_BrightCyan : CC_BrightBlue, CC_Black);

  fputs(pVariableInfo->name, stdout);

  fflush(stdout);
  SetConsoleColour(isNewReference ? CC_DarkCyan : CC_DarkBlue, CC_Black);

  if (pVariableInfo->inRegister)
    printf(" @ register %" PRIu64 " ", pVariableInfo->position);
  else
    printf(" @ stack offset %" PRIi64 " ", (int64_t)pVariableInfo->position);

  switch (pVariableInfo->type)
  {
  case DT_U8:
  {
    uint8_t value = pVariableInfo->inRegister ? (uint8_t)pIRegister[pVariableInfo->position] : *(uint8_t *)(pStack - pVariableInfo->position);
    printf(" (u8) : %" PRIu8 " / %02" PRIX8 "\n", value, value);
    break;
  }

  case DT_I8:
  {
    int8_t value = pVariableInfo->inRegister ? (int8_t)pIRegister[pVariableInfo->position] : *(int8_t *)(pStack - pVariableInfo->position);
    printf(" (i8) : %" PRIi8 " / %02" PRIX8 "\n", value, value);
    break;
  }

  case DT_U16:
  {
    uint16_t value = pVariableInfo->inRegister ? (uint16_t)pIRegister[pVariableInfo->position] : *(uint16_t *)(pStack - pVariableInfo->position);
    printf(" (u16) : %" PRIu16 " / %04" PRIX16 "\n", value, value);
    break;
  }

  case DT_I16:
  {
    int16_t value = pVariableInfo->inRegister ? (int16_t)pIRegister[pVariableInfo->position] : *(int16_t *)(pStack - pVariableInfo->position);
    printf(" (i16) : %" PRIi16 " / %04" PRIX16 "\n", value, value);
    break;
  }

  case DT_U32:
  {
    uint32_t value = pVariableInfo->inRegister ? (uint32_t)pIRegister[pVariableInfo->position] : *(uint32_t *)(pStack - pVariableInfo->position);
    printf(" (u32) : %" PRIu32 " / %08" PRIX32 "\n", value, value);
    break;
  }

  case DT_I32:
  {
    int32_t value = pVariableInfo->inRegister ? (int32_t)pIRegister[pVariableInfo->position] : *(int32_t *)(pStack - pVariableInfo->position);
    printf(" (i32) : %" PRIi32 " / %08" PRIX32 "\n", value, value);
    break;
  }

  case DT_U64:
  {
    uint64_t value = pVariableInfo->inRegister ? (uint64_t)pIRegister[pVariableInfo->position] : *(uint64_t *)(pStack - pVariableInfo->position);
    printf(" (u64) : %" PRIu64 " / %016" PRIX64 "\n", value, value);
    break;
  }

  case DT_I64:
  {
    int64_t value = pVariableInfo->inRegister ? (int64_t)pIRegister[pVariableInfo->position] : *(int64_t *)(pStack - pVariableInfo->position);
    printf(" (i64) : %" PRIi64 " / %016" PRIX64 "\n", value, value);
    break;
  }

  case DT_F32:
  {
    float value = pVariableInfo->inRegister ? (float)pFRegister[pVariableInfo->position - LLS_IREGISTER_COUNT] : *(float *)(pStack - pVariableInfo->position);
    printf(" (f32) : %e / %f\n", value, value);
    break;
  }

  case DT_F64:
  {
    double value = pVariableInfo->inRegister ? (double)pFRegister[pVariableInfo->position - LLS_IREGISTER_COUNT] : *(double *)(pStack - pVariableInfo->position);
    printf(" (f64) : %e / %f\n", value, value);
    break;
  }

  case DT_OtherPtr:
  {
    uint8_t *pValue = pVariableInfo->inRegister ? (uint8_t *)pIRegister[pVariableInfo->position] : *(uint8_t **)(pStack - pVariableInfo->position);
    printf(" (ptr<other>) : 0x%" PRIX64 "\n --> ", (size_t)pValue);

    if (_IsBadReadPtr(pValue, 32))
    {
      puts("<BAD_PTR>");
    }
    else
    {
      for (size_t i = 0; i < 32; i++)
        printf("%02" PRIX8 " ", pValue[i]);

      fputs("...\n --> \"", stdout);

      bool end = true;

      for (size_t i = 0; i < 32; i++)
      {
        if (pValue[i] == 0)
        {
          end = false;
          break;
        }

        printf("%c", pValue[i]);
      }

      if (end)
        puts("...\"");
      else
        puts("\"");
    }

    break;
  }

  case DT_U8Ptr:
  {
    uint8_t *pValue = pVariableInfo->inRegister ? (uint8_t *)pIRegister[pVariableInfo->position] : *(uint8_t **)(pStack - pVariableInfo->position);
    printf(" (ptr<u8>) : 0x%" PRIX64 "\n --> ", (size_t)pValue);

    if (_IsBadReadPtr(pValue, 24))
    {
      puts("<BAD_PTR>");
    }
    else
    {
      for (size_t i = 0; i < 24 / sizeof(uint8_t); i++)
        printf("%" PRIu8 ", ", pValue[i]);

      puts("...");
    }

    break;
  }

  case DT_I8Ptr:
  {
    int8_t *pValue = pVariableInfo->inRegister ? (int8_t *)pIRegister[pVariableInfo->position] : *(int8_t **)(pStack - pVariableInfo->position);
    printf(" (ptr<i8>) : 0x%" PRIX64 "\n --> ", (size_t)pValue);

    if (_IsBadReadPtr(pValue, 24))
    {
      puts("<BAD_PTR>");
    }
    else
    {
      for (size_t i = 0; i < 24 / sizeof(int8_t); i++)
        printf("%" PRIi8 ", ", pValue[i]);

      fputs("...\n --> \"", stdout);

      bool end = true;

      for (size_t i = 0; i < 24 / sizeof(int8_t); i++)
      {
        if (pValue[i] == 0)
        {
          end = false;
          break;
        }

        printf("%c", pValue[i]);
      }

      if (end)
        puts("...\"");
      else
        puts("\"");
    }

    break;
  }

  case DT_U16Ptr:
  {
    uint16_t *pValue = pVariableInfo->inRegister ? (uint16_t *)pIRegister[pVariableInfo->position] : *(uint16_t **)(pStack - pVariableInfo->position);
    printf(" (ptr<u16>) : 0x%" PRIX64 "\n --> ", (size_t)pValue);

    if (_IsBadReadPtr(pValue, 24))
    {
      puts("<BAD_PTR>");
    }
    else
    {
      for (size_t i = 0; i < 24 / sizeof(uint16_t); i++)
        printf("%" PRIu16 ", ", pValue[i]);

      puts("...");
    }

    break;
  }

  case DT_I16Ptr:
  {
    int16_t *pValue = pVariableInfo->inRegister ? (int16_t *)pIRegister[pVariableInfo->position] : *(int16_t **)(pStack - pVariableInfo->position);
    printf(" (ptr<i16>) : 0x%" PRIX64 "\n --> ", (size_t)pValue);

    if (_IsBadReadPtr(pValue, 24))
    {
      puts("<BAD_PTR>");
    }
    else
    {
      for (size_t i = 0; i < 24 / sizeof(int16_t); i++)
        printf("%" PRIi16 ", ", pValue[i]);

      puts("...");
    }

    break;
  }

  case DT_U32Ptr:
  {
    uint32_t *pValue = pVariableInfo->inRegister ? (uint32_t *)pIRegister[pVariableInfo->position] : *(uint32_t **)(pStack - pVariableInfo->position);
    printf(" (ptr<u32>) : 0x%" PRIX64 "\n --> ", (size_t)pValue);

    if (_IsBadReadPtr(pValue, 24))
    {
      puts("<BAD_PTR>");
    }
    else
    {
      for (size_t i = 0; i < 24 / sizeof(uint32_t); i++)
        printf("%" PRIu32 ", ", pValue[i]);

      puts("...");
    }

    break;
  }

  case DT_I32Ptr:
  {
    int32_t *pValue = pVariableInfo->inRegister ? (int32_t *)pIRegister[pVariableInfo->position] : *(int32_t **)(pStack - pVariableInfo->position);
    printf(" (ptr<i32>) : 0x%" PRIX64 "\n --> ", (size_t)pValue);

    if (_IsBadReadPtr(pValue, 24))
    {
      puts("<BAD_PTR>");
    }
    else
    {
      for (size_t i = 0; i < 24 / sizeof(int32_t); i++)
        printf("%" PRIi32 ", ", pValue[i]);

      puts("...");
    }

    break;
  }

  case DT_U64Ptr:
  {
    uint64_t *pValue = pVariableInfo->inRegister ? (uint64_t *)pIRegister[pVariableInfo->position] : *(uint64_t **)(pStack - pVariableInfo->position);
    printf(" (ptr<u64>) : 0x%" PRIX64 "\n --> ", (size_t)pValue);

    if (_IsBadReadPtr(pValue, 24))
    {
      puts("<BAD_PTR>");
    }
    else
    {
      for (size_t i = 0; i < 24 / sizeof(uint64_t); i++)
        printf("%" PRIu64 ", ", pValue[i]);

      puts("...");
    }

    break;
  }

  case DT_I64Ptr:
  {
    int64_t *pValue = pVariableInfo->inRegister ? (int64_t *)pIRegister[pVariableInfo->position] : *(int64_t **)(pStack - pVariableInfo->position);
    printf(" (ptr<i64>) : 0x%" PRIX64 "\n --> ", (size_t)pValue);

    if (_IsBadReadPtr(pValue, 24))
    {
      puts("<BAD_PTR>");
    }
    else
    {
      for (size_t i = 0; i < 24 / sizeof(int64_t); i++)
        printf("%" PRIi64 ", ", pValue[i]);

      puts("...");
    }

    break;
  }

  case DT_F32Ptr:
  {
    float *pValue = pVariableInfo->inRegister ? (float *)pIRegister[pVariableInfo->position] : *(float **)(pStack - pVariableInfo->position);
    printf(" (ptr<f32>) : 0x%" PRIX64 "\n --> ", (size_t)pValue);

    if (_IsBadReadPtr(pValue, 24))
    {
      puts("<BAD_PTR>");
    }
    else
    {
      for (size_t i = 0; i < 24 / sizeof(float); i++)
        printf("%e, ", pValue[i]);

      puts("...");
    }

    break;
  }

  case DT_F64Ptr:
  {
    double *pValue = pVariableInfo->inRegister ? (double *)pIRegister[pVariableInfo->position] : *(double **)(pStack - pVariableInfo->position);
    printf(" (ptr<f64>) : 0x%" PRIX64 "\n --> ", (size_t)pValue);

    if (_IsBadReadPtr(pValue, 24))
    {
      puts("<BAD_PTR>");
    }
    else
    {
      for (size_t i = 0; i < 24 / sizeof(double); i++)
        printf("%e, ", pValue[i]);

      puts("...");
    }

    break;
  }

  case DT_OtherArray:
  {
    uint8_t *pValue = pVariableInfo->inRegister ? (uint8_t *)pIRegister[pVariableInfo->position] : (uint8_t *)(pStack - pVariableInfo->position);
    printf(" (array<other>) : 0x%" PRIX64 "\n --> ", (size_t)pValue);

    if (_IsBadReadPtr(pValue, 24))
    {
      puts("<BAD_PTR>");
    }
    else
    {
      for (size_t i = 0; i < 32; i++)
        printf("%02" PRIX8 " ", pValue[i]);

      fputs("...\n --> \"", stdout);

      bool end = true;

      for (size_t i = 0; i < 32; i++)
      {
        if (pValue[i] == 0)
        {
          end = false;
          break;
        }

        printf("%c", pValue[i]);
      }

      if (end)
        puts("...\"");
      else
        puts("\"");
    }

    break;
  }

  case DT_U8Array:
  {
    uint8_t *pValue = pVariableInfo->inRegister ? (uint8_t *)pIRegister[pVariableInfo->position] : (uint8_t *)(pStack - pVariableInfo->position);
    printf(" (array<u8>) : 0x%" PRIX64 "\n --> ", (size_t)pValue);

    if (_IsBadReadPtr(pValue, 24))
    {
      puts("<BAD_PTR>");
    }
    else
    {
      for (size_t i = 0; i < 24 / sizeof(uint8_t); i++)
        printf("%" PRIu8 ", ", pValue[i]);

      puts("...");
    }

    break;
  }

  case DT_I8Array:
  {
    int8_t *pValue = pVariableInfo->inRegister ? (int8_t *)pIRegister[pVariableInfo->position] : (int8_t *)(pStack - pVariableInfo->position);
    printf(" (array<i8>) : 0x%" PRIX64 "\n --> ", (size_t)pValue);

    if (_IsBadReadPtr(pValue, 24))
    {
      puts("<BAD_PTR>");
    }
    else
    {
      for (size_t i = 0; i < 24 / sizeof(int8_t); i++)
        printf("%" PRIi8 ", ", pValue[i]);

      fputs("...\n --> \"", stdout);

      bool end = true;

      for (size_t i = 0; i < 24 / sizeof(int8_t); i++)
      {
        if (pValue[i] == 0)
        {
          end = false;
          break;
        }

        printf("%c", pValue[i]);
      }

      if (end)
        puts("...\"");
      else
        puts("\"");
    }

    break;
  }

  case DT_U16Array:
  {
    uint16_t *pValue = pVariableInfo->inRegister ? (uint16_t *)pIRegister[pVariableInfo->position] : (uint16_t *)(pStack - pVariableInfo->position);
    printf(" (array<u16>) : 0x%" PRIX64 "\n --> ", (size_t)pValue);

    if (_IsBadReadPtr(pValue, 24))
    {
      puts("<BAD_PTR>");
    }
    else
    {
      for (size_t i = 0; i < 24 / sizeof(uint16_t); i++)
        printf("%" PRIu16 ", ", pValue[i]);

      puts("...");
    }

    break;
  }

  case DT_I16Array:
  {
    int16_t *pValue = pVariableInfo->inRegister ? (int16_t *)pIRegister[pVariableInfo->position] : (int16_t *)(pStack - pVariableInfo->position);
    printf(" (array<i16>) : 0x%" PRIX64 "\n --> ", (size_t)pValue);

    if (_IsBadReadPtr(pValue, 24))
    {
      puts("<BAD_PTR>");
    }
    else
    {
      for (size_t i = 0; i < 24 / sizeof(int16_t); i++)
        printf("%" PRIi16 ", ", pValue[i]);

      puts("...");
    }

    break;
  }

  case DT_U32Array:
  {
    uint32_t *pValue = pVariableInfo->inRegister ? (uint32_t *)pIRegister[pVariableInfo->position] : (uint32_t *)(pStack - pVariableInfo->position);
    printf(" (array<u32>) : 0x%" PRIX64 "\n --> ", (size_t)pValue);

    if (_IsBadReadPtr(pValue, 24))
    {
      puts("<BAD_PTR>");
    }
    else
    {
      for (size_t i = 0; i < 24 / sizeof(uint32_t); i++)
        printf("%" PRIu32 ", ", pValue[i]);

      puts("...");
    }

    break;
  }

  case DT_I32Array:
  {
    int32_t *pValue = pVariableInfo->inRegister ? (int32_t *)pIRegister[pVariableInfo->position] : (int32_t *)(pStack - pVariableInfo->position);
    printf(" (array<i32>) : 0x%" PRIX64 "\n --> ", (size_t)pValue);

    if (_IsBadReadPtr(pValue, 24))
    {
      puts("<BAD_PTR>");
    }
    else
    {
      for (size_t i = 0; i < 24 / sizeof(int32_t); i++)
        printf("%" PRIi32 ", ", pValue[i]);

      puts("...");
    }

    break;
  }

  case DT_U64Array:
  {
    uint64_t *pValue = pVariableInfo->inRegister ? (uint64_t *)pIRegister[pVariableInfo->position] : (uint64_t *)(pStack - pVariableInfo->position);
    printf(" (array<u64>) : 0x%" PRIX64 "\n --> ", (size_t)pValue);

    if (_IsBadReadPtr(pValue, 24))
    {
      puts("<BAD_PTR>");
    }
    else
    {
      for (size_t i = 0; i < 24 / sizeof(uint64_t); i++)
        printf("%" PRIu64 ", ", pValue[i]);

      puts("...");
    }

    break;
  }

  case DT_I64Array:
  {
    int64_t *pValue = pVariableInfo->inRegister ? (int64_t *)pIRegister[pVariableInfo->position] : (int64_t *)(pStack - pVariableInfo->position);
    printf(" (array<i64>) : 0x%" PRIX64 "\n --> ", (size_t)pValue);

    if (_IsBadReadPtr(pValue, 24))
    {
      puts("<BAD_PTR>");
    }
    else
    {
      for (size_t i = 0; i < 24 / sizeof(int64_t); i++)
        printf("%" PRIi64 ", ", pValue[i]);

      puts("...");
    }

    break;
  }

  case DT_F32Array:
  {
    float *pValue = pVariableInfo->inRegister ? (float *)pIRegister[pVariableInfo->position] : (float *)(pStack - pVariableInfo->position);
    printf(" (array<f32>) : 0x%" PRIX64 "\n --> ", (size_t)pValue);

    if (_IsBadReadPtr(pValue, 24))
    {
      puts("<BAD_PTR>");
    }
    else
    {
      for (size_t i = 0; i < 24 / sizeof(float); i++)
        printf("%e, ", pValue[i]);

      puts("...");
    }

    break;
  }

  case DT_F64Array:
  {
    double *pValue = pVariableInfo->inRegister ? (double *)pIRegister[pVariableInfo->position] : (double *)(pStack - pVariableInfo->position);
    printf(" (array<f64>) : 0x%" PRIX64 "\n --> ", (size_t)pValue);

    if (_IsBadReadPtr(pValue, 24))
    {
      puts("<BAD_PTR>");
    }
    else
    {
      for (size_t i = 0; i < 24 / sizeof(double); i++)
        printf("%e, ", pValue[i]);

      puts("...");
    }

    break;
  }

  default:
  case DT_Other:
  {
    fputs(" (Other)\n", stdout);
    break;
  }
  }

  ResetConsoleColour();
}

#endif
