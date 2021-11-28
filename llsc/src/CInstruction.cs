using System;
using System.Collections.Generic;
using System.Linq;

namespace llsc
{
  public abstract class CInstruction
  {
    public readonly string file;
    public readonly int line;

    public CInstruction(string file, int line)
    {
      this.file = file;
      this.line = line;
    }

    public abstract void GetLLInstructions(ref ByteCodeState byteCodeState);
  }

  public class CInstruction_CustomAction : CInstruction
  {
    readonly Action<ByteCodeState> action;

    public CInstruction_CustomAction(Action<ByteCodeState> action, string file, int line) : base(file, line) => this.action = action;

    public override void GetLLInstructions(ref ByteCodeState byteCodeState)
    {
      action(byteCodeState);
    }
  }

  public class CInstruction_Label : CInstruction
  {
    readonly LLI_Label_PseudoInstruction label;

    public CInstruction_Label(LLI_Label_PseudoInstruction label, string file, int line) : base(file, line) => this.label = label;

    public override void GetLLInstructions(ref ByteCodeState byteCodeState)
    {
      if (byteCodeState.instructions.Contains(label))
        throw new Exception("Internal Compiler Error: Label already contained in instructions.");

      byteCodeState.instructions.Add(label);
    }
  }

  public class CInstruction_GotoLabel : CInstruction
  {
    readonly LLI_Label_PseudoInstruction label;

    public CInstruction_GotoLabel(LLI_Label_PseudoInstruction label, string file, int line) : base(file, line) => this.label = label;

    public override void GetLLInstructions(ref ByteCodeState byteCodeState)
    {
      byteCodeState.instructions.Add(new LLI_JumpToLabel(label));
    }
  }

  public class CInstruction_BeginFunction : CInstruction
  {
    private readonly CFunction function;

    public CInstruction_BeginFunction(CFunction function) : base(function.file, function.line)
    {
      this.function = function;
    }

    public override void GetLLInstructions(ref ByteCodeState byteCodeState)
    {
      for (int i = 0; i < byteCodeState.registers.Length; i++)
      {
        byteCodeState.registers[i] = null;
        byteCodeState.registerLocked[i] = false;
      }

      function.ResetRegisterPositions();

      byteCodeState.instructions.Add(new LLI_Comment_PseudoInstruction("Start of Function: " + function));

      byteCodeState.instructions.Add(function.functionStartLabel);

      for (int i = 0; i < byteCodeState.registers.Length; i++)
        byteCodeState.registers[i] = null;

      byteCodeState.instructions.Add(new LLI_StackIncrementImm(function.minStackSize, 0));

      foreach (var parameter in function.parameters)
        byteCodeState.MarkValueAsPosition(parameter.value, parameter.value.position, function.minStackSize, false);

      byteCodeState.instructions.Add(new LLI_Location_PseudoInstruction(new CValue(file, line, new PtrCType(VoidCType.Instance), true) { description = $"Instruction pointer of the calling function", hasPosition = true, position = Position.StackOffset(0) }, function.minStackSize, byteCodeState));
    }
  }

  public class CInstruction_BeginGlobalScope : CInstruction
  {
    private readonly SharedValue<long> stackSize;

    public CInstruction_BeginGlobalScope(SharedValue<long> stackSize, string file, int line) : base(file, line)
    {
      this.stackSize = stackSize;
    }

    public override void GetLLInstructions(ref ByteCodeState byteCodeState)
    {
      for (int i = 0; i < byteCodeState.registers.Length; i++)
        byteCodeState.registers[i] = null;

      byteCodeState.instructions.Add(new LLI_StackIncrementImm(stackSize, 0));
    }
  }

  public class CInstruction_EndFunction : CInstruction
  {
    private readonly CFunction function;

    public CInstruction_EndFunction(CFunction function) : base(function.file, function.line)
    {
      this.function = function;
    }

    public override void GetLLInstructions(ref ByteCodeState byteCodeState)
    {
      byteCodeState.instructions.Add(function.functionEndLabel);

      for (int i = 0; i < byteCodeState.registers.Length; i++)
        byteCodeState.registers[i] = null;

      foreach (var parameter in function.parameters)
        if (parameter.value.position.type == PositionType.InRegister)
          byteCodeState.registers[parameter.value.position.registerIndex] = parameter.value;

      byteCodeState.instructions.Add(new LLI_StackDecrementImm(function.scope.maxRequiredStackSpace, 0));

      byteCodeState.instructions.Add(new LLI_Location_PseudoInstruction(new CValue(file, line, new PtrCType(VoidCType.Instance), true) { description = $"Instruction pointer of the calling function", hasPosition = true, position = Position.StackOffset(0) }, new SharedValue<long>(0), byteCodeState));

      byteCodeState.instructions.Add(new LLI_Return());

      byteCodeState.instructions.Add(new LLI_Comment_PseudoInstruction("End of Function: " + function));

      if (function.OnFunctionEnd != null)
        function.OnFunctionEnd();
    }
  }

  public class CInstruction_EndGlobalScope : CInstruction
  {
    private readonly SharedValue<long> stackSize;

    public CInstruction_EndGlobalScope(SharedValue<long> stackSize, string file, int line) : base(file, line)
    {
      this.stackSize = stackSize;
    }

    public override void GetLLInstructions(ref ByteCodeState byteCodeState)
    {
      for (int i = 0; i < byteCodeState.registers.Length; i++)
        byteCodeState.registers[i] = null;

      byteCodeState.instructions.Add(new LLI_StackDecrementImm(stackSize, 0));
      byteCodeState.instructions.Add(new LLI_Exit());
    }
  }

  public class CInstruction_InitializeArray : CInstruction
  {
    private readonly CValue value;
    private readonly byte[] data;
    private readonly SharedValue<long> stackSize;

    public CInstruction_InitializeArray(CValue value, byte[] data, string file, int line, SharedValue<long> stackSize) : base(file, line)
    {
      this.data = data;
      this.value = value;
      this.stackSize = stackSize;

      if (value.hasPosition && value.position.type == PositionType.InRegister)
        throw new Exception("Internal Compiler Error.");
    }

    public override void GetLLInstructions(ref ByteCodeState byteCodeState)
    {
      if (!value.hasPosition)
      {
        value.hasPosition = true;

        if (value.type.isConst || (value.type as ArrayCType).type.isConst || (value is CNamedValue && (value as CNamedValue).isStatic && Compiler.Assumptions.ByteCodeMutable))
        {
          value.position = Position.CodeBaseOffset(value, data, file, line);
        }
        else if (value is CNamedValue && (value as CNamedValue).isStatic)
        {
          value.position = Position.GlobalStackBaseOffset(Compiler.GlobalScope.maxRequiredStackSpace.Value);

          Compiler.GlobalScope.maxRequiredStackSpace.Value += value.type.GetSize();
        }
        else
        {
          value.position = Position.StackOffset(stackSize.Value);

          stackSize.Value += value.type.GetSize();
        }

        if (value is CNamedValue)
        {
          var t = value as CNamedValue;

          t.hasHomePosition = true;
          t.homePosition = value.position;
          t.modifiedSinceLastHome = false;
        }
      }

      byteCodeState.instructions.Add(new LLI_Location_PseudoInstruction(value, stackSize, byteCodeState));

      switch (value.position.type)
      {
        case PositionType.CodeBaseOffset:
          {
            byteCodeState.postInstructionDataStorage.Add(value.position.codeBaseOffset);
            break;
          }

        case PositionType.OnStack:
        case PositionType.GlobalStackOffset:
          {
            long dataSizeRemaining = data.LongLength;
            int stackPtrRegister = byteCodeState.GetFreeIntegerRegister(stackSize);
            byteCodeState.registers[stackPtrRegister] = new CValue(file, line, new PtrCType(BuiltInCType.Types["u64"]), true) { remainingReferences = 1, lastTouchedInstructionCount = byteCodeState.instructions.Count };

            if (value.position.type == PositionType.OnStack)
            {
              byteCodeState.instructions.Add(new LLI_LoadEffectiveAddress_StackOffsetToRegister(stackSize, value.position.stackOffsetForward, stackPtrRegister));
            }
            else if (value.position.type == PositionType.GlobalStackOffset)
            {
              byteCodeState.instructions.Add(new LLI_MovRuntimeParamToRegister(LLI_RuntimeParam.LLS_RP_STACK_BASE_PTR, (byte)stackPtrRegister));
              byteCodeState.instructions.Add(new LLI_AddImm(stackPtrRegister, BitConverter.GetBytes(value.position.globalStackBaseOffset)));
            }
            else
            {
              throw new NotImplementedException();
            }

            using (var registerLock = byteCodeState.LockRegister(stackPtrRegister))
            {
              int valueRegister = byteCodeState.GetFreeIntegerRegister(stackSize);
              byteCodeState.registers[stackPtrRegister] = new CValue(file, line, BuiltInCType.Types["u64"], true) { remainingReferences = 1, lastTouchedInstructionCount = byteCodeState.instructions.Count };

              long offset = 0;

              byte[] _last = null;
              byte[] _data = new byte[8];

              while (dataSizeRemaining >= 8)
              {
                for (int i = 0; i < 8; i++)
                  _data[i] = data[offset + i];

                bool equalsLast = true;

                if (_last == null)
                {
                  equalsLast = false;
                }
                else
                {
                  for (int i = 0; i < 8; i++)
                  {
                    if (_last[i] != _data[i])
                    {
                      equalsLast = false;
                      break;
                    }
                  }
                }

                if (!equalsLast)
                {
                  byteCodeState.instructions.Add(new LLI_MovImmToRegister(valueRegister, _data.ToArray())); // move value to register.
                }
                else if (Compiler.OptimizationLevel != 0 && dataSizeRemaining >= 16)
                {
                  long consecutiveBlocks = 1;
                  long _dataSizeRemaining = dataSizeRemaining;
                  long _offset = offset;
                  bool flawFound = false;

                  while (_dataSizeRemaining >= 8)
                  {
                    for (int i = 0; i < 8; i++)
                    {
                      if (_data[i] != data[_offset + i])
                      {
                        flawFound = true;
                        break;
                      }
                    }

                    if (flawFound)
                      break;

                    _offset += 8;
                    _dataSizeRemaining -= 8;
                    consecutiveBlocks++;
                  }

                  if (consecutiveBlocks > 3)
                  {
                    using (byteCodeState.LockRegister(valueRegister))
                    {
                      int countRegister = -1;

                      if (-1 != (countRegister = byteCodeState.GetTriviallyFreeIntegerRegister()))
                      {
                        using (byteCodeState.LockRegister(countRegister))
                        {
                          var counter = new CValue(null, 0, BuiltInCType.Types["u64"], true) { description = "Loop Counter", hasPosition = true, position = Position.Register(countRegister) };

                          var label = new LLI_Label_PseudoInstruction($"Set {consecutiveBlocks} consecutive 8 byte blocks in '{value}' @ 0x{(offset - 8):X}.");
                          byteCodeState.instructions.Insert(byteCodeState.instructions.Count - 2, new LLI_MovImmToRegister(countRegister, BitConverter.GetBytes((ulong)consecutiveBlocks)));
                          byteCodeState.instructions.Insert(byteCodeState.instructions.Count - 2, label);

                          if (Compiler.DetailedIntermediateOutput)
                            byteCodeState.MarkValueAsPosition(counter, counter.position, stackSize, false);
                          
                          byteCodeState.instructions.Add(new LLI_AddImm(countRegister, BitConverter.GetBytes((long)-1)));

                          if (Compiler.DetailedIntermediateOutput)
                            byteCodeState.MarkValueAsPosition(counter, counter.position, stackSize, false);

                          byteCodeState.instructions.Add(new LLI_CmpNotEq_ImmRegister(new byte[8], (byte)countRegister));
                          byteCodeState.instructions.Add(new LLI_JumpIfTrue_Imm(label));

                          if (Compiler.DetailedIntermediateOutput)
                            byteCodeState.DumpValue(counter);

                          byteCodeState.MarkValueAsPosition(new CConstIntValue(0, BuiltInCType.Types["u64"], null, 0) { description = $"leftover counter value from consecutive 8 byte blocks in '{value}'" }, Position.Register(countRegister), stackSize, false);
                        }

                        dataSizeRemaining = _dataSizeRemaining;
                        offset = _offset;

                        continue;
                      }
                    }
                  }
                }
                
                byteCodeState.instructions.Add(new LLI_MovRegisterToPtrInRegister(stackPtrRegister, valueRegister)); // move register to ptr.

                dataSizeRemaining -= 8;
                offset += 8;

                if (dataSizeRemaining > 0)
                  byteCodeState.instructions.Add(new LLI_AddImm(stackPtrRegister, BitConverter.GetBytes((ulong)8))); // inc ptr by 8.

                if (_last == null)
                  _last = new byte[8];

                Array.Copy(_data, _last, 8);
              }

              if (dataSizeRemaining > 0)
              {
                for (int i = 0; i < 8 && offset + i < data.LongLength; i++)
                  _data[i] = data[offset + i];

                byteCodeState.instructions.Add(new LLI_MovImmToRegister(valueRegister, _data.ToArray())); // move value to register.
                byteCodeState.instructions.Add(new LLI_MovRegisterToPtrInRegister_NBytes(stackPtrRegister, valueRegister, (byte)dataSizeRemaining)); // move register to ptr.
              }

              byteCodeState.registers[stackPtrRegister] = null;
              byteCodeState.registers[valueRegister] = null;
            }

            break;
          }

        default:
          throw new Exception("Invalid Position Type for array.");
      }

      value.isInitialized = true;
    }
  }

  public class CInstruction_SetValueTo : CInstruction
  {
    private readonly CValue targetValue, sourceValue;
    private readonly SharedValue<long> stackSize;

    public CInstruction_SetValueTo(CValue targetValue, CValue sourceValue, string file, int line, SharedValue<long> stackSize) : base(file, line)
    {
      this.targetValue = targetValue;
      this.sourceValue = sourceValue;
      this.stackSize = stackSize;

      if (targetValue.type.isConst)
        Compiler.Error($"Cannot assign const value '{targetValue}' to {sourceValue}.", file, line);

      if (!sourceValue.type.CanImplicitCastTo(targetValue.type))
      {
        if ((sourceValue is CConstIntValue) && (sourceValue as CConstIntValue).smallestPossibleSignedType != null && (sourceValue as CConstIntValue).smallestPossibleSignedType.CanImplicitCastTo(targetValue.type))
          this.sourceValue = new CConstIntValue((sourceValue as CConstIntValue).uvalue, (sourceValue as CConstIntValue).smallestPossibleSignedType, sourceValue.file, sourceValue.line) { description = $"signed equivalient value of '{sourceValue}'" };

        Compiler.Error($"Type Mismatch: '{sourceValue}' cannot be assigned to '{targetValue}', because there is no implicit conversion available.", file, line);
      }

      this.sourceValue.remainingReferences++;
    }

    public override void GetLLInstructions(ref ByteCodeState byteCodeState)
    {
      if (sourceValue == targetValue)
        return;

      if (!(sourceValue is CConstIntValue || sourceValue is CConstFloatValue))
        if (!sourceValue.hasPosition)
          throw new Exception($"Internal Compiler Error: Source Value {sourceValue} has no position.");

      // if setting the target to a temp value.
      if (!(sourceValue is CNamedValue) && sourceValue.hasPosition && sourceValue.position.type == PositionType.InRegister)
      {
        if (targetValue.hasPosition && targetValue.position.type == PositionType.InRegister)
          byteCodeState.registers[targetValue.position.registerIndex] = null;

        targetValue.hasPosition = true;
        targetValue.position = sourceValue.position;
        targetValue.isInitialized = true;
      
        if (targetValue is CNamedValue && (targetValue as CNamedValue).hasHomePosition)
          (targetValue as CNamedValue).modifiedSinceLastHome = true;
      
        sourceValue.remainingReferences--;
        byteCodeState.DumpValue(sourceValue);
      
        byteCodeState.MarkValueAsPosition(targetValue, targetValue.position, stackSize, true);
      
        if (sourceValue.type.GetSize() > targetValue.type.GetSize())
          byteCodeState.TruncateRegister(sourceValue.position.registerIndex, targetValue.type.GetSize());
      
        return;
      }

      if (!targetValue.hasPosition)
      {
        targetValue.hasPosition = true;

        if (!(targetValue.type is ArrayCType) && targetValue.type.GetSize() <= 8)
        {
          // Move to register.
          targetValue.position = Position.Register(targetValue.type is BuiltInCType && (targetValue.type as BuiltInCType).IsFloat() ? byteCodeState.GetFreeFloatRegister(stackSize) : byteCodeState.GetFreeIntegerRegister(stackSize));

          byteCodeState.registers[targetValue.position.registerIndex] = targetValue;
        }
        else
        {
          bool storeInCodeBase = targetValue is CNamedValue && (targetValue as CNamedValue).isStatic && Compiler.Assumptions.ByteCodeMutable; // All the const options are gone, because we're currently in the process of assigning a value to `targetValue`, so it better not be const...
          bool storeOnGlobalStack = !storeInCodeBase && targetValue is CNamedValue && (targetValue as CNamedValue).isStatic;

          if (storeInCodeBase)
          {
            targetValue.position = Position.CodeBaseOffset(targetValue, new byte[targetValue.type.GetSize()], targetValue.file, targetValue.line);
            byteCodeState.postInstructionDataStorage.Add(targetValue.position.codeBaseOffset);
          }
          else if (storeOnGlobalStack)
          {
            targetValue.position = Position.GlobalStackBaseOffset(Compiler.GlobalScope.maxRequiredStackSpace.Value);
            Compiler.GlobalScope.maxRequiredStackSpace.Value += targetValue.type.GetSize();
          }
          else
          {
            targetValue.position = Position.StackOffset(stackSize.Value);
            stackSize.Value += targetValue.type.GetSize();
          }

          if (targetValue is CNamedValue)
          {
            var t = targetValue as CNamedValue;

            t.hasHomePosition = true;
            t.homePosition = targetValue.position;
            t.modifiedSinceLastHome = false;
          }
        }
      }
      else if (targetValue.type.GetSize() <= 8 && targetValue.position.type != PositionType.InRegister && targetValue.remainingReferences > 0)
      {
        int registerIndex = (targetValue.type is BuiltInCType && (targetValue.type as BuiltInCType).IsFloat()) ? byteCodeState.GetTriviallyFreeFloatRegister() : byteCodeState.GetTriviallyFreeIntegerRegister();

        if (registerIndex != -1)
          targetValue.position = Position.Register(registerIndex);
      }

      byteCodeState.CopyValueToPosition(sourceValue, targetValue.position, stackSize, targetValue.type.GetSize());
      sourceValue.remainingReferences--;

      byteCodeState.MarkValueAsPosition(targetValue, targetValue.position, stackSize, true);

      if (targetValue is CNamedValue)
      {
        var t = targetValue as CNamedValue;

        if (t.hasHomePosition && t.position != t.homePosition)
          t.modifiedSinceLastHome = true;
      }

      targetValue.isInitialized = true;
    }
  }

  public class CInstruction_SetValuePtrToValue : CInstruction
  {
    private readonly CValue targetValuePtr, sourceValue;
    private readonly SharedValue<long> stackSize;

    public CInstruction_SetValuePtrToValue(CValue targetValuePtr, CValue sourceValue, string file, int line, SharedValue<long> stackSize) : base(file, line)
    {
      if (!(targetValuePtr.type is PtrCType))
        Compiler.Error($"Cannot assign value '{sourceValue}' to non-pointer value '{targetValuePtr}' when dereference-assigning.", file, line);

      this.targetValuePtr = targetValuePtr;
      this.sourceValue = sourceValue;
      this.stackSize = stackSize;

      targetValuePtr.remainingReferences++;
      sourceValue.remainingReferences++;

      if (!sourceValue.type.CanImplicitCastTo((targetValuePtr.type as PtrCType).pointsTo))
        Compiler.Error($"Type Mismatch: '{sourceValue}' cannot be assigned dereference of {targetValuePtr} to of type '{(targetValuePtr.type as PtrCType).pointsTo}', because there is no implicit conversion available.", file, line);
    }

    public override void GetLLInstructions(ref ByteCodeState byteCodeState)
    {
      if (!(sourceValue is CConstIntValue || sourceValue is CConstFloatValue))
        if (!sourceValue.hasPosition)
          throw new Exception($"Internal Compiler Error: Source Value {sourceValue} has no position.");

      // TODO: Handle casting!

      var pointerType = targetValuePtr.type as PtrCType;
      var size = pointerType.pointsTo.GetSize();

      if (size <= 8)
      {
        int sourceValueRegister = byteCodeState.MoveValueToAnyRegister(sourceValue, stackSize);

        using (var lockedRegister = byteCodeState.LockRegister(sourceValueRegister))
        {
          int targetPtrRegister = byteCodeState.MoveValueToAnyRegister(targetValuePtr, stackSize);

          if (size < 8)
            byteCodeState.instructions.Add(new LLI_MovRegisterToPtrInRegister_NBytes(targetPtrRegister, sourceValueRegister, (byte)size));
          else
            byteCodeState.instructions.Add(new LLI_MovRegisterToPtrInRegister(targetPtrRegister, sourceValueRegister));
        }
      }
      else // if (size > 8)
      {
        var remainingSize = size;

        int sourceValueRegister = byteCodeState.GetFreeIntegerRegister(stackSize);

        using (var lockedSourceValueRegister = byteCodeState.LockRegister(sourceValueRegister))
        {
          int sourcePtrRegister = byteCodeState.GetFreeIntegerRegister(stackSize);

          using (var lockedPtrValueRegister = byteCodeState.LockRegister(sourcePtrRegister))
          {
            int targetPtrRegister = byteCodeState.CopyValueToAnyRegister(targetValuePtr, stackSize);

            if (sourceValue.position.type == PositionType.InRegister)
              throw new Exception("Internal Compiler Error!");

            byteCodeState.instructions.Add(new LLI_LoadEffectiveAddress_StackOffsetToRegister(stackSize, sourceValue.position.stackOffsetForward, sourcePtrRegister));

            while (remainingSize > 0)
            {
              byteCodeState.instructions.Add(new LLI_MovFromPtrInRegisterToRegister(sourcePtrRegister, sourceValueRegister));

              if (remainingSize < 8)
                byteCodeState.instructions.Add(new LLI_MovRegisterToPtrInRegister_NBytes(targetPtrRegister, sourceValueRegister, (byte)size));
              else
                byteCodeState.instructions.Add(new LLI_MovRegisterToPtrInRegister(targetPtrRegister, sourceValueRegister));

              remainingSize -= 8;

              if (remainingSize > 0)
              {
                byteCodeState.instructions.Add(new LLI_AddImm(sourcePtrRegister, BitConverter.GetBytes((ulong)8)));
                byteCodeState.instructions.Add(new LLI_AddImm(targetPtrRegister, BitConverter.GetBytes((ulong)8)));
              }
            }
          }
        }
      }

      targetValuePtr.remainingReferences--;
      sourceValue.remainingReferences--;
    }
  }

  public class CInstruction_CallFunction : CInstruction
  {
    private readonly CFunction function;
    private readonly List<CValue> arguments;
    private readonly CValue returnValue;
    private readonly SharedValue<long> stackSize;

    public CInstruction_CallFunction(CFunction function, List<CValue> arguments, out CValue returnValue, SharedValue<long> stackSize, string file, int line) : base(file, line)
    {
      this.function = function;
      this.arguments = arguments;
      this.stackSize = stackSize;

      if (arguments.Count != function.parameters.Length)
        Compiler.Error($"Invalid parameter count for function '{function}'. {arguments.Count} parameters given, {function.parameters.Length} expected.", file, line);

      for (int i = 0; i < arguments.Count; i++)
      {
        if (!arguments[i].type.CanImplicitCastTo(function.parameters[i].type))
          Compiler.Error($"In function call to '{function}': Argument {(i + 1)} '{arguments[i]}' for parameter {function.parameters[i].type} {function.parameters[i].name} is of mismatching type '{arguments[i].type}' and cannot be converted implicitly. Value defined in File '{arguments[i].file ?? "?"}', Line: {arguments[i].line}.", file, line);

        arguments[i].remainingReferences++;
      }

      if (function.returnType is BuiltInCType || function.returnType is PtrCType)
      {
        this.returnValue = new CValue(file, line, function.returnType.MakeCastableClone(function.returnType), true) { description = $"Return Value of \"{function}\"" };
        this.returnValue.type.explicitCast = null;
        this.returnValue.type.isConst = true;
      }
      else if (!(function.returnType is VoidCType))
      {
        throw new NotImplementedException();
      }
      else
      {
        this.returnValue = null;
      }

      returnValue = this.returnValue;
    }

    public override void GetLLInstructions(ref ByteCodeState byteCodeState)
    {
      for (int i = 0; i < arguments.Count; i++)
        if (!arguments[i].isInitialized)
          Compiler.Error($"In function call to '{function}': Argument {(i + 1)} '{arguments[i]}' for parameter {function.parameters[i].type} {function.parameters[i].name} has not been initialized yet. Defined in File '{arguments[i].file ?? "?"}', Line: {arguments[i].line}.", file, line);

      if (function.returnType is ArrayCType || function.returnType is StructCType)
        throw new NotImplementedException();

      // In case it's a recursive call: backup the parameter positions.
      var originalParameters = function.parameters;
      function.ResetRegisterPositions();

      // Backup Registers.
      byteCodeState.BackupRegisterValues(stackSize);

      List<ByteCodeState.RegisterLock> locks = new List<ByteCodeState.RegisterLock>();

      for (int i = arguments.Count - 1; i >= 0; i--)
      {
        var targetPosition = function.parameters[i].value.position;
        var sourceValue = arguments[i];

        if (targetPosition.type == PositionType.InRegister)
          locks.Add(new ByteCodeState.RegisterLock(targetPosition.registerIndex, byteCodeState));

        byteCodeState.CopyValueToPositionWithCast(sourceValue, targetPosition, function.parameters[i].type, stackSize);

        sourceValue.remainingReferences--;
      }

      foreach (var l in locks)
        l.Dispose();

      if (function is CBuiltInFunction)
      {
        Position targetPosition = Position.Register(0);

        byteCodeState.MoveValueToPosition(new CConstIntValue((function as CBuiltInFunction).builtinFunctionIndex, BuiltInCType.Types["u8"], file, line), targetPosition, stackSize, false);

        if (!(function.returnType is VoidCType))
        {
          returnValue.hasPosition = true;
          returnValue.position = Position.Register(0);
        }

        byteCodeState.instructions.Add(new LLI_CallBuiltInFunction_IDFromRegister_ResultToRegister(0, 0));

        byteCodeState.registers[0] = returnValue;

        byteCodeState.instructions.Add(new LLI_Location_PseudoInstruction(returnValue, stackSize, byteCodeState));
      }
      else
      {
        byteCodeState.instructions.Add(new LLI_CallFunctionAtRelativeImm(function.functionStartLabel));
        byteCodeState.instructions.Add(new LLI_Location_PseudoInstruction(new CValue(file, line, new PtrCType(VoidCType.Instance), true) { description = $"Return Instruction pointer for function call to {function}", hasPosition = true, position = Position.StackOffset(0) }, new SharedValue<long>(0), byteCodeState));

        if (!(function.returnType is VoidCType))
          byteCodeState.MarkValueAsPosition(returnValue, Position.Register(0), stackSize, true);
      }

      function.parameters = originalParameters;
    }
  }

  public class CInstruction_CallFunctionPtr : CInstruction
  {
    private readonly CValue functionPtr;
    private readonly List<CValue> arguments;
    private readonly CValue returnValue;
    private readonly SharedValue<long> stackSize;

    public CInstruction_CallFunctionPtr(CValue functionPtr, List<CValue> arguments, out CValue returnValue, SharedValue<long> stackSize, string file, int line) : base(file, line)
    {
      this.functionPtr = functionPtr;
      this.arguments = arguments;
      this.stackSize = stackSize;

      var functionType = functionPtr.type;

      if (!(functionType is FuncCType || functionType is ExternFuncCType))
        throw new Exception($"Internal Compiler Error: Attempting to call '{functionPtr}' which is neither a 'func' nor an 'extern_func'.");

      var function = functionType as _FuncCTypeWrapper;

      if (arguments.Count != function.parameters.Length)
        Compiler.Error($"Invalid parameter count for function '{functionPtr}'. {arguments.Count} parameters given, {function.parameters.Length} expected.", file, line);

      for (int i = 0; i < arguments.Count; i++)
      {
        if (!arguments[i].type.CanImplicitCastTo(function.parameters[i]))
          Compiler.Error($"In function call to '{functionPtr}': Argument {(i + 1)} '{arguments[i]}' for parameter of type '{function.parameters[i]}' is of mismatching type '{arguments[i].type}' and cannot be converted implicitly. Value defined in File '{arguments[i].file ?? "?"}', Line: {arguments[i].line}.", file, line);

        arguments[i].remainingReferences++;
      }

      if (function.returnType is BuiltInCType || function.returnType is PtrCType)
      {
        this.returnValue = new CValue(file, line, function.returnType.MakeCastableClone(function.returnType), true) { description = $"Return Value of call to function ptr \"{functionPtr}\"" };
        this.returnValue.type.explicitCast = null;
        this.returnValue.type.isConst = true;
      }
      else if (!(function.returnType is VoidCType))
      {
        throw new NotImplementedException();
      }
      else
      {
        this.returnValue = null;
      }

      returnValue = this.returnValue;
    }

    public override void GetLLInstructions(ref ByteCodeState byteCodeState)
    {
      for (int i = 0; i < arguments.Count; i++)
        if (!arguments[i].isInitialized)
          Compiler.Error($"In function call to '{functionPtr}': Argument {(i + 1)} '{arguments[i]}' for parameter of type '{(functionPtr.type as _FuncCTypeWrapper).parameters[i]}' has not been initialized yet. Defined in File '{arguments[i].file ?? "?"}', Line: {arguments[i].line}.", file, line);

      if (functionPtr.type is FuncCType)
      {
        throw new NotImplementedException();
      }
      else if (functionPtr.type is ExternFuncCType)
      {
        long pushedBytes = 0;
        int chosenIntRegister = -1;
        int chosenFloatRegister = -1;
        int returnValueRegister = -1;

        if (!functionPtr.hasPosition)
          throw new Exception("Internal Compiler Error. Function Ptr has no Position.");

        if (functionPtr.position.type == PositionType.InRegister)
          chosenIntRegister = functionPtr.position.registerIndex;

        var function = functionPtr.type as ExternFuncCType;

        if (function.returnType is VoidCType)
        {
          returnValueRegister = chosenIntRegister = byteCodeState.GetFreeIntegerRegister(stackSize);
        }
        else if (function.returnType is BuiltInCType && (function.returnType as BuiltInCType).IsFloat())
        {
          chosenFloatRegister = byteCodeState.GetFreeFloatRegister(stackSize);
          returnValueRegister = chosenIntRegister = byteCodeState.GetFreeIntegerRegister(stackSize);
          byteCodeState.registers[chosenFloatRegister] = null;
        }
        else if (chosenIntRegister == -1)
        {
          returnValueRegister = chosenIntRegister = byteCodeState.GetFreeIntegerRegister(stackSize);
          byteCodeState.registers[chosenIntRegister] = null;
        }
        else
        {
          returnValueRegister = chosenIntRegister;
        }

        foreach (var param in function.parameters)
        {
          if (param is BuiltInCType && (param as BuiltInCType).IsFloat())
          {
            if (chosenFloatRegister == -1)
            {
              chosenFloatRegister = byteCodeState.GetFreeFloatRegister(stackSize);
              byteCodeState.registers[chosenFloatRegister] = null;
            }
          }
          else
          {
            if (chosenIntRegister == -1)
            {
              chosenIntRegister = byteCodeState.GetFreeIntegerRegister(stackSize);
              byteCodeState.registers[chosenIntRegister] = null;
            }
          }
        }

        byteCodeState.instructions.Add(new LLI_Location_PseudoInstruction(functionPtr, stackSize, byteCodeState));

        // Function Pointer.
        if (functionPtr.position.type == PositionType.InRegister)
        {
          if (byteCodeState.registers[functionPtr.position.registerIndex] != functionPtr)
            throw new Exception("Internal Compiler Error!");

          byteCodeState.FreeRegister(returnValueRegister, stackSize);
          byteCodeState.instructions.Add(new LLI_PushRegister((byte)functionPtr.position.registerIndex));
          pushedBytes += 8;
        }
        else if (functionPtr.position.type == PositionType.OnStack)
        {
          byteCodeState.instructions.Add(new LLI_MovStackOffsetToStackOffset(stackSize, functionPtr.position.stackOffsetForward + pushedBytes, new SharedValue<long>(0), 0));
          byteCodeState.instructions.Add(new LLI_StackIncrementImm(8));
          pushedBytes += 8;
        }
        else if (functionPtr.position.type == PositionType.GlobalStackOffset || functionPtr.position.type == PositionType.CodeBaseOffset)
        {
          byteCodeState.CopyPositionToRegisterInplace(chosenIntRegister, functionPtr.type.GetSize(), functionPtr.position, stackSize);
          byteCodeState.instructions.Add(new LLI_PushRegister((byte)chosenIntRegister));
          pushedBytes += 8;
        }
        else
        {
          throw new NotImplementedException();
        }

        // Type of return value.
        {
          if (function.returnType is BuiltInCType && (function.returnType as BuiltInCType).IsFloat())
            byteCodeState.instructions.Add(new LLI_MovImmToRegister(chosenIntRegister, BitConverter.GetBytes((ulong)1)));
          else
            byteCodeState.instructions.Add(new LLI_MovImmToRegister(chosenIntRegister, BitConverter.GetBytes((ulong)0)));

          byteCodeState.instructions.Add(new LLI_PushRegister((byte)chosenIntRegister));
          pushedBytes += 8;
        }

        // Signal Last Argument.
        {
          byteCodeState.instructions.Add(new LLI_MovImmToRegister(chosenIntRegister, BitConverter.GetBytes((ulong)0)));

          byteCodeState.instructions.Add(new LLI_PushRegister((byte)returnValueRegister));
          pushedBytes += 8;
        }

        byteCodeState.instructions.Add(new LLI_StackDecrementImm(pushedBytes));

        // Push Arguments to the Stack.
        for (int i = function.parameters.Length - 1; i >= 0; i--)
        {
          bool isFloat = function.parameters[i] is BuiltInCType && (function.parameters[i] as BuiltInCType).IsFloat();

          Position targetPosition = Position.Register(isFloat ? chosenFloatRegister : chosenIntRegister);
          
          byteCodeState.CopyValueToPositionWithCast(arguments[i], targetPosition, function.parameters[i], stackSize);
          byteCodeState.MarkValueAsTouched(arguments[i]);
          arguments[i].remainingReferences--;

          byteCodeState.instructions.Add(new LLI_MovRegisterToStackOffset(targetPosition.registerIndex, new SharedValue<long>(0), pushedBytes));
          pushedBytes += 8;

          if (function.parameters[i] is BuiltInCType && (function.parameters[i] as BuiltInCType).IsFloat())
            byteCodeState.instructions.Add(new LLI_MovImmToRegister(chosenIntRegister, BitConverter.GetBytes((long)-1)));
          else
            byteCodeState.instructions.Add(new LLI_MovImmToRegister(chosenIntRegister, BitConverter.GetBytes((ulong)1)));

          byteCodeState.instructions.Add(new LLI_MovRegisterToStackOffset(chosenIntRegister, new SharedValue<long>(0), pushedBytes));
          pushedBytes += 8;
        }

        byteCodeState.instructions.Add(new LLI_StackIncrementImm(pushedBytes));

        // Perform Call.
        byteCodeState.instructions.Add(new LLI_CallExternFunction_ResultToRegister((byte)returnValueRegister));

        // Cleanup Stack.
        byteCodeState.instructions.Add(new LLI_StackDecrementImm(pushedBytes));

        // Set Return Value.
        if (function.returnType is VoidCType)
          byteCodeState.registers[returnValueRegister] = null;
        else
          byteCodeState.MarkValueAsPosition(returnValue, Position.Register(returnValueRegister), stackSize, true);
      }
      else
      {
        throw new Exception("Internal Compiler Error.");
      }
    }
  }

  public class CInstruction_AddressOfVariable : CInstruction
  {
    protected CNamedValue value;
    protected CGlobalValueReference outValue;
    protected SharedValue<long> stackSize;

    protected CInstruction_AddressOfVariable(string file, int line) : base(file, line) { }

    public CInstruction_AddressOfVariable(CNamedValue value, out CGlobalValueReference outValue, SharedValue<long> stackSize, string file, int line) : base(file, line)
    {
      this.value = value;
      this.outValue = outValue = new CGlobalValueReference(new PtrCType(value.type), file, line) { description = $"reference to '{value}'" };
      this.stackSize = stackSize;
    }

    public override void GetLLInstructions(ref ByteCodeState byteCodeState)
    {
      value.isVolatile = true;
      value.isInitialized = true; // Do we want this?

      if (!value.hasPosition)
      {
        if ((value.type is ArrayCType && (value.type as ArrayCType).type.isConst) || (value.isStatic && Compiler.Assumptions.ByteCodeMutable))
        {
          value.position = Position.CodeBaseOffset(value, new byte[value.type.GetSize()], file, line);
          byteCodeState.postInstructionDataStorage.Add(value.position.codeBaseOffset);
        }
        else if (value.isStatic)
        {
          value.position = Position.GlobalStackBaseOffset(Compiler.GlobalScope.maxRequiredStackSpace.Value);

          Compiler.GlobalScope.maxRequiredStackSpace.Value += value.type.GetSize();
        }
        else
        {
          value.position = Position.StackOffset(stackSize.Value);
          stackSize.Value += value.type.GetSize();
        }

        value.hasPosition = true;
        value.hasHomePosition = true;
        value.homePosition = value.position;
        value.modifiedSinceLastHome = false;
      }
      else if (value.hasPosition && value.position.type == PositionType.InRegister)
      {
        byteCodeState.MoveValueToHome(value, stackSize);
      }

      int register = byteCodeState.GetFreeIntegerRegister(stackSize);

      switch (value.position.type)
      {
        case PositionType.OnStack:
          byteCodeState.instructions.Add(new LLI_LoadEffectiveAddress_StackOffsetToRegister(stackSize, value.position.stackOffsetForward, register));
          break;

        case PositionType.GlobalStackOffset:
          byteCodeState.instructions.Add(new LLI_MovRuntimeParamToRegister(LLI_RuntimeParam.LLS_RP_STACK_BASE_PTR, (byte)register));
          byteCodeState.instructions.Add(new LLI_AddImm(register, BitConverter.GetBytes(value.position.globalStackBaseOffset)));
          break;

        case PositionType.CodeBaseOffset:
          byteCodeState.instructions.Add(new LLI_MovRuntimeParamToRegister(LLI_RuntimeParam.LLS_RP_CODE_BASE_PTR, (byte)register));
          byteCodeState.instructions.Add(new LLI_AddImmInstructionOffset(register, value.position.codeBaseOffset));
          break;

        default:
          throw new NotImplementedException();
      }

      byteCodeState.MarkValueAsPosition(outValue, Position.Register(register), stackSize, true);
    }
  }

  public class CInstruction_ArrayVariableToPtr : CInstruction_AddressOfVariable
  {
    public CInstruction_ArrayVariableToPtr(CNamedValue value, out CGlobalValueReference outValue, SharedValue<long> stackSize, string file, int line) : base(file, line)
    {
      if (!(value.type is ArrayCType))
        Compiler.Error($"Parameter {nameof(value)} ({value}) to {nameof(CInstruction_ArrayVariableToPtr)} is not an array but {value.type}.", file, line);

      this.value = value;
      this.outValue = outValue = new CGlobalValueReference(new PtrCType((value.type as ArrayCType).type) { explicitCast = value.type.explicitCast }, file, line) { description = $"ptr to '{value}'" };
      this.stackSize = stackSize;
    }
  }

  public class CInstruction_DereferencePtr : CInstruction
  {
    CValue value;
    CValue outValue;
    SharedValue<long> stackSize;

    public CInstruction_DereferencePtr(CValue value, out CValue outValue, SharedValue<long> stackSize, string file, int line) : base(file, line)
    {
      this.value = value;
      this.outValue = outValue = new CValue(file, line, ((PtrCType)(value.type)).pointsTo, false) { description = $"dereference of '{value}'" };
      this.stackSize = stackSize;

      value.remainingReferences++;
    }

    public override void GetLLInstructions(ref ByteCodeState byteCodeState)
    {
      bool isMutableValue = !(value is CNamedValue);
      var sourcePtrRegister = isMutableValue ? byteCodeState.MoveValueToAnyRegister(value, stackSize) : byteCodeState.CopyValueToAnyRegister(value, stackSize);

      using (var registerLock = byteCodeState.LockRegister(sourcePtrRegister))
      {
        var size = outValue.type.GetSize();

        if (size <= 8)
        {
          int register = isMutableValue ? sourcePtrRegister : ((!(outValue.type is BuiltInCType) || !(outValue.type as BuiltInCType).IsFloat()) ? byteCodeState.GetFreeIntegerRegister(stackSize) : byteCodeState.GetFreeFloatRegister(stackSize));

          byteCodeState.instructions.Add(new LLI_MovFromPtrInRegisterToRegister(sourcePtrRegister, register));

          if (size < 8)
            byteCodeState.TruncateRegister(register, size);

          value.remainingReferences--;

          if (isMutableValue)
            byteCodeState.DumpValue(value);

          byteCodeState.MarkValueAsPosition(outValue, Position.Register(register), stackSize, true);
        }
        else
        {
          throw new Exception("Internal Compiler Error. What exactly is this trying to archive?!");

          //var remainingSize = size;
          //int register = (!(outValue.type is BuiltInCType) || !(outValue.type as BuiltInCType).IsFloat()) ? byteCodeState.GetFreeIntegerRegister(stackSize) : byteCodeState.GetFreeFloatRegister(stackSize);
          //
          //while (remainingSize > 0)
          //{
          //  byteCodeState.instructions.Add(new LLI_MovFromPtrInRegisterToRegister(sourcePtrRegister, register));
          //
          //  if (remainingSize < 8)
          //    byteCodeState.TruncateRegister(register, size);
          //  else
          //    byteCodeState.instructions.Add(new LLI_AddImm(sourcePtrRegister, BitConverter.GetBytes((ulong)8)));
          //
          //  remainingSize -= 8;
          //}
        }
      }

      outValue.isInitialized = true;
    }
  }

  public class CInstruction_CopyPositionFromValueToValue : CInstruction
  {
    CValue source, target;
    SharedValue<long> stackSize;

    public CInstruction_CopyPositionFromValueToValue(CValue source, CValue target, SharedValue<long> stackSize, string file, int line) : base(file, line)
    {
      this.source = source;
      this.target = target;
      this.stackSize = stackSize;

      source.remainingReferences++;
    }

    public override void GetLLInstructions(ref ByteCodeState byteCodeState)
    {
      if ((source is CConstFloatValue || source is CConstIntValue) && !source.isInitialized)
        Compiler.Error($"Casting or assigning uninitialized value {source} to {target} is not allowed.", file, line);

      if (!source.hasPosition)
      {
        if (source is CConstFloatValue || source is CConstIntValue)
          byteCodeState.MoveValueToAnyRegister(source, stackSize);
        else
          throw new Exception($"Internal Compiler Error: Source value {source} doesn't have a position to copy.");
      }

      if (source.position.type == PositionType.InRegister)
        if (byteCodeState.registers[source.position.registerIndex] != source)
          throw new Exception("Internal Compiler Error: Register Index not actually referenced.");

      Position originalPosition = source.position;

      if (source is CNamedValue)
        byteCodeState.MoveValueToHome(source as CNamedValue, stackSize);
      else
        byteCodeState.DumpValue(source);

      if (source.type is BuiltInCType && target.type is BuiltInCType && (source.type as BuiltInCType).GetSize() > (target.type as BuiltInCType).GetSize())
      {
        byteCodeState.MoveValueToAnyRegister(target, stackSize);
        byteCodeState.TruncateValueInRegister(target);
      }

      source.remainingReferences--;
      target.isInitialized = true;

      byteCodeState.MarkValueAsPosition(target, originalPosition, stackSize, true);
    }

    public override string ToString()
    {
      return base.ToString() + $" ({source} to {target})";
    }
  }

  public class CInstruction_IfNonZeroJumpToLabel : CInstruction
  {
    CValue value;
    LLI_Label_PseudoInstruction label;
    SharedValue<long> stackSize;
    bool backupRegisters;

    public CInstruction_IfNonZeroJumpToLabel(CValue value, LLI_Label_PseudoInstruction label, SharedValue<long> stackSize, bool backupRegisters, string file, int line) : base(file, line)
    {
      this.value = value;
      this.label = label;
      this.stackSize = stackSize;
      this.backupRegisters = backupRegisters;

      value.remainingReferences++;
    }

    public override void GetLLInstructions(ref ByteCodeState byteCodeState)
    {
      byte registerIndex = byteCodeState.MoveValueToAnyRegister(value, stackSize);
      byteCodeState.instructions.Add(new LLI_CmpNotEq_ImmRegister(BitConverter.GetBytes((long)0), registerIndex));

      value.remainingReferences--;

      if (backupRegisters)
        byteCodeState.BackupRegisterValues(stackSize);
      else
        throw new Exception("What?");

      byteCodeState.instructions.Add(new LLI_JumpIfTrue_Imm(label));
    }
  }

  public class CInstruction_IfZeroJumpToLabel : CInstruction
  {
    CValue value;
    LLI_Label_PseudoInstruction label;
    SharedValue<long> stackSize;
    bool backupRegisters;

    public CInstruction_IfZeroJumpToLabel(CValue value, LLI_Label_PseudoInstruction label, SharedValue<long> stackSize, bool backupRegisters, string file, int line) : base(file, line)
    {
      this.value = value;
      this.label = label;
      this.stackSize = stackSize;
      this.backupRegisters = backupRegisters;

      value.remainingReferences++;
    }

    public override void GetLLInstructions(ref ByteCodeState byteCodeState)
    {
      bool isMutableValue = !(value is CNamedValue);
      int registerIndex = isMutableValue ? byteCodeState.MoveValueToAnyRegister(value, stackSize) : byteCodeState.CopyValueToAnyRegister(value, stackSize);
      byteCodeState.instructions.Add(new LLI_NotRegister(registerIndex));
      byteCodeState.instructions.Add(new LLI_CmpNotEq_ImmRegister(BitConverter.GetBytes((long)0), (byte)registerIndex));

      if (isMutableValue)
        byteCodeState.DumpValue(value);

      value.remainingReferences--;

      if (backupRegisters)
        byteCodeState.BackupRegisterValues(stackSize);
      else
        throw new Exception("What?");

      byteCodeState.instructions.Add(new LLI_JumpIfTrue_Imm(label));
    }
  }

  public class CInstruction_EndOfConditional_WipeAllRegisters : CInstruction
  {
    Scope scope;

    public CInstruction_EndOfConditional_WipeAllRegisters(Scope scope) : base(null, -1)
    {
      if (scope.parentScope == null)
        throw new Exception("Internal Compiler Error: EndOfConditional doesn't work with top level scopes.");

      this.scope = scope.parentScope;
    }

    public override void GetLLInstructions(ref ByteCodeState byteCodeState)
    {
      byteCodeState.BackupRegisterValues(scope.maxRequiredStackSpace);
    }
  }

  public class CInstruction_AddImm : CInstruction
  {
    CValue value;
    byte[] imm;
    SharedValue<long> stackSize;
    CValue resultingValue;
    bool toSelf;

    public CInstruction_AddImm(CValue value, long imm, SharedValue<long> stackSize, bool toSelf, out CValue resultingValue, string file, int line) : base(file, line)
    {
      if (value.type is BuiltInCType && (value.type as BuiltInCType).IsFloat())
        throw new Exception("Internal Compiler Error: Should not be float value.");

      if (value.type is PtrCType && (value.type as PtrCType).pointsTo.GetSize() != 1)
        imm *= (value.type as PtrCType).pointsTo.GetSize();

      this.value = value;
      this.imm = BitConverter.GetBytes(imm);
      this.stackSize = stackSize;
      this.toSelf = toSelf;

      if (toSelf)
        this.resultingValue = value;
      else
        this.resultingValue = new CValue(file, line, value.type, true) { description = $"({value} + {imm})" };

      resultingValue = this.resultingValue;
      value.remainingReferences++;
    }

    public CInstruction_AddImm(CValue value, ulong imm, SharedValue<long> stackSize, bool toSelf, out CValue resultingValue, string file, int line) : base(file, line)
    {
      if (value.type is BuiltInCType && (value.type as BuiltInCType).IsFloat())
        throw new Exception("Internal Compiler Error: Should not be float value.");

      if (value.type is PtrCType && (value.type as PtrCType).pointsTo.GetSize() != 1)
        imm *= (ulong)(value.type as PtrCType).pointsTo.GetSize();

      this.value = value;
      this.imm = BitConverter.GetBytes(imm);
      this.stackSize = stackSize;
      this.toSelf = toSelf;

      if (toSelf)
        this.resultingValue = value;
      else
        this.resultingValue = new CValue(file, line, value.type, true) { description = $"({value} + {imm})" };

      resultingValue = this.resultingValue;
      value.remainingReferences++;
    }

    public CInstruction_AddImm(CValue value, double imm, SharedValue<long> stackSize, bool toSelf, out CValue resultingValue, string file, int line) : base(file, line)
    {
      if (!(value.type is BuiltInCType) || !(value.type as BuiltInCType).IsFloat())
        throw new Exception("Internal Compiler Error: Should be float value.");

      if (value.type is PtrCType)
        Compiler.Error($"Unable to add floating point value {imm} to pointer {value}.", file, line);

      this.value = value;
      this.imm = BitConverter.GetBytes(imm);
      this.stackSize = stackSize;
      this.toSelf = toSelf;

      if (toSelf)
        this.resultingValue = value;
      else
        this.resultingValue = new CValue(file, line, value.type, true) { description = $"({value} + {imm})" };

      resultingValue = this.resultingValue;
      value.remainingReferences++;
    }

    public override void GetLLInstructions(ref ByteCodeState byteCodeState)
    {
      if (!value.isInitialized)
        Compiler.Error($"Cannot perform operator on uninitialized {value}.", file, line);

      bool isMutableValue = !(value is CNamedValue);
      int registerIndex = isMutableValue || toSelf ? byteCodeState.MoveValueToAnyRegister(value, stackSize) : byteCodeState.CopyValueToAnyRegister(value, stackSize);

      byteCodeState.instructions.Add(new LLI_AddImm(registerIndex, imm));

      value.remainingReferences--;

      if (isMutableValue)
        byteCodeState.DumpValue(value);

      if (toSelf)
      {
        if (value is CNamedValue)
          (value as CNamedValue).modifiedSinceLastHome = true;
        else if (value is CGlobalValueReference)
          throw new NotImplementedException();
      }
      else
      {
        byteCodeState.MarkValueAsPosition(resultingValue, Position.Register(registerIndex), stackSize, true);
      }
    }
  }

  public class CInstruction_Add : CInstruction
  {
    CValue left, right;
    SharedValue<long> stackSize;
    CValue resultingValue;
    bool toSelf;

    public CInstruction_Add(CValue left, CValue right, SharedValue<long> stackSize, bool toSelf, out CValue resultingValue, string file, int line) : base(file, line)
    {
      if ((left.type is BuiltInCType && (left.type as BuiltInCType).IsFloat()) != (right.type is BuiltInCType && (right.type as BuiltInCType).IsFloat()))
        Compiler.Error($"Both values of the operator have to be floating point or non-floating point. Given: '{left}' and '{right}'.", file, line);

      this.left = left;
      this.right = right;
      this.stackSize = stackSize;
      this.toSelf = toSelf;

      if (toSelf)
        this.resultingValue = left;
      else
        this.resultingValue = new CValue(file, line, left.type.GetSize() >= right.type.GetSize() ? left.type : right.type, true) { description = $"({left} + {right})" };

      if ((left is CConstIntValue || left is CConstFloatValue) && toSelf)
        throw new Exception("Internal Compiler Error: operator cannot be applied to the lvalue directly if it's constant imm.");

      if (left.type is PtrCType && right.type is BuiltInCType && (right.type as BuiltInCType).IsFloat())
        Compiler.Error($"Attempting to add floating point value {right} to pointer {left}.", file, line);

      if (!toSelf && (left is CConstFloatValue && !(right is CConstFloatValue)) || (left is CConstIntValue && !(right is CConstIntValue)))
      {
        var tmp = left;
        left = right;
        right = tmp;
      }

      resultingValue = this.resultingValue;
      left.remainingReferences++;
      right.remainingReferences++;
    }

    public override void GetLLInstructions(ref ByteCodeState byteCodeState)
    {
      if (!left.isInitialized)
        Compiler.Error($"Cannot perform operator on uninitialized value {left}.", file, line);

      if (!right.isInitialized)
        Compiler.Error($"Cannot perform operator on uninitialized value {right}.", file, line);

      bool leftIsMutableValue = !(left is CNamedValue);
      int leftRegisterIndex = leftIsMutableValue || toSelf ? byteCodeState.MoveValueToAnyRegister(left, stackSize) : byteCodeState.CopyValueToAnyRegister(left, stackSize);

      using (var registerLock = byteCodeState.LockRegister(leftRegisterIndex))
      {
        if (right is CConstIntValue)
        {
          ulong imm = (right as CConstIntValue).uvalue;

          if (left.type is PtrCType && (left.type as PtrCType).pointsTo.GetSize() > 1)
            imm *= (ulong)(left.type as PtrCType).pointsTo.GetSize();

          byteCodeState.instructions.Add(new LLI_AddImm(leftRegisterIndex, BitConverter.GetBytes(imm)));
        }
        else if (right is CConstFloatValue)
        {
          byteCodeState.instructions.Add(new LLI_AddImm(leftRegisterIndex, BitConverter.GetBytes((right as CConstFloatValue).value)));
        }
        else
        {
          int rightRegisterIndex;

          if (left.type is PtrCType && right.type is BuiltInCType && (left.type as PtrCType).pointsTo.GetSize() != 1)
          {
            rightRegisterIndex = byteCodeState.CopyValueToAnyRegister(right, stackSize);

            byteCodeState.instructions.Add(new LLI_MultiplySignedImm(rightRegisterIndex, BitConverter.GetBytes((left.type as PtrCType).pointsTo.GetSize())));
          }
          else
          {
            rightRegisterIndex = byteCodeState.MoveValueToAnyRegister(right, stackSize);
          }

          byteCodeState.instructions.Add(new LLI_AddRegister(leftRegisterIndex, rightRegisterIndex));
        }

        left.remainingReferences--;
        right.remainingReferences--;

        if (leftIsMutableValue)
          byteCodeState.DumpValue(left);

        if (toSelf)
        {
          if (left is CNamedValue)
            (left as CNamedValue).modifiedSinceLastHome = true;
          else if (left is CGlobalValueReference)
            throw new NotImplementedException();
        }
        else
        {
          byteCodeState.MarkValueAsPosition(resultingValue, Position.Register(leftRegisterIndex), stackSize, true);
        }
      }
    }
  }

  public class CInstruction_Subtract : CInstruction
  {
    CValue left, right;
    SharedValue<long> stackSize;
    CValue resultingValue;
    bool toSelf;

    public CInstruction_Subtract(CValue left, CValue right, SharedValue<long> stackSize, bool toSelf, out CValue resultingValue, string file, int line) : base(file, line)
    {
      if ((left.type is BuiltInCType && (left.type as BuiltInCType).IsFloat()) != (right.type is BuiltInCType && (right.type as BuiltInCType).IsFloat()))
        Compiler.Error($"Both values of the operator have to be floating point or non-floating point. Given: '{left}' and '{right}'.", file, line);

      this.left = left;
      this.right = right;
      this.stackSize = stackSize;
      this.toSelf = toSelf;

      if (toSelf)
        this.resultingValue = left;
      else
        this.resultingValue = new CValue(file, line, left.type.GetSize() >= right.type.GetSize() ? left.type : right.type, true) { description = $"({left} - {right})" };

      resultingValue = this.resultingValue;
      left.remainingReferences++;
      right.remainingReferences++;
    }

    public override void GetLLInstructions(ref ByteCodeState byteCodeState)
    {
      if (!left.isInitialized)
        Compiler.Error($"Cannot perform operator on uninitialized value {left}.", file, line);

      if (!right.isInitialized)
        Compiler.Error($"Cannot perform operator on uninitialized value {right}.", file, line);

      bool leftIsMutableValue = !(left is CNamedValue);
      int leftRegisterIndex = leftIsMutableValue || toSelf ? byteCodeState.MoveValueToAnyRegister(left, stackSize) : byteCodeState.CopyValueToAnyRegister(left, stackSize);

      using (var registerLock = byteCodeState.LockRegister(leftRegisterIndex))
      {
        int rightRegisterIndex;

        if (left.type is PtrCType && right.type is BuiltInCType && (left.type as PtrCType).pointsTo.GetSize() != 1)
        {
          rightRegisterIndex = byteCodeState.CopyValueToAnyRegister(right, stackSize);

          byteCodeState.instructions.Add(new LLI_MultiplySignedImm(rightRegisterIndex, BitConverter.GetBytes((left.type as PtrCType).pointsTo.GetSize())));
        }
        else if (left is CNamedValue)
        {
          rightRegisterIndex = byteCodeState.CopyValueToAnyRegister(right, stackSize);
        }
        else
        {
          rightRegisterIndex = byteCodeState.MoveValueToAnyRegister(right, stackSize);
        }

        byteCodeState.instructions.Add(new LLI_NegateRegister(rightRegisterIndex));
        byteCodeState.instructions.Add(new LLI_AddRegister(leftRegisterIndex, rightRegisterIndex));

        left.remainingReferences--;
        right.remainingReferences--;

        if (leftIsMutableValue)
          byteCodeState.DumpValue(left);

        if (toSelf)
        {
          if (left is CNamedValue)
            (left as CNamedValue).modifiedSinceLastHome = true;
          else if (left is CGlobalValueReference)
            throw new NotImplementedException();
        }
        else
        {
          byteCodeState.MarkValueAsPosition(resultingValue, Position.Register(leftRegisterIndex), stackSize, true);
        }
      }
    }
  }

  public class CInstruction_Multiply : CInstruction
  {
    CValue left, right;
    SharedValue<long> stackSize;
    CValue resultingValue;
    bool toSelf;

    public CInstruction_Multiply(CValue lvalue, CValue rvalue, SharedValue<long> stackSize, bool toSelf, out CValue resultingValue, string file, int line) : base(file, line)
    {
      if ((lvalue.type is BuiltInCType && (lvalue.type as BuiltInCType).IsFloat()) != (rvalue.type is BuiltInCType && (rvalue.type as BuiltInCType).IsFloat()))
        Compiler.Error($"Both values of the operator have to be floating point or non-floating point. Given: '{lvalue}' and '{rvalue}'.", file, line);

      this.left = lvalue;
      this.right = rvalue;
      this.stackSize = stackSize;
      this.toSelf = toSelf;

      if (toSelf)
        this.resultingValue = lvalue;
      else
        this.resultingValue = new CValue(file, line, lvalue.type.GetSize() >= rvalue.type.GetSize() ? lvalue.type : rvalue.type, true) { description = $"({lvalue} * {rvalue})" };

      if ((lvalue is CConstIntValue || lvalue is CConstFloatValue) && toSelf)
        throw new Exception("Internal Compiler Error: operator cannot be applied to the lvalue directly if it's constant imm.");

      if ((lvalue is CConstFloatValue && !(rvalue is CConstFloatValue)) || (lvalue is CConstIntValue && !(rvalue is CConstIntValue)))
      {
        var tmp = lvalue;
        lvalue = rvalue;
        rvalue = tmp;
      }

      resultingValue = this.resultingValue;
      left.remainingReferences++;
      right.remainingReferences++;
    }

    public override void GetLLInstructions(ref ByteCodeState byteCodeState)
    {
      if (!left.isInitialized)
        Compiler.Error($"Cannot perform operator on uninitialized left value {left}.", file, line);

      if (!right.isInitialized)
        Compiler.Error($"Cannot perform operator on uninitialized right value {right}.", file, line);

      bool isNop = (right is CConstIntValue && ((right.type as BuiltInCType).IsUnsigned() && (right as CConstIntValue).uvalue == 1) || (!(right.type as BuiltInCType).IsUnsigned() && (right as CConstIntValue).ivalue == 1)) || (right is CConstFloatValue && (right as CConstFloatValue).value == 1.0);

      if (isNop)
      {
        left.remainingReferences--;
        right.remainingReferences--;
        return;
      }

      bool isWrite = (right is CConstIntValue && (((right.type as BuiltInCType).IsUnsigned() && (right as CConstIntValue).uvalue == 0) || (!(right.type as BuiltInCType).IsUnsigned() && (right as CConstIntValue).ivalue == 0))) || (right is CConstFloatValue && ((right as CConstFloatValue).value == 0 || double.IsNaN((right as CConstFloatValue).value)));

      bool leftIsMutableValue = !(left is CNamedValue);
      int leftRegisterIndex = 0;

      if (isWrite)
      {
        if ((leftIsMutableValue || toSelf) && left.position.type == PositionType.InRegister)
          leftRegisterIndex = left.position.registerIndex;
        else
          leftRegisterIndex = (left.type as BuiltInCType).IsFloat() ? byteCodeState.GetFreeFloatRegister(stackSize) : byteCodeState.GetFreeIntegerRegister(stackSize);
      }
      else
      {
        leftRegisterIndex = leftIsMutableValue || toSelf ? byteCodeState.MoveValueToAnyRegister(left, stackSize) : byteCodeState.CopyValueToAnyRegister(left, stackSize);
      }

      using (var registerLock = byteCodeState.LockRegister(leftRegisterIndex))
      {
        // lvalue can't be imm if rvalue is const, values would've been swapped by the constructor.
        if (right is CConstIntValue)
        {
          if ((left.type is BuiltInCType && !(left.type as BuiltInCType).IsUnsigned()) || (right.type is BuiltInCType && !(right.type as BuiltInCType).IsUnsigned()))
          {
            var rimm = right as CConstIntValue;

            if (rimm.ivalue == 0)
              byteCodeState.instructions.Add(new LLI_MovImmToRegister(leftRegisterIndex, new byte[8]));
            else if (rimm.ivalue == 1)
            { }
            else
              byteCodeState.instructions.Add(new LLI_MultiplySignedImm(leftRegisterIndex, BitConverter.GetBytes(rimm.uvalue)));
          }
          else
          {
            var rimm = right as CConstIntValue;

            if (rimm.uvalue == 0)
              byteCodeState.instructions.Add(new LLI_MovImmToRegister(leftRegisterIndex, new byte[8]));
            else if (rimm.uvalue == 1)
            { }  
            else
              byteCodeState.instructions.Add(new LLI_MultiplyUnsignedImm(leftRegisterIndex, BitConverter.GetBytes(rimm.uvalue)));
          }
        }
        else if (right is CConstFloatValue)
        {
          var rimm = right as CConstFloatValue;

          if (rimm.value == 0)
            byteCodeState.instructions.Add(new LLI_MovImmToRegister(leftRegisterIndex, new byte[8]));
          else if (double.IsNaN(rimm.value))
          {
            Compiler.Warn($"Multiplying {left} with {right}, which is NaN.", file, line);
            byteCodeState.instructions.Add(new LLI_MovImmToRegister(leftRegisterIndex, BitConverter.GetBytes(rimm.value)));
          }
          else if (rimm.value == 1.0)
          { }
          else
            byteCodeState.instructions.Add(new LLI_MultiplySignedImm(leftRegisterIndex, BitConverter.GetBytes((right as CConstFloatValue).value)));
        }
        else
        {
          int rvalueRegisterIndex = byteCodeState.MoveValueToAnyRegister(right, stackSize);

          if ((left.type is BuiltInCType && (!(left.type as BuiltInCType).IsUnsigned() || (left.type as BuiltInCType).IsFloat())) || (right.type is BuiltInCType && (!(right.type as BuiltInCType).IsUnsigned() || (right.type as BuiltInCType).IsFloat())))
            byteCodeState.instructions.Add(new LLI_MultiplySignedRegister(leftRegisterIndex, rvalueRegisterIndex));
          else
            byteCodeState.instructions.Add(new LLI_MultiplyUnsignedRegister(leftRegisterIndex, rvalueRegisterIndex));
        }

        left.remainingReferences--;
        right.remainingReferences--;

        if (leftIsMutableValue)
          byteCodeState.DumpValue(left);

        if (toSelf)
        {
          if (left is CNamedValue)
            (left as CNamedValue).modifiedSinceLastHome = true;
          else if (left is CGlobalValueReference)
            throw new NotImplementedException();
        }
        else
        {
          byteCodeState.MarkValueAsPosition(resultingValue, Position.Register(leftRegisterIndex), stackSize, true);
        }
      }
    }
  }

  public class CInstruction_Divide : CInstruction
  {
    CValue left, right;
    SharedValue<long> stackSize;
    CValue resultingValue;
    bool toSelf;

    public CInstruction_Divide(CValue lvalue, CValue rvalue, SharedValue<long> stackSize, bool toSelf, out CValue resultingValue, string file, int line) : base(file, line)
    {
      if ((lvalue.type is BuiltInCType && (lvalue.type as BuiltInCType).IsFloat()) != (rvalue.type is BuiltInCType && (rvalue.type as BuiltInCType).IsFloat()))
        Compiler.Error($"Both values of the operator have to be floating point or non-floating point. Given: '{lvalue}' and '{rvalue}'.", file, line);

      this.left = lvalue;
      this.right = rvalue;
      this.stackSize = stackSize;
      this.toSelf = toSelf;

      if (toSelf)
        this.resultingValue = lvalue;
      else
        this.resultingValue = new CValue(file, line, lvalue.type.GetSize() >= rvalue.type.GetSize() ? lvalue.type : rvalue.type, true) { description = $"({lvalue} / {rvalue})" };

      if ((lvalue is CConstIntValue || lvalue is CConstFloatValue) && toSelf)
        throw new Exception("Internal Compiler Error: operator cannot be applied to the lvalue directly if it's constant imm.");

      resultingValue = this.resultingValue;
      left.remainingReferences++;
      right.remainingReferences++;
    }

    public override void GetLLInstructions(ref ByteCodeState byteCodeState)
    {
      if (!left.isInitialized)
        Compiler.Error($"Cannot perform operator on uninitialized left value {left}.", file, line);

      if (!right.isInitialized)
        Compiler.Error($"Cannot perform operator on uninitialized right value {right}.", file, line);

      bool isNop = (right is CConstIntValue && ((right.type as BuiltInCType).IsUnsigned() && (right as CConstIntValue).uvalue == 1) || (!(right.type as BuiltInCType).IsUnsigned() && (right as CConstIntValue).ivalue == 1)) || (right is CConstFloatValue && (right as CConstFloatValue).value == 1.0);

      if (isNop)
      {
        left.remainingReferences--;
        right.remainingReferences--;
        return;
      }

      bool leftIsMutableValue = !(left is CNamedValue);
      int leftRegisterIndex = leftIsMutableValue || toSelf ? byteCodeState.MoveValueToAnyRegister(left, stackSize) : byteCodeState.CopyValueToAnyRegister(left, stackSize);

      using (var registerLock = byteCodeState.LockRegister(leftRegisterIndex))
      {
        if (right is CConstIntValue)
        {
          var rimm = right as CConstIntValue;

          if ((left.type is BuiltInCType && !(left.type as BuiltInCType).IsUnsigned()) || (right.type is BuiltInCType && !(right.type as BuiltInCType).IsUnsigned()))
          {
            if (rimm.ivalue == 0)
              Compiler.Error($"Attempting to divide {left} by zero ({right}).", file, line);

            byteCodeState.instructions.Add(new LLI_DivideSignedImm(leftRegisterIndex, BitConverter.GetBytes(rimm.ivalue)));
          }
          else
          {
            if (rimm.uvalue == 0)
              Compiler.Error($"Attempting to divide {left} by zero ({right}).", file, line);

            byteCodeState.instructions.Add(new LLI_DivideUnsignedImm(leftRegisterIndex, BitConverter.GetBytes(rimm.uvalue)));
          }
        }
        else if (right is CConstFloatValue)
        {
          // Multiplying should be faster than dividing.
          //byteCodeState.instructions.Add(new LLI_DivideSignedImm(leftRegisterIndex, BitConverter.GetBytes((right as CConstFloatValue).value)));
          byteCodeState.instructions.Add(new LLI_MultiplySignedImm(leftRegisterIndex, BitConverter.GetBytes(1.0 / (right as CConstFloatValue).value)));
        }
        else
        {
          int rvalueRegisterIndex = byteCodeState.MoveValueToAnyRegister(right, stackSize);

          if ((left.type is BuiltInCType && (!(left.type as BuiltInCType).IsUnsigned() || (left.type as BuiltInCType).IsFloat())) || (right.type is BuiltInCType && (!(right.type as BuiltInCType).IsUnsigned() || (right.type as BuiltInCType).IsFloat())))
            byteCodeState.instructions.Add(new LLI_DivideSignedRegister(leftRegisterIndex, rvalueRegisterIndex));
          else
            byteCodeState.instructions.Add(new LLI_DivideUnsignedRegister(leftRegisterIndex, rvalueRegisterIndex));
        }

        left.remainingReferences--;
        right.remainingReferences--;

        if (leftIsMutableValue)
          byteCodeState.DumpValue(left);

        if (toSelf)
        {
          if (left is CNamedValue)
            (left as CNamedValue).modifiedSinceLastHome = true;
          else if (left is CGlobalValueReference)
            throw new NotImplementedException();
        }
        else
        {
          byteCodeState.MarkValueAsPosition(resultingValue, Position.Register(leftRegisterIndex), stackSize, true);
        }
      }
    }
  }

  public class CInstruction_Modulo : CInstruction
  {
    CValue left, right;
    SharedValue<long> stackSize;
    CValue resultingValue;
    bool toSelf;

    public CInstruction_Modulo(CValue lvalue, CValue rvalue, SharedValue<long> stackSize, bool toSelf, out CValue resultingValue, string file, int line) : base(file, line)
    {
      if ((lvalue.type is BuiltInCType && (lvalue.type as BuiltInCType).IsFloat()) || (rvalue.type is BuiltInCType && (rvalue.type as BuiltInCType).IsFloat()))
        Compiler.Error($"Both values of the operator have to be non-floating point. Given: '{lvalue}' and '{rvalue}'.", file, line);

      this.left = lvalue;
      this.right = rvalue;
      this.stackSize = stackSize;
      this.toSelf = toSelf;

      if (toSelf)
        this.resultingValue = lvalue;
      else
        this.resultingValue = new CValue(file, line, lvalue.type.GetSize() >= rvalue.type.GetSize() ? lvalue.type : rvalue.type, true) { description = $"({lvalue} % {rvalue})" };

      if ((lvalue is CConstIntValue || lvalue is CConstFloatValue) && toSelf)
        throw new Exception("Internal Compiler Error: operator cannot be applied to the lvalue directly if it's constant imm.");

      resultingValue = this.resultingValue;
      left.remainingReferences++;
      right.remainingReferences++;
    }

    public override void GetLLInstructions(ref ByteCodeState byteCodeState)
    {
      if (!left.isInitialized)
        Compiler.Error($"Cannot perform operator on uninitialized left value {left}.", file, line);

      if (!right.isInitialized)
        Compiler.Error($"Cannot perform operator on uninitialized left {right}.", file, line);

      bool leftIsMutableValue = !(left is CNamedValue);
      int leftRegisterIndex = leftIsMutableValue || toSelf ? byteCodeState.MoveValueToAnyRegister(left, stackSize) : byteCodeState.CopyValueToAnyRegister(left, stackSize);

      using (var registerLock = byteCodeState.LockRegister(leftRegisterIndex))
      {
        // lvalue can't be imm if rvalue is const, values would've been swapped by the constructor.
        if (right is CConstIntValue)
        {
          byteCodeState.instructions.Add(new LLI_ModuloImm(leftRegisterIndex, BitConverter.GetBytes((right as CConstIntValue).uvalue)));
        }
        else
        {
          int rvalueRegisterIndex = byteCodeState.MoveValueToAnyRegister(right, stackSize);

          byteCodeState.instructions.Add(new LLI_ModuloRegister(leftRegisterIndex, rvalueRegisterIndex));
        }

        left.remainingReferences--;
        right.remainingReferences--;

        if (leftIsMutableValue)
          byteCodeState.DumpValue(left);

        if (toSelf)
        {
          if (left is CNamedValue)
            (left as CNamedValue).modifiedSinceLastHome = true;
          else if (left is CGlobalValueReference)
            throw new NotImplementedException();
        }
        else
        {
          byteCodeState.MarkValueAsPosition(resultingValue, Position.Register(leftRegisterIndex), stackSize, true);
        }
      }
    }
  }

  public class CInstruction_And : CInstruction
  {
    CValue left, right;
    SharedValue<long> stackSize;
    CValue resultingValue;
    bool toSelf;

    public CInstruction_And(CValue lvalue, CValue rvalue, SharedValue<long> stackSize, bool toSelf, out CValue resultingValue, string file, int line) : base(file, line)
    {
      if ((lvalue.type is BuiltInCType && (lvalue.type as BuiltInCType).IsFloat()) || (rvalue.type is BuiltInCType && (rvalue.type as BuiltInCType).IsFloat()))
        Compiler.Error($"Both values of the operator have to be non-floating point. Given: '{lvalue}' and '{rvalue}'.", file, line);

      this.left = lvalue;
      this.right = rvalue;
      this.stackSize = stackSize;
      this.toSelf = toSelf;

      if (toSelf)
        this.resultingValue = lvalue;
      else
        this.resultingValue = new CValue(file, line, lvalue.type.GetSize() >= rvalue.type.GetSize() ? lvalue.type : rvalue.type, true) { description = $"({lvalue} & {rvalue})" };

      if ((lvalue is CConstIntValue || lvalue is CConstFloatValue) && toSelf)
        throw new Exception("Internal Compiler Error: operator cannot be applied to the lvalue directly if it's constant imm.");

      if (lvalue is CConstIntValue && !(rvalue is CConstIntValue))
      {
        var tmp = lvalue;
        lvalue = rvalue;
        rvalue = tmp;
      }

      resultingValue = this.resultingValue;
      left.remainingReferences++;
      right.remainingReferences++;
    }

    public override void GetLLInstructions(ref ByteCodeState byteCodeState)
    {
      if (!left.isInitialized)
        Compiler.Error($"Cannot perform operator on uninitialized left value {left}.", file, line);

      if (!right.isInitialized)
        Compiler.Error($"Cannot perform operator on uninitialized right value {right}.", file, line);

      bool leftIsMutableValue = !(left is CNamedValue);
      int leftRegisterIndex = leftIsMutableValue || toSelf ? byteCodeState.MoveValueToAnyRegister(left, stackSize) : byteCodeState.CopyValueToAnyRegister(left, stackSize);

      using (var registerLock = byteCodeState.LockRegister(leftRegisterIndex))
      {
        // lvalue can't be imm if rvalue is const, values would've been swapped by the constructor.
        if (right is CConstIntValue)
        {
          byteCodeState.instructions.Add(new LLI_AndImm(leftRegisterIndex, BitConverter.GetBytes((right as CConstIntValue).uvalue)));
        }
        else
        {
          int rvalueRegisterIndex = byteCodeState.MoveValueToAnyRegister(right, stackSize);

          byteCodeState.instructions.Add(new LLI_AndRegister(leftRegisterIndex, rvalueRegisterIndex));
        }

        left.remainingReferences--;
        right.remainingReferences--;

        if (leftIsMutableValue)
          byteCodeState.DumpValue(left);

        if (toSelf)
        {
          if (left is CNamedValue)
            (left as CNamedValue).modifiedSinceLastHome = true;
          else if (left is CGlobalValueReference)
            throw new NotImplementedException();
        }
        else
        {
          byteCodeState.MarkValueAsPosition(resultingValue, Position.Register(leftRegisterIndex), stackSize, true);
        }
      }
    }
  }

  public class CInstruction_IntOnIntRegister : CInstruction
  {
    CValue left, right;
    SharedValue<long> stackSize;
    CValue resultingValue;
    bool toSelf;
    Func<int, int, LLInstruction> operation;

    public CInstruction_IntOnIntRegister(CValue lvalue, CValue rvalue, SharedValue<long> stackSize, bool toSelf, out CValue resultingValue, string file, int line, string operatorChars, Func<int, int, LLInstruction> operation) : base(file, line)
    {
      if ((lvalue.type is BuiltInCType && (lvalue.type as BuiltInCType).IsFloat()) || (rvalue.type is BuiltInCType && (rvalue.type as BuiltInCType).IsFloat()))
        Compiler.Error($"Both values of the operator have to be non-floating point. Given: '{lvalue}' and '{rvalue}'.", file, line);

      this.left = lvalue;
      this.right = rvalue;
      this.stackSize = stackSize;
      this.toSelf = toSelf;
      this.operation = operation;

      if (toSelf)
        this.resultingValue = lvalue;
      else
        this.resultingValue = new CValue(file, line, lvalue.type.GetSize() >= rvalue.type.GetSize() ? lvalue.type : rvalue.type, true) { description = $"({lvalue} {operatorChars} {rvalue})" };

      if ((lvalue is CConstIntValue || lvalue is CConstFloatValue) && toSelf)
        throw new Exception("Internal Compiler Error: operator cannot be applied to the lvalue directly if it's constant imm.");

      resultingValue = this.resultingValue;
      left.remainingReferences++;
      right.remainingReferences++;
    }

    public override void GetLLInstructions(ref ByteCodeState byteCodeState)
    {
      if (!left.isInitialized)
        Compiler.Error($"Cannot perform operator on uninitialized left value {left}.", file, line);

      if (!right.isInitialized)
        Compiler.Error($"Cannot perform operator on uninitialized right value {right}.", file, line);

      bool leftIsMutableValue = !(left is CNamedValue);
      int leftRegisterIndex = leftIsMutableValue || toSelf ? byteCodeState.MoveValueToAnyRegister(left, stackSize) : byteCodeState.CopyValueToAnyRegister(left, stackSize);

      using (var registerLock = byteCodeState.LockRegister(leftRegisterIndex))
      {
        int rvalueRegisterIndex = byteCodeState.MoveValueToAnyRegister(right, stackSize);

        byteCodeState.instructions.Add(operation(leftRegisterIndex, rvalueRegisterIndex));

        left.remainingReferences--;
        right.remainingReferences--;

        if (leftIsMutableValue)
          byteCodeState.DumpValue(left);

        if (toSelf)
        {
          if (left is CNamedValue)
            (left as CNamedValue).modifiedSinceLastHome = true;
          else if (left is CGlobalValueReference)
            throw new NotImplementedException();
        }
        else
        {
          byteCodeState.MarkValueAsPosition(resultingValue, Position.Register(leftRegisterIndex), stackSize, true);
        }
      }
    }
  }

  public class CInstruction_Or : CInstruction_IntOnIntRegister
  {
    public CInstruction_Or(CValue lvalue, CValue rvalue, SharedValue<long> stackSize, bool toSelf, out CValue resultingValue, string file, int line) : base(lvalue, rvalue, stackSize, toSelf, out resultingValue, file, line, "|", (int lval, int rval) => new LLI_OrRegister(lval, rval))
    {
    }
  }

  public class CInstruction_XOr : CInstruction_IntOnIntRegister
  {
    public CInstruction_XOr(CValue lvalue, CValue rvalue, SharedValue<long> stackSize, bool toSelf, out CValue resultingValue, string file, int line) : base(lvalue, rvalue, stackSize, toSelf, out resultingValue, file, line, "^", (int lval, int rval) => new LLI_XOrRegister(lval, rval))
    {
    }
  }

  public class CInstruction_BitShiftLeft : CInstruction_IntOnIntRegister
  {
    public CInstruction_BitShiftLeft(CValue lvalue, CValue rvalue, SharedValue<long> stackSize, bool toSelf, out CValue resultingValue, string file, int line) : base(lvalue, rvalue, stackSize, toSelf, out resultingValue, file, line, "#<#", (int lval, int rval) => new LLI_BitShiftLeftRegister(lval, rval))
    {
    }
  }

  public class CInstruction_BitShiftRight : CInstruction_IntOnIntRegister
  {
    public CInstruction_BitShiftRight(CValue lvalue, CValue rvalue, SharedValue<long> stackSize, bool toSelf, out CValue resultingValue, string file, int line) : base(lvalue, rvalue, stackSize, toSelf, out resultingValue, file, line, "#>#", (int lval, int rval) => new LLI_BitShiftRightRegister(lval, rval))
    {
    }
  }

  public class CInstruction_LogicalAnd : CInstruction_IntOnIntRegister
  {
    public CInstruction_LogicalAnd(CValue lvalue, CValue rvalue, SharedValue<long> stackSize, bool toSelf, out CValue resultingValue, string file, int line) : base(lvalue, rvalue, stackSize, toSelf, out resultingValue, file, line, "||", (int lval, int rval) => new LLI_LogicalAndRegister(lval, rval))
    {
    }
  }

  public class CInstruction_LogicalOr : CInstruction_IntOnIntRegister
  {
    public CInstruction_LogicalOr(CValue lvalue, CValue rvalue, SharedValue<long> stackSize, bool toSelf, out CValue resultingValue, string file, int line) : base(lvalue, rvalue, stackSize, toSelf, out resultingValue, file, line, "||", (int lval, int rval) => new LLI_LogicalOrRegister(lval, rval))
    {
    }
  }

  public class CInstruction_Equals : CInstruction
  {
    CValue left, right;
    SharedValue<long> stackSize;
    CValue resultingValue;

    public CInstruction_Equals(CValue lvalue, CValue rvalue, SharedValue<long> stackSize, out CValue resultingValue, string file, int line) : base(file, line)
    {
      if ((lvalue.type is BuiltInCType && (lvalue.type as BuiltInCType).IsFloat()) || (rvalue.type is BuiltInCType && (rvalue.type as BuiltInCType).IsFloat()))
        Compiler.Error($"Both values of the operator have to be non-floating point. Given: '{lvalue}' and '{rvalue}'.", file, line);

      this.left = lvalue;
      this.right = rvalue;
      this.stackSize = stackSize;

      this.resultingValue = new CValue(file, line, BuiltInCType.Types["u8"], true) { description = $"({lvalue} == {rvalue})" };

      resultingValue = this.resultingValue;
      left.remainingReferences++;
      right.remainingReferences++;
    }

    public override void GetLLInstructions(ref ByteCodeState byteCodeState)
    {
      if (!left.isInitialized)
        Compiler.Error($"Cannot perform operator on uninitialized lvalue {left}.", file, line);

      if (!right.isInitialized)
        Compiler.Error($"Cannot perform operator on uninitialized rvalue {right}.", file, line);

      bool leftIsMutableValue = !(left is CNamedValue);
      int leftRegisterIndex = leftIsMutableValue ? byteCodeState.MoveValueToAnyRegister(left, stackSize) : byteCodeState.CopyValueToAnyRegister(left, stackSize);

      using (var registerLock = byteCodeState.LockRegister(leftRegisterIndex))
      {
        int rvalueRegisterIndex = byteCodeState.MoveValueToAnyRegister(right, stackSize);

        byteCodeState.instructions.Add(new LLI_EqualsRegister(leftRegisterIndex, rvalueRegisterIndex));

        left.remainingReferences--;
        right.remainingReferences--;

        if (leftIsMutableValue)
          byteCodeState.DumpValue(left);

        byteCodeState.MarkValueAsPosition(resultingValue, Position.Register(leftRegisterIndex), stackSize, true);
      }
    }
  }

  public class CInstruction_NotEquals : CInstruction
  {
    CValue left, right;
    SharedValue<long> stackSize;
    CValue resultingValue;

    public CInstruction_NotEquals(CValue lvalue, CValue rvalue, SharedValue<long> stackSize, out CValue resultingValue, string file, int line) : base(file, line)
    {
      if ((lvalue.type is BuiltInCType && (lvalue.type as BuiltInCType).IsFloat()) || (rvalue.type is BuiltInCType && (rvalue.type as BuiltInCType).IsFloat()))
        Compiler.Error($"Both values of the operator have to be non-floating point. Given: '{lvalue}' and '{rvalue}'.", file, line);

      this.left = lvalue;
      this.right = rvalue;
      this.stackSize = stackSize;

      this.resultingValue = new CValue(file, line, BuiltInCType.Types["u8"], true) { description = $"({lvalue} != {rvalue})" };

      resultingValue = this.resultingValue;
      left.remainingReferences++;
      right.remainingReferences++;
    }

    public override void GetLLInstructions(ref ByteCodeState byteCodeState)
    {
      if (!left.isInitialized)
        Compiler.Error($"Cannot perform operator on uninitialized lvalue {left}.", file, line);

      if (!right.isInitialized)
        Compiler.Error($"Cannot perform operator on uninitialized rvalue {right}.", file, line);

      bool leftIsMutableValue = !(left is CNamedValue);
      int leftRegisterIndex = leftIsMutableValue ? byteCodeState.MoveValueToAnyRegister(left, stackSize) : byteCodeState.CopyValueToAnyRegister(left, stackSize);

      using (var registerLock = byteCodeState.LockRegister(leftRegisterIndex))
      {
        int rvalueRegisterIndex = byteCodeState.MoveValueToAnyRegister(right, stackSize);

        byteCodeState.instructions.Add(new LLI_EqualsRegister(leftRegisterIndex, rvalueRegisterIndex));

        left.remainingReferences--;
        right.remainingReferences--;

        if (leftIsMutableValue)
          byteCodeState.DumpValue(left);

        byteCodeState.instructions.Add(new LLI_NotRegister(leftRegisterIndex));

        byteCodeState.MarkValueAsPosition(resultingValue, Position.Register(leftRegisterIndex), stackSize, true);
      }
    }
  }

  public class CInstruction_LessOrEqual : CInstruction
  {
    CValue left, right;
    SharedValue<long> stackSize;
    CValue resultingValue;

    public CInstruction_LessOrEqual(CValue lvalue, CValue rvalue, SharedValue<long> stackSize, out CValue resultingValue, string file, int line) : base(file, line)
    {
      if ((lvalue.type is BuiltInCType && (lvalue.type as BuiltInCType).IsFloat()) || (rvalue.type is BuiltInCType && (rvalue.type as BuiltInCType).IsFloat()))
        Compiler.Error($"Both values of the operator have to be non-floating point. Given: '{lvalue}' and '{rvalue}'.", file, line);

      this.left = lvalue;
      this.right = rvalue;
      this.stackSize = stackSize;

      this.resultingValue = new CValue(file, line, BuiltInCType.Types["u8"], true) { description = $"({lvalue} <= {rvalue})" };

      resultingValue = this.resultingValue;
      left.remainingReferences++;
      right.remainingReferences++;
    }

    public override void GetLLInstructions(ref ByteCodeState byteCodeState)
    {
      if (!left.isInitialized)
        Compiler.Error($"Cannot perform operator on uninitialized lvalue {left}.", file, line);

      if (!right.isInitialized)
        Compiler.Error($"Cannot perform operator on uninitialized rvalue {right}.", file, line);

      bool leftIsMutableValue = !(left is CNamedValue);
      int leftRegisterIndex = leftIsMutableValue ? byteCodeState.MoveValueToAnyRegister(left, stackSize) : byteCodeState.CopyValueToAnyRegister(left, stackSize);

      using (var registerLock = byteCodeState.LockRegister(leftRegisterIndex))
      {
        int rvalueRegisterIndex = byteCodeState.MoveValueToAnyRegister(right, stackSize);

        byteCodeState.instructions.Add(new LLI_GreaterThanRegister(leftRegisterIndex, rvalueRegisterIndex));

        left.remainingReferences--;
        right.remainingReferences--;

        if (leftIsMutableValue)
          byteCodeState.DumpValue(left);

        byteCodeState.instructions.Add(new LLI_NotRegister(leftRegisterIndex));

        byteCodeState.MarkValueAsPosition(resultingValue, Position.Register(leftRegisterIndex), stackSize, true);
      }
    }
  }

  public class CInstruction_GreaterOrEqual : CInstruction
  {
    CValue left, right;
    SharedValue<long> stackSize;
    CValue resultingValue;

    public CInstruction_GreaterOrEqual(CValue lvalue, CValue rvalue, SharedValue<long> stackSize, out CValue resultingValue, string file, int line) : base(file, line)
    {
      if ((lvalue.type is BuiltInCType && (lvalue.type as BuiltInCType).IsFloat()) || (rvalue.type is BuiltInCType && (rvalue.type as BuiltInCType).IsFloat()))
        Compiler.Error($"Both values of the operator have to be non-floating point. Given: '{lvalue}' and '{rvalue}'.", file, line);

      this.left = lvalue;
      this.right = rvalue;
      this.stackSize = stackSize;

      this.resultingValue = new CValue(file, line, BuiltInCType.Types["u8"], true) { description = $"({lvalue} >= {rvalue})" };

      resultingValue = this.resultingValue;
      left.remainingReferences++;
      right.remainingReferences++;
    }

    public override void GetLLInstructions(ref ByteCodeState byteCodeState)
    {
      if (!left.isInitialized)
        Compiler.Error($"Cannot perform operator on uninitialized lvalue {left}.", file, line);

      if (!right.isInitialized)
        Compiler.Error($"Cannot perform operator on uninitialized rvalue {right}.", file, line);

      bool leftIsMutableValue = !(left is CNamedValue);
      int leftRegisterIndex = leftIsMutableValue ? byteCodeState.MoveValueToAnyRegister(left, stackSize) : byteCodeState.CopyValueToAnyRegister(left, stackSize);

      using (var registerLock = byteCodeState.LockRegister(leftRegisterIndex))
      {
        int rvalueRegisterIndex = byteCodeState.MoveValueToAnyRegister(right, stackSize);

        byteCodeState.instructions.Add(new LLI_LessThanRegister(leftRegisterIndex, rvalueRegisterIndex));

        left.remainingReferences--;
        right.remainingReferences--;

        if (leftIsMutableValue)
          byteCodeState.DumpValue(left);

        byteCodeState.instructions.Add(new LLI_NotRegister(leftRegisterIndex));

        byteCodeState.MarkValueAsPosition(resultingValue, Position.Register(leftRegisterIndex), stackSize, true);
      }
    }
  }

  public class CInstruction_Less : CInstruction
  {
    CValue left, right;
    SharedValue<long> stackSize;
    CValue resultingValue;

    public CInstruction_Less(CValue lvalue, CValue rvalue, SharedValue<long> stackSize, out CValue resultingValue, string file, int line) : base(file, line)
    {
      if ((lvalue.type is BuiltInCType && (lvalue.type as BuiltInCType).IsFloat()) || (rvalue.type is BuiltInCType && (rvalue.type as BuiltInCType).IsFloat()))
        Compiler.Error($"Both values of the operator have to be non-floating point. Given: '{lvalue}' and '{rvalue}'.", file, line);

      this.left = lvalue;
      this.right = rvalue;
      this.stackSize = stackSize;

      this.resultingValue = new CValue(file, line, BuiltInCType.Types["u8"], true) { description = $"({lvalue} < {rvalue})" };

      resultingValue = this.resultingValue;
      left.remainingReferences++;
      right.remainingReferences++;
    }

    public override void GetLLInstructions(ref ByteCodeState byteCodeState)
    {
      if (!left.isInitialized)
        Compiler.Error($"Cannot perform operator on uninitialized lvalue {left}.", file, line);

      if (!right.isInitialized)
        Compiler.Error($"Cannot perform operator on uninitialized rvalue {right}.", file, line);

      bool leftIsMutableValue = !(left is CNamedValue);
      int leftRegisterIndex = leftIsMutableValue ? byteCodeState.MoveValueToAnyRegister(left, stackSize) : byteCodeState.CopyValueToAnyRegister(left, stackSize);

      using (var registerLock = byteCodeState.LockRegister(leftRegisterIndex))
      {
        int rvalueRegisterIndex = byteCodeState.MoveValueToAnyRegister(right, stackSize);

        byteCodeState.instructions.Add(new LLI_LessThanRegister(leftRegisterIndex, rvalueRegisterIndex));

        left.remainingReferences--;
        right.remainingReferences--;

        if (leftIsMutableValue)
          byteCodeState.DumpValue(left);

        byteCodeState.MarkValueAsPosition(resultingValue, Position.Register(leftRegisterIndex), stackSize, true);
      }
    }
  }

  public class CInstruction_Greater : CInstruction
  {
    CValue left, right;
    SharedValue<long> stackSize;
    CValue resultingValue;

    public CInstruction_Greater(CValue lvalue, CValue rvalue, SharedValue<long> stackSize, out CValue resultingValue, string file, int line) : base(file, line)
    {
      if ((lvalue.type is BuiltInCType && (lvalue.type as BuiltInCType).IsFloat()) || (rvalue.type is BuiltInCType && (rvalue.type as BuiltInCType).IsFloat()))
        Compiler.Error($"Both values of the operator have to be non-floating point. Given: '{lvalue}' and '{rvalue}'.", file, line);

      this.left = lvalue;
      this.right = rvalue;
      this.stackSize = stackSize;

      this.resultingValue = new CValue(file, line, BuiltInCType.Types["u8"], true) { description = $"({lvalue} > {rvalue})" };

      resultingValue = this.resultingValue;
      left.remainingReferences++;
      right.remainingReferences++;
    }

    public override void GetLLInstructions(ref ByteCodeState byteCodeState)
    {
      if (!left.isInitialized)
        Compiler.Error($"Cannot perform operator on uninitialized lvalue {left}.", file, line);

      if (!right.isInitialized)
        Compiler.Error($"Cannot perform operator on uninitialized rvalue {right}.", file, line);

      bool leftIsMutableValue = !(left is CNamedValue);
      int leftRegisterIndex = leftIsMutableValue ? byteCodeState.MoveValueToAnyRegister(left, stackSize) : byteCodeState.CopyValueToAnyRegister(left, stackSize);

      using (var registerLock = byteCodeState.LockRegister(leftRegisterIndex))
      {
        int rvalueRegisterIndex = byteCodeState.MoveValueToAnyRegister(right, stackSize);

        byteCodeState.instructions.Add(new LLI_GreaterThanRegister(leftRegisterIndex, rvalueRegisterIndex));

        left.remainingReferences--;
        right.remainingReferences--;

        if (leftIsMutableValue)
          byteCodeState.DumpValue(left);

        byteCodeState.MarkValueAsPosition(resultingValue, Position.Register(leftRegisterIndex), stackSize, true);
      }
    }
  }

  public class CInstruction_Inverse : CInstruction
  {
    CValue value;
    SharedValue<long> stackSize;
    CValue resultingValue;

    public CInstruction_Inverse(CValue value, SharedValue<long> stackSize, out CValue resultingValue, string file, int line) : base(file, line)
    {
      if (value.type is BuiltInCType && (value.type as BuiltInCType).IsFloat())
        Compiler.Error($"Value of the operator have to be non-floating point. Given: '{value}'.", file, line);

      this.value = value;
      this.stackSize = stackSize;

      this.resultingValue = new CValue(file, line, value.type, true) { description = $"~({value})" };

      resultingValue = this.resultingValue;
      value.remainingReferences++;
    }

    public override void GetLLInstructions(ref ByteCodeState byteCodeState)
    {
      if (!value.isInitialized)
        Compiler.Error($"Cannot perform operator on uninitialized lvalue {value}.", file, line);

      bool isMutableValue = !(value is CNamedValue);
      int registerIndex = isMutableValue ? byteCodeState.MoveValueToAnyRegister(value, stackSize) : byteCodeState.CopyValueToAnyRegister(value, stackSize);

      using (var registerLock = byteCodeState.LockRegister(registerIndex))
      {
        byteCodeState.instructions.Add(new LLI_InverseRegister(registerIndex));

        value.remainingReferences--;

        if (isMutableValue)
          byteCodeState.DumpValue(value);

        byteCodeState.MarkValueAsPosition(resultingValue, Position.Register(registerIndex), stackSize, true);
      }
    }
  }

  public class CInstruction_LogicalNot : CInstruction
  {
    CValue value;
    SharedValue<long> stackSize;
    CValue resultingValue;

    public CInstruction_LogicalNot(CValue value, SharedValue<long> stackSize, out CValue resultingValue, string file, int line) : base(file, line)
    {
      if (value.type is BuiltInCType && (value.type as BuiltInCType).IsFloat())
        Compiler.Error($"Value of the operator have to be non-floating point. Given: '{value}'.", file, line);

      this.value = value;
      this.stackSize = stackSize;

      this.resultingValue = new CValue(file, line, value.type, true) { description = $"!({value})" };

      resultingValue = this.resultingValue;
      value.remainingReferences++;
    }

    public override void GetLLInstructions(ref ByteCodeState byteCodeState)
    {
      if (!value.isInitialized)
        Compiler.Error($"Cannot perform operator on uninitialized lvalue {value}.", file, line);

      bool isMutableValue = !(value is CNamedValue);
      int registerIndex = isMutableValue ? byteCodeState.MoveValueToAnyRegister(value, stackSize) : byteCodeState.CopyValueToAnyRegister(value, stackSize);

      using (var registerLock = byteCodeState.LockRegister(registerIndex))
      {
        byteCodeState.instructions.Add(new LLI_NotRegister(registerIndex));

        value.remainingReferences--;

        if (isMutableValue)
          byteCodeState.DumpValue(value);

        byteCodeState.MarkValueAsPosition(resultingValue, Position.Register(registerIndex), stackSize, true);
      }
    }
  }

  public class CInstruction_Negate : CInstruction
  {
    CValue value;
    SharedValue<long> stackSize;
    CValue resultingValue;

    public CInstruction_Negate(CValue value, SharedValue<long> stackSize, out CValue resultingValue, string file, int line) : base(file, line)
    {
      if (value.type is BuiltInCType && (value.type as BuiltInCType).IsUnsigned())
        Compiler.Error($"Attempting to negate unsigned value '{value}'. This requires a cast.", file, line);

      this.value = value;
      this.stackSize = stackSize;

      this.resultingValue = new CValue(file, line, value.type, true) { description = $"-({value})" };

      resultingValue = this.resultingValue;
      value.remainingReferences++;
    }

    public override void GetLLInstructions(ref ByteCodeState byteCodeState)
    {
      if (!value.isInitialized)
        Compiler.Error($"Cannot perform operator on uninitialized lvalue {value}.", file, line);

      bool isMutableValue = !(value is CNamedValue);
      int registerIndex = isMutableValue ? byteCodeState.MoveValueToAnyRegister(value, stackSize) : byteCodeState.CopyValueToAnyRegister(value, stackSize);

      using (var registerLock = byteCodeState.LockRegister(registerIndex))
      {
        byteCodeState.instructions.Add(new LLI_NegateRegister(registerIndex));

        value.remainingReferences--;

        if (isMutableValue)
          byteCodeState.DumpValue(value);

        byteCodeState.MarkValueAsPosition(resultingValue, Position.Register(registerIndex), stackSize, true);
      }
    }
  }
}
