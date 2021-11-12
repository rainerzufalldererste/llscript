using System;
using System.Collections.Generic;
using System.Linq;

namespace llsc
{
  public class ByteCodeState
  {
    public List<byte> byteCode = new List<byte>();
    public List<LLInstruction> instructions = new List<LLInstruction>();

    public CValue[] registers = new CValue[Compiler.IntegerRegisters + Compiler.FloatRegisters];
    public bool[] registerLocked = new bool[Compiler.IntegerRegisters + Compiler.FloatRegisters];

    public class RegisterLock : IDisposable
    {
      int register;
      ByteCodeState byteCodeState;

      public RegisterLock(int register, ByteCodeState byteCodeState)
      {
        this.register = register;
        this.byteCodeState = byteCodeState;

        this.byteCodeState.registerLocked[register] = true;
      }

      public void Dispose()
      {
        byteCodeState.registerLocked[register] = false;
      }
    }

    public RegisterLock LockRegister(int register)
    {
      return new RegisterLock(register, this);
    }

    private void DumpValue(CValue value)
    {
      value.hasPosition = false;

      if (Compiler.DetailedIntermediateOutput)
        instructions.Add(new LLI_Comment_PseudoInstruction("Dumping Value: " + value));
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
      int oldestValue = 0;

      // With Home.

      for (int i = Compiler.IntegerRegisters - 1; i >= 0; i--)
      {
        if (registerLocked[i])
          continue;

        if (registers[i] is CNamedValue && (registers[i] as CNamedValue).hasStackOffset)
        {
          if (!(registers[i] as CNamedValue).modifiedSinceLastHome)
          {
            registers[i].position = Position.StackOffset((registers[i] as CNamedValue).homeStackOffset);
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
        if (registers[i].remainingReferences == 0 && !registerLocked[i])
        {
          DumpValue(registers[i]);
          registers[i] = null;
          return i;
        }

      int oldestIndex = -1;
      int oldestValue = 0;

      // With Home.

      for (int i = Compiler.IntegerRegisters + Compiler.FloatRegisters - 1; i >= Compiler.IntegerRegisters; i--)
      {
        if (registerLocked[i])
          continue;

        if (registers[i] != null && registers[i] is CNamedValue && (registers[i] as CNamedValue).hasStackOffset)
        {
          if (!(registers[i] as CNamedValue).modifiedSinceLastHome)
          {
            registers[i].position = Position.StackOffset((registers[i] as CNamedValue).homeStackOffset);
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
        if (registers[i] != null && registers[i] is CNamedValue && (registers[i] as CNamedValue).hasStackOffset && !registerLocked[i])
          if (!(registers[i] as CNamedValue).modifiedSinceLastHome)
          {
            registers[i].position = Position.StackOffset((registers[i] as CNamedValue).homeStackOffset);
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
        if (registers[i] != null && registers[i] is CNamedValue && (registers[i] as CNamedValue).hasStackOffset && !registerLocked[i])
          if (!(registers[i] as CNamedValue).modifiedSinceLastHome)
          {
            registers[i].position = Position.StackOffset((registers[i] as CNamedValue).homeStackOffset);
            registers[i] = null;
            return i;
          }

      return -1;
    }

    public void FreeRegister(int register, SharedValue<long> stackSize)
    {
      if (registers[register] == null || registers[register].remainingReferences == 0)
        return;

      if (registers[register] is CNamedValue && (registers[register] as CNamedValue).hasStackOffset && !(registers[register] as CNamedValue).modifiedSinceLastHome)
      {
        registers[register].position = Position.StackOffset((registers[register] as CNamedValue).homeStackOffset);
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

    private void FreeUsedRegister(int register, SharedValue<long> stackSize)
    {
      if (registers[register] == null)
        throw new Exception("Internal Compiler Error: No Free Register Found.");

      var size = registers[register].type.GetSize();

      if (registers[register] is CNamedValue)
      {
        var namedValue = registers[register] as CNamedValue;

        if (namedValue.hasStackOffset)
        {
          if (size == 8)
            instructions.Add(new LLI_MovRegisterToStackOffset(register, stackSize, namedValue.homeStackOffset));
          else
            instructions.Add(new LLI_MovRegisterToStackOffset_NBytes(register, stackSize, namedValue.homeStackOffset, (byte)size));

          namedValue.position.inRegister = false;
          namedValue.position.stackOffsetForward = namedValue.homeStackOffset;
          namedValue.modifiedSinceLastHome = false;
          registers[register] = null;

          instructions.Add(new LLI_Location_PseudoInstruction(namedValue, stackSize, this));
        }
        else
        {
          if (size == 8)
            instructions.Add(new LLI_MovRegisterToStackOffset(register, stackSize, stackSize.Value));
          else
            instructions.Add(new LLI_MovRegisterToStackOffset_NBytes(register, stackSize, stackSize.Value, (byte)size));

          namedValue.hasStackOffset = true;
          namedValue.homeStackOffset = stackSize.Value;

          namedValue.position.inRegister = false;
          namedValue.position.stackOffsetForward = namedValue.homeStackOffset;
          namedValue.modifiedSinceLastHome = false;
          registers[register] = null;

          stackSize.Value += size;

          instructions.Add(new LLI_Location_PseudoInstruction(namedValue, stackSize, this));
        }
      }
      else
      {
        if (size == 8)
          instructions.Add(new LLI_MovRegisterToStackOffset(register, stackSize, stackSize.Value));
        else
          instructions.Add(new LLI_MovRegisterToStackOffset_NBytes(register, stackSize, stackSize.Value, (byte)size));

        registers[register].position.inRegister = false;
        registers[register].position.stackOffsetForward = stackSize.Value;
        instructions.Add(new LLI_Location_PseudoInstruction(registers[register], stackSize, this));

        registers[register] = null;

        stackSize.Value += size;
      }
    }

    internal void TruncateValueInRegister(CValue value)
    {
      if (!(value.type is BuiltInCType))
        throw new Exception($"Unexpected Type: Trying to truncate {value}. Exected builtin type.");

      if (!value.hasPosition || !value.position.inRegister)
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

    public void CompileInstructionsToBytecode()
    {
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

    public void MoveValueToPosition(CValue sourceValue, Position targetPosition, SharedValue<long> stackSize, bool addReference)
    {
      if (Compiler.DetailedIntermediateOutput)
        instructions.Add(new LLI_Comment_PseudoInstruction($"Moving Value '{sourceValue}' to {targetPosition}."));

      // TODO: Work out the reference count...

      if (targetPosition.inRegister && sourceValue.type.GetSize() > 8)
        throw new Exception($"Internal Compiler Error: Value '{sourceValue}' cannot be moved to a register, because it's > 8 bytes.");

      if (sourceValue is CConstIntValue)
      {
        if ((sourceValue.hasPosition && targetPosition.inRegister && sourceValue.position.inRegister && sourceValue.position.registerIndex == targetPosition.registerIndex) || (targetPosition.inRegister && registers[targetPosition.registerIndex] != null && registers[targetPosition.registerIndex] is CConstIntValue && (registers[targetPosition.registerIndex] as CConstIntValue).uvalue == (sourceValue as CConstIntValue).uvalue))
        {
          if (addReference)
            registers[targetPosition.registerIndex].remainingReferences++;

          registers[targetPosition.registerIndex].lastTouchedInstructionCount = instructions.Count;
        }
        else if (targetPosition.inRegister)
        {
          FreeRegister(targetPosition.registerIndex, stackSize);
          instructions.Add(new LLI_MovImmToRegister(targetPosition.registerIndex, BitConverter.GetBytes((sourceValue as CConstIntValue).uvalue)));
          registers[targetPosition.registerIndex] = sourceValue;

          sourceValue.hasPosition = true;
          sourceValue.position.inRegister = true;
          sourceValue.position.registerIndex = targetPosition.registerIndex;

          if (addReference)
            sourceValue.remainingReferences++;

          sourceValue.lastTouchedInstructionCount = instructions.Count();
        }
        else
        {
          int registerIndex = -1;

          if (!(sourceValue.type as BuiltInCType).IsFloat())
          {
            for (int j = 0; j < Compiler.IntegerRegisters; j++)
            {
              if (registers[j] is CConstIntValue && (registers[j] as CConstIntValue).uvalue == (sourceValue as CConstIntValue).uvalue)
              {
                registerIndex = j;
                break;
              }
            }

          }
          else
          {
            throw new NotImplementedException();
          }

          if (registerIndex == -1)
          {
            registerIndex = GetFreeIntegerRegister(stackSize);
            instructions.Add(new LLI_MovImmToRegister(registerIndex, BitConverter.GetBytes((sourceValue as CConstIntValue).uvalue)));
          }

          var bytes = sourceValue.type.GetSize();

          if (bytes == 8)
            instructions.Add(new LLI_MovRegisterToStackOffset(registerIndex, stackSize, targetPosition.stackOffsetForward));
          else
            instructions.Add(new LLI_MovRegisterToStackOffset_NBytes(registerIndex, stackSize, targetPosition.stackOffsetForward, (int)bytes));

          sourceValue.hasPosition = true;
          sourceValue.position.inRegister = false;
          sourceValue.position.stackOffsetForward = targetPosition.stackOffsetForward;

          if (addReference)
            sourceValue.remainingReferences++;
        }
      }
      else if (sourceValue is CConstFloatValue)
      {
        throw new NotImplementedException();
      }
      else
      {
        if (!sourceValue.hasPosition)
          throw new Exception("Internal Compiler Error: Move To Position, but source value has no origin and is not constant.");

        /* if (sourceValue.position.inRegister == targetPosition.inRegister && ((targetPosition.inRegister && sourceValue.position.registerIndex == targetPosition.registerIndex) || (!targetPosition.inRegister && sourceValue.position.stackOffsetForward == targetPosition.stackOffsetForward)))
        {
          // Everything already set up.
          if (targetPosition.inRegister)
          {
            registers[targetPosition.registerIndex].remainingReferences++;
            registers[targetPosition.registerIndex].lastTouchedInstructionCount = instructions.Count;
          }
          else
          {
            if (addReference)
              sourceValue.remainingReferences++;

            sourceValue.lastTouchedInstructionCount = instructions.Count;
          }
        }
        else */
        if (targetPosition.inRegister)
        {
          if (sourceValue.position.inRegister)
          {
            instructions.Add(new LLI_MovRegisterToRegister(sourceValue.position.registerIndex, targetPosition.registerIndex));

            if (addReference)
              sourceValue.remainingReferences++;

            sourceValue.lastTouchedInstructionCount = instructions.Count;
          }
          else
          {
            var bytes = sourceValue.type.GetSize();

            instructions.Add(new LLI_MovStackOffsetToRegister(stackSize, sourceValue.position.stackOffsetForward, targetPosition.registerIndex));

            if (bytes != 8)
              instructions.Add(new LLI_AndImm(targetPosition.registerIndex, BitConverter.GetBytes(((ulong)1 << (int)(bytes * 8)) - 1)));

            if (addReference)
              sourceValue.remainingReferences++;

            sourceValue.lastTouchedInstructionCount = instructions.Count;
          }
        }
        else
        {
          if (sourceValue.position.inRegister)
          {
            var bytes = sourceValue.type.GetSize();

            if (bytes == 8)
              instructions.Add(new LLI_MovRegisterToStackOffset(sourceValue.position.registerIndex, stackSize, targetPosition.stackOffsetForward));
            else
              instructions.Add(new LLI_MovRegisterToStackOffset_NBytes(sourceValue.position.registerIndex, stackSize, targetPosition.stackOffsetForward, (int)bytes));

            if (addReference)
              sourceValue.remainingReferences++;

            sourceValue.lastTouchedInstructionCount = instructions.Count;
          }
          else
          {
            var bytes = sourceValue.type.GetSize();

            if (bytes == 8)
            {
              instructions.Add(new LLI_MovStackOffsetToStackOffset(stackSize, sourceValue.position.stackOffsetForward, stackSize, targetPosition.stackOffsetForward));
            }
            else
            {
              long offset = 0;

              while (bytes > 0)
              {
                var copyBytes = Math.Min(byte.MaxValue, bytes);

                instructions.Add(new LLI_MovStackOffsetToStackOffset_NBytes(stackSize, sourceValue.position.stackOffsetForward + offset, stackSize, targetPosition.stackOffsetForward + offset, (byte)copyBytes));

                bytes -= copyBytes;
                offset += copyBytes;
              }
            }

            if (addReference)
              sourceValue.remainingReferences++;

            sourceValue.lastTouchedInstructionCount = instructions.Count;
          }
        }
      }

      sourceValue.position = targetPosition;
      instructions.Add(new LLI_Location_PseudoInstruction(sourceValue, stackSize, this));
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

      if (sourceValue.type is ArrayCType && targetType is PtrCType)
      {
        if (targetPosition.inRegister)
        {
          if (!sourceValue.hasPosition)
            throw new Exception($"Internal Compiler Error: sourceValue {sourceValue} doesn't have a position.");

          FreeRegister(targetPosition.registerIndex, stackSize);
          instructions.Add(new LLI_LoadEffectiveAddress_StackOffsetToRegister(stackSize, sourceValue.position.stackOffsetForward, targetPosition.registerIndex));

          sourceValue.lastTouchedInstructionCount = instructions.Count;
          registers[targetPosition.registerIndex] = sourceValue;
        }
        else
        {
          int triviallyFreeRegister = GetTriviallyFreeIntegerRegister();

          if (triviallyFreeRegister == -1)
          {
            instructions.Add(new LLI_PushRegister(0));
            instructions.Add(new LLI_LoadEffectiveAddress_StackOffsetToRegister(stackSize, sourceValue.position.stackOffsetForward + 8, 0));
            instructions.Add(new LLI_PopRegister(0));
          }
          else
          {
            instructions.Add(new LLI_LoadEffectiveAddress_StackOffsetToRegister(stackSize, sourceValue.position.stackOffsetForward, triviallyFreeRegister));
            instructions.Add(new LLI_MovRegisterToStackOffset(triviallyFreeRegister, stackSize, sourceValue.position.stackOffsetForward));

            registers[triviallyFreeRegister] = sourceValue;
          }
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
            if (targetPosition.inRegister)
            {
              CopyValueToPosition(sourceValue, targetPosition, stackSize);
              TruncateRegister(targetPosition.registerIndex, targetType.GetSize());
            }
            else
            {
              var tempPosition = Position.Register(GetFreeIntegerRegister(stackSize));
              CopyValueToPosition(sourceValue, tempPosition, stackSize);
              TruncateRegister(tempPosition.registerIndex, targetType.GetSize());
              CopyValueToPosition(new CValue(sourceValue.file, sourceValue.line, targetType, false, true), targetPosition, stackSize);
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
        int registerIndex = this.GetFreeIntegerRegister(stackSize);

        instructions.Add(new LLI_MovImmToRegister((byte)registerIndex, BitConverter.GetBytes((value as CConstIntValue).uvalue)));

        registers[registerIndex] = value;
        value.hasPosition = true;
        value.position.inRegister = true;
        value.position.registerIndex = registerIndex;

        return (byte)registerIndex;
      }
      else if (value is CConstFloatValue)
      {
        int registerIndex = this.GetFreeFloatRegister(stackSize);

        instructions.Add(new LLI_MovImmToRegister((byte)registerIndex, BitConverter.GetBytes((value as CConstFloatValue).value)));

        registers[registerIndex] = value;
        value.hasPosition = true;
        value.position.inRegister = true;
        value.position.registerIndex = registerIndex;

        return (byte)registerIndex;
      }
      else
      {
        if (!value.hasPosition)
          throw new Exception("Internal Compiler Error: Cannot move value without position.");

        if (value.position.inRegister)
        {
          if (registers[value.position.registerIndex] != value)
          {
            string expectedValue = registers[value.position.registerIndex]?.ToString() ?? "{null}";
            instructions.Add(new LLI_Comment_PseudoInstruction($"!!! Compiler Error: r:{value.position.registerIndex} is {expectedValue} but was expected to be {value}."));
            throw new Exception("Internal Compiler Error.");
          }

          return (byte)value.position.registerIndex;
        }

        var registerIndex = (!(value.type is BuiltInCType) || !(value.type as BuiltInCType).IsFloat()) ? GetFreeIntegerRegister(stackSize) : GetFreeFloatRegister(stackSize);

        instructions.Add(new LLI_MovStackOffsetToRegister(stackSize, value.position.stackOffsetForward, (byte)registerIndex));

        registers[registerIndex] = value;
        value.position.inRegister = true;
        value.position.registerIndex = registerIndex;

        // Truncate smaller types.
        if (value.type.GetSize() < 8)
          TruncateValueInRegister(value);

        instructions.Add(new LLI_Location_PseudoInstruction(value, stackSize, this));

        return (byte)registerIndex;
      }
    }

    internal void MoveValueToHome(CNamedValue value, SharedValue<long> stackSize)
    {
      if (!value.hasPosition)
        throw new Exception("Internal Compiler Error");

      if (value.hasStackOffset && value.hasPosition && !value.position.inRegister && value.position.stackOffsetForward != value.homeStackOffset)
        throw new Exception("Internal Compiler Error");

      if (!value.position.inRegister)
        return;

      if (!value.hasStackOffset)
      {
        value.hasStackOffset = true;
        value.homeStackOffset = stackSize.Value;
        stackSize.Value += value.type.GetSize();
        value.modifiedSinceLastHome = true;
      }

      if (!value.modifiedSinceLastHome)
      {
        value.position = Position.StackOffset(value.homeStackOffset);
        instructions.Add(new LLI_Location_PseudoInstruction(value, stackSize, this));
        return;
      }

      Position position = Position.StackOffset(value.homeStackOffset);

      MoveValueToPosition(value, position, stackSize, false);

      value.modifiedSinceLastHome = false;
    }

    public void CopyValueToPosition(CValue sourceValue, Position position, SharedValue<long> stackSize)
    {
      if (Compiler.DetailedIntermediateOutput)
        instructions.Add(new LLI_Comment_PseudoInstruction($"Copying Value '{sourceValue}' to {position}."));

      var size = sourceValue.type.GetSize();

      if (sourceValue is CConstIntValue)
      {
        var bytes = BitConverter.GetBytes((sourceValue as CConstIntValue).uvalue);

        if (position.inRegister)
        {
          instructions.Add(new LLI_MovImmToRegister(position.registerIndex, bytes));
        }
        else
        {
          var register = GetFreeIntegerRegister(stackSize);

          instructions.Add(new LLI_MovImmToRegister(register, bytes));

          if (size == 8)
            instructions.Add(new LLI_MovRegisterToStackOffset(register, stackSize, position.stackOffsetForward));
          else
            instructions.Add(new LLI_MovRegisterToStackOffset_NBytes(register, stackSize, position.stackOffsetForward, (int)size));
        }
      }
      else if (sourceValue is CConstFloatValue)
      {
        var bytes = BitConverter.GetBytes((sourceValue as CConstFloatValue).value);

        if (position.inRegister)
        {
          instructions.Add(new LLI_MovImmToRegister(position.registerIndex, bytes));
        }
        else
        {
          var register = GetFreeFloatRegister(stackSize);

          instructions.Add(new LLI_MovImmToRegister(register, bytes));

          if (size == 8)
            instructions.Add(new LLI_MovRegisterToStackOffset(register, stackSize, position.stackOffsetForward));
          else
            instructions.Add(new LLI_MovRegisterToStackOffset_NBytes(register, stackSize, position.stackOffsetForward, (int)size));
        }
      }
      else if (sourceValue is CNullValue)
      {
        var bytes = BitConverter.GetBytes((ulong)0);

        if (position.inRegister)
        {
          instructions.Add(new LLI_MovImmToRegister(position.registerIndex, bytes));
        }
        else
        {
          var register = GetFreeIntegerRegister(stackSize);

          instructions.Add(new LLI_MovImmToRegister(register, bytes));
          instructions.Add(new LLI_MovRegisterToStackOffset(register, stackSize, position.stackOffsetForward));
        }
      }
      else
      {
        if (!sourceValue.hasPosition)
          throw new Exception("Internal Compiler Error.");

        if (sourceValue.position.inRegister)
        {
          if (position.inRegister)
          {
            if (sourceValue.position.registerIndex == position.registerIndex)
              return;

            instructions.Add(new LLI_MovRegisterToRegister(sourceValue.position.registerIndex, position.registerIndex));
          }
          else
          {
            if (size == 8)
              instructions.Add(new LLI_MovRegisterToStackOffset(sourceValue.position.registerIndex, stackSize, position.stackOffsetForward));
            else
              instructions.Add(new LLI_MovRegisterToStackOffset_NBytes(sourceValue.position.registerIndex, stackSize, position.stackOffsetForward, (int)size));
          }
        }
        else
        {
          if (position.inRegister)
          {
            instructions.Add(new LLI_MovStackOffsetToRegister(stackSize, sourceValue.position.stackOffsetForward, position.registerIndex));

            if (size != 8)
              instructions.Add(new LLI_AndImm(position.registerIndex, BitConverter.GetBytes(((ulong)1 << (int)(size * 8)) - 1)));
          }
          else
          {
            if (size == 8)
              instructions.Add(new LLI_MovStackOffsetToStackOffset(stackSize, sourceValue.position.stackOffsetForward, stackSize, position.stackOffsetForward));
            else
              instructions.Add(new LLI_MovStackOffsetToStackOffset_NBytes(stackSize, sourceValue.position.stackOffsetForward, stackSize, position.stackOffsetForward, (byte)size));
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
