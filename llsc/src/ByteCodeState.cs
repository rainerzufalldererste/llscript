using System;
using System.Collections.Generic;
using System.Linq;

namespace llsc
{
  public class ByteCodeState
  {
    public List<byte> byteCode = new List<byte>();
    public List<LLInstruction> instructions = new List<LLInstruction>();
    public List<LLInstruction> postInstructionDataStorage = new List<LLInstruction>();

    public CValue[] registers = new CValue[Compiler.IntegerRegisters + Compiler.FloatRegisters];
    public bool[] registerLocked = new bool[Compiler.IntegerRegisters + Compiler.FloatRegisters];

    public class RegisterLock : IDisposable
    {
      int register;
      ByteCodeState byteCodeState;
      bool previousValue;

      public RegisterLock(int register, ByteCodeState byteCodeState)
      {
        this.register = register;
        this.byteCodeState = byteCodeState;

        previousValue = this.byteCodeState.registerLocked[register];
        this.byteCodeState.registerLocked[register] = true;
      }

      public void Dispose()
      {
        byteCodeState.registerLocked[register] = previousValue;
      }
    }

    public RegisterLock LockRegister(int register)
    {
      return new RegisterLock(register, this);
    }

    public void CompileInstructionsToBytecode()
    {
      instructions.AddRange(postInstructionDataStorage);
      postInstructionDataStorage = null;

      ulong position = 0;

      foreach (var instruction in instructions)
      {
        if (Compiler.OptimizationLevel > 0)
          instruction.AfterCodeGen();

        instruction.position = position;
        position += instruction.bytecodeSize;
      }

      foreach (var instruction in instructions)
      {
        if (instruction.bytecodeSize != 0)
        {
          ulong expectedSizeAfter = (ulong)byteCode.LongCount() + instruction.bytecodeSize;

          instruction.AppendBytecode(ref byteCode);

          if (expectedSizeAfter != (ulong)byteCode.LongCount())
            throw new Exception($"Internal Compiler Error: Instruction '{instruction}' wrote a different amount of bytes than originally promised.");
        }
      }
    }

    public void DumpValue(CValue value)
    {
      if (value.hasPosition && value.position.type == PositionType.InRegister)
        registers[value.position.registerIndex] = null;

      value.hasPosition = false;
      value.position.type = PositionType.Invalid;

      if (Compiler.DetailedIntermediateOutput && value is CNamedValue)
        instructions.Add(new LLI_Comment_PseudoInstruction("Dumping Value: " + value));
    }

    public void MarkValueAsPosition(CValue value, Position position, SharedValue<long> stackSize, bool isTouched)
    {
      if (position.type == PositionType.InRegister)
        registers[position.registerIndex] = value;


      value.hasPosition = true;
      value.position = position;

      instructions.Add(new LLI_Location_PseudoInstruction(value, stackSize, this));

      if (isTouched)
        MarkValueAsTouched(value);
    }

    public void MarkValueAsTouched(CValue value)
    {
      value.lastTouchedInstructionCount = instructions.Count();
    }

    public int GetFreeIntegerRegister(SharedValue<long> stackSize)
    {
      for (int i = Compiler.IntegerRegisters - 1; i >= 0; i--)
        if (registers[i] == null && !registerLocked[i])
          return i;

      for (int i = Compiler.IntegerRegisters - 1; i >= 0; i--)
      {
        if (registers[i] != null && registers[i].remainingReferences == 0 && !registerLocked[i])
        {
          DumpValue(registers[i]);
          registers[i] = null;
          return i;
        }
      }

      int oldestIndex = -1;
      int oldestValue = instructions.Count;

      // With Home.

      for (int i = Compiler.IntegerRegisters - 1; i >= 0; i--)
      {
        if (registerLocked[i])
          continue;

        if (registers[i] is CNamedValue && (registers[i] as CNamedValue).hasHomePosition)
        {
          if (!(registers[i] as CNamedValue).modifiedSinceLastHome)
          {
            registers[i].position = (registers[i] as CNamedValue).homePosition;
            registers[i] = null;
            return i;
          }

          if (registers[i].lastTouchedInstructionCount <= oldestValue)
          {
            oldestValue = registers[i].lastTouchedInstructionCount;
            oldestIndex = i;
          }
        }
      }

      if (oldestIndex < 0)
      {
        oldestIndex = -1;

        for (int i = Compiler.IntegerRegisters - 1; i >= 0; i--)
        {
          if (!registerLocked[i] && registers[i].lastTouchedInstructionCount <= oldestValue)
          {
            oldestValue = registers[i].lastTouchedInstructionCount;
            oldestIndex = i;
          }
        }
      }

      if (oldestIndex == -1)
        throw new Exception("Internal Compiler Error: Unable to find index, all indices seem locked.");

      FreeUsedRegister(oldestIndex, stackSize);

      return oldestIndex;
    }

    public int GetFreeFloatRegister(SharedValue<long> stackSize)
    {
      for (int i = Compiler.IntegerRegisters + Compiler.FloatRegisters - 1; i >= Compiler.IntegerRegisters; i--)
        if (registers[i] == null && !registerLocked[i])
          return i;

      for (int i = Compiler.IntegerRegisters + Compiler.FloatRegisters - 1; i >= Compiler.IntegerRegisters; i--)
        if (registers[i] != null && registers[i].remainingReferences == 0 && !registerLocked[i])
        {
          DumpValue(registers[i]);
          registers[i] = null;
          return i;
        }

      int oldestIndex = -1;
      int oldestValue = instructions.Count;

      // With Home.

      for (int i = Compiler.IntegerRegisters + Compiler.FloatRegisters - 1; i >= Compiler.IntegerRegisters; i--)
      {
        if (registerLocked[i])
          continue;

        if (registers[i] != null && registers[i] is CNamedValue && (registers[i] as CNamedValue).hasHomePosition)
        {
          if (!(registers[i] as CNamedValue).modifiedSinceLastHome)
          {
            registers[i].position = (registers[i] as CNamedValue).homePosition;
            registers[i] = null;
            return i;
          }

          if (registers[i].lastTouchedInstructionCount <= oldestValue)
          {
            oldestValue = registers[i].lastTouchedInstructionCount;
            oldestIndex = i;
          }
        }
      }

      if (oldestIndex < 0)
      {
        oldestIndex = -1;

        for (int i = Compiler.IntegerRegisters + Compiler.FloatRegisters - 1; i >= Compiler.IntegerRegisters; i--)
        {
          if (registers[i].lastTouchedInstructionCount <= oldestValue)
          {
            oldestValue = registers[i].lastTouchedInstructionCount;
            oldestIndex = i;
          }
        }
      }

      if (oldestIndex == -1)
        throw new Exception("Internal Compiler Error: Unable to find index, all indices seem locked.");

      FreeUsedRegister(oldestIndex, stackSize);

      return oldestIndex;
    }

    public int GetTriviallyFreeIntegerRegister()
    {
      for (int i = Compiler.IntegerRegisters - 1; i >= 0; i--)
        if (registers[i] == null && !registerLocked[i])
          return i;

      for (int i = Compiler.IntegerRegisters - 1; i >= 0; i--)
        if (registers[i] != null && registers[i].remainingReferences == 0 && !registerLocked[i])
        {
          DumpValue(registers[i]);
          registers[i] = null;
          return i;
        }

      for (int i = Compiler.IntegerRegisters - 1; i >= 0; i--)
        if (registers[i] != null && registers[i] is CNamedValue && (registers[i] as CNamedValue).hasHomePosition && !registerLocked[i])
          if (!(registers[i] as CNamedValue).modifiedSinceLastHome)
          {
            registers[i].position = (registers[i] as CNamedValue).homePosition;
            registers[i] = null;
            return i;
          }

      return -1;
    }

    public int GetTriviallyFreeFloatRegister()
    {
      for (int i = Compiler.IntegerRegisters + Compiler.FloatRegisters - 1; i >= Compiler.IntegerRegisters; i--)
        if (registers[i] == null && !registerLocked[i])
          return i;

      for (int i = Compiler.IntegerRegisters + Compiler.FloatRegisters - 1; i >= Compiler.IntegerRegisters; i--)
        if (registers[i] != null && registers[i].remainingReferences == 0 && !registerLocked[i])
        {
          DumpValue(registers[i]);
          registers[i] = null;
          return i;
        }

      for (int i = Compiler.IntegerRegisters + Compiler.FloatRegisters - 1; i >= Compiler.IntegerRegisters; i--)
        if (registers[i] != null && registers[i] is CNamedValue && (registers[i] as CNamedValue).hasHomePosition && !registerLocked[i])
          if (!(registers[i] as CNamedValue).modifiedSinceLastHome)
          {
            registers[i].position = (registers[i] as CNamedValue).homePosition;
            registers[i] = null;
            return i;
          }

      return -1;
    }

    public void FreeRegister(int register, SharedValue<long> stackSize)
    {
      if (registers[register] == null || registers[register].remainingReferences == 0)
        return;

      if (registers[register] is CNamedValue && (registers[register] as CNamedValue).hasHomePosition && !(registers[register] as CNamedValue).modifiedSinceLastHome)
      {
        registers[register].position = (registers[register] as CNamedValue).homePosition;
        registers[register] = null;
        return;
      }

      int triviallyFreeRegister = register < Compiler.IntegerRegisters ? GetTriviallyFreeIntegerRegister() : GetTriviallyFreeFloatRegister();

      if (triviallyFreeRegister == -1)
      {
        FreeUsedRegister(register, stackSize);
      }
      else
      {
        instructions.Add(new LLI_MovRegisterToRegister(register, triviallyFreeRegister));

        registers[register].position = Position.Register(triviallyFreeRegister);

        registers[triviallyFreeRegister] = registers[register];
        registers[register] = null;
      }
    }

    public void FreeUsedRegister(int registerIndex, SharedValue<long> stackSize)
    {
      if (registers[registerIndex] == null)
        return;

      if (!registers[registerIndex].hasPosition)
        throw new Exception($"Internal Compiler Error when trying to remove {registers[registerIndex]} from r:{registerIndex}, which is claiming to not live anywhere.");

      if (registers[registerIndex].position.type != PositionType.InRegister || registers[registerIndex].position.registerIndex != registerIndex)
        throw new Exception($"Internal Compiler Error when trying to remove {registers[registerIndex]} from r:{registerIndex}, which is claiming to live in {registers[registerIndex].position}.");

      if (registers[registerIndex] is CNamedValue)
      {
        var value = registers[registerIndex] as CNamedValue;
        MoveRegisterValueToHome(value, stackSize);
      }
      else if (registers[registerIndex].remainingReferences > 0)
      {
        var value = registers[registerIndex];

        if (value is CConstFloatValue || value is CConstFloatValue)
        {
          DumpValue(value);
        }
        else
        {
          var newPosition = Position.StackOffset(stackSize.Value);
          stackSize.Value += value.type.GetSize();

          if (Compiler.DetailedIntermediateOutput)
            instructions.Add(new LLI_Comment_PseudoInstruction($"Moving temporary value '{value}' to the stack ({value.remainingReferences} references remaining), because we're running out of registers."));

          MoveValueToPosition(value, newPosition, stackSize, false);
        }
      }

      registers[registerIndex] = null;
    }

    public void MoveRegisterValueToHome(CNamedValue value, SharedValue<long> stackSize)
    {
      if (!value.hasPosition)
        throw new Exception($"Internal Compiler Error. Trying to move {value}, eventhough it doesn't claim to have a position.");

      if (value.position.type != PositionType.InRegister)
        throw new Exception($"Internal Compiler Error. Expected value {value} to live in a register.");

      if (registers[value.position.registerIndex] != value)
        throw new Exception($"Internal Compiler Error. Value {value} claimed to be in register {value.position} it didn't actually inhabit.");

      int registerIndex = value.position.registerIndex;

      if (value.hasHomePosition && !value.modifiedSinceLastHome)
      {
        value.position = value.homePosition;
        instructions.Add(new LLI_Location_PseudoInstruction(value, stackSize, this));
        registers[registerIndex] = null;
      }
      else
      {
        if (!value.hasHomePosition && Compiler.DetailedIntermediateOutput)
          instructions.Add(new LLI_Comment_PseudoInstruction($"Assigning a home location for '{value}'."));

        if (!value.hasHomePosition && value.hasPosition && value.position.type == PositionType.OnStack)
          throw new Exception("How is this value on the stack?!");

        bool storeInCodeBase = value.type.isConst || (value.type is PtrCType && (value.type as PtrCType).pointsTo.isConst) || (value.type is ArrayCType && (value.type as ArrayCType).type.isConst) || (value.isStatic && Compiler.Assumptions.ByteCodeMutable);
        bool storeOnGlobalStack = !storeInCodeBase && value.isStatic;

        if (storeInCodeBase || storeOnGlobalStack)
        {
          if (!value.hasHomePosition)
          {
            if (storeInCodeBase)
            {
              value.homePosition = Position.CodeBaseOffset(value, new byte[value.type.GetSize()], value.file, value.line);
              postInstructionDataStorage.Add(value.homePosition.codeBaseOffset);
              value.hasHomePosition = true;
            }
            else // if (storeOnGlobalStack)
            {
              value.homePosition = Position.GlobalStackBaseOffset(Compiler.GlobalScope.maxRequiredStackSpace.Value);
              Compiler.GlobalScope.maxRequiredStackSpace.Value += value.type.GetSize();
              value.hasHomePosition = true;
            }
          }

          int freeIntRegisters = 0;
          int firstFreeRegister = 0;

          for (int i = 0; i < Compiler.IntegerRegisters; i++)
          {
            if (!registerLocked[i] && registers[i] == null)
            {
              freeIntRegisters++;
              firstFreeRegister = i;
              break;
            }
          }

          if (freeIntRegisters == 0)
          {
            for (int i = 0; i < Compiler.IntegerRegisters; i++)
            {
              if (!registerLocked[i] && registers[i] != null && registers[i] is CConstIntValue)
              {
                freeIntRegisters++;
                firstFreeRegister = i;

                DumpValue(registers[i]);

                break;
              }
            }
          }

          if (freeIntRegisters > 0)
          {
            if (storeInCodeBase)
            {
              instructions.Add(new LLI_MovRuntimeParamToRegister(LLI_RuntimeParam.LLS_RP_CODE_BASE_PTR, (byte)firstFreeRegister));
              instructions.Add(new LLI_AddImmInstructionOffset(firstFreeRegister, value.homePosition.codeBaseOffset));
            }
            else // if (storeOnGlobalStack)
            {
              instructions.Add(new LLI_MovRuntimeParamToRegister(LLI_RuntimeParam.LLS_RP_STACK_BASE_PTR, (byte)firstFreeRegister));
              instructions.Add(new LLI_AddImm(firstFreeRegister, BitConverter.GetBytes(value.homePosition.globalStackBaseOffset)));
            }

            if (value.type.GetSize() == 8)
              instructions.Add(new LLI_MovRegisterToPtrInRegister(firstFreeRegister, registerIndex));
            else
              instructions.Add(new LLI_MovRegisterToPtrInRegister_NBytes(firstFreeRegister, registerIndex, (int)value.type.GetSize()));
          }
          else
          {
            // No Registers Available, We'll free one up and then restore it's original value.
            byte chosenRegister = (byte)((registerIndex + 1) % Compiler.IntegerRegisters);

            if (Compiler.DetailedIntermediateOutput)
              instructions.Add(new LLI_Comment_PseudoInstruction($"Temporarily using r:{chosenRegister} to store the pointer to {value}."));

            instructions.Add(new LLI_PushRegister(chosenRegister));

            if (storeInCodeBase)
            {
              instructions.Add(new LLI_MovRuntimeParamToRegister(LLI_RuntimeParam.LLS_RP_CODE_BASE_PTR, chosenRegister));
              instructions.Add(new LLI_AddImmInstructionOffset(chosenRegister, value.homePosition.codeBaseOffset));
            }
            else // if (storeOnGlobalStack)
            {
              instructions.Add(new LLI_MovRuntimeParamToRegister(LLI_RuntimeParam.LLS_RP_STACK_BASE_PTR, chosenRegister));
              instructions.Add(new LLI_AddImm(chosenRegister, BitConverter.GetBytes(value.homePosition.globalStackBaseOffset)));
            }

            if (value.type.GetSize() == 8)
              instructions.Add(new LLI_MovRegisterToPtrInRegister(chosenRegister, registerIndex));
            else
              instructions.Add(new LLI_MovRegisterToPtrInRegister_NBytes(chosenRegister, registerIndex, (int)value.type.GetSize()));

            instructions.Add(new LLI_PopRegister(chosenRegister));
          }
        }
        else
        {
          if (!value.hasHomePosition)
          {
            value.homePosition = Position.StackOffset(stackSize.Value);
            var size = value.type.GetSize();
            stackSize.Value += size;
            value.hasHomePosition = true;
          }

          if (value.type.GetSize() == 8)
            instructions.Add(new LLI_MovRegisterToStackOffset(registerIndex, stackSize, value.homePosition.stackOffsetForward));
          else
            instructions.Add(new LLI_MovRegisterToStackOffset_NBytes(registerIndex, stackSize, value.homePosition.stackOffsetForward, (int)value.type.GetSize()));
        }

        registers[registerIndex] = null;

        value.modifiedSinceLastHome = false;
        value.hasPosition = true;
        value.hasHomePosition = true;
        value.position = value.homePosition;

        instructions.Add(new LLI_Location_PseudoInstruction(value, stackSize, this));
      }
    }

    public void BackupRegisterValues(SharedValue<long> stackSize)
    {
      instructions.Add(new LLI_Comment_PseudoInstruction("Backup Register Values."));

      for (byte i = 0; i < registers.Length; i++)
      {
        if (registers[i] == null)
          continue;

        FreeUsedRegister(i, stackSize);

        registers[i] = null;
      }
    }

    internal void TruncateValueInRegister(CValue value)
    {
      if (!(value.type is BuiltInCType))
        throw new Exception($"Unexpected Type: Trying to truncate {value}. Exected builtin type.");

      if (!value.hasPosition || value.position.type != PositionType.InRegister)
        throw new Exception($"Unexpected Position for {value}.");

      if ((value.type as BuiltInCType).IsFloat())
        throw new NotImplementedException();

      if (value.type.GetSize() >= 8)
        return;

      if (Compiler.DetailedIntermediateOutput)
        instructions.Add(new LLI_Comment_PseudoInstruction($"Truncating Value '{value}' to {value.type.GetSize()} bytes."));

      instructions.Add(new LLI_AndImm(value.position.registerIndex, BitConverter.GetBytes(((ulong)1 << (int)(value.type.GetSize() * 8)) - 1)));
    }

    internal void TruncateRegister(int registerIndex, long bytes)
    {
      if (bytes == 8)
        return;

      if (Compiler.DetailedIntermediateOutput)
        instructions.Add(new LLI_Comment_PseudoInstruction($"Truncating Value in Register {registerIndex} to {bytes} bytes."));

      instructions.Add(new LLI_AndImm(registerIndex, BitConverter.GetBytes(((ulong)1 << (int)(bytes * 8)) - 1)));
    }

    public void MoveValueToPosition(CValue sourceValue, Position targetPosition, SharedValue<long> stackSize, bool isTouched)
    {
      // TODO: Work out the reference count...

      if (targetPosition.type != PositionType.OnStack && sourceValue.type.GetSize() > 8)
        throw new Exception($"Internal Compiler Error: Value '{sourceValue}' cannot be moved, because it's > 8 bytes.");

      CopyValueToPosition(sourceValue, targetPosition, stackSize);
      MarkValueAsPosition(sourceValue, targetPosition, stackSize, isTouched);
    }

    public void CopyValueToPositionWithCast(CValue sourceValue, Position targetPosition, CType targetType, SharedValue<long> stackSize)
    {
      // TODO: What type will be stored? The converted one? The original one? Should we return the value?!

      if (!sourceValue.type.Equals(targetType) && !sourceValue.type.CanImplicitCastTo(targetType) && !sourceValue.type.CanExplicitCastTo(targetType))
        throw new Exception($"Internal Compiler Error: Trying to Move Value with Cast from type '{sourceValue.type}' to type '{targetType}' but there is no cast available.");

      if (sourceValue.type.Equals(targetType))
      {
        CopyValueToPosition(sourceValue, targetPosition, stackSize);
        return;
      }

      if (Compiler.DetailedIntermediateOutput)
        instructions.Add(new LLI_Comment_PseudoInstruction($"Copying Value '{sourceValue}' to {targetPosition} with cast to {targetType}."));

      if (sourceValue.type is ArrayCType && targetType is PtrCType)
      {
        if (targetPosition.type == PositionType.InRegister)
        {
          if (!sourceValue.hasPosition)
            throw new Exception($"Internal Compiler Error: sourceValue {sourceValue} doesn't have a position.");

          FreeRegister(targetPosition.registerIndex, stackSize);

          switch (sourceValue.position.type)
          {
            case PositionType.OnStack:
              instructions.Add(new LLI_LoadEffectiveAddress_StackOffsetToRegister(stackSize, sourceValue.position.stackOffsetForward, targetPosition.registerIndex));
              break;

            case PositionType.CodeBaseOffset:
              instructions.Add(new LLI_MovRuntimeParamToRegister(LLI_RuntimeParam.LLS_RP_CODE_BASE_PTR, (byte)targetPosition.registerIndex));
              instructions.Add(new LLI_AddImmInstructionOffset(targetPosition.registerIndex, sourceValue.position.codeBaseOffset));
              break;

            case PositionType.GlobalStackOffset:
              instructions.Add(new LLI_MovRuntimeParamToRegister(LLI_RuntimeParam.LLS_RP_STACK_BASE_PTR, (byte)targetPosition.registerIndex));
              instructions.Add(new LLI_AddImm(targetPosition.registerIndex, BitConverter.GetBytes((ulong)sourceValue.position.globalStackBaseOffset)));
              break;

            default:
              throw new NotImplementedException();
          }

          sourceValue.lastTouchedInstructionCount = instructions.Count;

          registers[targetPosition.registerIndex] = new CValue(sourceValue.file, sourceValue.line, new PtrCType((sourceValue.type as ArrayCType).type) { isConst = sourceValue.type.isConst }, sourceValue.isInitialized)
          {
            description = $"ptr to array '{sourceValue}'",
            hasPosition = true,
            position = Position.Register(targetPosition.registerIndex)
          };

          instructions.Add(new LLI_Location_PseudoInstruction(registers[targetPosition.registerIndex], stackSize, this));
        }
        else
        {
          CopyValueToPosition(sourceValue, targetPosition, stackSize);
        }
      }
      else if (sourceValue.type is FuncCType)
      {
        throw new NotImplementedException();
      }
      else if (sourceValue.type is BuiltInCType && targetType is BuiltInCType)
      {
        if ((sourceValue.type as BuiltInCType).IsFloat() ^ (targetType as BuiltInCType).IsFloat())
        {
          CopyValueToPosition(sourceValue, targetPosition, stackSize);
          throw new NotImplementedException();
        }
        else if (!(sourceValue.type as BuiltInCType).IsFloat() && !(targetType as BuiltInCType).IsFloat())
        {
          if (targetType.GetSize() >= sourceValue.type.GetSize())
          {
            CopyValueToPosition(sourceValue, targetPosition, stackSize);
          }
          else
          {
            if (targetPosition.type == PositionType.InRegister)
            {
              CopyValueToPosition(sourceValue, targetPosition, stackSize);
              TruncateRegister(targetPosition.registerIndex, targetType.GetSize());
            }
            else if (targetPosition.type == PositionType.OnStack)
            {
              var tempPosition = Position.Register(GetFreeIntegerRegister(stackSize));
              CopyValueToPosition(sourceValue, tempPosition, stackSize);
              TruncateRegister(tempPosition.registerIndex, targetType.GetSize());
              CopyValueToPosition(new CValue(sourceValue.file, sourceValue.line, targetType, true), targetPosition, stackSize);
            }
            else
            {
              throw new NotImplementedException();
            }
          }
        }
        else
        {
          throw new NotImplementedException();
        }
      }
      else
      {
        CopyValueToPosition(sourceValue, targetPosition, stackSize);
      }
    }

    internal byte MoveValueToAnyRegister(CValue value, SharedValue<long> stackSize)
    {
      if (Compiler.DetailedIntermediateOutput)
        instructions.Add(new LLI_Comment_PseudoInstruction($"Moving Value '{value}' to a register."));

      if (value is CConstIntValue)
      {
        int registerIndex = GetFreeIntegerRegister(stackSize);

        instructions.Add(new LLI_MovImmToRegister((byte)registerIndex, BitConverter.GetBytes((value as CConstIntValue).uvalue)));

        registers[registerIndex] = value;
        value.hasPosition = true;
        value.position = Position.Register(registerIndex);

        return (byte)registerIndex;
      }
      else if (value is CConstFloatValue)
      {
        int registerIndex = GetFreeFloatRegister(stackSize);

        instructions.Add(new LLI_MovImmToRegister((byte)registerIndex, BitConverter.GetBytes((value as CConstFloatValue).value)));

        registers[registerIndex] = value;
        value.hasPosition = true;
        value.position = Position.Register(registerIndex);

        return (byte)registerIndex;
      }
      else
      {
        if (!value.hasPosition)
          throw new Exception("Internal Compiler Error: Cannot move value without position.");

        if (value.position.type == PositionType.InRegister)
        {
          if (registers[value.position.registerIndex] != value)
          {
            string actualValue = registers[value.position.registerIndex]?.ToString() ?? "{null}";
            instructions.Add(new LLI_Comment_PseudoInstruction($"!!! Compiler Error: r:{value.position.registerIndex} contains {actualValue} but {value} claimed to inhabit it."));
            throw new Exception($"Internal Compiler Error. r:{value.position.registerIndex} contains {actualValue} but {value} claimed to inhabit it.");
          }

          MarkValueAsTouched(value);

          return (byte)value.position.registerIndex;
        }
        else if (value.position.type == PositionType.OnStack)
        {
          var registerIndex = (!(value.type is BuiltInCType) || !(value.type as BuiltInCType).IsFloat()) ? GetFreeIntegerRegister(stackSize) : GetFreeFloatRegister(stackSize);

          instructions.Add(new LLI_MovStackOffsetToRegister(stackSize, value.position.stackOffsetForward, (byte)registerIndex));

          // Truncate smaller types.
          if (value.type.GetSize() < 8)
            TruncateValueInRegister(value);

          MarkValueAsPosition(value, Position.Register(registerIndex), stackSize, true);

          return (byte)registerIndex;
        }
        else
        {
          var registerIndex = (!(value.type is BuiltInCType) || !(value.type as BuiltInCType).IsFloat()) ? GetFreeIntegerRegister(stackSize) : GetFreeFloatRegister(stackSize);

          using (LockRegister(registerIndex))
            MoveValueToPosition(value, Position.Register(registerIndex), stackSize, true);

          MarkValueAsPosition(value, Position.Register(registerIndex), stackSize, true);

          return (byte)registerIndex;
        }
      }
    }

    internal void MoveValueToHome(CNamedValue value, SharedValue<long> stackSize)
    {
      if (!value.hasPosition)
        throw new Exception($"Internal Compiler Error. Value {value} doesn't have a position.");

      if (value.hasHomePosition && value.position.type == PositionType.OnStack && value.position.stackOffsetForward != value.homePosition.stackOffsetForward)
        throw new Exception($"Internal Compiler Error. Value {value} somehow made it to an invalid stack position.");

      if (value.position.type != PositionType.InRegister)
        return;

      MoveRegisterValueToHome(value, stackSize);
    }

    public void CopyRegisterToPosition(int registerIndex, long size, Position position, SharedValue<long> stackSize)
    {
      if (position.type == PositionType.InRegister)
      {
        if (registerIndex == position.registerIndex)
          return;

        instructions.Add(new LLI_MovRegisterToRegister(registerIndex, position.registerIndex));
      }
      else
      {
        if (position.type == PositionType.OnStack)
        {
          if (size == 8)
            instructions.Add(new LLI_MovRegisterToStackOffset(registerIndex, stackSize, position.stackOffsetForward));
          else
            instructions.Add(new LLI_MovRegisterToStackOffset_NBytes(registerIndex, stackSize, position.stackOffsetForward, (int)size));
        }
        else
        {
          using (var _lock = new RegisterLock(registerIndex, this))
          {
            var ptr_register = GetFreeIntegerRegister(stackSize);

            if (position.type == PositionType.GlobalStackOffset)
            {
              instructions.Add(new LLI_MovRuntimeParamToRegister(LLI_RuntimeParam.LLS_RP_STACK_BASE_PTR, (byte)ptr_register));
              instructions.Add(new LLI_AddImm(ptr_register, BitConverter.GetBytes(position.globalStackBaseOffset)));
            }
            else if (position.type == PositionType.CodeBaseOffset)
            {
              instructions.Add(new LLI_MovRuntimeParamToRegister(LLI_RuntimeParam.LLS_RP_CODE_BASE_PTR, (byte)ptr_register));
              instructions.Add(new LLI_AddImmInstructionOffset(ptr_register, position.codeBaseOffset));
            }
            else
            {
              throw new NotImplementedException();
            }

            if (size == 8)
              instructions.Add(new LLI_MovRegisterToPtrInRegister(ptr_register, registerIndex));
            else
              instructions.Add(new LLI_MovRegisterToPtrInRegister_NBytes(ptr_register, registerIndex, (int)size));
          }
        }
      }
    }

    public void CopyPositionToRegister(int registerIndex, long size, Position position, SharedValue<long> stackSize)
    {
      if (position.type == PositionType.InRegister)
      {
        if (registerIndex == position.registerIndex)
          return;

        instructions.Add(new LLI_MovRegisterToRegister(registerIndex, position.registerIndex));
      }
      else
      {
        if (position.type == PositionType.OnStack)
        {
          instructions.Add(new LLI_MovStackOffsetToRegister(stackSize, position.stackOffsetForward, registerIndex));
          
          if (size != 8)
            TruncateRegister(registerIndex, size);
        }
        else
        {
          using (var _lock = new RegisterLock(registerIndex, this))
          {
            var ptr_register = GetFreeIntegerRegister(stackSize);

            if (position.type == PositionType.GlobalStackOffset)
            {
              instructions.Add(new LLI_MovRuntimeParamToRegister(LLI_RuntimeParam.LLS_RP_STACK_BASE_PTR, (byte)ptr_register));
              instructions.Add(new LLI_AddImm(ptr_register, BitConverter.GetBytes(position.globalStackBaseOffset)));
            }
            else if (position.type == PositionType.CodeBaseOffset)
            {
              instructions.Add(new LLI_MovRuntimeParamToRegister(LLI_RuntimeParam.LLS_RP_CODE_BASE_PTR, (byte)ptr_register));
              instructions.Add(new LLI_AddImmInstructionOffset(ptr_register, position.codeBaseOffset));
            }
            else
            {
              throw new NotImplementedException();
            }

            instructions.Add(new LLI_MovFromPtrInRegisterToRegister(ptr_register, registerIndex));

            if (size != 8)
              TruncateRegister(registerIndex, size);
          }
        }
      }
    }

    public void CopyPositionToRegisterInplace(int registerIndex, long size, Position position, SharedValue<long> stackSize)
    {
      if (position.type == PositionType.InRegister)
      {
        if (registerIndex == position.registerIndex)
          return;

        instructions.Add(new LLI_MovRegisterToRegister(registerIndex, position.registerIndex));
      }
      else
      {
        if (position.type == PositionType.OnStack)
        {
          instructions.Add(new LLI_MovStackOffsetToRegister(stackSize, position.stackOffsetForward, registerIndex));

          if (size != 8)
            TruncateRegister(registerIndex, size);
        }
        else
        {
          if (position.type == PositionType.GlobalStackOffset)
          {
            instructions.Add(new LLI_MovRuntimeParamToRegister(LLI_RuntimeParam.LLS_RP_STACK_BASE_PTR, (byte)registerIndex));
            instructions.Add(new LLI_AddImm(registerIndex, BitConverter.GetBytes(position.globalStackBaseOffset)));
          }
          else if (position.type == PositionType.CodeBaseOffset)
          {
            instructions.Add(new LLI_MovRuntimeParamToRegister(LLI_RuntimeParam.LLS_RP_CODE_BASE_PTR, (byte)registerIndex));
            instructions.Add(new LLI_AddImmInstructionOffset(registerIndex, position.codeBaseOffset));
          }
          else
          {
            throw new NotImplementedException();
          }

          instructions.Add(new LLI_MovFromPtrInRegisterToRegister(registerIndex, registerIndex));

          if (size != 8)
            TruncateRegister(registerIndex, size);
        }
      }
    }

    public void CopyValueToPosition(CValue sourceValue, Position position, SharedValue<long> stackSize)
    {
      if (Compiler.DetailedIntermediateOutput)
        instructions.Add(new LLI_Comment_PseudoInstruction($"Copying Value '{sourceValue}' to {position}."));

      var size = sourceValue.type.GetSize();

      if (sourceValue is CConstIntValue)
      {
        var bytes = BitConverter.GetBytes((sourceValue as CConstIntValue).uvalue);

        if (position.type == PositionType.InRegister)
        {
          instructions.Add(new LLI_MovImmToRegister(position.registerIndex, bytes));
        }
        else
        {
          var register = GetFreeIntegerRegister(stackSize);

          instructions.Add(new LLI_MovImmToRegister(register, bytes));

          CopyRegisterToPosition(register, size, position, stackSize);
        }
      }
      else if (sourceValue is CConstFloatValue)
      {
        var bytes = BitConverter.GetBytes((sourceValue as CConstFloatValue).value);

        if (position.type == PositionType.InRegister)
        {
          instructions.Add(new LLI_MovImmToRegister(position.registerIndex, bytes));
        }
        else
        {
          var register = GetFreeFloatRegister(stackSize);

          instructions.Add(new LLI_MovImmToRegister(register, bytes));

          CopyRegisterToPosition(register, size, position, stackSize);
        }
      }
      else if (sourceValue is CNullValue)
      {
        var bytes = BitConverter.GetBytes((ulong)0);

        if (position.type == PositionType.InRegister)
        {
          instructions.Add(new LLI_MovImmToRegister(position.registerIndex, bytes));
        }
        else
        {
          var register = GetFreeIntegerRegister(stackSize);

          instructions.Add(new LLI_MovImmToRegister(register, bytes));

          CopyRegisterToPosition(register, size, position, stackSize);
        }
      }
      else
      {
        if (!sourceValue.hasPosition)
          throw new Exception("Internal Compiler Error.");

        if (sourceValue.position.type == PositionType.InRegister)
        {
          CopyRegisterToPosition(sourceValue.position.registerIndex, size, position, stackSize);
        }
        else if (sourceValue.position.type == PositionType.OnStack)
        {
          if (position.type == PositionType.InRegister)
          {
            instructions.Add(new LLI_MovStackOffsetToRegister(stackSize, sourceValue.position.stackOffsetForward, position.registerIndex));

            TruncateRegister(position.registerIndex, size);
          }
          else if (position.type == PositionType.OnStack)
          {
            if (size == 8)
              instructions.Add(new LLI_MovStackOffsetToStackOffset(stackSize, sourceValue.position.stackOffsetForward, stackSize, position.stackOffsetForward));
            else
              instructions.Add(new LLI_MovStackOffsetToStackOffset_NBytes(stackSize, sourceValue.position.stackOffsetForward, stackSize, position.stackOffsetForward, (byte)size));
          }
          else
          {
            int register = GetFreeIntegerRegister(stackSize);

            instructions.Add(new LLI_MovStackOffsetToRegister(stackSize, sourceValue.position.stackOffsetForward, register));

            CopyRegisterToPosition(register, size, position, stackSize);
          }
        }
        else
        {
          // Store inplace.
          if (position.type == PositionType.InRegister && !(sourceValue.type is BuiltInCType && (sourceValue.type as BuiltInCType).IsFloat()))
          {
            CopyPositionToRegisterInplace(position.registerIndex, size, sourceValue.position, stackSize);
          }
          else
          {
            var temp_register = GetFreeIntegerRegister(stackSize);

            using (LockRegister(temp_register))
            {
              CopyPositionToRegister(temp_register, size, sourceValue.position, stackSize);
              CopyRegisterToPosition(temp_register, size, position, stackSize);
            }
          }
        }
      }
    }

    public int CopyValueToAnyRegister(CValue value, SharedValue<long> stackSize)
    {
      int registerIndex = value.type is BuiltInCType && (value.type as BuiltInCType).IsFloat() ? GetFreeFloatRegister(stackSize) : GetFreeIntegerRegister(stackSize);

      CopyValueToPosition(value, Position.Register(registerIndex), stackSize);

      return registerIndex;
    }
  }
}
