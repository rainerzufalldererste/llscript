using System;
using System.Collections.Generic;
using System.Linq;

namespace llsc
{
  public class CFunction
  {
    public readonly string file;
    public readonly int line;
    public readonly string name;

    public readonly CType returnType;
    public FunctionParameter[] parameters;

    public readonly SharedValue<long> minStackSize = new SharedValue<long>(0);
    public readonly long callStackSize;
    public List<Node> nodes;
    public Scope scope;
    public LLI_Label_PseudoInstruction functionStartLabel, functionEndLabel;
    public Action OnFunctionEnd = null;

    public CFunction(string name, CType returnType, IEnumerable<FunctionParameter> parameters, string file, int line)
    {
      this.name = name;
      this.returnType = returnType;
      this.parameters = parameters.ToArray();
      this.file = file;
      this.line = line;

      functionStartLabel = new LLI_Label_PseudoInstruction($"Function Start Label for '{this}' ({file}:{line + 1})");
      functionEndLabel = new LLI_Label_PseudoInstruction($"Function End Label for '{this}' ({file}:{line + 1})");

      minStackSize.Value = 8; // Return Address.
      
      // + Return Value Ptr if not void or in register.
      if (!(returnType is VoidCType || returnType is BuiltInCType || returnType is PtrCType))
        minStackSize.Value += 8;

      int intRegistersTaken = (this is CBuiltInFunction ? 1 : 0);
      int floatRegistersTaken = 0;

      if (this is CBuiltInFunction)
        minStackSize.Value = 0;

      foreach (var param in this.parameters)
      {
        if (param.type is VoidCType || param.type is ArrayCType)
        {
          Compiler.Error($"Cannot use type '{param.type}' for function parameters.", param.file, param.line);
        }
        else if (param.type is BuiltInCType)
        {
          var type = param.type as BuiltInCType;

          if (type.IsFloat())
          {
            if (floatRegistersTaken < Compiler.FloatRegisters)
            {
              param.value.position = Position.Register(Compiler.IntegerRegisters + floatRegistersTaken);
              floatRegistersTaken++;
              continue;
            }
          }
          else
          {
            if (intRegistersTaken < Compiler.IntegerRegisters)
            {
              param.value.position = Position.Register(intRegistersTaken);
              intRegistersTaken++;
              continue;
            }
          }
        }
        else if (param.type is PtrCType)
        {
          if (intRegistersTaken < Compiler.IntegerRegisters)
          {
            param.value.position = Position.Register(intRegistersTaken);
            intRegistersTaken++;
            continue;
          }
        }

        // Whatever doesn't call 'continue' will be dealt with here:
        // Value on stack.
        param.value.position = Position.StackOffset(minStackSize.Value);
        param.value.hasPosition = true;
        minStackSize.Value += param.type.GetSize();
      }

      callStackSize = minStackSize.Value;
    }

    public void ResetRegisterPositions()
    {
      int intRegistersTaken = (this is CBuiltInFunction ? 1 : 0);
      int floatRegistersTaken = 0;

      long minStackSize = 8; // Return Address.

      // + Return Value Ptr if not void or in register.
      if (!(returnType is VoidCType || returnType is BuiltInCType || returnType is PtrCType))
        minStackSize += 8;

      if (this is CBuiltInFunction)
        minStackSize = 0;

      foreach (var param in this.parameters)
      {
        if (param.type is VoidCType || param.type is ArrayCType)
        {
          Compiler.Error($"Cannot use type '{param.type}' for function parameters.", param.file, param.line);
        }
        else if (param.type is BuiltInCType)
        {
          var type = param.type as BuiltInCType;

          if (type.IsFloat())
          {
            if (floatRegistersTaken < Compiler.FloatRegisters)
            {
              param.value.position = Position.Register(Compiler.IntegerRegisters + floatRegistersTaken);
              param.value.hasPosition = true;
              floatRegistersTaken++;
              continue;
            }
          }
          else
          {
            if (intRegistersTaken < Compiler.IntegerRegisters)
            {
              param.value.position = Position.Register(intRegistersTaken);
              intRegistersTaken++;
              param.value.hasPosition = true;
              continue;
            }
          }
        }
        else if (param.type is PtrCType)
        {
          if (intRegistersTaken < Compiler.IntegerRegisters)
          {
            param.value.position = Position.Register(intRegistersTaken);
            param.value.hasPosition = true;
            intRegistersTaken++;
            continue;
          }
        }

        // Whatever doesn't call 'continue' will be dealt with here:
        // Value on stack.
        param.value.position = Position.StackOffset(minStackSize);
        param.value.hasPosition = true;
        minStackSize += param.type.GetSize();
      }
    }

    public override string ToString()
    {
      string ret = $"function {returnType} {name} (";

      for (int i = 0; i < parameters.Length; i++)
      {
        if (i > 0)
          ret += ", ";

        ret += $"{parameters[i].type} {parameters[i].name}";
      }
      
      return ret + ")";
    }
  }

  public enum BuiltInFunctions
  {
    LLS_BF_ALLOC = 0,
    LLS_BF_FREE,
    LLS_BF_REALLOC,
    LLS_BF_LOAD_LIBRARY,
    LLS_BF_GET_PROC_ADDRESS
  }

  public class FunctionParameter
  {
    public readonly string file;
    public readonly int line;
    public readonly string name;
    public readonly CType type;
    public readonly CNamedValue value;

    public FunctionParameter(CType type, string name, string file, int line)
    {
      this.file = file;
      this.line = line;
      this.name = name;
      this.type = type;
      this.value = new CNamedValue(name, type, true, file, line);
    }
  }

  public class CBuiltInFunction : CFunction
  {
    public readonly byte builtinFunctionIndex;

    public static CBuiltInFunction[] Functions = new CBuiltInFunction[]
    {
      new CBuiltInFunction("alloc", (byte)BuiltInFunctions.LLS_BF_ALLOC, new PtrCType(VoidCType.Instance), new Tuple<string, CType>[] { Tuple.Create("size", (CType)BuiltInCType.Types["u64"]) }),
      new CBuiltInFunction("free", (byte)BuiltInFunctions.LLS_BF_FREE, VoidCType.Instance, new Tuple<string, CType>[] { Tuple.Create("ptr", (CType)new PtrCType(VoidCType.Instance) { isConst = true }) }),
      new CBuiltInFunction("realloc", (byte)BuiltInFunctions.LLS_BF_REALLOC, new PtrCType(VoidCType.Instance), new Tuple<string, CType>[] { Tuple.Create("ptr", (CType)new PtrCType(VoidCType.Instance)), Tuple.Create("newSize", (CType)BuiltInCType.Types["u64"]) }),
      new CBuiltInFunction("load_library", (byte)BuiltInFunctions.LLS_BF_LOAD_LIBRARY, new PtrCType(VoidCType.Instance), new Tuple<string, CType>[] { Tuple.Create("libraryName", (CType)new PtrCType(BuiltInCType.ConstTypes["i8"])) }),
      new CBuiltInFunction("get_proc_address", (byte)BuiltInFunctions.LLS_BF_GET_PROC_ADDRESS, new PtrCType(VoidCType.Instance), new Tuple<string, CType>[] { Tuple.Create("libraryHandle", (CType)new PtrCType(VoidCType.Instance)), Tuple.Create("functionName", (CType)new PtrCType(BuiltInCType.ConstTypes["i8"])) }),
    };

    private const string File = "{compiler internal / built in function}";

    private CBuiltInFunction(string name, byte index, CType returnType, Tuple<string, CType>[] parameters) : base(name, returnType, (from x in parameters select new FunctionParameter(x.Item2, x.Item1, File, 0)), File, 0)
    {
      this.builtinFunctionIndex = index;
    }
  }
}
