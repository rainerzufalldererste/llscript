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

      foreach (var parameter in function.parameters)
        if (parameter.value.position.type == PositionType.InRegister)
          byteCodeState.registers[parameter.value.position.registerIndex] = parameter.value;

      foreach (var param in function.parameters)
        byteCodeState.instructions.Add(new LLI_Location_PseudoInstruction(param.value, function.minStackSize, byteCodeState));

      byteCodeState.instructions.Add(new LLI_StackIncrementImm(function.minStackSize, 0));
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
      byteCodeState.instructions.Add(new LLI_Return());

      byteCodeState.instructions.Add(new LLI_Comment_PseudoInstruction("End of Function: " + function));
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
    private readonly Scope scope;

    public CInstruction_InitializeArray(CValue value, byte[] data, string file, int line, SharedValue<long> stackSize, Scope parentScope) : base(file, line)
    {
      this.data = data;
      this.value = value;
      this.stackSize = stackSize;
      this.scope = parentScope;

      if (value.hasPosition && value.position.type == PositionType.InRegister)
        throw new Exception("Internal Compiler Error.");

      value.isInitialized = true;
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
          Scope motherScope = scope;

          while (motherScope.parentScope != null)
            motherScope = motherScope.parentScope;

          value.position = Position.GlobalStackBaseOffset(motherScope.maxRequiredStackSpace.Value);

          motherScope.maxRequiredStackSpace.Value += value.type.GetSize();
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

              byte[] _data = new byte[8];

              while (dataSizeRemaining >= 8)
              {
                for (int i = 0; i < 8; i++)
                  _data[i] = data[offset + i];

                byteCodeState.instructions.Add(new LLI_MovImmToRegister(valueRegister, _data.ToArray())); // move value to register.
                byteCodeState.instructions.Add(new LLI_MovRegisterToPtrInRegister(stackPtrRegister, valueRegister)); // move register to ptr.

                dataSizeRemaining -= 8;
                offset += 8;

                if (dataSizeRemaining > 0)
                  byteCodeState.instructions.Add(new LLI_AddImm(stackPtrRegister, BitConverter.GetBytes((ulong)8))); // inc ptr by 8.
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

      if (!sourceValue.type.CanImplicitCastTo(targetValue.type))
      {
        if ((sourceValue is CConstIntValue) && (sourceValue as CConstIntValue).smallestPossibleSignedType != null && (sourceValue as CConstIntValue).smallestPossibleSignedType.CanImplicitCastTo(targetValue.type))
          this.sourceValue = new CConstIntValue((sourceValue as CConstIntValue).uvalue, (sourceValue as CConstIntValue).smallestPossibleSignedType, sourceValue.file, sourceValue.line) { description = $"signed equivalient value of '{sourceValue}'" };

        Compiler.Error($"Type Mismatch: '{sourceValue}' cannot be assigned to '{targetValue}', because there is no implicit conversion available.", file, line);
      }
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
        targetValue.hasPosition = true;
        targetValue.position = sourceValue.position;
        targetValue.isInitialized = true;

        if (targetValue is CNamedValue && (targetValue as CNamedValue).hasHomePosition)
          (targetValue as CNamedValue).modifiedSinceLastHome = true;

        byteCodeState.registers[sourceValue.position.registerIndex] = targetValue;
        sourceValue.hasPosition = false;
        
        if (sourceValue.type.GetSize() > targetValue.type.GetSize())
          byteCodeState.TruncateRegister(sourceValue.position.registerIndex, targetValue.type.GetSize());

        byteCodeState.instructions.Add(new LLI_Location_PseudoInstruction(targetValue, stackSize, byteCodeState));
        
        return;
      }
      // This is too dangerous because I believe we don't properly keep track of references.
      //// if setting the target to a named value that has no more references in the future (and is of the same size).
      //else if (sourceValue is CNamedValue && sourceValue.hasPosition && sourceValue.remainingReferences == 0 && sourceValue.type.GetSize() == targetValue.type.GetSize())
      //{
      //  targetValue.hasPosition = true;
      //  sourceValue.hasPosition = false;
      //  
      //  targetValue.position = sourceValue.position;
      //
      //  sourceValue.isInitialized = false;
      //  targetValue.isInitialized = true;
      //
      //  (targetValue as CNamedValue).hasStackOffset = (sourceValue as CNamedValue).hasStackOffset;
      //  (targetValue as CNamedValue).homeStackOffset = (sourceValue as CNamedValue).homeStackOffset;
      //  (sourceValue as CNamedValue).hasStackOffset = false;
      //}

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
          // Place on stack.
          targetValue.position = Position.StackOffset(stackSize.Value);

          if (targetValue is CNamedValue)
          {
            var t = targetValue as CNamedValue;

            t.hasHomePosition = true;
            t.homePosition = targetValue.position;
            t.modifiedSinceLastHome = false;
          }

          stackSize.Value += targetValue.type.GetSize();
        }
      }

      byteCodeState.CopyValueToPosition(sourceValue, targetValue.position, stackSize);
      byteCodeState.instructions.Add(new LLI_Location_PseudoInstruction(targetValue, stackSize, byteCodeState));

      if (targetValue.position.type == PositionType.InRegister && targetValue is CNamedValue)
      {
        var t = targetValue as CNamedValue;

        if (t.hasHomePosition)
          t.modifiedSinceLastHome = true;
      }
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
            int targetPtrRegister = byteCodeState.MoveValueToAnyRegister(targetValuePtr, stackSize);

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
        if (!arguments[i].isInitialized)
          Compiler.Error($"In function call to '{function}': Argument {(i + 1)} '{arguments[i]}' for parameter {function.parameters[i].type} {function.parameters[i].name} has not been initialized yet. Defined in File '{arguments[i].file ?? "?"}', Line: {arguments[i].line}.", file, line);

        if (!arguments[i].type.CanImplicitCastTo(function.parameters[i].type))
          Compiler.Error($"In function call to '{function}': Argument {(i + 1)} '{arguments[i]}' for parameter {function.parameters[i].type} {function.parameters[i].name} is of mismatching type '{arguments[i].type}' and cannot be converted implicitly. Value defined in File '{arguments[i].file ?? "?"}', Line: {arguments[i].line}.", file, line);
      }


      if (function.returnType is BuiltInCType || function.returnType is PtrCType)
      {
        this.returnValue = new CValue(file, line, function.returnType, true) { description = $"Return Value of \"{function}\"" };
        this.returnValue.type.isConst = true;
      }
      else if (!(function.returnType is VoidCType))
      {
        throw new NotImplementedException();
        // this.returnValue.type.isConst = true;
      }
      else
      {
        this.returnValue = null;
      }

      returnValue = this.returnValue;
    }

    public override void GetLLInstructions(ref ByteCodeState byteCodeState)
    {
      // In case it's a recursive call: backup the parameter positions.
      var originalParameters = function.parameters;
      function.ResetRegisterPositions();

      // Backup Registers.
      byteCodeState.BackupRegisterValues(stackSize);

      for (int i = arguments.Count - 1; i >= 0; i--)
      {
        if (function.returnType is ArrayCType || function.returnType is StructCType)
          throw new NotImplementedException();

        var targetPosition = function.parameters[i].value.position;
        var sourceValue = arguments[i];

        byteCodeState.CopyValueToPositionWithCast(sourceValue, targetPosition, function.parameters[i].type, stackSize);

        sourceValue.remainingReferences--;
      }

      if (function is CBuiltInFunction)
      {
        Position targetPosition = Position.Register(0);

        byteCodeState.MoveValueToPosition(new CConstIntValue((function as CBuiltInFunction).builtinFunctionIndex, BuiltInCType.Types["u8"], file, line), targetPosition, stackSize, true);

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

        if (!(function.returnType is VoidCType))
        {
          returnValue.hasPosition = true;
          returnValue.position = Position.Register(0);
          byteCodeState.registers[0] = returnValue;

          byteCodeState.instructions.Add(new LLI_Location_PseudoInstruction(returnValue, stackSize, byteCodeState));
        }
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
        if (!arguments[i].isInitialized)
          Compiler.Error($"In function call to '{functionPtr}': Argument {(i + 1)} '{arguments[i]}' for parameter of type '{function.parameters[i]}' has not been initialized yet. Defined in File '{arguments[i].file ?? "?"}', Line: {arguments[i].line}.", file, line);

        if (!arguments[i].type.CanImplicitCastTo(function.parameters[i]))
          Compiler.Error($"In function call to '{functionPtr}': Argument {(i + 1)} '{arguments[i]}' for parameter of type '{function.parameters[i]}' is of mismatching type '{arguments[i].type}' and cannot be converted implicitly. Value defined in File '{arguments[i].file ?? "?"}', Line: {arguments[i].line}.", file, line);
      }

      if (function.returnType is BuiltInCType || function.returnType is PtrCType)
      {
        this.returnValue = new CValue(file, line, function.returnType, true) { description = $"Return Value of call to function ptr \"{functionPtr}\"" };
        this.returnValue.type.isConst = true;
      }
      else if (!(function.returnType is VoidCType))
      {
        throw new NotImplementedException();
        // this.returnValue.type.isConst = true;
      }
      else
      {
        this.returnValue = null;
      }

      returnValue = this.returnValue;
    }

    public override void GetLLInstructions(ref ByteCodeState byteCodeState)
    {
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
          byteCodeState.instructions.Add(new LLI_Location_PseudoInstruction(functionPtr, stackSize, byteCodeState));
          byteCodeState.instructions.Add(new LLI_PushRegister((byte)functionPtr.position.registerIndex));
          pushedBytes += 8;
        }
        else if (functionPtr.position.type == PositionType.OnStack)
        {
          byteCodeState.instructions.Add(new LLI_MovStackOffsetToStackOffset(stackSize, functionPtr.position.stackOffsetForward + pushedBytes, new SharedValue<long>(0), 0));
          byteCodeState.instructions.Add(new LLI_StackIncrementImm(8));
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
          byteCodeState.CopyValueToPositionWithCast(this.arguments[i], targetPosition, function.parameters[i], stackSize);
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
        {
          byteCodeState.registers[returnValueRegister] = null;
        }
        else
        {
          returnValue.hasPosition = true;
          returnValue.position = Position.Register(returnValueRegister);

          byteCodeState.registers[returnValueRegister] = returnValue;
        }
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
        value.position = Position.StackOffset(stackSize.Value);
        stackSize.Value += value.type.GetSize();
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

      outValue.hasPosition = true;
      outValue.position = Position.Register(register);
      byteCodeState.registers[register] = outValue;

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

      byteCodeState.instructions.Add(new LLI_Location_PseudoInstruction(outValue, stackSize, byteCodeState));
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
      this.outValue = outValue = new CValue(file, line, ((PtrCType)(value.type)).pointsTo, true) { description = $"dereference of '{value}'" };
      this.stackSize = stackSize;
    }

    public override void GetLLInstructions(ref ByteCodeState byteCodeState)
    {
      byteCodeState.MoveValueToAnyRegister(value, stackSize);
      var sourcePtrRegister = value.position.registerIndex;

      using (var registerLock = byteCodeState.LockRegister(sourcePtrRegister))
      {
        var size = outValue.type.GetSize();

        if (size <= 8)
        {
          int register = (!(outValue.type is BuiltInCType) || !(outValue.type as BuiltInCType).IsFloat()) ? byteCodeState.GetFreeIntegerRegister(stackSize) : byteCodeState.GetFreeFloatRegister(stackSize);

          byteCodeState.instructions.Add(new LLI_MovFromPtrInRegisterToRegister(sourcePtrRegister, register));

          if (size < 8)
            byteCodeState.instructions.Add(new LLI_AndImm(register, BitConverter.GetBytes(((ulong)1 << (int)(size * 8)) - 1)));

          byteCodeState.registers[register] = outValue;
          outValue.hasPosition = true;
          outValue.position = Position.Register(register);

          byteCodeState.instructions.Add(new LLI_Location_PseudoInstruction(outValue, stackSize, byteCodeState));
        }
        else
        {
          if (value is CNamedValue)
            byteCodeState.MoveValueToHome(value as CNamedValue, stackSize); // Copy in register is left untouched.

          var remainingSize = size;
          int register = (!(outValue.type is BuiltInCType) || !(outValue.type as BuiltInCType).IsFloat()) ? byteCodeState.GetFreeIntegerRegister(stackSize) : byteCodeState.GetFreeFloatRegister(stackSize);

          while (remainingSize > 0)
          {
            byteCodeState.instructions.Add(new LLI_MovFromPtrInRegisterToRegister(sourcePtrRegister, register));

            if (remainingSize < 8)
              byteCodeState.instructions.Add(new LLI_AndImm(register, BitConverter.GetBytes(((ulong)1 << (int)(size * 8)) - 1)));
            else
              byteCodeState.instructions.Add(new LLI_AddImm(sourcePtrRegister, BitConverter.GetBytes((ulong)8)));

            remainingSize -= 8;
          }
        }
      }

      value.remainingReferences--;
    }
  }

  public class CInstruction_GetPointerOfArrayStart : CInstruction
  {
    CNamedValue value;
    CValue outValue;
    SharedValue<long> stackSize;

    public CInstruction_GetPointerOfArrayStart(CNamedValue value, out CValue outValue, SharedValue<long> stackSize, string file, int line) : base(file, line)
    {
      if (!(value.type is ArrayCType))
        throw new Exception("Internal Compiler Error: Unexpected Type.");

      this.value = value;
      this.outValue = outValue = new CValue(file, line, new PtrCType((value.type as ArrayCType).type), true) { description = $"reference to '{value}'[0]" };
      this.stackSize = stackSize;
    }

    public override void GetLLInstructions(ref ByteCodeState byteCodeState)
    {
      value.isVolatile = true;
      value.isInitialized = true; // Do we want this?

      if (!value.hasPosition)
      {
        value.position = Position.StackOffset(stackSize.Value);
        stackSize.Value += value.type.GetSize();
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

      outValue.hasPosition = true;
      outValue.position = Position.Register(register);
      byteCodeState.registers[register] = outValue;

      byteCodeState.instructions.Add(new LLI_LoadEffectiveAddress_StackOffsetToRegister(stackSize, value.position.stackOffsetForward, register));
      byteCodeState.instructions.Add(new LLI_Location_PseudoInstruction(outValue, stackSize, byteCodeState));
    }
  }

  public class CInstruction_CopyPositionFromValueToValue : CInstruction
  {
    CValue source, target;
    SharedValue<long> stackSize;

    public CInstruction_CopyPositionFromValueToValue(CValue source, CValue target, SharedValue<long> stackSize) : base(null, 0)
    {
      this.source = source;
      this.target = target;
      this.stackSize = stackSize;
    }

    public override void GetLLInstructions(ref ByteCodeState byteCodeState)
    {
      if (source.hasPosition && source.position.type == PositionType.InRegister)
      {
        if (byteCodeState.registers[source.position.registerIndex] != source)
          throw new Exception("Internal Compiler Error: Register Index not actually referenced.");

        byteCodeState.registers[source.position.registerIndex] = target;
      }

      target.hasPosition = source.hasPosition;
      target.position = source.position;

      if (target.hasPosition)
        byteCodeState.instructions.Add(new LLI_Location_PseudoInstruction(target, stackSize, byteCodeState));

      source.remainingReferences--;

      if (source is CNamedValue)
      {
        byteCodeState.MoveValueToHome(source as CNamedValue, stackSize);
      }
      else if (source.hasPosition && source.position.type == PositionType.InRegister && source.remainingReferences > 0)
      {
        var newPosition = Position.StackOffset(stackSize.Value);
        stackSize.Value += source.type.GetSize();

        byteCodeState.MoveValueToPosition(source, newPosition, stackSize, false);
      }
    }

    public override string ToString()
    {
      return base.ToString() + $" ({source} " + (source.hasPosition ? $"@{source.position}" : "{AT_NO_POSITION}") + $" to {target})";
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
    }

    public override void GetLLInstructions(ref ByteCodeState byteCodeState)
    {
      byte registerIndex = byteCodeState.MoveValueToAnyRegister(value, stackSize);
      byteCodeState.instructions.Add(new LLI_CmpNotEq_ImmRegister(BitConverter.GetBytes((long)0), registerIndex));

      if (backupRegisters)
        byteCodeState.BackupRegisterValues(stackSize);

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
    }

    public override void GetLLInstructions(ref ByteCodeState byteCodeState)
    {
      if (!value.isInitialized)
        Compiler.Error($"Cannot perform operator on uninitialized {value}.", file, line);

      int registerIndex = byteCodeState.MoveValueToAnyRegister(value, stackSize);

      if (!toSelf && value is CNamedValue)
      {
        if (value is CNamedValue)
          byteCodeState.MoveValueToHome(value as CNamedValue, stackSize);
      }

      byteCodeState.instructions.Add(new LLI_AddImm(registerIndex, imm));

      value.remainingReferences--;

      if (toSelf)
      {
        if (value is CNamedValue)
          (value as CNamedValue).modifiedSinceLastHome = true;
        else if (value is CGlobalValueReference)
          throw new NotImplementedException();
      }
      else
      {
        byteCodeState.registers[registerIndex] = resultingValue;
        resultingValue.hasPosition = true;
        resultingValue.position = Position.Register(registerIndex);
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
    }

    public override void GetLLInstructions(ref ByteCodeState byteCodeState)
    {
      if (!left.isInitialized)
        Compiler.Error($"Cannot perform operator on uninitialized value {left}.", file, line);

      if (!right.isInitialized)
        Compiler.Error($"Cannot perform operator on uninitialized value {right}.", file, line);

      int leftRegisterIndex = byteCodeState.MoveValueToAnyRegister(left, stackSize);

      using (var registerLock = byteCodeState.LockRegister(leftRegisterIndex))
      {
        if (!toSelf && left is CNamedValue)
        {
          if (left is CNamedValue)
            byteCodeState.MoveValueToHome(left as CNamedValue, stackSize);
        }

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

        if (toSelf)
        {
          if (left is CNamedValue)
            (left as CNamedValue).modifiedSinceLastHome = true;
          else if (left is CGlobalValueReference)
            throw new NotImplementedException();
        }
        else
        {
          byteCodeState.registers[leftRegisterIndex] = resultingValue;
          resultingValue.hasPosition = true;
          resultingValue.position = Position.Register(leftRegisterIndex);
        }

        byteCodeState.instructions.Add(new LLI_Location_PseudoInstruction(resultingValue, stackSize, byteCodeState));
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
    }

    public override void GetLLInstructions(ref ByteCodeState byteCodeState)
    {
      if (!left.isInitialized)
        Compiler.Error($"Cannot perform operator on uninitialized value {left}.", file, line);

      if (!right.isInitialized)
        Compiler.Error($"Cannot perform operator on uninitialized value {right}.", file, line);

      int leftRegisterIndex = byteCodeState.MoveValueToAnyRegister(left, stackSize);

      using (var registerLock = byteCodeState.LockRegister(leftRegisterIndex))
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

        if (!toSelf && left is CNamedValue)
        {
          if (left is CNamedValue)
            byteCodeState.MoveValueToHome(left as CNamedValue, stackSize);
        }

        byteCodeState.instructions.Add(new LLI_NegateRegister(rightRegisterIndex));
        byteCodeState.instructions.Add(new LLI_AddRegister(leftRegisterIndex, rightRegisterIndex));

        left.remainingReferences--;
        right.remainingReferences--;

        if (toSelf)
        {
          if (left is CNamedValue)
            (left as CNamedValue).modifiedSinceLastHome = true;
          else if (left is CGlobalValueReference)
            throw new NotImplementedException();
        }
        else
        {
          byteCodeState.registers[leftRegisterIndex] = resultingValue;
          resultingValue.hasPosition = true;
          resultingValue.position = Position.Register(leftRegisterIndex);
        }
      }
    }
  }

  public class CInstruction_Multiply : CInstruction
  {
    CValue lvalue, rvalue;
    SharedValue<long> stackSize;
    CValue resultingValue;
    bool toSelf;

    public CInstruction_Multiply(CValue lvalue, CValue rvalue, SharedValue<long> stackSize, bool toSelf, out CValue resultingValue, string file, int line) : base(file, line)
    {
      if ((lvalue.type is BuiltInCType && (lvalue.type as BuiltInCType).IsFloat()) != (rvalue.type is BuiltInCType && (rvalue.type as BuiltInCType).IsFloat()))
        Compiler.Error($"Both values of the operator have to be floating point or non-floating point. Given: '{lvalue}' and '{rvalue}'.", file, line);

      this.lvalue = lvalue;
      this.rvalue = rvalue;
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
    }

    public override void GetLLInstructions(ref ByteCodeState byteCodeState)
    {
      if (!lvalue.isInitialized)
        Compiler.Error($"Cannot perform operator on uninitialized lvalue {lvalue}.", file, line);

      if (!rvalue.isInitialized)
        Compiler.Error($"Cannot perform operator on uninitialized rvalue {rvalue}.", file, line);

      int lvalueRegisterIndex = byteCodeState.MoveValueToAnyRegister(lvalue, stackSize);

      using (var registerLock = byteCodeState.LockRegister(lvalueRegisterIndex))
      {
        if (!toSelf && (lvalue is CNamedValue || lvalue is CGlobalValueReference))
        {
          if (lvalue is CNamedValue)
            byteCodeState.MoveValueToHome(lvalue as CNamedValue, stackSize);
          else if (lvalue is CGlobalValueReference)
            throw new NotImplementedException();
        }

        // lvalue can't be imm if rvalue is const, values would've been swapped by the constructor.
        if (rvalue is CConstIntValue)
        {
          if ((lvalue.type is BuiltInCType && !(lvalue.type as BuiltInCType).IsUnsigned()) || (rvalue.type is BuiltInCType && !(rvalue.type as BuiltInCType).IsUnsigned()))
            byteCodeState.instructions.Add(new LLI_MultiplySignedImm(lvalueRegisterIndex, BitConverter.GetBytes((rvalue as CConstIntValue).uvalue)));
          else
            byteCodeState.instructions.Add(new LLI_MultiplyUnsignedImm(lvalueRegisterIndex, BitConverter.GetBytes((rvalue as CConstIntValue).uvalue)));
        }
        else if (rvalue is CConstFloatValue)
        {
          byteCodeState.instructions.Add(new LLI_MultiplySignedImm(lvalueRegisterIndex, BitConverter.GetBytes((rvalue as CConstFloatValue).value)));
        }
        else
        {
          int rvalueRegisterIndex = byteCodeState.MoveValueToAnyRegister(rvalue, stackSize);

          if ((lvalue.type is BuiltInCType && (!(lvalue.type as BuiltInCType).IsUnsigned() || (lvalue.type as BuiltInCType).IsFloat())) || (rvalue.type is BuiltInCType && (!(rvalue.type as BuiltInCType).IsUnsigned() || (rvalue.type as BuiltInCType).IsFloat())))
            byteCodeState.instructions.Add(new LLI_MultiplySignedRegister(lvalueRegisterIndex, rvalueRegisterIndex));
          else
            byteCodeState.instructions.Add(new LLI_MultiplyUnsignedRegister(lvalueRegisterIndex, rvalueRegisterIndex));
        }

        lvalue.remainingReferences--;
        rvalue.remainingReferences--;

        if (toSelf)
        {
          if (lvalue is CNamedValue)
            (lvalue as CNamedValue).modifiedSinceLastHome = true;
          else if (lvalue is CGlobalValueReference)
            throw new NotImplementedException();
        }
        else
        {
          byteCodeState.registers[lvalueRegisterIndex] = resultingValue;
          resultingValue.hasPosition = true;
          resultingValue.position = Position.Register(lvalueRegisterIndex);
        }
      }
    }
  }

  public class CInstruction_Divide : CInstruction
  {
    CValue lvalue, rvalue;
    SharedValue<long> stackSize;
    CValue resultingValue;
    bool toSelf;

    public CInstruction_Divide(CValue lvalue, CValue rvalue, SharedValue<long> stackSize, bool toSelf, out CValue resultingValue, string file, int line) : base(file, line)
    {
      if ((lvalue.type is BuiltInCType && (lvalue.type as BuiltInCType).IsFloat()) != (rvalue.type is BuiltInCType && (rvalue.type as BuiltInCType).IsFloat()))
        Compiler.Error($"Both values of the operator have to be floating point or non-floating point. Given: '{lvalue}' and '{rvalue}'.", file, line);

      this.lvalue = lvalue;
      this.rvalue = rvalue;
      this.stackSize = stackSize;
      this.toSelf = toSelf;

      if (toSelf)
        this.resultingValue = lvalue;
      else
        this.resultingValue = new CValue(file, line, lvalue.type.GetSize() >= rvalue.type.GetSize() ? lvalue.type : rvalue.type, true) { description = $"({lvalue} / {rvalue})" };

      if ((lvalue is CConstIntValue || lvalue is CConstFloatValue) && toSelf)
        throw new Exception("Internal Compiler Error: operator cannot be applied to the lvalue directly if it's constant imm.");

      resultingValue = this.resultingValue;
    }

    public override void GetLLInstructions(ref ByteCodeState byteCodeState)
    {
      if (!lvalue.isInitialized)
        Compiler.Error($"Cannot perform operator on uninitialized lvalue {lvalue}.", file, line);

      if (!lvalue.isInitialized)
        Compiler.Error($"Cannot perform operator on uninitialized rvalue {rvalue}.", file, line);

      int lvalueRegisterIndex = byteCodeState.MoveValueToAnyRegister(lvalue, stackSize);

      using (var registerLock = byteCodeState.LockRegister(lvalueRegisterIndex))
      {
        if (!toSelf && (lvalue is CNamedValue || lvalue is CGlobalValueReference))
        {
          if (lvalue is CNamedValue)
            byteCodeState.MoveValueToHome(lvalue as CNamedValue, stackSize);
          else if (lvalue is CGlobalValueReference)
            throw new NotImplementedException();
        }

        if (rvalue is CConstIntValue)
        {
          if ((lvalue.type is BuiltInCType && !(lvalue.type as BuiltInCType).IsUnsigned()) || (rvalue.type is BuiltInCType && !(rvalue.type as BuiltInCType).IsUnsigned()))
            byteCodeState.instructions.Add(new LLI_DivideSignedImm(lvalueRegisterIndex, BitConverter.GetBytes((rvalue as CConstIntValue).uvalue)));
          else
            byteCodeState.instructions.Add(new LLI_DivideUnsignedImm(lvalueRegisterIndex, BitConverter.GetBytes((rvalue as CConstIntValue).uvalue)));
        }
        else if (rvalue is CConstFloatValue)
        {
          byteCodeState.instructions.Add(new LLI_DivideSignedImm(lvalueRegisterIndex, BitConverter.GetBytes((rvalue as CConstFloatValue).value)));
        }
        else
        {
          int rvalueRegisterIndex = byteCodeState.MoveValueToAnyRegister(rvalue, stackSize);

          if ((lvalue.type is BuiltInCType && (!(lvalue.type as BuiltInCType).IsUnsigned() || (lvalue.type as BuiltInCType).IsFloat())) || (rvalue.type is BuiltInCType && (!(rvalue.type as BuiltInCType).IsUnsigned() || (rvalue.type as BuiltInCType).IsFloat())))
            byteCodeState.instructions.Add(new LLI_DivideSignedRegister(lvalueRegisterIndex, rvalueRegisterIndex));
          else
            byteCodeState.instructions.Add(new LLI_DivideUnsignedRegister(lvalueRegisterIndex, rvalueRegisterIndex));
        }

        lvalue.remainingReferences--;
        rvalue.remainingReferences--;

        if (toSelf)
        {
          if (lvalue is CNamedValue)
            (lvalue as CNamedValue).modifiedSinceLastHome = true;
          else if (lvalue is CGlobalValueReference)
            throw new NotImplementedException();
        }
        else
        {
          byteCodeState.registers[lvalueRegisterIndex] = resultingValue;
          resultingValue.hasPosition = true;
          resultingValue.position = Position.Register(lvalueRegisterIndex);
        }
      }
    }
  }

  public class CInstruction_Modulo : CInstruction
  {
    CValue lvalue, rvalue;
    SharedValue<long> stackSize;
    CValue resultingValue;
    bool toSelf;

    public CInstruction_Modulo(CValue lvalue, CValue rvalue, SharedValue<long> stackSize, bool toSelf, out CValue resultingValue, string file, int line) : base(file, line)
    {
      if ((lvalue.type is BuiltInCType && (lvalue.type as BuiltInCType).IsFloat()) || (rvalue.type is BuiltInCType && (rvalue.type as BuiltInCType).IsFloat()))
        Compiler.Error($"Both values of the operator have to be non-floating point. Given: '{lvalue}' and '{rvalue}'.", file, line);

      this.lvalue = lvalue;
      this.rvalue = rvalue;
      this.stackSize = stackSize;
      this.toSelf = toSelf;

      if (toSelf)
        this.resultingValue = lvalue;
      else
        this.resultingValue = new CValue(file, line, lvalue.type.GetSize() >= rvalue.type.GetSize() ? lvalue.type : rvalue.type, true) { description = $"({lvalue} % {rvalue})" };

      if ((lvalue is CConstIntValue || lvalue is CConstFloatValue) && toSelf)
        throw new Exception("Internal Compiler Error: operator cannot be applied to the lvalue directly if it's constant imm.");

      resultingValue = this.resultingValue;
    }

    public override void GetLLInstructions(ref ByteCodeState byteCodeState)
    {
      if (!lvalue.isInitialized)
        Compiler.Error($"Cannot perform operator on uninitialized lvalue {lvalue}.", file, line);

      if (!rvalue.isInitialized)
        Compiler.Error($"Cannot perform operator on uninitialized rvalue {rvalue}.", file, line);

      int lvalueRegisterIndex = byteCodeState.MoveValueToAnyRegister(lvalue, stackSize);

      using (var registerLock = byteCodeState.LockRegister(lvalueRegisterIndex))
      {
        if (!toSelf && (lvalue is CNamedValue || lvalue is CGlobalValueReference))
        {
          if (lvalue is CNamedValue)
            byteCodeState.MoveValueToHome(lvalue as CNamedValue, stackSize);
          else if (lvalue is CGlobalValueReference)
            throw new NotImplementedException();
        }

        // lvalue can't be imm if rvalue is const, values would've been swapped by the constructor.
        if (rvalue is CConstIntValue)
        {
          byteCodeState.instructions.Add(new LLI_ModuloImm(lvalueRegisterIndex, BitConverter.GetBytes((rvalue as CConstIntValue).uvalue)));
        }
        else
        {
          int rvalueRegisterIndex = byteCodeState.MoveValueToAnyRegister(rvalue, stackSize);

          byteCodeState.instructions.Add(new LLI_ModuloRegister(lvalueRegisterIndex, rvalueRegisterIndex));
        }

        lvalue.remainingReferences--;
        rvalue.remainingReferences--;

        if (toSelf)
        {
          if (lvalue is CNamedValue)
            (lvalue as CNamedValue).modifiedSinceLastHome = true;
          else if (lvalue is CGlobalValueReference)
            throw new NotImplementedException();
        }
        else
        {
          byteCodeState.registers[lvalueRegisterIndex] = resultingValue;
          resultingValue.hasPosition = true;
          resultingValue.position = Position.Register(lvalueRegisterIndex);
        }
      }
    }
  }

  public class CInstruction_And : CInstruction
  {
    CValue lvalue, rvalue;
    SharedValue<long> stackSize;
    CValue resultingValue;
    bool toSelf;

    public CInstruction_And(CValue lvalue, CValue rvalue, SharedValue<long> stackSize, bool toSelf, out CValue resultingValue, string file, int line) : base(file, line)
    {
      if ((lvalue.type is BuiltInCType && (lvalue.type as BuiltInCType).IsFloat()) || (rvalue.type is BuiltInCType && (rvalue.type as BuiltInCType).IsFloat()))
        Compiler.Error($"Both values of the operator have to be non-floating point. Given: '{lvalue}' and '{rvalue}'.", file, line);

      this.lvalue = lvalue;
      this.rvalue = rvalue;
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
    }

    public override void GetLLInstructions(ref ByteCodeState byteCodeState)
    {
      if (!lvalue.isInitialized)
        Compiler.Error($"Cannot perform operator on uninitialized lvalue {lvalue}.", file, line);

      if (!rvalue.isInitialized)
        Compiler.Error($"Cannot perform operator on uninitialized rvalue {rvalue}.", file, line);

      int lvalueRegisterIndex = byteCodeState.MoveValueToAnyRegister(lvalue, stackSize);

      using (var registerLock = byteCodeState.LockRegister(lvalueRegisterIndex))
      {
        if (!toSelf && (lvalue is CNamedValue || lvalue is CGlobalValueReference))
        {
          if (lvalue is CNamedValue)
            byteCodeState.MoveValueToHome(lvalue as CNamedValue, stackSize);
          else if (lvalue is CGlobalValueReference)
            throw new NotImplementedException();
        }

        // lvalue can't be imm if rvalue is const, values would've been swapped by the constructor.
        if (rvalue is CConstIntValue)
        {
          byteCodeState.instructions.Add(new LLI_AndImm(lvalueRegisterIndex, BitConverter.GetBytes((rvalue as CConstIntValue).uvalue)));
        }
        else
        {
          int rvalueRegisterIndex = byteCodeState.MoveValueToAnyRegister(rvalue, stackSize);

          byteCodeState.instructions.Add(new LLI_AndRegister(lvalueRegisterIndex, rvalueRegisterIndex));
        }

        lvalue.remainingReferences--;
        rvalue.remainingReferences--;

        if (toSelf)
        {
          if (lvalue is CNamedValue)
            (lvalue as CNamedValue).modifiedSinceLastHome = true;
          else if (lvalue is CGlobalValueReference)
            throw new NotImplementedException();
        }
        else
        {
          byteCodeState.registers[lvalueRegisterIndex] = resultingValue;
          resultingValue.hasPosition = true;
          resultingValue.position = Position.Register(lvalueRegisterIndex);
        }
      }
    }
  }

  public class CInstruction_IntOnIntRegister : CInstruction
  {
    CValue lvalue, rvalue;
    SharedValue<long> stackSize;
    CValue resultingValue;
    bool toSelf;
    Func<int, int, LLInstruction> operation;

    public CInstruction_IntOnIntRegister(CValue lvalue, CValue rvalue, SharedValue<long> stackSize, bool toSelf, out CValue resultingValue, string file, int line, string operatorChars, Func<int, int, LLInstruction> operation) : base(file, line)
    {
      if ((lvalue.type is BuiltInCType && (lvalue.type as BuiltInCType).IsFloat()) || (rvalue.type is BuiltInCType && (rvalue.type as BuiltInCType).IsFloat()))
        Compiler.Error($"Both values of the operator have to be non-floating point. Given: '{lvalue}' and '{rvalue}'.", file, line);

      this.lvalue = lvalue;
      this.rvalue = rvalue;
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
    }

    public override void GetLLInstructions(ref ByteCodeState byteCodeState)
    {
      if (!lvalue.isInitialized)
        Compiler.Error($"Cannot perform operator on uninitialized lvalue {lvalue}.", file, line);

      if (!rvalue.isInitialized)
        Compiler.Error($"Cannot perform operator on uninitialized rvalue {rvalue}.", file, line);

      int lvalueRegisterIndex = byteCodeState.MoveValueToAnyRegister(lvalue, stackSize);

      using (var registerLock = byteCodeState.LockRegister(lvalueRegisterIndex))
      {
        if (!toSelf && (lvalue is CNamedValue || lvalue is CGlobalValueReference))
        {
          if (lvalue is CNamedValue)
            byteCodeState.MoveValueToHome(lvalue as CNamedValue, stackSize);
          else if (lvalue is CGlobalValueReference)
            throw new NotImplementedException();
        }

        int rvalueRegisterIndex = byteCodeState.MoveValueToAnyRegister(rvalue, stackSize);

        byteCodeState.instructions.Add(operation(lvalueRegisterIndex, rvalueRegisterIndex));

        lvalue.remainingReferences--;
        rvalue.remainingReferences--;

        if (toSelf)
        {
          if (lvalue is CNamedValue)
            (lvalue as CNamedValue).modifiedSinceLastHome = true;
          else if (lvalue is CGlobalValueReference)
            throw new NotImplementedException();
        }
        else
        {
          byteCodeState.registers[lvalueRegisterIndex] = resultingValue;
          resultingValue.hasPosition = true;
          resultingValue.position = Position.Register(lvalueRegisterIndex);
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
    CValue lvalue, rvalue;
    SharedValue<long> stackSize;
    CValue resultingValue;

    public CInstruction_Equals(CValue lvalue, CValue rvalue, SharedValue<long> stackSize, out CValue resultingValue, string file, int line) : base(file, line)
    {
      if ((lvalue.type is BuiltInCType && (lvalue.type as BuiltInCType).IsFloat()) || (rvalue.type is BuiltInCType && (rvalue.type as BuiltInCType).IsFloat()))
        Compiler.Error($"Both values of the operator have to be non-floating point. Given: '{lvalue}' and '{rvalue}'.", file, line);

      this.lvalue = lvalue;
      this.rvalue = rvalue;
      this.stackSize = stackSize;

      this.resultingValue = new CValue(file, line, BuiltInCType.Types["u8"], true) { description = $"({lvalue} == {rvalue})" };

      resultingValue = this.resultingValue;
    }

    public override void GetLLInstructions(ref ByteCodeState byteCodeState)
    {
      if (!lvalue.isInitialized)
        Compiler.Error($"Cannot perform operator on uninitialized lvalue {lvalue}.", file, line);

      if (!rvalue.isInitialized)
        Compiler.Error($"Cannot perform operator on uninitialized rvalue {rvalue}.", file, line);

      int lvalueRegisterIndex = byteCodeState.CopyValueToAnyRegister(lvalue, stackSize);

      using (var registerLock = byteCodeState.LockRegister(lvalueRegisterIndex))
      {
        int rvalueRegisterIndex = byteCodeState.MoveValueToAnyRegister(rvalue, stackSize);

        if (lvalue is CNamedValue || lvalue is CGlobalValueReference)
        {
          if (lvalue is CNamedValue)
            byteCodeState.MoveValueToHome(lvalue as CNamedValue, stackSize);
          else if (lvalue is CGlobalValueReference)
            throw new NotImplementedException();
        }

        byteCodeState.instructions.Add(new LLI_EqualsRegister(lvalueRegisterIndex, rvalueRegisterIndex));

        lvalue.remainingReferences--;
        rvalue.remainingReferences--;

        byteCodeState.registers[lvalueRegisterIndex] = resultingValue;
        resultingValue.hasPosition = true;
        resultingValue.position = Position.Register(lvalueRegisterIndex);
      }
    }
  }

  public class CInstruction_NotEquals : CInstruction
  {
    CValue lvalue, rvalue;
    SharedValue<long> stackSize;
    CValue resultingValue;

    public CInstruction_NotEquals(CValue lvalue, CValue rvalue, SharedValue<long> stackSize, out CValue resultingValue, string file, int line) : base(file, line)
    {
      if ((lvalue.type is BuiltInCType && (lvalue.type as BuiltInCType).IsFloat()) || (rvalue.type is BuiltInCType && (rvalue.type as BuiltInCType).IsFloat()))
        Compiler.Error($"Both values of the operator have to be non-floating point. Given: '{lvalue}' and '{rvalue}'.", file, line);

      this.lvalue = lvalue;
      this.rvalue = rvalue;
      this.stackSize = stackSize;

      this.resultingValue = new CValue(file, line, BuiltInCType.Types["u8"], true) { description = $"({lvalue} != {rvalue})" };

      resultingValue = this.resultingValue;
    }

    public override void GetLLInstructions(ref ByteCodeState byteCodeState)
    {
      if (!lvalue.isInitialized)
        Compiler.Error($"Cannot perform operator on uninitialized lvalue {lvalue}.", file, line);

      if (!rvalue.isInitialized)
        Compiler.Error($"Cannot perform operator on uninitialized rvalue {rvalue}.", file, line);

      int lvalueRegisterIndex = byteCodeState.CopyValueToAnyRegister(lvalue, stackSize);

      using (var registerLock = byteCodeState.LockRegister(lvalueRegisterIndex))
      {
        int rvalueRegisterIndex = byteCodeState.MoveValueToAnyRegister(rvalue, stackSize);

        if (lvalue is CNamedValue || lvalue is CGlobalValueReference)
        {
          if (lvalue is CNamedValue)
            byteCodeState.MoveValueToHome(lvalue as CNamedValue, stackSize);
          else if (lvalue is CGlobalValueReference)
            throw new NotImplementedException();
        }

        byteCodeState.instructions.Add(new LLI_EqualsRegister(lvalueRegisterIndex, rvalueRegisterIndex));

        lvalue.remainingReferences--;
        rvalue.remainingReferences--;

        byteCodeState.instructions.Add(new LLI_NotRegister(lvalueRegisterIndex));

        byteCodeState.registers[lvalueRegisterIndex] = resultingValue;
        resultingValue.hasPosition = true;
        resultingValue.position = Position.Register(lvalueRegisterIndex);
      }
    }
  }

  public class CInstruction_LessOrEqual : CInstruction
  {
    CValue lvalue, rvalue;
    SharedValue<long> stackSize;
    CValue resultingValue;

    public CInstruction_LessOrEqual(CValue lvalue, CValue rvalue, SharedValue<long> stackSize, out CValue resultingValue, string file, int line) : base(file, line)
    {
      if ((lvalue.type is BuiltInCType && (lvalue.type as BuiltInCType).IsFloat()) || (rvalue.type is BuiltInCType && (rvalue.type as BuiltInCType).IsFloat()))
        Compiler.Error($"Both values of the operator have to be non-floating point. Given: '{lvalue}' and '{rvalue}'.", file, line);

      this.lvalue = lvalue;
      this.rvalue = rvalue;
      this.stackSize = stackSize;

      this.resultingValue = new CValue(file, line, BuiltInCType.Types["u8"], true) { description = $"({lvalue} <= {rvalue})" };

      resultingValue = this.resultingValue;
    }

    public override void GetLLInstructions(ref ByteCodeState byteCodeState)
    {
      if (!lvalue.isInitialized)
        Compiler.Error($"Cannot perform operator on uninitialized lvalue {lvalue}.", file, line);

      if (!rvalue.isInitialized)
        Compiler.Error($"Cannot perform operator on uninitialized rvalue {rvalue}.", file, line);

      int lvalueRegisterIndex = byteCodeState.CopyValueToAnyRegister(lvalue, stackSize);

      using (var registerLock = byteCodeState.LockRegister(lvalueRegisterIndex))
      {
        int rvalueRegisterIndex = byteCodeState.MoveValueToAnyRegister(rvalue, stackSize);

        if (lvalue is CNamedValue || lvalue is CGlobalValueReference)
        {
          if (lvalue is CNamedValue)
            byteCodeState.MoveValueToHome(lvalue as CNamedValue, stackSize);
          else if (lvalue is CGlobalValueReference)
            throw new NotImplementedException();
        }

        byteCodeState.instructions.Add(new LLI_GreaterThanRegister(lvalueRegisterIndex, rvalueRegisterIndex));

        lvalue.remainingReferences--;
        rvalue.remainingReferences--;

        byteCodeState.instructions.Add(new LLI_NotRegister(lvalueRegisterIndex));

        byteCodeState.registers[lvalueRegisterIndex] = resultingValue;
        resultingValue.hasPosition = true;
        resultingValue.position = Position.Register(lvalueRegisterIndex);
      }
    }
  }

  public class CInstruction_GreaterOrEqual : CInstruction
  {
    CValue lvalue, rvalue;
    SharedValue<long> stackSize;
    CValue resultingValue;

    public CInstruction_GreaterOrEqual(CValue lvalue, CValue rvalue, SharedValue<long> stackSize, out CValue resultingValue, string file, int line) : base(file, line)
    {
      if ((lvalue.type is BuiltInCType && (lvalue.type as BuiltInCType).IsFloat()) || (rvalue.type is BuiltInCType && (rvalue.type as BuiltInCType).IsFloat()))
        Compiler.Error($"Both values of the operator have to be non-floating point. Given: '{lvalue}' and '{rvalue}'.", file, line);

      this.lvalue = lvalue;
      this.rvalue = rvalue;
      this.stackSize = stackSize;

      this.resultingValue = new CValue(file, line, BuiltInCType.Types["u8"], true) { description = $"({lvalue} >= {rvalue})" };

      resultingValue = this.resultingValue;
    }

    public override void GetLLInstructions(ref ByteCodeState byteCodeState)
    {
      if (!lvalue.isInitialized)
        Compiler.Error($"Cannot perform operator on uninitialized lvalue {lvalue}.", file, line);

      if (!rvalue.isInitialized)
        Compiler.Error($"Cannot perform operator on uninitialized rvalue {rvalue}.", file, line);

      int lvalueRegisterIndex = byteCodeState.CopyValueToAnyRegister(lvalue, stackSize);

      using (var registerLock = byteCodeState.LockRegister(lvalueRegisterIndex))
      {
        int rvalueRegisterIndex = byteCodeState.MoveValueToAnyRegister(rvalue, stackSize);

        if (lvalue is CNamedValue || lvalue is CGlobalValueReference)
        {
          if (lvalue is CNamedValue)
            byteCodeState.MoveValueToHome(lvalue as CNamedValue, stackSize);
          else if (lvalue is CGlobalValueReference)
            throw new NotImplementedException();
        }

        byteCodeState.instructions.Add(new LLI_LessThanRegister(lvalueRegisterIndex, rvalueRegisterIndex));

        lvalue.remainingReferences--;
        rvalue.remainingReferences--;

        byteCodeState.instructions.Add(new LLI_NotRegister(lvalueRegisterIndex));

        byteCodeState.registers[lvalueRegisterIndex] = resultingValue;
        resultingValue.hasPosition = true;
        resultingValue.position = Position.Register(lvalueRegisterIndex);
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
    }

    public override void GetLLInstructions(ref ByteCodeState byteCodeState)
    {
      if (!left.isInitialized)
        Compiler.Error($"Cannot perform operator on uninitialized lvalue {left}.", file, line);

      if (!right.isInitialized)
        Compiler.Error($"Cannot perform operator on uninitialized rvalue {right}.", file, line);

      int lvalueRegisterIndex = byteCodeState.CopyValueToAnyRegister(left, stackSize);

      using (var registerLock = byteCodeState.LockRegister(lvalueRegisterIndex))
      {
        int rvalueRegisterIndex = byteCodeState.MoveValueToAnyRegister(right, stackSize);

        if (left is CNamedValue || left is CGlobalValueReference)
        {
          if (left is CNamedValue)
            byteCodeState.MoveValueToHome(left as CNamedValue, stackSize);
          else if (left is CGlobalValueReference)
            throw new NotImplementedException();
        }

        byteCodeState.instructions.Add(new LLI_LessThanRegister(lvalueRegisterIndex, rvalueRegisterIndex));

        left.remainingReferences--;
        right.remainingReferences--;

        byteCodeState.registers[lvalueRegisterIndex] = resultingValue;
        resultingValue.hasPosition = true;
        resultingValue.position = Position.Register(lvalueRegisterIndex);
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
    }

    public override void GetLLInstructions(ref ByteCodeState byteCodeState)
    {
      if (!left.isInitialized)
        Compiler.Error($"Cannot perform operator on uninitialized lvalue {left}.", file, line);

      if (!right.isInitialized)
        Compiler.Error($"Cannot perform operator on uninitialized rvalue {right}.", file, line);

      int lvalueRegisterIndex = byteCodeState.CopyValueToAnyRegister(left, stackSize);

      using (var registerLock = byteCodeState.LockRegister(lvalueRegisterIndex))
      {
        int rvalueRegisterIndex = byteCodeState.MoveValueToAnyRegister(right, stackSize);

        if (left is CNamedValue || left is CGlobalValueReference)
        {
          if (left is CNamedValue)
            byteCodeState.MoveValueToHome(left as CNamedValue, stackSize);
          else if (left is CGlobalValueReference)
            throw new NotImplementedException();
        }

        byteCodeState.instructions.Add(new LLI_GreaterThanRegister(lvalueRegisterIndex, rvalueRegisterIndex));

        left.remainingReferences--;
        right.remainingReferences--;

        byteCodeState.registers[lvalueRegisterIndex] = resultingValue;
        resultingValue.hasPosition = true;
        resultingValue.position = Position.Register(lvalueRegisterIndex);
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
    }

    public override void GetLLInstructions(ref ByteCodeState byteCodeState)
    {
      if (!value.isInitialized)
        Compiler.Error($"Cannot perform operator on uninitialized lvalue {value}.", file, line);

      int lvalueRegisterIndex = byteCodeState.CopyValueToAnyRegister(value, stackSize);

      using (var registerLock = byteCodeState.LockRegister(lvalueRegisterIndex))
      {
        if (value is CNamedValue || value is CGlobalValueReference)
        {
          if (value is CNamedValue)
            byteCodeState.MoveValueToHome(value as CNamedValue, stackSize);
          else if (value is CGlobalValueReference)
            throw new NotImplementedException();
        }

        byteCodeState.instructions.Add(new LLI_InverseRegister(lvalueRegisterIndex));

        value.remainingReferences--;

        byteCodeState.registers[lvalueRegisterIndex] = resultingValue;
        resultingValue.hasPosition = true;
        resultingValue.position = Position.Register(lvalueRegisterIndex);
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
    }

    public override void GetLLInstructions(ref ByteCodeState byteCodeState)
    {
      if (!value.isInitialized)
        Compiler.Error($"Cannot perform operator on uninitialized lvalue {value}.", file, line);

      int lvalueRegisterIndex = byteCodeState.CopyValueToAnyRegister(value, stackSize);

      using (var registerLock = byteCodeState.LockRegister(lvalueRegisterIndex))
      {
        if (value is CNamedValue || value is CGlobalValueReference)
        {
          if (value is CNamedValue)
            byteCodeState.MoveValueToHome(value as CNamedValue, stackSize);
          else if (value is CGlobalValueReference)
            throw new NotImplementedException();
        }

        byteCodeState.instructions.Add(new LLI_NotRegister(lvalueRegisterIndex));

        value.remainingReferences--;

        byteCodeState.registers[lvalueRegisterIndex] = resultingValue;
        resultingValue.hasPosition = true;
        resultingValue.position = Position.Register(lvalueRegisterIndex);
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
    }

    public override void GetLLInstructions(ref ByteCodeState byteCodeState)
    {
      if (!value.isInitialized)
        Compiler.Error($"Cannot perform operator on uninitialized lvalue {value}.", file, line);

      int lvalueRegisterIndex = byteCodeState.CopyValueToAnyRegister(value, stackSize);

      using (var registerLock = byteCodeState.LockRegister(lvalueRegisterIndex))
      {
        if (value is CNamedValue || value is CGlobalValueReference)
        {
          if (value is CNamedValue)
            byteCodeState.MoveValueToHome(value as CNamedValue, stackSize);
          else if (value is CGlobalValueReference)
            throw new NotImplementedException();
        }

        byteCodeState.instructions.Add(new LLI_NegateRegister(lvalueRegisterIndex));

        value.remainingReferences--;

        byteCodeState.registers[lvalueRegisterIndex] = resultingValue;
        resultingValue.hasPosition = true;
        resultingValue.position = Position.Register(lvalueRegisterIndex);
      }
    }
  }
}
