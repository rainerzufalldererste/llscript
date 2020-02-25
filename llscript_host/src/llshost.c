#include "llshost.h"
#include "llshost_opcodes.h"
#include "llshost_builtin_func.h"

#include <stdbool.h>

#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <intrin.h>

#pragma warning (push, 0)
#include <winternl.h>

#ifndef LLS_PERF_MODE
#define ASSERT_NO_ELSE else { __debugbreak(); return; }
#define IF_LAST_OPT(...) if (__VA_ARGS__)
#define ASSERT(x) do { if (!(x)) { __debugbreak(); return; } } while (0)
#else
#define ASSERT_NO_ELSE
#define IF_LAST_OPT(...)
#define ASSERT(x)
#endif

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

__forceinline void llshost_EvaluateCode(llshost_state_t *pState)
{
  uint8_t *pStack = pState->pStack;
  lls_code_t *pCodePtr = pState->pCode;

  uint64_t iregister[8];
  double fregister[8];
  bool cmp = false;

  CopyBytes(iregister, pState->registerValues, sizeof(iregister));
  CopyBytes(fregister, pState->registerValues + 8, sizeof(fregister));

#ifdef LLS_DEBUG_MODE
  bool silent = false;
  bool stepInstructions = true;
  bool stepOut = false;
  uint64_t breakpoint = (uint64_t)-1;

  memset(iregister, 0, sizeof(iregister));
  memset(fregister, 0, sizeof(fregister));

  puts("llshost byte code interpreter.\n\n\t'c' to run / continue execution.\n\t'n' to step.\n\t'f' to step out.\n\t'b' to set the breakpoint\n\t'r' for registers\n\t'p' for stack bytes\n\t'y' for advanced stack bytes\n\t'i' to inspect a value\n\t'm' to modify a value\n\t's' toggle silent.\n\t'x' to quit.\n\n");
#endif

  while (1)
  {
#ifdef LLS_DEBUG_MODE
    if (pCodePtr - pState->pCode == breakpoint)
    {
      printf("\n\tBreakpoint Hit (0x%" PRIX64 ")!\n\n", breakpoint);
      stepInstructions = true;
    }

    if (stepInstructions)
    {
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
          goto continue_execution;

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
          silent = ~silent;
          break;

        case 'x':
          return;

        default:;
        }
      }

    continue_execution:
      ;
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
        iregister[source_register] == (iregister[source_register] && iregister[operand_register]) ? 1 : 0;
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
        iregister[source_register] == (iregister[source_register] || iregister[operand_register]) ? 1 : 0;
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
  }
}

__forceinline void llshost_Setup(llshost_state_t *pState)
{
  if (pState->pStack != NULL)
    return;

  if (pState->stackSize == 0)
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

  typedef LPVOID (*HeapAllocFunc)(HANDLE hHeap, DWORD dwFlags, SIZE_T dwBytes);
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

__forceinline void llshost_Cleanup(llshost_state_t *pState)
{
  if (pState->pHeapHandle != NULL)
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

void llshost(void *pCodePtr, void *pCallFunc)
{
  llshost_state_t state;

  for (size_t i = 0; i < sizeof(state); i++)
    ((uint8_t *)&state)[i] = 0;

  state.pCode = pCodePtr; 
  state.pCallFuncShellcode = pCallFunc;

  llshost_Setup(&state);
  llshost_EvaluateCode(&state);
  llshost_Cleanup(&state);
}

void llshost_from_state(llshost_state_t *pState)
{
  llshost_Setup(pState);
  llshost_EvaluateCode(pState);
  llshost_Cleanup(pState);
}
