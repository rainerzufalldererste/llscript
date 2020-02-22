using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace llsc
{
  public enum ByteCodeInstructions
  {
    LLS_OP_EXIT = 0,

    LLS_OP_MOV_IMM_REGISTER,
    LLS_OP_MOV_REGISTER_REGISTER,
    LLS_OP_MOV_REGISTER_STACK,
    LLS_OP_MOV_STACK_REGISTER,
    LLS_OP_MOV_STACK_STACK,
    LLS_OP_MOV_REGISTER__PTR_IN_REGISTER,
    LLS_OP_MOV_PTR_IN_REGISTER__REGISTER,

    LLS_OP_MOV_REGISTER_STACK_N_BYTES,
    LLS_OP_MOV_STACK_STACK_N_BYTES,
    LLS_OP_MOV_REGISTER__PTR_IN_REGISTER_N_BYTES,

    LLS_OP_LEA_STACK_TO_REGISTER,

    LLS_OP_PUSH_REGISTER,
    LLS_OP_POP_REGISTER,

    LLS_OP_STACK_INC_IMM,
    LLS_OP_STACK_INC_REGISTER,
    LLS_OP_STACK_DEC_IMM,
    LLS_OP_STACK_DEC_REGISTER,

    LLS_OP_ADD_IMM,
    LLS_OP_ADD_REGISTER,
    LLS_OP_MULI_IMM,
    LLS_OP_MULI_REGISTER,
    LLS_OP_DIVI_IMM,
    LLS_OP_DIVI_REGISTER,
    LLS_OP_MULU_IMM,
    LLS_OP_MULU_REGISTER,
    LLS_OP_DIVU_IMM,
    LLS_OP_DIVU_REGISTER,
    LLS_OP_MOD_IMM,
    LLS_OP_MOD_REGISTER,

    LLS_OP_BSL_REGISTER,
    LLS_OP_BSR_REGISTER,
    LLS_OP_AND_REGISTER,
    LLS_OP_AND_IMM,
    LLS_OP_OR_REGISTER,
    LLS_OP_XOR_REGISTER,

    LLS_OP_LOGICAL_AND_REGISTER,
    LLS_OP_LOGICAL_OR_REGISTER,

    LLS_OP_INV_REGISTER,
    LLS_OP_NOT_REGISTER,

    LLS_OP_EQ_REGISTER,
    LLS_OP_LT_REGISTER,
    LLS_OP_GT_REGISTER,

    LLS_OP_NEGATE_REGISTER,

    LLS_OP_CMP_NEQ_IMM_REGISTER,

    LLS_OP_JMP,
    LLS_OP_JMP_RELATIVE_IMM,
    LLS_OP_JMP_RELATIVE_REGISTER,

    LLS_OP_JUMP_CMP_TRUE,
    LLS_OP_JUMP_CMP_TRUE_RELATIVE_IMM,
    LLS_OP_JUMP_CMP_TRUE_RELATIVE_REGISTER,

    LLS_OP_CALL_INTERNAL_IMM,
    LLS_OP_CALL_INTERNAL_REGISTER,
    LLS_OP_RETURN_INTERNAL,

    LLS_OP_CALL_EXTERNAL__RESULT_TO_REGISTER,

    LLS_OP_CALL_BUILTIN__RESULT_TO_REGISTER__ID_FROM_REGISTER,
  };

  public enum BuiltInFunctions
  {
    LLS_BF_ALLOC = 0,
    LLS_BF_FREE,
    LLS_BF_REALLOC,
    LLS_BF_LOAD_LIBRARY,
    LLS_BF_GET_PROC_ADDRESS
  }

  public abstract class Node
  {
    public readonly string file;
    public readonly int line;

    public Node(string file, int line)
    {
      this.file = file;
      this.line = line;
    }

    public abstract override string ToString();
  }

  public class NName : Node
  {
    public string name;

    public NName(string name, string file, int line) : base(file, line) => this.name = name;

    public override string ToString() => "identifier '" + name + "'";
  }

  public class NType : Node
  {
    public CType type;

    public NType(CType type, string file, int line) : base(file, line) => this.type = type;

    public override string ToString() => "type '" + type.ToString() + "'";
  }

  public class NOperator : Node
  {
    public string operatorType;

    public NOperator(string operatorType, string file, int line) : base(file, line) => this.operatorType = operatorType;

    public override string ToString() => "operator '" + operatorType + "'";
  }

  public class NLineEnd : Node
  {
    public NLineEnd(string file, int line) : base(file, line) { }

    public override string ToString() => "line end (';')";
  }

  public class NComma : Node
  {
    public NComma(string file, int line) : base(file, line) { }

    public override string ToString() => "delimiter (',')";
  }

  public class NAttributeOperator : Node
  {
    public NAttributeOperator(string file, int line) : base(file, line) { }

    public override string ToString() => "attribute operator ('.')";
  }
  
  public class NDereferenceAttributeOperator : Node
  {
    public NDereferenceAttributeOperator(string file, int line) : base(file, line) { }

    public override string ToString() => "attribute dereference operator ('->')";
  }

  public class NOpenScope : Node
  {
    public NOpenScope(string file, int line) : base(file, line) { }

    public override string ToString() => "'{'";
  }

  public class NCloseScope : Node
  {
    public NCloseScope(string file, int line) : base(file, line) { }

    public override string ToString() => "'}'";
  }

  public class NOpenBracket : Node
  {
    public NOpenBracket(string file, int line) : base(file, line) { }

    public override string ToString() => "'['";
  }

  public class NCloseBracket : Node
  {
    public NCloseBracket(string file, int line) : base(file, line) { }

    public override string ToString() => "']'";
  }
  
  public class NOpenParanthesis : Node
  {
    public NOpenParanthesis(string file, int line) : base(file, line) { }

    public override string ToString() => "'('";
  }

  public class NCloseParanthesis : Node
  {
    public NCloseParanthesis(string file, int line) : base(file, line) { }

    public override string ToString() => "')'";
  }

  public class NStructKeyword : Node
  {
    public NStructKeyword(string file, int line) : base(file, line) { }

    public override string ToString() => "'struct'";
  }

  public class NFunctionKeyword : Node
  {
    public NFunctionKeyword(string file, int line) : base(file, line) { }

    public override string ToString() => "'function'";
  }

  public class NPtrKeyword : Node
  {
    public NPtrKeyword(string file, int line) : base(file, line) { }

    public override string ToString() => "'ptr'";
  }

  public class NArrayKeyword : Node
  {
    public NArrayKeyword(string file, int line) : base(file, line) { }

    public override string ToString() => "'array'";
  }

  public class NExternFuncKeyword : Node
  {
    public NExternFuncKeyword(string file, int line) : base(file, line) { }

    public override string ToString() => "'extern_func'";
  }

  public class NFuncKeyword : Node
  {
    public NFuncKeyword(string file, int line) : base(file, line) { }

    public override string ToString() => "'func'";
  }

  public class NVarKeyword : Node
  {
    public NVarKeyword(string file, int line) : base(file, line) { }

    public override string ToString() => "'var'";
  }

  public class NCastKeyword : Node
  {
    public NCastKeyword(string file, int line) : base(file, line) { }

    public override string ToString() => "'cast'";
  }

  public class NFloatKeyword : Node
  {
    public NFloatKeyword(string file, int line) : base(file, line) { }

    public override string ToString() => "'float'";
  }

  public class NIfKeyword : Node
  {
    public NIfKeyword(string file, int line) : base(file, line) { }

    public override string ToString() => "'if'";
  }

  public class NElseKeyword : Node
  {
    public NElseKeyword(string file, int line) : base(file, line) { }

    public override string ToString() => "'else'";
  }

  public class NWhileKeyword : Node
  {
    public NWhileKeyword(string file, int line) : base(file, line) { }

    public override string ToString() => "'while'";
  }

  public enum NPseudoFunctionType
  {
    SizeOf,
    CountOf,
    AddressOf,
    ValueOf,
    OffsetOf,
    FromRegister,
    ToRegister,
    Exit
  }

  public class NPseudoFunction : Node
  {
    public readonly NPseudoFunctionType type;

    public NPseudoFunction(string name, string file, int line) : base(file, line)
    {
      switch (name)
      {
        case "sizeof":
          type = NPseudoFunctionType.SizeOf;
          break;

        case "countof":
          type = NPseudoFunctionType.CountOf;
          break;

        case "addressof":
          type = NPseudoFunctionType.AddressOf;
          break;

        case "valueof":
          type = NPseudoFunctionType.ValueOf;
          break;

        case "offsetof":
          type = NPseudoFunctionType.OffsetOf;
          break;

        case "__from_register":
          type = NPseudoFunctionType.FromRegister;
          break;

        case "__to_register":
          type = NPseudoFunctionType.ToRegister;
          break;

        case "__exit":
          type = NPseudoFunctionType.Exit;
          break;

        default:
          throw new Exception("Internal Compiler Error: Invalid Type");
      }
    }

    public override string ToString() => $"pseudo function '{type}'";
  }

  public class NStringValue : Node
  {
    public readonly string value;

    // Includes trailing null character.
    public readonly long length;
    public readonly byte[] bytes;

    public NStringValue(string value, string file, int line) : base(file, line)
    {
      this.value = "";

      while (value.Length > 0)
      {
        int i = 0;

        for (; i < value.Length; i++)
        {
          if (value[i] == '\\' || value[i] == '#')
            break;
        }

        if (i == value.Length)
        {
          this.value += value;
          break;
        }
        else
        {
          if (i > 0)
          {
            this.value += value.Substring(0, i - 1);
            value = value.Substring(i);
          }
          else
          {
            value = value.Substring(1);
          }

          if (value.Length == 0)
          {
            Compiler.Error($"Invalid Escape Character. '#' has to be replaced by '\\h' if not used to escape '\"'.", file, line);
          }
          else
          {
            switch (value[0])
            {
              case 'h':
                this.value += "#";
                break;

              case '\"':
                this.value += "\"";
                break;

              case 't':
                this.value += "\t";
                break;
            }

            value = value.Substring(1);
          }
        }
      }


      this.bytes = Encoding.UTF8.GetBytes(this.value + "\0");
      this.length = bytes.LongLength;
    }

    public override string ToString() => $"string value '{value}' ({length} Bytes)";
  }

  public class NIntegerValue : Node
  {
    private readonly ulong _uint_value;
    private readonly long _int_value;

    public readonly bool isForcefullyNegative;

    public ulong uint_value { get { if (isForcefullyNegative) throw new Exception("Retrieving Unsigned Value of Forcefully Negative Integer."); return _uint_value; } }

    public long int_value { get { return _int_value; } }

    private NIntegerValue(bool forcefullyNegative, ulong uvalue, long value, string file, int line) : base(file, line)
    {
      this._uint_value = uvalue;
      this._int_value = value;
      this.isForcefullyNegative = forcefullyNegative;
    }

    public static NIntegerValue GetIntegerValue(string value, string file, int line)
    {
      if (value.Length == 0)
        return null;

      bool forcefullyNegative = false;

      if (value.StartsWith("0x"))
      {
        try
        {
          ulong u = Convert.ToUInt64(value.Substring(2), 16);
          long i;

          unchecked { i = (long)u; }

          return new NIntegerValue(false, u, i, file, line);
        }
        catch (Exception)
        {
          Compiler.Error($"Invalid Value '{value}'. Names cannot start with '0x'.", file, line);
        }
      }
      else if (value.StartsWith("0b"))
      {
        try
        {
          ulong u = Convert.ToUInt64(value.Substring(2), 2);
          long i;

          unchecked { i = (long)u; }

          return new NIntegerValue(false, u, i, file, line);
        }
        catch (Exception)
        {
          Compiler.Error($"Invalid Value '{value}'. Names cannot start with '0b'.", file, line);
        }
      }
      else if (value.StartsWith("-"))
      {
        forcefullyNegative = true;

        if (value.Length < 2)
          return null;

        if (!char.IsDigit(value[1]))
          return null;
        else
          for (int i = 2; i < value.Length; i++)
            if (!char.IsDigit(value[i]))
              Compiler.Error($"Invalid Value '{value}'. Names cannot start with a digit.", file, line);
      }
      else if (char.IsDigit(value[0]))
      {
        for (int i = 1; i < value.Length; i++)
          if (!char.IsDigit(value[i]))
            return null;
      }

      try
      {
        long i = Convert.ToInt64(value);
        ulong u = 0;

        if (!forcefullyNegative)
          u = Convert.ToUInt64(value);
        else
          unchecked { u = (ulong)i; }

        return new NIntegerValue(forcefullyNegative, u, i, file, line);
      }
      catch (Exception)
      {
        return null;
      }
    }

    public override string ToString() => $"integer value (signed)'{_int_value}' / (unsigned)'{_uint_value}'";
  }

  public class NFloatingPointValue : Node
  {
    public readonly double value;

    public NFloatingPointValue(double value, string file, int line) : base(file, line)
    {
      this.value = value;
    }

    public override string ToString() => $"floating point value '{value}'";
  }

  public abstract class CType
  {
    public CType explicitCast = null;

    public abstract long GetSize();

    public override bool Equals(object obj) => false;

    public override int GetHashCode() => base.GetHashCode();

    public static bool operator ==(CType a, CType b)
    {
      return a.Equals(b);
    }

    public static bool operator !=(CType a, CType b)
    {
      return !a.Equals(b);
    }

    public virtual bool CanImplicitCastTo(CType type) => type.Equals(explicitCast) || Equals(type);

    public virtual bool CanExplicitCastTo(CType type) => CanImplicitCastTo(type);

    public abstract CType MakeCastableClone(CType targetType);
  }

  public class VoidCType : CType
  {
    public static VoidCType Instance = new VoidCType();

    private VoidCType() { }

    public override long GetSize() => 0;

    public override string ToString() => "void";

    public override bool Equals(object obj) => obj is VoidCType;

    public override int GetHashCode() => 0x13579bdf;

    public override CType MakeCastableClone(CType targetType) => new VoidCType() { explicitCast = targetType };
  }

  public enum BuiltInTypes
  {
    u64, i64, f64, f32, u32, i32, u16, i16, u8, i8
  }

  public class BuiltInCType : CType
  {
    public static Dictionary<string, BuiltInCType> Types = (from x in Enum.GetNames(typeof(BuiltInTypes)) select x).ToDictionary(x => x, x => new BuiltInCType((BuiltInTypes)Enum.Parse(typeof(BuiltInTypes), x)));

    public readonly BuiltInTypes type;

    public BuiltInCType(BuiltInTypes type)
    {
      this.type = type;
    }

    public override bool Equals(object obj) => (obj is BuiltInCType && (obj as BuiltInCType).type == type);

    public override int GetHashCode() => type.GetHashCode();

    public override long GetSize()
    {
      switch (type)
      {
        case BuiltInTypes.f64:
        case BuiltInTypes.i64:
        case BuiltInTypes.u64:
          return 64 / 8;

        case BuiltInTypes.f32:
        case BuiltInTypes.i32:
        case BuiltInTypes.u32:
          return 32 / 8;

        case BuiltInTypes.i16:
        case BuiltInTypes.u16:
          return 16 / 8;

        case BuiltInTypes.i8:
        case BuiltInTypes.u8:
          return 8 / 8;

        default:
          throw new Exception("Invalid Type");
      }
    }

    public bool IsFloat() => type == BuiltInTypes.f64 || type == BuiltInTypes.f32;

    public bool IsUnsigned() => type == BuiltInTypes.u64 || type == BuiltInTypes.u32 || type == BuiltInTypes.u16 || type == BuiltInTypes.u8;
    
    public override string ToString() => type.ToString();

    public byte[] GetAsBytes(NIntegerValue value)
    {
      if (value.isForcefullyNegative && this.IsUnsigned())
        Compiler.Warn($"Value '{value.int_value}' is forcefully negative but will be casted to unsigned type '{this}'.", value.file, value.line);

      if (value.isForcefullyNegative && this.IsFloat())
      {
        switch (this.type)
        {
          case BuiltInTypes.f32:
            return BitConverter.GetBytes((float)value.int_value);

          case BuiltInTypes.f64:
            return BitConverter.GetBytes((double)value.int_value);

          default:
            throw new Exception("Internal Compiler Error.");
        }
      }
      else
      {
        switch (this.type)
        {
          case BuiltInTypes.f32:
            return BitConverter.GetBytes((float)value.uint_value);

          case BuiltInTypes.f64:
            return BitConverter.GetBytes((double)value.uint_value);

          case BuiltInTypes.u64:
            return BitConverter.GetBytes(value.uint_value);

          case BuiltInTypes.i64:
            return BitConverter.GetBytes(value.int_value);

          case BuiltInTypes.u32:
            unchecked { return BitConverter.GetBytes((uint)value.uint_value); }

          case BuiltInTypes.i32:
            unchecked { return BitConverter.GetBytes((int)value.int_value); }

          case BuiltInTypes.u16:
            unchecked { return BitConverter.GetBytes((ushort)value.uint_value); }

          case BuiltInTypes.i16:
            unchecked { return BitConverter.GetBytes((short)value.int_value); }

          case BuiltInTypes.u8:
            unchecked { return BitConverter.GetBytes((byte)value.uint_value); }

          case BuiltInTypes.i8:
            unchecked { return BitConverter.GetBytes((sbyte)value.int_value); }

          default:
            throw new Exception("Internal Compiler Error.");
        }
      }
    }

    public override bool CanImplicitCastTo(CType type) => type.Equals(explicitCast) || (type is BuiltInCType && !(IsFloat() ^ (type as BuiltInCType).IsFloat()) && GetSize() <= type.GetSize());

    public override bool CanExplicitCastTo(CType type) => type is BuiltInCType || (!IsFloat() && GetSize() == type.GetSize() && (type is PtrCType || type is ExternFuncCType || type is ExternFuncCType || type is FuncCType));

    public override CType MakeCastableClone(CType targetType) => new BuiltInCType(type) { explicitCast = targetType };
  }

  public class PtrCType : CType
  {
    public readonly CType pointsTo;

    public PtrCType(CType pointsTo)
    {
      this.pointsTo = pointsTo;
    }

    public override long GetSize() => 8;
    public override bool Equals(object obj) => (obj is PtrCType && (obj as PtrCType).pointsTo == pointsTo);

    public override int GetHashCode() => -(pointsTo.GetHashCode() + 1);

    public override string ToString() => "ptr<" + pointsTo.ToString() + ">";

    public override bool CanImplicitCastTo(CType type) => type.Equals(explicitCast) || (type is PtrCType && ((type as PtrCType).pointsTo is VoidCType || pointsTo is VoidCType || (type as PtrCType).pointsTo == pointsTo));

    public override bool CanExplicitCastTo(CType type) => type is PtrCType || (type is BuiltInCType && !(type as BuiltInCType).IsFloat() && type.GetSize() == GetSize()) || type is FuncCType || type is ExternFuncCType;

    public override CType MakeCastableClone(CType targetType) => new PtrCType(pointsTo) { explicitCast = targetType };
  }

  public class ArrayCType : CType
  {
    public readonly CType type;
    public readonly long count;

    public ArrayCType(CType type, long count)
    {
      this.type = type;
      this.count = count;
    }

    public override long GetSize() => count * type.GetSize();

    public override bool Equals(object obj) => (obj is ArrayCType && (obj as ArrayCType).type == type && (obj as ArrayCType).count == count);

    public override int GetHashCode() => -(type.GetHashCode() ^ count.GetHashCode());

    public override string ToString() => "array<" + type.ToString() + ", " + count + ">";

    public override bool CanImplicitCastTo(CType type) => type.Equals(explicitCast) || (this.Equals(type) || (type is PtrCType && (type as PtrCType).pointsTo == this.type));

    public override bool CanExplicitCastTo(CType type) => this.Equals(type) || type is PtrCType;

    public override CType MakeCastableClone(CType targetType) => new ArrayCType(type, count) { explicitCast = targetType };
  }

  public class _FuncCTypeWrapper : CType
  {
    public readonly CType[] parameters;
    public readonly CType returnType;

    public _FuncCTypeWrapper(CType returns, IEnumerable<CType> parameters)
    {
      returnType = returns;
      this.parameters = parameters.ToArray();
    }

    public override long GetSize() => 8;

    public override bool Equals(object obj)
    {
      if (obj is _FuncCTypeWrapper)
      {
        _FuncCTypeWrapper other = (obj as _FuncCTypeWrapper);

        if (other.returnType != returnType || other.parameters.Length != parameters.Length)
          return false;

        for (int i = 0; i < parameters.Length; i++)
          if (!other.parameters[i].Equals(parameters[i]))
            return false;

        return true;
      }

      return false;
    }

    public override int GetHashCode()
    {
      int hashCode = ~returnType.GetHashCode();
      int parameterIndex = 1;

      foreach (var type in parameters)
        hashCode ^= (type.GetHashCode() + parameterIndex++);

      return hashCode;
    }

    public override CType MakeCastableClone(CType targetType) => throw new Exception("Internal Compiler Error. Please override this function.");
  }

  public class ExternFuncCType : _FuncCTypeWrapper
  {
    public ExternFuncCType(CType returns, IEnumerable<CType> parameters) : base(returns, parameters) { }

    public override string ToString()
    {
      string ret = "extern_func<" + returnType.ToString() + " (";

      for (int i = 0; i < parameters.Length; i++)
      {
        ret += parameters[i].ToString();

        if (i + 1 < parameters.Length)
          ret += ", ";
      }

      return ret += ")>";
    }

    public override bool Equals(object obj) => base.Equals(obj) && obj is ExternFuncCType;

    public override int GetHashCode() => base.GetHashCode();

    public override bool CanImplicitCastTo(CType type) => type.Equals(explicitCast) || this.Equals(type);

    public override bool CanExplicitCastTo(CType type) => this.Equals(type) || (type is PtrCType && (type as PtrCType).pointsTo is VoidCType);

    public override CType MakeCastableClone(CType targetType) => new ExternFuncCType(returnType, parameters) { explicitCast = targetType };
  }

  public class FuncCType : _FuncCTypeWrapper
  {
    public FuncCType(CType returns, IEnumerable<CType> parameters) : base(returns, parameters) { }

    public override bool Equals(object obj) => base.Equals(obj) && obj is FuncCType;

    public override int GetHashCode() => ~base.GetHashCode();

    public override string ToString()
    {
      string ret = "func<" + returnType.ToString() + " (";

      for (int i = 0; i < parameters.Length; i++)
      {
        ret += parameters[i].ToString();

        if (i + 1 < parameters.Length)
          ret += ", ";
      }

      return ret += ")>";
    }

    public override bool CanImplicitCastTo(CType type) => type.Equals(explicitCast) || this.Equals(type);

    public override bool CanExplicitCastTo(CType type) => this.Equals(type) || (type is PtrCType && (type as PtrCType).pointsTo is VoidCType);

    public override CType MakeCastableClone(CType targetType) => new FuncCType(returnType, parameters) { explicitCast = targetType };
  }

  public class StructCType : CType
  {
    public readonly string file;
    public readonly int line;
    public readonly string name;
    public readonly StructAttribute[] attributes;
    private readonly long size;

    public StructCType(string name, IEnumerable<StructAttribute> attributes, string file, int line)
    {
      this.name = name;
      this.attributes = attributes.ToArray();
      this.file = file;
      this.line = line;

      size = 0;

      for (int i = 0; i < this.attributes.Length; i++)
        for (int j = i + 1; j < this.attributes.Length; j++)
          if (this.attributes[i].name == this.attributes[j].name)
            Compiler.Error($"In Struct '{this.name}' (File: '{this.file}', Line {this.line}) member {(i + 1)} (of type {this.attributes[i].type}) and {(j + 1)} (of type {this.attributes[j].type}) have the name '{this.attributes[j].name}'.", this.attributes[j].file, this.attributes[j].line);

      foreach (var attribute in this.attributes)
      {
        attribute.offset = size;
        size += attribute.type.GetSize();
      }
    }

    public override long GetSize()
    {
      return size;
    }

    public override bool Equals(object obj)
    {
      return ReferenceEquals(this, obj);
    }

    public override int GetHashCode()
    {
      return base.GetHashCode();
    }

    public override string ToString()
    {
      return "struct " + name;
    }

    public override CType MakeCastableClone(CType targetType) => throw new Exception("Internal Compiler Error: Structs cannot be type converted.");
  }

  public class StructAttribute
  {
    public readonly string name;
    public readonly CType type;
    public long offset;

    public readonly string file;
    public readonly int line;

    public StructAttribute(string name, CType type, string file, int line)
    {
      this.name = name;
      this.type = type;
      this.file = file;
      this.line = line;

      this.offset = 0;
    }
  }

  public class CValue
  {
    public CType type { get; protected set; }

    public string description = "";

    public string file { get; protected set; }

    public int line { get; protected set; }

    public bool isInitialized;
    public Position position;
    public bool hasPosition = false;

    public int remainingReferences = 0;

    public bool isConst { get; protected set; }

    public int lastTouchedInstructionCount = 0;

    private static int lastIndex = 0;
    private readonly int index = ++lastIndex;

    protected CValue()
    {

    }

    public CValue(string file, int line, CType type, bool isConst, bool isInitialized) : base()
    {
      this.file = file;
      this.line = line;
      this.type = type;
      this.isConst = isConst;
      this.isInitialized = isInitialized;
    }

    public override string ToString() => $"unnamed value #{index} [" + (isConst ? "const " : "") + type + (string.IsNullOrWhiteSpace(description) ? "" : $" '{description}'") + "]";

    public virtual CValue DeepClone(Scope scope, ref ByteCodeState byteCodeState)
    {
      var ret = new CValue();
      ret.type = type;
      ret.file = file;
      ret.line = line;
      ret.isInitialized = isInitialized;
      ret.position = position;
      ret.hasPosition = hasPosition;
      ret.remainingReferences = remainingReferences;
      ret.isConst = isConst;
      ret.lastTouchedInstructionCount = lastTouchedInstructionCount;
      ret.description = description;

      scope.instructions.Add(new CInstruction_CopyPositionFromValueToValue(this, ret, scope.maxRequiredStackSpace));

      return ret;
    }

    public virtual CValue MakeCastableClone(CType targetType, Scope scope, ref ByteCodeState byteCodeState)
    {
      var ret = this.DeepClone(scope, ref byteCodeState);

      ret.type = type.MakeCastableClone(targetType);

      return ret;
    }
  }

  public class CConstIntValue : CValue
  {
    public ulong uvalue { get; protected set; }

    public long ivalue { get; protected set; }

    protected CConstIntValue() : base() { }

    public CConstIntValue(ulong value, CType type, string file, int line) : base(file, line, type, true, true)
    {
      this.uvalue = value;
      unchecked { this.ivalue = (long)value; }
    }

    public CConstIntValue(NIntegerValue value, CType type) : base(value.file, value.line, type, true, true)
    {
      if (!(type is BuiltInCType) || (type as BuiltInCType).IsFloat())
        Compiler.Error($"Invalid Constant Value. Integer value cannot be assigned to type '{ type.ToString() }'.", value.file, value.line);

      var _type = (type as BuiltInCType);

      if (value.isForcefullyNegative && (_type.type == BuiltInTypes.u8 || _type.type == BuiltInTypes.u16 || _type.type == BuiltInTypes.u32 || _type.type == BuiltInTypes.u64))
        Compiler.Warn($"[E] Forcefully negative value '{value.int_value}' is being assigned to an unsigned type '{_type.ToString()}'", value.file, value.line);

      if (_type.type == BuiltInTypes.i8 && (value.int_value > sbyte.MaxValue || value.int_value < sbyte.MinValue))
      {
        Compiler.Warn($"[E] Value '{value.int_value}' needs to be truncated in order to be assigned to a value of type '{_type.ToString()}' (Range: {sbyte.MinValue} to {sbyte.MaxValue}).", value.file, value.line);
        unchecked { ivalue = (sbyte)ivalue; uvalue = (ulong)ivalue; };
      }
      else if (_type.type == BuiltInTypes.i16 && (value.int_value > short.MaxValue || value.int_value < short.MinValue))
      {
        Compiler.Warn($"[E] Value '{value.int_value}' needs to be truncated in order to be assigned to a value of type '{_type.ToString()}' (Range: {short.MinValue} to {short.MaxValue}).", value.file, value.line);
        unchecked { ivalue = (short)uvalue; uvalue = (ulong)ivalue; };
      }
      else if (_type.type == BuiltInTypes.i32 && (value.int_value > int.MaxValue || value.int_value < int.MinValue))
      {
        Compiler.Warn($"[E] Value '{value.int_value}' needs to be truncated in order to be assigned to a value of type '{_type.ToString()}' (Range: {int.MinValue} to {int.MaxValue}).", value.file, value.line);
        unchecked { ivalue = (int)uvalue; uvalue = (ulong)ivalue; };
      }
      else if (_type.type == BuiltInTypes.u8 && (value.int_value > byte.MaxValue || value.int_value < byte.MinValue))
      {
        Compiler.Warn($"[E] Value '{value.uint_value}' needs to be truncated in order to be assigned to a value of type '{_type.ToString()}' (Range: {byte.MinValue} to {byte.MaxValue}).", value.file, value.line);
        unchecked { uvalue = (byte)uvalue; ivalue = (long)uvalue; };
      }
      else if (_type.type == BuiltInTypes.u16 && (value.int_value > ushort.MaxValue || value.int_value < ushort.MinValue))
      {
        Compiler.Warn($"[E] Value '{value.uint_value}' needs to be truncated in order to be assigned to a value of type '{_type.ToString()}' (Range: {ushort.MinValue} to {ushort.MaxValue}).", value.file, value.line);
        unchecked { uvalue = (ushort)uvalue; ivalue = (long)uvalue; };
      }
      else if (_type.type == BuiltInTypes.u32 && (value.int_value > uint.MaxValue || value.int_value < uint.MinValue))
      {
        Compiler.Warn($"[E] Value '{value.uint_value}' needs to be truncated in order to be assigned to a value of type '{_type.ToString()}' (Range: {uint.MinValue} to {uint.MaxValue}).", value.file, value.line);
        unchecked { uvalue = (uint)uvalue; ivalue = (long)uvalue; };
      }

      if (!value.isForcefullyNegative)
        uvalue = value.uint_value;

      ivalue = value.int_value;
    }

    public override string ToString() => "unnamed immediate value [" + (isConst ? "const " : "") + type + "] (" + ((type as BuiltInCType).IsUnsigned() ? uvalue.ToString() : ivalue.ToString()) + ")" + (string.IsNullOrWhiteSpace(description) ? "" : $" ('{description}')");

    public override CValue DeepClone(Scope scope, ref ByteCodeState byteCodeState)
    {
      var ret = new CConstIntValue();
      ret.type = type;
      ret.file = file;
      ret.line = line;
      ret.isInitialized = isInitialized;
      ret.position = position;
      ret.hasPosition = hasPosition;
      ret.remainingReferences = remainingReferences;
      ret.isConst = isConst;
      ret.lastTouchedInstructionCount = lastTouchedInstructionCount;
      ret.description = description;

      ret.uvalue = uvalue;
      ret.ivalue = ivalue;

      scope.instructions.Add(new CInstruction_CopyPositionFromValueToValue(this, ret, scope.maxRequiredStackSpace));

      return ret;
    }
  }

  public class CConstFloatValue : CValue
  {
    public double value { get; protected set; }

    protected CConstFloatValue() : base() { }

    public CConstFloatValue(double value, CType type, string file, int line) : base(file, line, type, true, true)
    {
      this.value = value;
    }

    public CConstFloatValue(NFloatingPointValue value, CType type) : base(value.file, value.line, type, true, true)
    {
      if (!(type is BuiltInCType) || !(type as BuiltInCType).IsFloat())
        Compiler.Error($"Invalid Constant Value. Floating point value cannot be assigned to type '{ type.ToString() }'.", value.file, value.line);

      var _type = (type as BuiltInCType);

      this.value = value.value;
    }

    public override string ToString() => "unnamed immediate value [" + (isConst ? "const " : "") + type + "] (" + value.ToString() + ")" + (string.IsNullOrWhiteSpace(description) ? "" : $" ('{description}')");

    public override CValue DeepClone(Scope scope, ref ByteCodeState byteCodeState)
    {
      var ret = new CConstFloatValue();
      ret.isInitialized = isInitialized;
      ret.position = position;
      ret.hasPosition = hasPosition;
      ret.remainingReferences = remainingReferences;
      ret.isConst = isConst;
      ret.lastTouchedInstructionCount = lastTouchedInstructionCount;
      ret.description = description;

      ret.value = value;

      scope.instructions.Add(new CInstruction_CopyPositionFromValueToValue(this, ret, scope.maxRequiredStackSpace));

      return ret;
    }
  }

  public class CNamedValue : CValue
  {
    public string name { get; protected set; }
    public bool hasStackOffset = false;
    public long homeStackOffset;
    public bool modifiedSinceLastHome = false;
    public bool isVolatile = false;

    protected CNamedValue() : base() { }

    public CNamedValue(NName name, CType type, bool isConst, bool isInitialized) : base(name.file, name.line, type, isConst, isInitialized)
    {
      this.name = name.name;
    }

    public CNamedValue(string name, CType type, bool isConst, bool isInitialized, string file, int line) : base(file, line, type, isConst, isInitialized)
    {
      this.name = name;
    }

    public override string ToString() => (isConst ? "const " : "") + type + " " + name + (string.IsNullOrWhiteSpace(description) ? "" : $" ('{description}')");

    public override CValue DeepClone(Scope scope, ref ByteCodeState byteCodeState)
    {
      var ret = new CNamedValue();
      ret.type = type;
      ret.file = file;
      ret.line = line;
      ret.isInitialized = isInitialized;
      ret.position = position;
      ret.hasPosition = hasPosition;
      ret.remainingReferences = remainingReferences;
      ret.isConst = isConst;
      ret.lastTouchedInstructionCount = lastTouchedInstructionCount;
      ret.description = description;

      ret.name = name;
      ret.hasStackOffset = false;
      ret.homeStackOffset = 0;
      ret.modifiedSinceLastHome = false;

      scope.instructions.Add(new CInstruction_CopyPositionFromValueToValue(this, ret, scope.maxRequiredStackSpace));

      return ret;
    }
  }

  public class CGlobalValueReference : CValue
  {
    public bool isModified = false;
    
    public CGlobalValueReference(CType type, string file, int line, bool isConst) : base(file, line, type, isConst, true)
    {

    }

    public override string ToString() => "unnamed global (?) value [" + (isConst ? "const " : "") + type + (string.IsNullOrWhiteSpace(description) ? "" : $" '{description}'") + "]";
  }

  public class FileContents
  {
    public string filename;
    public string[] lines;

    public List<Node> nodes = new List<Node>();
  }

  public class CompileFailureException : Exception
  {

  }

  public static class ParsingExtensions
  {
    public static bool FindString(this string s, string find, out int index)
    {
      if (s.Length < find.Length)
      {
        index = 0;
        return false;
      }

      int[] findIndexes = find.GetKMP();

      for (int i = 0; i < s.Length; i++)
      {
        if (find.Length > s.Length - i)
        {
          index = i;
          return false;
        }

        int length = find.Length;

        for (int j = 0; j < length; j++)
        {
          if (s[i + j] != find[j])
          {
            i += findIndexes[j];
            break;
          }

          if (j + 1 == length)
          {
            index = i;
            return true;
          }
        }
      }

      index = s.Length;
      return false;
    }

    static int[] GetKMP(this string s)
    {
      int[] ret = new int[s.Length];

      int lastLength = 0;
      int i = 1;

      while (i < s.Length)
        if (s[i] == s[lastLength])
          ret[i++] = ++lastLength;
        else
            if (lastLength != 0)
          lastLength = ret[lastLength - 1];
        else
          ret[i++] = 0;

      return ret;
    }

    public static bool NextIs(this string s, int startIndex, string next)
    {
      if (s.Length > startIndex + next.Length)
      {
        for (int i = 0; i < next.Length; i++)
          if (s[startIndex + i] != next[i])
            return false;

        return true;
      }

      return false;
    }

    public static bool NextIs(this List<Node> nodes, params Type[] types)
    {
      if (nodes.Count >= types.Length)
      {
        for (int i = 0; i < types.Length; i++)
          if (!nodes[i].GetType().IsAssignableFrom(types[i]))
            return false;

        return true;
      }

      return false;
    }

    public static bool NextIs(this List<Node> nodes, int offset, params Type[] types)
    {
      if (nodes.Count - offset >= types.Length)
      {
        for (int i = 0; i < types.Length; i++)
          if (!nodes[offset + i].GetType().IsAssignableFrom(types[i]))
            return false;

        return true;
      }

      return false;
    }

    public static int FindNextSameScope(this List<Node> nodes, int start, Func<Node, bool> check)
    {
      int bracketLevel = 0;
      int parantesisLevel = 0;
      int braceLevel = 0;

      for (int i = start; i < nodes.Count; i++)
      {
        if (nodes[i] is NOpenParanthesis)
          ++parantesisLevel;
        else if (nodes[i] is NCloseParanthesis)
          --parantesisLevel;
        else if (nodes[i] is NOpenBracket)
          ++bracketLevel;
        else if (nodes[i] is NCloseBracket)
          --bracketLevel;
        else if (nodes[i] is NOpenScope)
          ++braceLevel;
        else if (nodes[i] is NCloseScope)
          --braceLevel;

        if (bracketLevel <= 0 && parantesisLevel <= 0 && braceLevel <= 0 && check(nodes[i]))
          return i;
      }

      return -1;
    }

    public static int FindNextSameScope(this List<Node> nodes, Func<Node, bool> check) => FindNextSameScope(nodes, 0, check);
    
    public static string ElementsToString(this IEnumerable<byte> list, string format)
    {
      string ret = "[";
      bool first = true;

      foreach (var x in list)
      {
        if (first)
          first = !first;
        else
          ret += ", ";

        ret += x.ToString(format);
      }

      return ret + "]";
    }
  }

  public struct Position
  {
    public bool inRegister;
    public int registerIndex;
    public long stackOffsetForward;

    public static Position Register(int registerIndex)
    {
      Position ret = new Position();

      ret.inRegister = true;
      ret.registerIndex = registerIndex;

      return ret;
    }

    public static Position StackOffset(long stackOffsetForward)
    {
      Position ret = new Position();

      ret.inRegister = false;
      ret.stackOffsetForward = stackOffsetForward;

      return ret;
    }

    public override string ToString()
    {
      if (inRegister)
        return $"r:{registerIndex}";
      return $"stackOffsetForward:{stackOffsetForward}";
    }
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
      this.value = new CNamedValue(name, type, false, true, file, line);
    }
  }

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
    public LLI_Label_PseudoInstruction functionStartLabel = new LLI_Label_PseudoInstruction(),
      functionEndLabel = new LLI_Label_PseudoInstruction();

    public CFunction(string name, CType returnType, IEnumerable<FunctionParameter> parameters, string file, int line)
    {
      this.name = name;
      this.returnType = returnType;
      this.parameters = parameters.ToArray();
      this.file = file;
      this.line = line;

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

  public class CBuiltInFunction : CFunction
  {
    public readonly byte builtinFunctionIndex;

    public static CBuiltInFunction[] Functions = new CBuiltInFunction[]
    {
      new CBuiltInFunction("alloc", (byte)BuiltInFunctions.LLS_BF_ALLOC, new PtrCType(VoidCType.Instance), new Tuple<string, CType>[] { Tuple.Create("size", (CType)BuiltInCType.Types["u64"]) }),
      new CBuiltInFunction("free", (byte)BuiltInFunctions.LLS_BF_FREE, VoidCType.Instance, new Tuple<string, CType>[] { Tuple.Create("ptr", (CType)new PtrCType(VoidCType.Instance)) }),
      new CBuiltInFunction("realloc", (byte)BuiltInFunctions.LLS_BF_REALLOC, new PtrCType(VoidCType.Instance), new Tuple<string, CType>[] { Tuple.Create("ptr", (CType)new PtrCType(VoidCType.Instance)), Tuple.Create("newSize", (CType)BuiltInCType.Types["u64"]) }),
      new CBuiltInFunction("load_library", (byte)BuiltInFunctions.LLS_BF_LOAD_LIBRARY, new PtrCType(VoidCType.Instance), new Tuple<string, CType>[] { Tuple.Create("libraryName", (CType)new PtrCType(BuiltInCType.Types["i8"])) }),
      new CBuiltInFunction("get_proc_address", (byte)BuiltInFunctions.LLS_BF_GET_PROC_ADDRESS, new PtrCType(VoidCType.Instance), new Tuple<string, CType>[] { Tuple.Create("libraryHandle", (CType)new PtrCType(VoidCType.Instance)), Tuple.Create("functionName", (CType)new PtrCType(BuiltInCType.Types["i8"])) }),
    };

    private const string File = "{compiler internal / built in function}";

    private CBuiltInFunction(string name, byte index, CType returnType, Tuple<string, CType>[] parameters) : base(name, returnType, (from x in parameters select new FunctionParameter(x.Item2, x.Item1, File, 0)), File, 0)
    {
      this.builtinFunctionIndex = index;
    }
  }

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
      }

      int triviallyFreeRegister = register < Compiler.IntegerRegisters ? GetTriviallyFreeIntegerRegister() : GetTriviallyFreeFloatRegister();

      if (triviallyFreeRegister == -1)
      {
        FreeUsedRegister(register, stackSize);
      }
      else
      {
        instructions.Add(new LLI_MovRegisterToRegister(register, triviallyFreeRegister));
        
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
              instructions.Add(new LLI_MovRegisterToStackOffset_NBytes(sourceValue.position.registerIndex, stackSize, targetPosition.registerIndex, (int)bytes));

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

        // TODO: Handle smaller types!

        instructions.Add(new LLI_MovStackOffsetToRegister(stackSize, value.position.stackOffsetForward, (byte)registerIndex));

        registers[registerIndex] = value;
        value.position.inRegister = true;
        value.position.registerIndex = registerIndex;
        
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
    }

    public void CopyValueToPosition(CValue sourceValue, Position position, SharedValue<long> stackSize)
    {
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
  }
  
  public class SharedValue<T> where T : IComparable<T>
  {
    public T Value;

    public SharedValue(T value) => this.Value = value;
  }

  public abstract class LLInstruction
  {
    public static string currentFile;
    public static int currentLine;

    public string file;
    public int line;

    public ulong position;

    public ulong bytecodeSize { get; protected set; }

    public LLInstruction(ulong bytecodeSize)
    {
      this.file = currentFile;
      this.line = currentLine;

      this.bytecodeSize = bytecodeSize;
    }

    /// <summary>
    /// Should not be called if bytecodeSize == 0;
    /// </summary>
    /// <param name="byteCode">the byte code to append to.</param>
    public abstract void AppendBytecode(ref List<byte> byteCode);

    public virtual void AfterCodeGen() { }
  }

  public class LLI_Exit : LLInstruction
  {
    public LLI_Exit() : base(1) { }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_EXIT);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_EXIT}";
  }

  public abstract class LLI_PseudoInstruction : LLInstruction
  {
    public LLI_PseudoInstruction(ulong size) : base(size) { }
  }

  public class LLI_Label_PseudoInstruction : LLI_PseudoInstruction
  {
    public LLI_Label_PseudoInstruction() : base(0) { }

    public override void AppendBytecode(ref List<byte> byteCode) { }

    public override string ToString() => $"label_0x{GetHashCode():X}_at_0x{position:X}:";
  }

  public class LLI_Comment_PseudoInstruction : LLI_PseudoInstruction
  {
    public string comment;

    public LLI_Comment_PseudoInstruction(string comment) : base(0)
    {
      this.comment = comment;
    }

    public override void AppendBytecode(ref List<byte> byteCode) { }

    public override string ToString() => $"# Comment: {comment}";
  }

  public class LLI_Location_PseudoInstruction : LLI_PseudoInstruction
  {
    public string name;
    public Position location;
    public SharedValue<long> stackSize;

    public LLI_Location_PseudoInstruction(CValue value, SharedValue<long> stackSize, ByteCodeState byteCodeState) : base(0)
    {
      if (!value.hasPosition)
        throw new Exception("Internal Compiler Error!");

      if (value is CNamedValue)
        name = (value as CNamedValue).name;
      else
        name = "(" + value.ToString() + ")";

      location = value.position;
      this.stackSize = stackSize;

      if (value.position.inRegister && byteCodeState.registers[value.position.registerIndex] != value)
        throw new Exception("Internal Compiler Error: Value not available in specified register.");
    }

    public override void AppendBytecode(ref List<byte> byteCode) { }

    public override string ToString() => $"# Value Location: '{name}' " + (location.inRegister ? $"r:{ location.registerIndex }" : $"s:{stackSize.Value - location.stackOffsetForward}");
  }

  public class LLI_JmpToLabel : LLInstruction
  {
    readonly LLI_Label_PseudoInstruction label;

    public LLI_JmpToLabel(LLI_Label_PseudoInstruction label) : base(9) => this.label = label;

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_JMP_RELATIVE_IMM);
      byteCode.AddRange(BitConverter.GetBytes((long)label.position - (long)(this.position + this.bytecodeSize)));
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_JMP_RELATIVE_IMM} label_0x{label.GetHashCode():X}_at_0x{label.position:X}";
  }

  public class LLI_StackIncrementImm : LLInstruction
  {
    readonly SharedValue<long> value;
    long offset = 0;

    public LLI_StackIncrementImm(long value) : base(9)
    {
      this.value = new SharedValue<long>(value);
    }

    public LLI_StackIncrementImm(SharedValue<long> value, long offset = 0) : base(9)
    {
      this.value = value;
      this.offset = offset;
    }

    public override void AfterCodeGen()
    {
      if (((long)this.value.Value + offset) == 0)
        base.bytecodeSize = 0;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_STACK_INC_IMM);
      byteCode.AddRange(BitConverter.GetBytes(value.Value - offset));
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_STACK_INC_IMM} {value.Value - offset}";
  }

  public class LLI_StackDecrementImm : LLInstruction
  {
    readonly SharedValue<long> value;
    long offset = 0;

    public LLI_StackDecrementImm(long value) : base(9)
    {
      this.value = new SharedValue<long>(value);
    }

    public LLI_StackDecrementImm(SharedValue<long> value, long offset = 0) : base(9)
    {
      this.value = value;
      this.offset = offset;
    }

    public override void AfterCodeGen()
    {
      if (((long)this.value.Value + offset) == 0)
        base.bytecodeSize = 0;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_STACK_DEC_IMM);
      byteCode.AddRange(BitConverter.GetBytes(value.Value - offset));
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_STACK_DEC_IMM} {value.Value - offset}";
  }

  public class LLI_LoadEffectiveAddress_StackOffsetToRegister : LLInstruction
  {
    readonly SharedValue<long> value;
    readonly SharedValue<long> offset;
    readonly int register;

    public LLI_LoadEffectiveAddress_StackOffsetToRegister(long value, long offset, int register) : base(10)
    {
      this.value = new SharedValue<long>(value);
      this.offset = new SharedValue<long>(offset);
      this.register = register;
    }

    public LLI_LoadEffectiveAddress_StackOffsetToRegister(SharedValue<long> value, long offset, int register) : base(10)
    {
      this.value = value;
      this.offset = new SharedValue<long>(offset);
      this.register = register;
    }

    public LLI_LoadEffectiveAddress_StackOffsetToRegister(SharedValue<long> value, SharedValue<long> offset, int register) : base(10)
    {
      this.value = value;
      this.offset = offset;
      this.register = register;
    }
    
    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_LEA_STACK_TO_REGISTER);
      byteCode.Add((byte)register);
      byteCode.AddRange(BitConverter.GetBytes(value.Value - offset.Value));
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_LEA_STACK_TO_REGISTER} r:{register}, {value.Value - offset.Value}";
  }

  public class LLI_MovRegisterToRegister : LLInstruction
  {
    readonly int sourceRegister, targetRegister;

    public LLI_MovRegisterToRegister(int sourceRegister, int targetRegister) : base(3)
    {
      this.sourceRegister = sourceRegister;
      this.targetRegister = targetRegister;
    }

    public override void AfterCodeGen()
    {
      if (sourceRegister == targetRegister)
        this.bytecodeSize = 0;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_MOV_REGISTER_REGISTER);
      byteCode.Add((byte)targetRegister);
      byteCode.Add((byte)sourceRegister);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_MOV_REGISTER_REGISTER} r:{targetRegister}, r:{sourceRegister}";
  }

  public class LLI_PushRegister : LLInstruction
  {
    readonly byte register;

    public LLI_PushRegister(byte register) : base(2)
    {
      this.register = register;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_PUSH_REGISTER);
      byteCode.Add(register);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_PUSH_REGISTER} r:{register}";
  }

  public class LLI_PopRegister : LLInstruction
  {
    readonly byte register;

    public LLI_PopRegister(byte register) : base(2)
    {
      this.register = register;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_POP_REGISTER);
      byteCode.Add(register);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_POP_REGISTER} r:{register}";
  }

  public class LLI_MovRegisterToStackOffset : LLInstruction
  {
    readonly int register;
    readonly SharedValue<long> stackSize;
    readonly long offset;

    public LLI_MovRegisterToStackOffset(int register, SharedValue<long> stackSize, long offset) : base(1 + 8 + 1)
    {
      this.register = register;
      this.stackSize = stackSize;
      this.offset = offset;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_MOV_REGISTER_STACK);
      byteCode.AddRange(BitConverter.GetBytes(stackSize.Value - offset));
      byteCode.Add((byte)register);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_MOV_REGISTER_STACK} {stackSize.Value - offset}, r:{register}";
  }

  public class LLI_MovRegisterToStackOffset_NBytes : LLInstruction
  {
    readonly int register;
    readonly SharedValue<long> stackSize;
    readonly long offset;
    readonly int bytes;

    public LLI_MovRegisterToStackOffset_NBytes(int register, SharedValue<long> stackSize, long offset, int bytes) : base(1 + 8 + 1 + 1)
    {
      this.register = register;
      this.stackSize = stackSize;
      this.offset = offset;
      this.bytes = bytes;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_MOV_REGISTER_STACK_N_BYTES);
      byteCode.AddRange(BitConverter.GetBytes(stackSize.Value - offset));
      byteCode.Add((byte)register);
      byteCode.Add((byte)bytes);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_MOV_REGISTER_STACK_N_BYTES} {stackSize.Value - offset}, r:{register}, {bytes}";
  }

  public class LLI_MovStackOffsetToRegister : LLInstruction
  {
    readonly int register;
    readonly SharedValue<long> stackSize;
    readonly long offset;

    public LLI_MovStackOffsetToRegister(SharedValue<long> stackSize, long offset, int register) : base(1 + 8 + 1)
    {
      this.register = register;
      this.stackSize = stackSize;
      this.offset = offset;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_MOV_STACK_REGISTER);
      byteCode.Add((byte)register);
      byteCode.AddRange(BitConverter.GetBytes(stackSize.Value - offset));
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_MOV_STACK_REGISTER} r:{register}, {stackSize.Value - offset}";
  }

  public class LLI_MovStackOffsetToStackOffset : LLInstruction
  {
    SharedValue<long> sourceStackSize;
    long sourceOffset;

    SharedValue<long> targetStackSize;
    long targetOffset;

    public LLI_MovStackOffsetToStackOffset(SharedValue<long> sourceStackSize, long sourceOffset, SharedValue<long> targetStackSize, long targetOffset) : base(1 + 8 + 8)
    {
      this.sourceStackSize = sourceStackSize;
      this.sourceOffset = sourceOffset;

      this.targetStackSize = targetStackSize;
      this.targetOffset = targetOffset;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_MOV_STACK_STACK);
      byteCode.AddRange(BitConverter.GetBytes(targetStackSize.Value - targetOffset));
      byteCode.AddRange(BitConverter.GetBytes(sourceStackSize.Value - sourceOffset));
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_MOV_STACK_STACK} {targetStackSize.Value - targetOffset}, {sourceStackSize.Value - sourceOffset}";
  }

  public class LLI_MovStackOffsetToStackOffset_NBytes : LLInstruction
  {
    SharedValue<long> sourceStackSize;
    long sourceOffset;

    SharedValue<long> targetStackSize;
    long targetOffset;

    byte bytes;

    public LLI_MovStackOffsetToStackOffset_NBytes(SharedValue<long> sourceStackSize, long sourceOffset, SharedValue<long> targetStackSize, long targetOffset, byte bytes) : base(1 + 8 + 8 + 1)
    {
      this.sourceStackSize = sourceStackSize;
      this.sourceOffset = sourceOffset;

      this.targetStackSize = targetStackSize;
      this.targetOffset = targetOffset;

      this.bytes = bytes;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_MOV_STACK_STACK_N_BYTES);
      byteCode.AddRange(BitConverter.GetBytes(targetStackSize.Value - targetOffset));
      byteCode.AddRange(BitConverter.GetBytes(sourceStackSize.Value - sourceOffset));
      byteCode.Add(bytes);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_MOV_STACK_STACK_N_BYTES} {targetStackSize.Value - targetOffset}, {sourceStackSize.Value - sourceOffset}, {bytes}";
  }

  public class LLI_MovRegisterToPtrInRegister : LLInstruction
  {
    int targetPtrRegister, sourceValueRegister;

    public LLI_MovRegisterToPtrInRegister(int targetPtrRegister, int sourceValueRegister) : base(3)
    {
      this.targetPtrRegister = targetPtrRegister;
      this.sourceValueRegister = sourceValueRegister;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_MOV_REGISTER__PTR_IN_REGISTER);
      byteCode.Add((byte)targetPtrRegister);
      byteCode.Add((byte)sourceValueRegister);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_MOV_REGISTER__PTR_IN_REGISTER} r:{targetPtrRegister}, r:{sourceValueRegister}";
  }

  public class LLI_MovRegisterToPtrInRegister_NBytes : LLInstruction
  {
    int targetPtrRegister, sourceValueRegister, bytes;

    public LLI_MovRegisterToPtrInRegister_NBytes(int targetPtrRegister, int sourceValueRegister, int bytes) : base(4)
    {
      this.targetPtrRegister = targetPtrRegister;
      this.sourceValueRegister = sourceValueRegister;
      this.bytes = bytes;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_MOV_REGISTER__PTR_IN_REGISTER_N_BYTES);
      byteCode.Add((byte)targetPtrRegister);
      byteCode.Add((byte)sourceValueRegister);
      byteCode.Add((byte)bytes);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_MOV_REGISTER__PTR_IN_REGISTER_N_BYTES} r:{targetPtrRegister}, r:{sourceValueRegister}, {bytes}";
  }

  public class LLI_MovFromPtrInRegisterToRegister : LLInstruction
  {
    int sourcePtrRegister, targetRegister;

    public LLI_MovFromPtrInRegisterToRegister(int sourcePtrRegister, int targetRegister) : base(3)
    {
      this.sourcePtrRegister = sourcePtrRegister;
      this.targetRegister = targetRegister;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_MOV_PTR_IN_REGISTER__REGISTER);
      byteCode.Add((byte)targetRegister);
      byteCode.Add((byte)sourcePtrRegister);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_MOV_PTR_IN_REGISTER__REGISTER} r:{targetRegister}, r:{sourcePtrRegister}";
  }

  public class LLI_MovImmToRegister : LLInstruction
  {
    int register;
    byte[] value;

    public LLI_MovImmToRegister(int register, byte[] value) : base(1 + 1 + 8)
    {
      this.register = register;
      this.value = value;

      if (this.value.Length != 8)
        throw new Exception("Internal Compiler Error!");
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_MOV_IMM_REGISTER);
      byteCode.Add((byte)register);
      byteCode.AddRange(value);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_MOV_IMM_REGISTER} r:{register}, {value.ElementsToString("X2")}";
  }

  public class LLI_AddImm : LLInstruction
  {
    int register;
    byte[] value;

    public LLI_AddImm(int register, byte[] value) : base(1 + 1 + 8)
    {
      this.register = register;
      this.value = value;

      if (this.value.Length != 8)
        throw new Exception("Internal Compiler Error!");
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_ADD_IMM);
      byteCode.Add((byte)register);
      byteCode.AddRange(value);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_ADD_IMM} r:{register}, {value.ElementsToString("X2")}";
  }

  public class LLI_AddRegister : LLInstruction
  {
    int src_dst;
    int operand;

    public LLI_AddRegister(int src_dst, int operand) : base(1 + 1 + 1)
    {
      this.src_dst = src_dst;
      this.operand = operand;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_ADD_REGISTER);
      byteCode.Add((byte)src_dst);
      byteCode.Add((byte)operand);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_ADD_REGISTER} r:{src_dst}, r:{operand}";
  }

  public class LLI_MultiplySignedRegister : LLInstruction
  {
    int src_dst;
    int operand;

    public LLI_MultiplySignedRegister(int src_dst, int operand) : base(1 + 1 + 1)
    {
      this.src_dst = src_dst;
      this.operand = operand;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_MULI_REGISTER);
      byteCode.Add((byte)src_dst);
      byteCode.Add((byte)operand);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_MULI_REGISTER} r:{src_dst}, r:{operand}";
  }

  public class LLI_MultiplySignedImm : LLInstruction
  {
    int register;
    byte[] value;

    public LLI_MultiplySignedImm(int register, byte[] value) : base(1 + 1 + 8)
    {
      this.register = register;
      this.value = value;

      if (this.value.Length != 8)
        throw new Exception("Internal Compiler Error!");
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_MULI_IMM);
      byteCode.Add((byte)register);
      byteCode.AddRange(value);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_MULI_IMM} r:{register}, {value.ElementsToString("X2")}";
  }

  public class LLI_MultiplyUnsignedRegister : LLInstruction
  {
    int src_dst;
    int operand;

    public LLI_MultiplyUnsignedRegister(int src_dst, int operand) : base(1 + 1 + 1)
    {
      this.src_dst = src_dst;
      this.operand = operand;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_MULU_REGISTER);
      byteCode.Add((byte)src_dst);
      byteCode.Add((byte)operand);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_MULU_REGISTER} r:{src_dst}, r:{operand}";
  }

  public class LLI_MultiplyUnsignedImm : LLInstruction
  {
    int register;
    byte[] value;

    public LLI_MultiplyUnsignedImm(int register, byte[] value) : base(1 + 1 + 8)
    {
      this.register = register;
      this.value = value;

      if (this.value.Length != 8)
        throw new Exception("Internal Compiler Error!");
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_MULU_IMM);
      byteCode.Add((byte)register);
      byteCode.AddRange(value);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_MULU_IMM} r:{register}, {value.ElementsToString("X2")}";
  }

  public class LLI_DivideSignedRegister : LLInstruction
  {
    int src_dst;
    int operand;

    public LLI_DivideSignedRegister(int src_dst, int operand) : base(1 + 1 + 1)
    {
      this.src_dst = src_dst;
      this.operand = operand;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_DIVI_REGISTER);
      byteCode.Add((byte)src_dst);
      byteCode.Add((byte)operand);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_DIVI_REGISTER} r:{src_dst}, r:{operand}";
  }

  public class LLI_DivideSignedImm : LLInstruction
  {
    int register;
    byte[] value;

    public LLI_DivideSignedImm(int register, byte[] value) : base(1 + 1 + 8)
    {
      this.register = register;
      this.value = value;

      if (this.value.Length != 8)
        throw new Exception("Internal Compiler Error!");
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_DIVI_IMM);
      byteCode.Add((byte)register);
      byteCode.AddRange(value);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_DIVI_IMM} r:{register}, {value.ElementsToString("X2")}";
  }

  public class LLI_DivideUnsignedRegister : LLInstruction
  {
    int src_dst;
    int operand;

    public LLI_DivideUnsignedRegister(int src_dst, int operand) : base(1 + 1 + 1)
    {
      this.src_dst = src_dst;
      this.operand = operand;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_DIVU_REGISTER);
      byteCode.Add((byte)src_dst);
      byteCode.Add((byte)operand);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_DIVU_REGISTER} r:{src_dst}, r:{operand}";
  }

  public class LLI_DivideUnsignedImm : LLInstruction
  {
    int register;
    byte[] value;

    public LLI_DivideUnsignedImm(int register, byte[] value) : base(1 + 1 + 8)
    {
      this.register = register;
      this.value = value;

      if (this.value.Length != 8)
        throw new Exception("Internal Compiler Error!");
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_DIVU_IMM);
      byteCode.Add((byte)register);
      byteCode.AddRange(value);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_DIVU_IMM} r:{register}, {value.ElementsToString("X2")}";
  }

  public class LLI_ModuloRegister : LLInstruction
  {
    int src_dst;
    int operand;

    public LLI_ModuloRegister(int src_dst, int operand) : base(1 + 1 + 1)
    {
      this.src_dst = src_dst;
      this.operand = operand;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_MOD_REGISTER);
      byteCode.Add((byte)src_dst);
      byteCode.Add((byte)operand);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_MOD_REGISTER} r:{src_dst}, r:{operand}";
  }

  public class LLI_ModuloImm : LLInstruction
  {
    int register;
    byte[] value;

    public LLI_ModuloImm(int register, byte[] value) : base(1 + 1 + 8)
    {
      this.register = register;
      this.value = value;

      if (this.value.Length != 8)
        throw new Exception("Internal Compiler Error!");
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_MOD_IMM);
      byteCode.Add((byte)register);
      byteCode.AddRange(value);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_MOD_IMM} r:{register}, {value.ElementsToString("X2")}";
  }

  public class LLI_AndImm : LLInstruction
  {
    int register;
    byte[] value;

    public LLI_AndImm(int register, byte[] value) : base(1 + 1 + 8)
    {
      this.register = register;
      this.value = value;

      if (this.value.Length != 8)
        throw new Exception("Internal Compiler Error!");
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_AND_IMM);
      byteCode.Add((byte)register);
      byteCode.AddRange(value);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_AND_IMM} r:{register}, {value.ElementsToString("X2")}";
  }

  public class LLI_AndRegister : LLInstruction
  {
    int src_dst;
    int operand;

    public LLI_AndRegister(int src_dst, int operand) : base(1 + 1 + 1)
    {
      this.src_dst = src_dst;
      this.operand = operand;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_AND_REGISTER);
      byteCode.Add((byte)src_dst);
      byteCode.Add((byte)operand);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_AND_REGISTER} r:{src_dst}, r:{operand}";
  }

  public class LLI_OrRegister : LLInstruction
  {
    int src_dst;
    int operand;

    public LLI_OrRegister(int src_dst, int operand) : base(1 + 1 + 1)
    {
      this.src_dst = src_dst;
      this.operand = operand;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_OR_REGISTER);
      byteCode.Add((byte)src_dst);
      byteCode.Add((byte)operand);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_OR_REGISTER} r:{src_dst}, r:{operand}";
  }

  public class LLI_XOrRegister : LLInstruction
  {
    int src_dst;
    int operand;

    public LLI_XOrRegister(int src_dst, int operand) : base(1 + 1 + 1)
    {
      this.src_dst = src_dst;
      this.operand = operand;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_XOR_REGISTER);
      byteCode.Add((byte)src_dst);
      byteCode.Add((byte)operand);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_XOR_REGISTER} r:{src_dst}, r:{operand}";
  }

  public class LLI_BitShiftLeftRegister : LLInstruction
  {
    int src_dst;
    int operand;

    public LLI_BitShiftLeftRegister(int src_dst, int operand) : base(1 + 1 + 1)
    {
      this.src_dst = src_dst;
      this.operand = operand;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_BSL_REGISTER);
      byteCode.Add((byte)src_dst);
      byteCode.Add((byte)operand);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_BSL_REGISTER} r:{src_dst}, r:{operand}";
  }

  public class LLI_BitShiftRightRegister : LLInstruction
  {
    int src_dst;
    int operand;

    public LLI_BitShiftRightRegister(int src_dst, int operand) : base(1 + 1 + 1)
    {
      this.src_dst = src_dst;
      this.operand = operand;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_BSR_REGISTER);
      byteCode.Add((byte)src_dst);
      byteCode.Add((byte)operand);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_BSR_REGISTER} r:{src_dst}, r:{operand}";
  }

  public class LLI_LogicalAndRegister : LLInstruction
  {
    int src_dst;
    int operand;

    public LLI_LogicalAndRegister(int src_dst, int operand) : base(1 + 1 + 1)
    {
      this.src_dst = src_dst;
      this.operand = operand;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_LOGICAL_AND_REGISTER);
      byteCode.Add((byte)src_dst);
      byteCode.Add((byte)operand);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_LOGICAL_AND_REGISTER} r:{src_dst}, r:{operand}";
  }

  public class LLI_LogicalOrRegister : LLInstruction
  {
    int src_dst;
    int operand;

    public LLI_LogicalOrRegister(int src_dst, int operand) : base(1 + 1 + 1)
    {
      this.src_dst = src_dst;
      this.operand = operand;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_LOGICAL_OR_REGISTER);
      byteCode.Add((byte)src_dst);
      byteCode.Add((byte)operand);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_LOGICAL_OR_REGISTER} r:{src_dst}, r:{operand}";
  }

  public class LLI_NegateRegister : LLInstruction
  {
    int register;

    public LLI_NegateRegister(int register) : base(1 + 1)
    {
      this.register = register;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_NEGATE_REGISTER);
      byteCode.Add((byte)register);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_NEGATE_REGISTER} r:{register}";
  }

  public class LLI_EqualsRegister : LLInstruction
  {
    int src_dst;
    int operand;
    public LLI_EqualsRegister(int src_dst, int operand) : base(1 + 1 + 1)
    {
      this.src_dst = src_dst;
      this.operand = operand;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_EQ_REGISTER);
      byteCode.Add((byte)src_dst);
      byteCode.Add((byte)operand);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_EQ_REGISTER} r:{src_dst}, r:{operand}";
  }

  public class LLI_NotRegister : LLInstruction
  {
    int register;

    public LLI_NotRegister(int register) : base(1 + 1)
    {
      this.register = register;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_NOT_REGISTER);
      byteCode.Add((byte)register);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_NOT_REGISTER} r:{register}";
  }

  public class LLI_CallBuiltInFunction_IDFromRegister_ResultToRegister : LLInstruction
  {
    byte idRegister;
    byte resultRegister;

    public LLI_CallBuiltInFunction_IDFromRegister_ResultToRegister(byte idRegister, byte resultRegister) : base(1 + 1 + 1)
    {
      this.idRegister = idRegister;
      this.resultRegister = resultRegister;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_CALL_BUILTIN__RESULT_TO_REGISTER__ID_FROM_REGISTER);
      byteCode.Add(idRegister);
      byteCode.Add(resultRegister);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_CALL_BUILTIN__RESULT_TO_REGISTER__ID_FROM_REGISTER} r:{idRegister}, r:{resultRegister}";
  }

  public class LLI_CallExternFunction_ResultToRegister : LLInstruction
  {
    byte resultRegister;

    public LLI_CallExternFunction_ResultToRegister(byte resultRegister) : base(1 + 1)
    {
      this.resultRegister = resultRegister;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_CALL_EXTERNAL__RESULT_TO_REGISTER);
      byteCode.Add(resultRegister);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_CALL_EXTERNAL__RESULT_TO_REGISTER} r:{resultRegister}";
  }

  public class LLI_CmpNotEq_ImmRegister : LLInstruction
  {
    byte register;
    byte[] imm;

    public LLI_CmpNotEq_ImmRegister(byte[] imm, byte registerIndex) : base(1 + 1 + 8)
    {
      if (imm.Length != 8)
        throw new Exception("Internal Compiler Error!");

      this.imm = imm;
      this.register = registerIndex;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_CMP_NEQ_IMM_REGISTER);
      byteCode.Add(register);
      byteCode.AddRange(imm);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_CMP_NEQ_IMM_REGISTER} r:{register}, {imm.ElementsToString("X2")}";
  }

  public class LLI_JumpIfTrue_Imm : LLInstruction
  {
    LLI_Label_PseudoInstruction label;

    public LLI_JumpIfTrue_Imm(LLI_Label_PseudoInstruction label) : base(1 + 8)
    {
      this.label = label;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_JUMP_CMP_TRUE_RELATIVE_IMM);
      byteCode.AddRange(BitConverter.GetBytes((long)label.position - (long)(this.position + this.bytecodeSize)));
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_JUMP_CMP_TRUE_RELATIVE_IMM} label_0x{label.GetHashCode():X}_at_0x{label.position:X}";
  }

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
      byteCodeState.instructions.Add(new LLI_JmpToLabel(label));
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
        if (parameter.value.position.inRegister)
          byteCodeState.registers[parameter.value.position.registerIndex] = parameter.value;

      foreach (var param in function.parameters)
        byteCodeState.instructions.Add(new LLI_Location_PseudoInstruction(param.value, function.minStackSize, byteCodeState));

      byteCodeState.instructions.Add(new LLI_StackIncrementImm(function.minStackSize, -(long)function.callStackSize));
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
        if (parameter.value.position.inRegister)
          byteCodeState.registers[parameter.value.position.registerIndex] = parameter.value;

      byteCodeState.instructions.Add(new LLI_StackDecrementImm(function.minStackSize, 0));

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

  public class CInstruction_SetArray : CInstruction
  {
    private readonly CValue value;
    private readonly byte[] data;
    private readonly SharedValue<long> stackSize;

    public CInstruction_SetArray(CValue value, byte[] data, string file, int line, SharedValue<long> stackSize) : base(file, line)
    {
      this.data = data;
      this.value = value;
      this.stackSize = stackSize;

      if (value.hasPosition && value.position.inRegister)
        throw new Exception("Internal Compiler Error.");

      if (!value.hasPosition)
      {
        value.hasPosition = true;
        value.position.inRegister = false;
        value.position.stackOffsetForward = stackSize.Value;

        if (value is CNamedValue)
        {
          var t = value as CNamedValue;

          t.hasStackOffset = true;
          t.homeStackOffset = value.position.stackOffsetForward;
          t.modifiedSinceLastHome = false;
        }

        stackSize.Value += value.type.GetSize();
      }
    }

    public override void GetLLInstructions(ref ByteCodeState byteCodeState)
    {
      long dataSizeRemaining = data.LongLength;
      int stackPtrRegister = byteCodeState.GetFreeIntegerRegister(stackSize);
      byteCodeState.registers[stackPtrRegister] = new CValue(file, line, new PtrCType(BuiltInCType.Types["u64"]), true, true) { remainingReferences = 1, lastTouchedInstructionCount = byteCodeState.instructions.Count };

      byteCodeState.instructions.Add(new LLI_LoadEffectiveAddress_StackOffsetToRegister(stackSize, value.position.stackOffsetForward, stackPtrRegister));
      
      int valueRegister = byteCodeState.GetFreeIntegerRegister(stackSize);
      byteCodeState.registers[stackPtrRegister] = new CValue(file, line, BuiltInCType.Types["u64"], true, true) { remainingReferences = 1, lastTouchedInstructionCount = byteCodeState.instructions.Count };

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
        Compiler.Error($"Type Mismatch: '{sourceValue}' cannot be assigned to '{targetValue}', because there is no implicit conversion available.", file, line);
    }

    public override void GetLLInstructions(ref ByteCodeState byteCodeState)
    {
      if (sourceValue == targetValue)
        return;

      if (!(sourceValue is CConstIntValue || sourceValue is CConstFloatValue))
        if (!sourceValue.hasPosition)
          throw new Exception($"Internal Compiler Error: Source Value {sourceValue} has no position.");

      if (!targetValue.hasPosition)
      {
        targetValue.hasPosition = true;

        if (!(targetValue.type is ArrayCType) && targetValue.type.GetSize() <= 8)
        {
          // Move to register.
          targetValue.position.inRegister = true;
          targetValue.position.registerIndex = targetValue.type is BuiltInCType && (targetValue.type as BuiltInCType).IsFloat() ? byteCodeState.GetFreeFloatRegister(stackSize) : byteCodeState.GetFreeIntegerRegister(stackSize);

          byteCodeState.registers[targetValue.position.registerIndex] = targetValue;
        }
        else
        {
          // Place on stack.
          targetValue.position.inRegister = false;
          targetValue.position.stackOffsetForward = stackSize.Value;

          if (targetValue is CNamedValue)
          {
            var t = targetValue as CNamedValue;

            t.hasStackOffset = true;
            t.homeStackOffset = targetValue.position.stackOffsetForward;
            t.modifiedSinceLastHome = false;
          }

          stackSize.Value += targetValue.type.GetSize();
        }
      }

      byteCodeState.CopyValueToPosition(sourceValue, targetValue.position, stackSize);
      byteCodeState.instructions.Add(new LLI_Location_PseudoInstruction(targetValue, stackSize, byteCodeState));

      if (targetValue.position.inRegister && targetValue is CNamedValue)
      {
        var t = targetValue as CNamedValue;

        if (t.hasStackOffset)
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
        Compiler.Error($"Cannot assign value '{sourceValue}' to non-pointer value '{sourceValue}' when dereference-assigning.", file, line);

      this.targetValuePtr = targetValuePtr;
      this.sourceValue = sourceValue;
      this.stackSize = stackSize;

      if (!sourceValue.type.CanImplicitCastTo((targetValuePtr.type as PtrCType).pointsTo))
        Compiler.Error($"Type Mismatch: '{sourceValue}' cannot be assigned to '{(targetValuePtr.type as PtrCType).pointsTo}', because there is no implicit conversion available.", file, line);
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

            if (sourceValue.position.inRegister)
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
        this.returnValue = new CValue(file, line, function.returnType, true, true) { description = $"Return Value of \"{function}\"" };
      else if (!(function.returnType is VoidCType))
        throw new NotImplementedException();
      else
        this.returnValue = null;

      returnValue = this.returnValue;
    }

    public override void GetLLInstructions(ref ByteCodeState byteCodeState)
    {
      // In case it's a recursive call: backup the parameter positions.
      var originalParameters = function.parameters;
      function.ResetRegisterPositions();

      for (int i = arguments.Count - 1; i >= 0; i--)
      {
        if (function.returnType is ArrayCType || function.returnType is StructCType)
          throw new NotImplementedException();

        var targetPosition = function.parameters[i].value.position;
        var sourceValue = arguments[i];
        
        byteCodeState.CopyValueToPositionWithCast(sourceValue, targetPosition, function.parameters[i].type, stackSize);
      }

      byteCodeState.instructions.Add(new LLI_StackIncrementImm(function.callStackSize));

      if (function is CBuiltInFunction)
      {
        Position targetPosition = new Position() { inRegister = true, registerIndex = 0 };

        byteCodeState.MoveValueToPosition(new CConstIntValue((function as CBuiltInFunction).builtinFunctionIndex, BuiltInCType.Types["u8"], file, line), targetPosition, stackSize, true);

        if (!(function.returnType is VoidCType))
        {
          returnValue.hasPosition = true;
          returnValue.position.inRegister = true;
          returnValue.position.registerIndex = 0;
        }

        byteCodeState.instructions.Add(new LLI_CallBuiltInFunction_IDFromRegister_ResultToRegister(0, 0));
        
        byteCodeState.registers[0] = returnValue;

        byteCodeState.instructions.Add(new LLI_Location_PseudoInstruction(returnValue, stackSize, byteCodeState));
      }
      else
      {
        throw new NotImplementedException();
      }

      byteCodeState.instructions.Add(new LLI_StackDecrementImm(function.callStackSize));

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
        throw new Exception($"Internal Compiler Error: Attempting to call '{functionType}' which is neither a 'func' nor an 'extern_func'.");

      var function = functionType as _FuncCTypeWrapper;

      if (arguments.Count != function.parameters.Length)
        Compiler.Error($"Invalid parameter count for function '{function}'. {arguments.Count} parameters given, {function.parameters.Length} expected.", file, line);

      for (int i = 0; i < arguments.Count; i++)
      {
        if (!arguments[i].isInitialized)
          Compiler.Error($"In function call to '{function}': Argument {(i + 1)} '{arguments[i]}' for parameter of type '{function.parameters[i]}' has not been initialized yet. Defined in File '{arguments[i].file ?? "?"}', Line: {arguments[i].line}.", file, line);

        if (!arguments[i].type.CanImplicitCastTo(function.parameters[i]))
          Compiler.Error($"In function call to '{function}': Argument {(i + 1)} '{arguments[i]}' for parameter of type '{function.parameters[i]}' is of mismatching type '{arguments[i].type}' and cannot be converted implicitly. Value defined in File '{arguments[i].file ?? "?"}', Line: {arguments[i].line}.", file, line);
      }

      if (function.returnType is BuiltInCType || function.returnType is PtrCType)
        this.returnValue = new CValue(file, line, function.returnType, true, true);
      else if (!(function.returnType is VoidCType))
        throw new NotImplementedException();
      else
        this.returnValue = null;

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

        if (functionPtr.position.inRegister)
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
        if (functionPtr.position.inRegister)
        {
          if (byteCodeState.registers[functionPtr.position.registerIndex] != functionPtr)
            throw new Exception("Internal Compiler Error!");

          byteCodeState.instructions.Add(new LLI_PushRegister((byte)functionPtr.position.registerIndex));
          pushedBytes += 8;
        }
        else
        {
          byteCodeState.instructions.Add(new LLI_MovStackOffsetToStackOffset(stackSize, functionPtr.position.stackOffsetForward + pushedBytes, new SharedValue<long>(0), 0));
          byteCodeState.instructions.Add(new LLI_StackIncrementImm(8));
          pushedBytes += 8;
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
          // do nothing.
        }
        else
        {
          returnValue.hasPosition = true;
          returnValue.position.inRegister = true;
          returnValue.position.registerIndex = returnValueRegister;
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
    CNamedValue value;
    CGlobalValueReference outValue;
    SharedValue<long> stackSize;

    public CInstruction_AddressOfVariable(CNamedValue value, out CGlobalValueReference outValue, SharedValue<long> stackSize, string file, int line) : base(file, line)
    {
      this.value = value;
      this.outValue = outValue = new CGlobalValueReference(new PtrCType(value.type), file, line, false) { description = $"reference to '{value}'" };
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
        value.hasStackOffset = true;
        value.homeStackOffset = value.position.stackOffsetForward;
        value.modifiedSinceLastHome = false;
      }
      else if (value.hasPosition & value.position.inRegister)
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

  public class CInstruction_DereferencePtr : CInstruction
  {
    CValue value;
    CValue outValue;
    SharedValue<long> stackSize;

    public CInstruction_DereferencePtr(CValue value, out CValue outValue, SharedValue<long> stackSize, string file, int line) : base(file, line)
    {
      this.value = value;
      this.outValue = outValue = new CValue(file, line, ((PtrCType)(value.type)).pointsTo, true, true) { description = $"dereference of '{value}'" };
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
      this.outValue = outValue = new CValue(file, line, new PtrCType((value.type as ArrayCType).type), false, true) { description = $"reference to '{value}'[0]" };
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
        value.hasStackOffset = true;
        value.homeStackOffset = value.position.stackOffsetForward;
        value.modifiedSinceLastHome = false;
      }
      else if (value.hasPosition & value.position.inRegister)
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
      if (source.hasPosition && source.position.inRegister)
      {
        if (byteCodeState.registers[source.position.registerIndex] != source)
          throw new Exception("Internal Compiler Error: Register Index not actually referenced.");

        byteCodeState.registers[source.position.registerIndex] = target;
      }

      target.hasPosition = source.hasPosition;
      target.position = source.position;

      byteCodeState.instructions.Add(new LLI_Location_PseudoInstruction(target, stackSize, byteCodeState));

      source.remainingReferences--;

      if (source is CNamedValue)
      {
        byteCodeState.MoveValueToHome(source as CNamedValue, stackSize);
      }
      else if (source.hasPosition && source.position.inRegister && source.remainingReferences > 0)
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
      {
        byteCodeState.instructions.Add(new LLI_Comment_PseudoInstruction("Backup Register Values."));

        for (byte i = 0; i < byteCodeState.registers.Length; i++)
        {
          if (byteCodeState.registers[i] == null)
            continue;

          if (byteCodeState.registers[i] is CNamedValue)
          {
            var value = byteCodeState.registers[i] as CNamedValue;

            if (value.hasStackOffset)
            {
              if (value.modifiedSinceLastHome)
              {
                if (value.type.GetSize() == 8)
                  byteCodeState.instructions.Add(new LLI_MovRegisterToStackOffset(i, stackSize, value.homeStackOffset));
                else
                  byteCodeState.instructions.Add(new LLI_MovRegisterToStackOffset_NBytes(i, stackSize, value.homeStackOffset, (int)value.type.GetSize()));

                value.modifiedSinceLastHome = false;
              }

              value.position.inRegister = false;
              value.position.stackOffsetForward = value.homeStackOffset;
              byteCodeState.instructions.Add(new LLI_Location_PseudoInstruction(value, stackSize, byteCodeState));
            }
            else
            {
              value.homeStackOffset = stackSize.Value;
              value.hasStackOffset = true;
              value.modifiedSinceLastHome = false;
              value.hasPosition = true;
              value.position = Position.StackOffset(value.homeStackOffset);
              var size = value.type.GetSize();
              stackSize.Value += size;

              if (value.type.GetSize() == 8)
                byteCodeState.instructions.Add(new LLI_MovRegisterToStackOffset(i, stackSize, value.homeStackOffset));
              else
                byteCodeState.instructions.Add(new LLI_MovRegisterToStackOffset_NBytes(i, stackSize, value.homeStackOffset, (int)size));
              
              byteCodeState.instructions.Add(new LLI_Location_PseudoInstruction(value, stackSize, byteCodeState));
            }
          }

          byteCodeState.registers[i] = null;
        }
      }

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
      byteCodeState.instructions.Add(new LLI_Comment_PseudoInstruction("Store Register Values."));
      
      for (byte i = 0; i < byteCodeState.registers.Length; i++)
      {
        if (byteCodeState.registers[i] == null)
          continue;

        if (byteCodeState.registers[i] is CNamedValue && scope.GetVariable((byteCodeState.registers[i] as CNamedValue).name) == byteCodeState.registers[i])
        {
          var value = byteCodeState.registers[i] as CNamedValue;

          if (value.hasStackOffset)
          {
            if (value.modifiedSinceLastHome)
            {
              if (value.type.GetSize() == 8)
                byteCodeState.instructions.Add(new LLI_MovRegisterToStackOffset(i, scope.maxRequiredStackSpace, value.homeStackOffset));
              else
                byteCodeState.instructions.Add(new LLI_MovRegisterToStackOffset_NBytes(i, scope.maxRequiredStackSpace, value.homeStackOffset, (int)value.type.GetSize()));

              value.modifiedSinceLastHome = false;
            }

            value.position.inRegister = false;
            value.position.stackOffsetForward = value.homeStackOffset;
            byteCodeState.instructions.Add(new LLI_Location_PseudoInstruction(value, scope.maxRequiredStackSpace, byteCodeState));
          }
          else
          {
            value.homeStackOffset = scope.maxRequiredStackSpace.Value;
            value.hasStackOffset = true;
            value.modifiedSinceLastHome = false;
            var size = value.type.GetSize();
            scope.maxRequiredStackSpace.Value += size;

            if (value.type.GetSize() == 8)
              byteCodeState.instructions.Add(new LLI_MovRegisterToStackOffset(i, scope.maxRequiredStackSpace, value.homeStackOffset));
            else
              byteCodeState.instructions.Add(new LLI_MovRegisterToStackOffset_NBytes(i, scope.maxRequiredStackSpace, value.homeStackOffset, (int)size));

            value.position.inRegister = false;
            value.position.stackOffsetForward = value.homeStackOffset;
            byteCodeState.instructions.Add(new LLI_Location_PseudoInstruction(value, scope.maxRequiredStackSpace, byteCodeState));
          }
        }
        else
        {
          byteCodeState.registers[i].hasPosition = false;
        }

        byteCodeState.registers[i] = null;
      }
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

      this.value = value;
      this.imm = BitConverter.GetBytes(imm);
      this.stackSize = stackSize;
      this.toSelf = toSelf;

      if (toSelf)
        this.resultingValue = value;
      else
        this.resultingValue = new CValue(file, line, value.type, false, true) { description = $"({value} + {imm})" };

      resultingValue = this.resultingValue;
    }

    public CInstruction_AddImm(CValue value, ulong imm, SharedValue<long> stackSize, bool toSelf, out CValue resultingValue, string file, int line) : base(file, line)
    {
      if (value.type is BuiltInCType && (value.type as BuiltInCType).IsFloat())
        throw new Exception("Internal Compiler Error: Should not be float value.");

      this.value = value;
      this.imm = BitConverter.GetBytes(imm);
      this.stackSize = stackSize;
      this.toSelf = toSelf;

      if (toSelf)
        this.resultingValue = value;
      else
        this.resultingValue = new CValue(file, line, value.type, false, true) { description = $"({value} + {imm})" };

      resultingValue = this.resultingValue;
    }

    public CInstruction_AddImm(CValue value, double imm, SharedValue<long> stackSize, bool toSelf, out CValue resultingValue, string file, int line) : base(file, line)
    {
      if (!(value.type is BuiltInCType) || !(value.type as BuiltInCType).IsFloat())
        throw new Exception("Internal Compiler Error: Should be float value.");

      this.value = value;
      this.imm = BitConverter.GetBytes(imm);
      this.stackSize = stackSize;
      this.toSelf = toSelf;

      if (toSelf)
        this.resultingValue = value;
      else
        this.resultingValue = new CValue(file, line, value.type, false, true) { description = $"({value} + {imm})" };

      resultingValue = this.resultingValue;
    }

    public override void GetLLInstructions(ref ByteCodeState byteCodeState)
    {
      if (!value.isInitialized)
        Compiler.Error($"Cannot perform operator on uninitialized {value}.", file, line);

      int registerIndex = byteCodeState.MoveValueToAnyRegister(value, stackSize);

      if (!toSelf && (value is CNamedValue || value is CGlobalValueReference))
      {
        if (value is CNamedValue)
          byteCodeState.MoveValueToHome(value as CNamedValue, stackSize);
        else if (value is CGlobalValueReference)
          throw new NotImplementedException();
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
        resultingValue.position.inRegister = true;
        resultingValue.position.registerIndex = registerIndex;
      }
    }
  }

  public class CInstruction_Add : CInstruction
  {
    CValue lvalue, rvalue;
    SharedValue<long> stackSize;
    CValue resultingValue;
    bool toSelf;

    public CInstruction_Add(CValue lvalue, CValue rvalue, SharedValue<long> stackSize, bool toSelf, out CValue resultingValue, string file, int line) : base(file, line)
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
        this.resultingValue = new CValue(file, line, lvalue.type.GetSize() >= rvalue.type.GetSize() ? lvalue.type : rvalue.type, false, true) { description = $"({lvalue} + {rvalue})" };

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
          byteCodeState.instructions.Add(new LLI_AddImm(lvalueRegisterIndex, BitConverter.GetBytes((rvalue as CConstIntValue).uvalue)));
        }
        else if (rvalue is CConstFloatValue)
        {
          byteCodeState.instructions.Add(new LLI_AddImm(lvalueRegisterIndex, BitConverter.GetBytes((rvalue as CConstFloatValue).value)));
        }
        else
        {
          int rvalueRegisterIndex = byteCodeState.MoveValueToAnyRegister(rvalue, stackSize);

          byteCodeState.instructions.Add(new LLI_AddRegister(lvalueRegisterIndex, rvalueRegisterIndex));
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
          resultingValue.position.inRegister = true;
          resultingValue.position.registerIndex = lvalueRegisterIndex;
        }
      }
    }
  }

  public class CInstruction_Subtract : CInstruction
  {
    CValue lvalue, rvalue;
    SharedValue<long> stackSize;
    CValue resultingValue;
    bool toSelf;

    public CInstruction_Subtract(CValue lvalue, CValue rvalue, SharedValue<long> stackSize, bool toSelf, out CValue resultingValue, string file, int line) : base(file, line)
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
        this.resultingValue = new CValue(file, line, lvalue.type.GetSize() >= rvalue.type.GetSize() ? lvalue.type : rvalue.type, false, true) { description = $"({lvalue} - {rvalue})" };

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
        int rvalueRegisterIndex = byteCodeState.MoveValueToAnyRegister(rvalue, stackSize);

        if (!toSelf && (lvalue is CNamedValue || lvalue is CGlobalValueReference))
        {
          if (lvalue is CNamedValue)
            byteCodeState.MoveValueToHome(lvalue as CNamedValue, stackSize);
          else if (lvalue is CGlobalValueReference)
            throw new NotImplementedException();
        }

        byteCodeState.instructions.Add(new LLI_NegateRegister(rvalueRegisterIndex));
        byteCodeState.instructions.Add(new LLI_AddRegister(lvalueRegisterIndex, rvalueRegisterIndex));

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
          resultingValue.position.inRegister = true;
          resultingValue.position.registerIndex = lvalueRegisterIndex;
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
        this.resultingValue = new CValue(file, line, lvalue.type.GetSize() >= rvalue.type.GetSize() ? lvalue.type : rvalue.type, false, true) { description = $"({lvalue} * {rvalue})" };

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
          resultingValue.position.inRegister = true;
          resultingValue.position.registerIndex = lvalueRegisterIndex;
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
        this.resultingValue = new CValue(file, line, lvalue.type.GetSize() >= rvalue.type.GetSize() ? lvalue.type : rvalue.type, false, true) { description = $"({lvalue} / {rvalue})" };

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
          resultingValue.position.inRegister = true;
          resultingValue.position.registerIndex = lvalueRegisterIndex;
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
        this.resultingValue = new CValue(file, line, lvalue.type.GetSize() >= rvalue.type.GetSize() ? lvalue.type : rvalue.type, false, true) { description = $"({lvalue} % {rvalue})" };

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
          resultingValue.position.inRegister = true;
          resultingValue.position.registerIndex = lvalueRegisterIndex;
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
        this.resultingValue = new CValue(file, line, lvalue.type.GetSize() >= rvalue.type.GetSize() ? lvalue.type : rvalue.type, false, true) { description = $"({lvalue} & {rvalue})" };

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
          resultingValue.position.inRegister = true;
          resultingValue.position.registerIndex = lvalueRegisterIndex;
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
        this.resultingValue = new CValue(file, line, lvalue.type.GetSize() >= rvalue.type.GetSize() ? lvalue.type : rvalue.type, false, true) { description = $"({lvalue} {operatorChars} {rvalue})" };

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
          resultingValue.position.inRegister = true;
          resultingValue.position.registerIndex = lvalueRegisterIndex;
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
    public CInstruction_BitShiftLeft(CValue lvalue, CValue rvalue, SharedValue<long> stackSize, bool toSelf, out CValue resultingValue, string file, int line) : base(lvalue, rvalue, stackSize, toSelf, out resultingValue, file, line, "<<", (int lval, int rval) => new LLI_BitShiftLeftRegister(lval, rval))
    {
    }
  }

  public class CInstruction_BitShiftRight : CInstruction_IntOnIntRegister
  {
    public CInstruction_BitShiftRight(CValue lvalue, CValue rvalue, SharedValue<long> stackSize, bool toSelf, out CValue resultingValue, string file, int line) : base(lvalue, rvalue, stackSize, toSelf, out resultingValue, file, line, ">>", (int lval, int rval) => new LLI_BitShiftRightRegister(lval, rval))
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

      this.resultingValue = new CValue(file, line, BuiltInCType.Types["u8"], false, true) { description = $"({lvalue} == {rvalue})" };

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
        resultingValue.position.inRegister = true;
        resultingValue.position.registerIndex = lvalueRegisterIndex;
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

      this.resultingValue = new CValue(file, line, BuiltInCType.Types["u8"], false, true) { description = $"({lvalue} != {rvalue})" };

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
        resultingValue.position.inRegister = true;
        resultingValue.position.registerIndex = lvalueRegisterIndex;
      }
    }
  }

  public class Scope
  {
    public SharedValue<long> maxRequiredStackSpace = new SharedValue<long>(0);

    public readonly Scope parentScope;
    public readonly bool isFunction;
    public readonly CFunction self;
    public int stackSpaceAllocationByteCodeIndex;
    public bool isConditional = false;

    private Dictionary<string, StructCType> definedStructs;
    private Dictionary<string, CFunction> definedFunctions;
    private Dictionary<string, CNamedValue> definedVariables;

    public LLI_Label_PseudoInstruction continueLabel, breakLabel, afterLabel;

    public List<CInstruction> instructions = new List<CInstruction>();

    /// <summary>
    /// Create Global Scope.
    /// </summary>
    public Scope()
    {
      parentScope = null;
      isFunction = false;
      self = null;
    }

    protected Scope(Scope parentScope, CFunction function)
    {
      this.parentScope = parentScope;
      this.isFunction = true;
      this.self = function;
      self.scope = this;
    }

    protected Scope(Scope parentScope)
    {
      this.parentScope = parentScope;
      this.isFunction = false;
      this.maxRequiredStackSpace = parentScope.maxRequiredStackSpace;
    }

    public void AddStruct(StructCType type)
    {
      if (definedStructs == null)
        definedStructs = new Dictionary<string, StructCType>();

      definedStructs.Add(type.name, type);
    }

    public void AddFunction(CFunction function)
    {
      if (definedFunctions == null)
        definedFunctions = new Dictionary<string, CFunction>();

      definedFunctions.Add(function.name, function);
    }

    public void AddVariable(CNamedValue value)
    {
      if (definedVariables == null)
        definedVariables = new Dictionary<string, CNamedValue>();

      if (value.type is VoidCType)
        Compiler.Error($"Identifier '{value.name}' cannot be of type '{value.type}'.", value.file, value.line);

      definedVariables.Add(value.name, value);
    }

    public StructCType GetStruct(string name)
    {
      if (definedStructs != null && definedStructs.ContainsKey(name))
        return definedStructs[name];

      if (parentScope != null)
        return parentScope.GetStruct(name);

      return null;
    }

    public CFunction GetFunction(string name)
    {
      if (definedFunctions != null && definedFunctions.ContainsKey(name))
        return definedFunctions[name];

      if (parentScope != null)
        return parentScope.GetFunction(name);

      return null;
    }

    public CNamedValue GetVariable(string name)
    {
      if (definedVariables != null && definedVariables.ContainsKey(name))
        return definedVariables[name];

      if (parentScope != null)
        return parentScope.GetVariable(name);

      return null;
    }

    public IEnumerable<CFunction> GetLocalFunctions()
    {
      return this.definedFunctions == null ? (IEnumerable<CFunction>)new CFunction[0] : this.definedFunctions.Values;
    }

    public Scope GetChildScopeForFunction(CFunction function)
    {
      return new Scope(this, function);
    }

    public Scope GetChildScopeForConditional(LLI_Label_PseudoInstruction afterLabel)
    {
      return new Scope(this) { afterLabel = afterLabel };
    }
  }

  static class Compiler
  {
    static bool WarningsAsErrors = false;
    static bool ShowWarnings = true;

    public static int OptimizationLevel { get; private set; } = 1;
    public static bool EmitAsm { get; private set; } = false;

    public static bool DetailedIntermediateOutput { get; private set; } = false;

    public static readonly int IntegerRegisters = 8;
    public static readonly int FloatRegisters = 8;

    public static void Error(string error, string file, int line)
    {
      Console.ForegroundColor = ConsoleColor.Red;

      Console.Write("\nError");

      if (file != null)
        Console.Write($" (in '{file}', Line {line + 1})");

      Console.WriteLine(":\n\t" + error + "\n");
      Console.ResetColor();

      throw new CompileFailureException();
    }

    public static void Warn(string warning, string file, int line)
    {
      if (ShowWarnings)
      {
        Console.ForegroundColor = ConsoleColor.Yellow;

        Console.Write("\nWarning");

        if (file != null)
          Console.Write($" (in '{file}', Line {line + 1})");

        Console.WriteLine(":\n\t" + warning + "\n");
        Console.ResetColor();
      }

      if (WarningsAsErrors)
        Error($"Warning treated as error: '{warning}'", file, line);
    }

    [STAThread]
    static void Main(string[] args)
    {
      Console.WriteLine("llsc - LLS Bytecode Compiler\n");

      string outFileName = "bytecode.lls";
      IEnumerable<FileContents> files = null;
      var globalScope = new Scope();
      var byteCodeState = new ByteCodeState();

      try
      {
        foreach (var argument in (from arg in args where arg.StartsWith("-") select arg))
        {
          switch (argument)
          {
            case "-NoWarn":
              ShowWarnings = false;
              break;

            case "-FatalWarnings":
              WarningsAsErrors = true;
              break;

            case "-O0":
              OptimizationLevel = 0;
              break;

            case "-S":
              EmitAsm = true;
              break;

            case "-S+":
              EmitAsm = true;
              DetailedIntermediateOutput = true;
              break;

            default:
              if (argument.StartsWith("-o="))
                outFileName = argument.Substring(3).Trim('\'', '\"');
              else
                Error($"Invalid Parameter '{argument}'.", null, 0);
              break;
          }
        }

        bool anyFilesCompiled = false;

        files = (from file in args where !file.StartsWith("-") select new FileContents() { filename = file, lines = File.ReadAllLines(file) });
        var allNodes = new List<Node>();

        foreach (var file in files)
        {
          anyFilesCompiled = true;

          Parse(file);
          ResolveKeywords(file);

          if (file.nodes.Count == 0)
            Warn($"[P] File '{file.filename}' didn't produce any parsed nodes.", file.filename, 0);
          else if (!(file.nodes.Last() is NLineEnd) && !(file.nodes.Last() is NCloseScope))
            Error($"File '{file}' doesn't end on either ';' or '}}'.", file.filename, file.nodes.Last().line);

          allNodes.AddRange(file.nodes);
        }

        if (!anyFilesCompiled)
          Error("No Files have been compiled.", null, 0);

        Console.WriteLine($"Parsing Succeeded. ({allNodes.Count} Nodes parsed from {files.Count()} Files)");

        if (!EmitAsm)
          files = null;


        CompileScope(globalScope, allNodes, ref byteCodeState);
        Console.WriteLine($"Instruction Generation Succeeded. ({byteCodeState.instructions.Count} Instructions & Pseudo-Instructions generated.)");

        byteCodeState.CompileInstructionsToBytecode();
        Console.WriteLine($"Code Generation Succeeded. ({byteCodeState.byteCode.Count} Bytes)");

        File.WriteAllBytes(outFileName, byteCodeState.byteCode.ToArray());
        Console.WriteLine($"Successfully wrote byte code to '{outFileName}'.");

        if (EmitAsm)
          WriteDisasm(outFileName, files, byteCodeState);
      }
      catch (CompileFailureException e)
      {
        try
        {
          byteCodeState.instructions.Add(new LLI_Comment_PseudoInstruction("Compilation Failed!"));

          if (EmitAsm)
            WriteDisasm(outFileName, files, byteCodeState);
        }
        catch { }

        Console.WriteLine("Compilation Failed.");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("\n" + e.ToString());
        Console.ResetColor();

        Environment.Exit(1);
      }
      catch (Exception e)
      {
        try
        {
          byteCodeState.instructions.Add(new LLI_Comment_PseudoInstruction("Compilation Failed!"));

          if (EmitAsm)
            WriteDisasm(outFileName, files, byteCodeState);
        }
        catch { }

        Console.WriteLine("Internal Compiler Error.");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("\n" + e.ToString());

        Environment.Exit(-1);
      }

      Console.WriteLine("\nCompilation Succeeded.");
    }

    private static void WriteDisasm(string outFileName, IEnumerable<FileContents> files, ByteCodeState byteCodeState)
    {
      string lastFile = null;
      int lastLine = -1;

      List<string> lines = new List<string>();

      lines.Add("# llsc bytecode disassembly output");
      lines.Add("# ================================");

      foreach (var instruction in byteCodeState.instructions)
      {
        if (instruction.bytecodeSize == 0 && !(instruction is LLI_PseudoInstruction))
          continue;

        bool printLine = false;
        bool printedFile = false;

        if (lastFile != instruction.file)
        {
          printLine = true;
          printedFile = true;
          lastFile = instruction.file;
          lastLine = -1;
          lines.Add("\r\n# File: " + (lastFile == null ? "<compiler internal>" : lastFile));
        }

        if (lastLine != instruction.line)
        {
          printLine = true;
          lastLine = instruction.line;
        }

        if (printLine)
        {
          var file = (from x in files where x.filename == lastFile select x.lines).FirstOrDefault();

          if (file != null && file.Length > lastLine && lastLine >= 0)
            lines.Add((printedFile ? "" : "\r\n") + $"# Line {lastLine + 1:   0}: {file[lastLine]}");
        }

        lines.Add($"{instruction.ToString().PadRight(90)} # 0x{instruction.position:X}");
      }

      File.WriteAllLines(outFileName + ".asm", lines);
    }

    private static void Parse(FileContents file)
    {
      try
      {
        List<Node> nodes = file.nodes;

        bool inMultilineComment = false;

        for (int line = 0; line < file.lines.Length; line++)
        {
          try
          {
            var lineString = file.lines[line];
            int start = 0;

            if (inMultilineComment)
            {
              if (lineString.FindString("*/", out start))
              {
                inMultilineComment = false;
                start += 2;
              }
              else
              {
                continue;
              }
            }

            while (start < lineString.Length)
            {
              if (lineString[start] == ' ')
              {
                start++;
              }
              else if (lineString.NextIs(start, "//"))
              {
                break;
              }
              else if (lineString.NextIs(start, "/*"))
              {
                start += 2;

                if (lineString.Substring(start).FindString("*/", out int nextStart))
                {
                  inMultilineComment = false;
                  start += 2 + nextStart;
                }
                else
                {
                  inMultilineComment = true;
                  break;
                }
              }
              else if (lineString.NextIs(start, "\""))
              {
                start++;

                bool endFound = false;

                for (int i = start; i < lineString.Length; i++)
                {
                  if (lineString[i] == '\"' && (i == start || lineString[i - 1] != '#'))
                  {
                    nodes.Add(new NStringValue(lineString.Substring(start, i - start), file.filename, line));
                    start = i + 1;
                    endFound = true;
                  }
                }

                if (!endFound)
                  Error($"Missing end of string '{lineString.Substring(start)}'", file.filename, line);
              }
              else if (lineString[start] == '.')
              {
                nodes.Add(new NAttributeOperator(file.filename, line));
                start++;
              }
              else if (lineString.NextIs(start, "->"))
              {
                nodes.Add(new NDereferenceAttributeOperator(file.filename, line));
                start += 2;
              }
              else if (lineString.NextIs(start, "==") || lineString.NextIs(start, "!=") || lineString.NextIs(start, "<=") || lineString.NextIs(start, ">=") || lineString.NextIs(start, "++") || lineString.NextIs(start, "--") || lineString.NextIs(start, "+=") || lineString.NextIs(start, "-=") || lineString.NextIs(start, "*=") || lineString.NextIs(start, "/=") || lineString.NextIs(start, "|=") || lineString.NextIs(start, "&=") || lineString.NextIs(start, "^=") || lineString.NextIs(start, "&&") || lineString.NextIs(start, "||"))
              {
                nodes.Add(new NOperator(lineString.Substring(start, 2), file.filename, line));
                start += 2;
              }
              else if (lineString[start] == '=' || lineString[start] == '!' || lineString[start] == '<' || lineString[start] == '>' || lineString[start] == '+' || lineString[start] == '-' || lineString[start] == '*' || lineString[start] == '/' || lineString[start] == '%' || lineString[start] == '&' || lineString[start] == '|' || lineString[start] == '^' || lineString[start] == '~')
              {
                nodes.Add(new NOperator(lineString.Substring(start, 1), file.filename, line));
                start++;
              }
              else if (lineString[start] == ',')
              {
                nodes.Add(new NComma(file.filename, line));
                start++;
              }
              else if (lineString[start] == '{')
              {
                nodes.Add(new NOpenScope(file.filename, line));
                start++;
              }
              else if (lineString[start] == '}')
              {
                nodes.Add(new NCloseScope(file.filename, line));
                start++;
              }
              else if (lineString[start] == '[')
              {
                nodes.Add(new NOpenBracket(file.filename, line));
                start++;
              }
              else if (lineString[start] == ']')
              {
                nodes.Add(new NCloseBracket(file.filename, line));
                start++;
              }
              else if (lineString[start] == '(')
              {
                nodes.Add(new NOpenParanthesis(file.filename, line));
                start++;
              }
              else if (lineString[start] == ')')
              {
                nodes.Add(new NCloseParanthesis(file.filename, line));
                start++;
              }
              else if (lineString[start] == ';')
              {
                nodes.Add(new NLineEnd(file.filename, line));
                start++;
              }
              else // some sort of name or value.
              {
                int originalStart = start;

                for (; start < lineString.Length; start++)
                  if (" .,;=!<>()[]{}+-*/%^&|~:".Contains(lineString[start]))
                    break;

                string foundString = lineString.Substring(originalStart, start - originalStart);

                Node node = null;

                if (null != (node = NIntegerValue.GetIntegerValue(foundString, file.filename, line)))
                {
                  nodes.Add(node);
                }
                else
                {
                  nodes.Add(new NName(foundString, file.filename, line));
                }
              }
            }
          }
          catch (CompileFailureException e)
          {
            throw e;
          }
          catch (Exception e)
          {
            Console.WriteLine($"Internal Compiler Error Parsing File '{file.filename}', Line {line + 1}.");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("\n" + e.ToString());

            Environment.Exit(-1);
          }
        }

        if (inMultilineComment)
          Warn("[P] Missing end of multiline comment ('*/').", file.filename, file.lines.Length - 1);
      }
      catch (CompileFailureException e)
      {
        throw e;
      }
      catch (Exception e)
      {
        Console.WriteLine($"Internal Compiler Error Parsing File '{file.filename}'.");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("\n" + e.ToString());

        Environment.Exit(-1);
      }
    }

    private static void ResolveKeywords(FileContents file)
    {
      for (int i = 0; i < file.nodes.Count; i++)
      {
        var node = file.nodes[i];

        if (node is NName)
        {
          switch (((NName)node).name)
          {
            case "struct":
              file.nodes[i] = new NStructKeyword(node.file, node.line);
              break;

            case "function":
              file.nodes[i] = new NFunctionKeyword(node.file, node.line);
              break;

            case "ptr":
              file.nodes[i] = new NPtrKeyword(node.file, node.line);
              break;

            case "var":
              file.nodes[i] = new NVarKeyword(node.file, node.line);
              break;

            case "cast":
              file.nodes[i] = new NCastKeyword(node.file, node.line);
              break;

            case "array":
              file.nodes[i] = new NArrayKeyword(node.file, node.line);
              break;

            case "func":
              file.nodes[i] = new NFuncKeyword(node.file, node.line);
              break;

            case "extern_func":
              file.nodes[i] = new NExternFuncKeyword(node.file, node.line);
              break;

            case "float":
              file.nodes[i] = new NFloatKeyword(node.file, node.line);
              break;

            case "if":
              file.nodes[i] = new NIfKeyword(node.file, node.line);
              break;

            case "else":
              file.nodes[i] = new NElseKeyword(node.file, node.line);
              break;

            case "while":
              file.nodes[i] = new NWhileKeyword(node.file, node.line);
              break;

            case "sizeof":
            case "countof":
            case "addressof":
            case "valueof":
            case "offsetof":
            case "__from_register":
            case "__to_register":
            case "__exit":
              file.nodes[i] = new NPseudoFunction(((NName)node).name, node.file, node.line);
              break;

            case "u64":
            case "i64":
            case "f64":
            case "u32":
            case "i32":
            case "f32":
            case "u16":
            case "i16":
            case "u8":
            case "i8":
              file.nodes[i] = new NType(BuiltInCType.Types[((NName)node).name], node.file, node.line);
              break;

            case "void":
              file.nodes[i] = new NType(VoidCType.Instance, node.file, node.line);
              break;

            case "text":
              if (file.nodes.NextIs(i + 1, typeof(NName), typeof(NOperator)) && (file.nodes[i + 2] as NOperator).operatorType == "=")
              {
                var originalNode = file.nodes[i];
                file.nodes[i] = new NArrayKeyword(originalNode.file, originalNode.line);
                file.nodes.Insert(i + 1, new NOperator("<", originalNode.file, originalNode.line));
                file.nodes.Insert(i + 2, new NType(BuiltInCType.Types["i8"], originalNode.file, originalNode.line));
                file.nodes.Insert(i + 3, new NOperator(">", originalNode.file, originalNode.line));
              }
              else
              {
                file.nodes[i] = new NType(new PtrCType(BuiltInCType.Types["i8"]), node.file, node.line);
              }
              break;

            case "voidptr":
              file.nodes[i] = new NType(new PtrCType(VoidCType.Instance), node.file, node.line);
              break;
          }
        }
      }
    }

    private static void PatchTypes(Scope scope, ref List<Node> nodes)
    {
      for (int i = nodes.Count - 1; i >= 0; i--)
      {
        // Ptr.
        if (nodes.NextIs(i, typeof(NPtrKeyword), typeof(NOperator), typeof(NType), typeof(NOperator)) && ((nodes[i + 1] as NOperator).operatorType == "<" && (nodes[i + 3] as NOperator).operatorType == ">"))
        {
          var start = nodes[i];
          NType type = nodes[i + 2] as NType;

          nodes.RemoveRange(i, 4);
          nodes.Insert(i, new NType(new PtrCType(type.type), start.file, start.line));
        }
        // Sized Array.
        else if (nodes.NextIs(i, typeof(NArrayKeyword), typeof(NOperator), typeof(NType), typeof(NComma), typeof(NIntegerValue), typeof(NOperator)) && ((nodes[i + 1] as NOperator).operatorType == "<" && (nodes[i + 5] as NOperator).operatorType == ">"))
        {
          if ((nodes[i + 4] as NIntegerValue).isForcefullyNegative || (nodes[i + 4] as NIntegerValue).uint_value == 0)
            Error($"Size of Array can not be less than 1. Given: '{(nodes[i + 4] as NIntegerValue).int_value}'.", nodes[i + 4].file, nodes[i + 4].line);

          var start = nodes[i];
          var type = nodes[i + 2] as NType;
          var size = nodes[i + 4] as NIntegerValue;

          nodes.RemoveRange(i, 6);
          nodes.Insert(i, new NType(new ArrayCType(type.type, (long)size.uint_value), start.file, start.line));
        }
        // extern_func / func.
        else if ((nodes[i] is NFuncKeyword || nodes[i] is NExternFuncKeyword) && nodes.NextIs(i + 1, typeof(NOperator), typeof(NType), typeof(NOpenParanthesis)) && (nodes[i + 1] as NOperator).operatorType == "<")
        {
          var funcTypeNode = nodes[i];
          var returnType = (nodes[i + 2] as NType).type;

          int j = i + 4;
          bool aborted = false;
          List<CType> parameters = new List<CType>();

          while (true)
          {
            if (nodes.Count <= j)
            {
              Error($"Unexpected end of function type definition.", funcTypeNode.file, funcTypeNode.line);
            }
            else if (nodes[j] is NOperator && (nodes[j] as NOperator).operatorType == ">")
            {
              j++;
              break;
            }
            else if (nodes[j] is NType)
            {
              parameters.Add((nodes[j] as NType).type);
              j++;

              if (nodes.Count <= j)
              {
                Error($"Unexpected end of function type definition.", funcTypeNode.file, funcTypeNode.line);
              }
              else if (nodes[j] is NComma)
              {
                j++;
              }
              else if (nodes[j] is NCloseParanthesis)
              {
                j++;
                break;
              }
              else
              {
                Error($"Unexpected '{nodes[j]}' in function type definition.", funcTypeNode.file, funcTypeNode.line);
              }
            }
            else if (nodes[j] is NName)
            {
              aborted = true;
              break;
            }
            else
            {
              Error($"Unexpected '{nodes[j]}' in function type definition.", funcTypeNode.file, funcTypeNode.line);
            }
          }

          if (nodes.Count <= j)
          {
            Error($"Unexpected end of function type definition.", funcTypeNode.file, funcTypeNode.line);
          }
          else if (nodes[j] is NOperator && (nodes[j] as NOperator).operatorType == ">")
          {
            j++;
          }
          else
          {
            Error($"Unexpected '{nodes[j]}' in function type definition.", funcTypeNode.file, funcTypeNode.line);
          }

          if (!aborted)
          {
            nodes.RemoveRange(i, j - i);

            if (funcTypeNode is NFuncKeyword)
              nodes.Insert(i, new NType(new FuncCType(returnType, parameters), funcTypeNode.file, funcTypeNode.line));
            else if (funcTypeNode is NExternFuncKeyword)
              nodes.Insert(i, new NType(new ExternFuncCType(returnType, parameters), funcTypeNode.file, funcTypeNode.line));
            else
              throw new Exception("Internal Compiler Error");
          }
        }
        else if (nodes.NextIs(typeof(NFloatKeyword), typeof(NOpenParanthesis), typeof(NStringValue), typeof(NCloseParanthesis)))
        {
          var start = nodes[i];
          var value = nodes[i + 2];
          double floatValue = 0;

          if (!double.TryParse((value as NStringValue).value, out floatValue))
            Error($"Invalid floating point value '{(value as NStringValue).value}'.", value.file, value.line);

          nodes.RemoveRange(i, 4);
          nodes.Insert(i, new NFloatingPointValue(floatValue, start.file, start.line));
        }
      }
    }

    private static void CompileScope(Scope scope, List<Node> nodes, ref ByteCodeState byteCodeState)
    {
      if (scope.isFunction)
      {
        scope.maxRequiredStackSpace = scope.self.minStackSize;

        foreach (var parameter in scope.self.parameters)
          scope.AddVariable(parameter.value);

        scope.instructions.Add(new CInstruction_BeginFunction(scope.self));
      }
      else if (scope.parentScope == null) // if it's the global scope.
      {
        if (nodes.Count == 0)
          throw new Exception("Internal Compiler Error: The Global Scope does not contain any nodes, but CompileScope was called on it.");

        PatchTypes(scope, ref nodes);

        // Add Builtin Functions.
        foreach (var function in CBuiltInFunction.Functions)
          scope.AddFunction(function);

        scope.instructions.Add(new CInstruction_BeginGlobalScope(scope.maxRequiredStackSpace, nodes[0].file, nodes[0].line));
      }

      while (nodes.Count > 0)
      {
        // Floating Line End.
        if (nodes[0] is NLineEnd)
        {
          nodes.RemoveAt(0);
        }
        // Struct.
        else if (nodes.NextIs(typeof(NStructKeyword), typeof(NName), typeof(NOpenScope)))
        {
          // Parse Struct.
          NName nameNode = nodes[1] as NName;
          string name = nameNode.name;
          List<StructAttribute> attributes = new List<StructAttribute>();

          nodes.RemoveRange(0, 3);

          while (true)
          {
            if (nodes.Count == 0)
            {
              Error($"Invalid Struct Definition for '{name}'.", nameNode.file, nameNode.line);
            }
            else if (nodes[0] is NType && nodes.Count > 1 && nodes[1] is NName)
            {
              CType type = (nodes[0] as NType).type;
              nodes.RemoveAt(0);

              while (nodes[0] is NName)
              {
                attributes.Add(new StructAttribute((nodes[0] as NName).name, type, nodes[0].file, nodes[0].line));

                if (nodes.Count == 1)
                {
                  Error($"Invalid Struct Definition for '{name}'.", nameNode.file, nameNode.line);
                }
                else if (nodes[1] is NComma)
                {
                  nodes.RemoveRange(0, 2);
                }
                else if (nodes[1] is NLineEnd)
                {
                  nodes.RemoveRange(0, 2);
                  break;
                }
                else
                {
                  Error($"Unexpected {nodes[1]} in struct declaration. Expected: ',' or ';'.", nodes[1].file, nodes[1].line);
                }
              }
            }
            else if (nodes[0] is NCloseScope)
            {
              nodes.RemoveAt(0);
              break;
            }
            else
            {
              Error($"Unexpected {nodes[0]} in struct declaration.", nodes[0].file, nodes[0].line);
            }
          }

          StructCType structType = new StructCType(name, attributes, nameNode.file, nameNode.line);

          // Add Struct to scope.
          scope.AddStruct(structType);

          // Patch Types.
          for (int i = 0; i < nodes.Count; i++)
            if (nodes[i] is NName && (nodes[i] as NName).name == structType.name)
              nodes[i] = new NType(structType, nodes[i].file, nodes[i].line);

          PatchTypes(scope, ref nodes);
        }
        // Function.
        else if (nodes.NextIs(typeof(NFunctionKeyword), typeof(NType), typeof(NName), typeof(NOpenParanthesis)))
        {
          var startNode = nodes[0];
          var type = (nodes[1] as NType).type;
          var nameNode = nodes[2] as NName;

          nodes.RemoveRange(0, 4);

          // Function already defined?
          {
            CFunction existingFunction = scope.GetFunction(nameNode.name);

            if (null != existingFunction)
              Error($"Duplicate function definition for identifier '{nameNode.name}'. A function with the same name has already been defined in File '{existingFunction.file}', Line {existingFunction.line}: {existingFunction}", nameNode.file, nameNode.line);
          }

          var parameters = new List<FunctionParameter>();

          // Get Parameters.
          while (true)
          {
            if (nodes.Count == 0)
            {
              Error("Unexpected end of function definition.", startNode.file, startNode.line);
            }
            else if (nodes.NextIs(typeof(NType), typeof(NName)))
            {
              parameters.Add(new FunctionParameter((nodes[0] as NType).type, (nodes[1] as NName).name, nodes[1].file, nodes[1].line));
              nodes.RemoveRange(0, 2);

              if (nodes.Count == 0)
              {
                Error("Unexpected end of function definition.", startNode.file, startNode.line);
              }
              else if (nodes[0] is NComma)
              {
                nodes.RemoveAt(0);
              }
              else if (nodes[0] is NCloseParanthesis)
              {
                nodes.RemoveAt(0);
                break;
              }
              else
              {
                Error($"Unexpected {nodes[0]} in function definition.", nodes[0].file, nodes[0].line);
              }
            }
            else if (nodes[0] is NCloseParanthesis)
            {
              nodes.RemoveAt(0);
              break;
            }
            else
            {
              Error($"Unexpected {nodes[0]} in function definition.", nodes[0].file, nodes[0].line);
            }
          }

          // Add Function to scope.
          var function = new CFunction(nameNode.name, type, parameters, startNode.file, startNode.line);
          scope.AddFunction(function);

          if (nodes.Count < 2)
            Error("Unexpected end of function.", startNode.file, startNode.line);
          else if (!(nodes[0] is NOpenScope))
            Error($"Unexpected {nodes[0]}. Expected function definition.", nodes[0].file, nodes[0].line);

          nodes.RemoveAt(0);

          // Find End.
          int openScopes = 1;
          int endIndex = 0;

          for (; endIndex < nodes.Count; endIndex++)
          {
            if (nodes[endIndex] is NOpenScope)
            {
              openScopes++;
            }
            else if (nodes[endIndex] is NCloseScope)
            {
              openScopes--;

              if (openScopes == 0)
                break;
            }
          }

          if (openScopes != 0)
            Error($"Missing end of function at File '{startNode.file}', Line {startNode.line}. Expected '}}'.", nodes.Last().file, nodes.Last().line);

          // Extract Nodes.
          var functionNodes = nodes.GetRange(0, endIndex);
          nodes.RemoveRange(0, endIndex + 1);

          function.nodes = functionNodes;
        }
        // Dynamic Array.
        else if (nodes.NextIs(typeof(NArrayKeyword), typeof(NOperator), typeof(NType), typeof(NOperator), typeof(NName), typeof(NOperator)) && ((nodes[1] as NOperator).operatorType == "<" && (nodes[3] as NOperator).operatorType == ">" && (nodes[5] as NOperator).operatorType == "="))
        {
          if (!((nodes[2] as NType).type is BuiltInCType))
            Error($"Invalid Type '{(nodes[2] as NType).type.ToString()}'. Dynamically Sized Arrays can only contain builtin types.", nodes[2].file, nodes[2].line);

          var builtinType = (nodes[2] as NType).type as BuiltInCType;

          if (builtinType.type == BuiltInTypes.i8 && nodes.Count > 6 && nodes[6] is NStringValue && nodes[7] is NLineEnd)
          {
            var stringValue = nodes[6] as NStringValue;
            var arrayType = new ArrayCType(builtinType, stringValue.length);
            var value = new CNamedValue(nodes[4] as NName, arrayType, false, true);

            scope.AddVariable(value);
            scope.instructions.Add(new CInstruction_SetArray(value, stringValue.bytes, nodes[0].file, nodes[0].line, scope.maxRequiredStackSpace));
            nodes.RemoveRange(0, 8);
          }
          else if (nodes.Count > 6 && nodes[6] is NOpenScope)
          {
            long valueCount = 0;
            List<byte> data = new List<byte>();

            var startNode = nodes[0];
            var nameNode = (nodes[4] as NName);

            nodes.RemoveRange(0, 7);

            while (true)
            {
              if (nodes.Count == 0)
              {
                Error("Unexpected end of array definition.", startNode.file, startNode.line);
              }
              else if (nodes[0] is NIntegerValue)
              {
                valueCount++;
                data.AddRange(builtinType.GetAsBytes(nodes[0] as NIntegerValue));
                nodes.RemoveAt(0);

                if (nodes.Count == 0)
                {
                  Error("Unexpected end of array definition.", startNode.file, startNode.line);
                }
                else if (nodes.NextIs(typeof(NCloseScope), typeof(NLineEnd)))
                {
                  nodes.RemoveRange(0, 2);
                  break;
                }
                else if (nodes.NextIs(typeof(NComma)))
                {
                  nodes.RemoveAt(0);
                  break;
                }
                else
                {
                  Error($"Unexpected {nodes[0]} in array definition.", nodes[0].file, nodes[0].line);
                }
              }
              else if (nodes.NextIs(typeof(NCloseScope), typeof(NLineEnd)))
              {
                nodes.RemoveRange(0, 2);
                break;
              }
              else
              {
                Error($"Unexpected {nodes[0]} in array definition.", nodes[0].file, nodes[0].line);
              }
            }

            var arrayType = new ArrayCType(builtinType, valueCount);
            var value = new CNamedValue(nameNode, arrayType, false, true);
            value.homeStackOffset = scope.maxRequiredStackSpace.Value;
            value.hasStackOffset = true;
            scope.maxRequiredStackSpace.Value += arrayType.GetSize();

            scope.AddVariable(value);
            scope.instructions.Add(new CInstruction_SetArray(value, data.ToArray(), startNode.file, startNode.line, scope.maxRequiredStackSpace));

            value.hasPosition = true;
            value.position.inRegister = false;
            value.position.stackOffsetForward = value.homeStackOffset;
          }
        }
        else if (nodes[0] is NIfKeyword)
        {
          var ifNode = nodes[0];
          nodes.RemoveAt(0);

          if (nodes.Count < 4 || !(nodes[0] is NOpenParanthesis))
            Error($"Unexpected {(nodes.Count == 0 ? "end" : nodes[0].ToString())} in conditional expression.", ifNode.file, ifNode.line);

          int endIndex = nodes.FindNextSameScope(n => n is NCloseParanthesis);

          if (endIndex == 1)
            Error($"Missing condition in conditional expression.", nodes[1].file, nodes[1].line);

          var condition = nodes.GetRange(1, endIndex - 1);
          var value = GetRValue(scope, condition.ToList(), ref byteCodeState);
          nodes.RemoveRange(0, endIndex + 1);

          var afterIfElseLabel = new LLI_Label_PseudoInstruction();

          // Handle Conditional Block.
          {
            var nextElseLabel = new LLI_Label_PseudoInstruction();
            scope.instructions.Add(new CInstruction_IfNonZeroJumpToLabel(value, nextElseLabel, scope.maxRequiredStackSpace, true, ifNode.file, ifNode.line));

            // Inside If Block.
            {
              List<Node> scopeNodes;

              if (nodes[0] is NOpenScope)
              {
                int scopeEndIndex = nodes.FindNextSameScope(n => n is NCloseScope);
                scopeNodes = nodes.GetRange(1, scopeEndIndex - 1);
                nodes.RemoveRange(0, scopeEndIndex + 1);
              }
              else
              {
                int scopeEndIndex = nodes.FindNextSameScope(n => n is NLineEnd);
                scopeNodes = nodes.GetRange(0, scopeEndIndex + 1);
                nodes.RemoveRange(0, scopeEndIndex + 1);
              }

              Scope childScope = scope.GetChildScopeForConditional(afterIfElseLabel);

              CompileScope(childScope, scopeNodes, ref byteCodeState);
            }

            scope.instructions.Add(new CInstruction_Label(nextElseLabel, ifNode.file, ifNode.line));
          }

          // Handle Else and Else-If blocks.
          while (true)
          {
            if (nodes.Count == 0 || !(nodes[0] is NElseKeyword))
              break;

            bool isLastElse = nodes.Count >= 1 && !(nodes[1] is NIfKeyword);

            var elseNode = nodes[0];
            nodes.RemoveAt(0);

            LLI_Label_PseudoInstruction nextElseLabel = new LLI_Label_PseudoInstruction();

            if (!isLastElse)
            {
              nodes.RemoveAt(0);

              if (nodes.Count < 4 || !(nodes[0] is NOpenParanthesis))
                Error($"Unexpected {(nodes.Count == 0 ? "end" : nodes[0].ToString())} in conditional expression.", ifNode.file, ifNode.line);

              int elseIfConditionEndIndex = nodes.FindNextSameScope(n => n is NCloseParanthesis);

              if (elseIfConditionEndIndex == 1)
                Error($"Missing condition in conditional expression.", nodes[1].file, nodes[1].line);

              var elseIfCondition = nodes.GetRange(1, elseIfConditionEndIndex - 1);
              var elseIfConditionValue = GetRValue(scope, elseIfCondition.ToList(), ref byteCodeState);
              nodes.RemoveRange(0, elseIfConditionEndIndex + 1);

              scope.instructions.Add(new CInstruction_IfNonZeroJumpToLabel(elseIfConditionValue, nextElseLabel, scope.maxRequiredStackSpace, false, ifNode.file, ifNode.line));
            }

            // Inside Else Block.
            {
              List<Node> scopeNodes;

              if (nodes[0] is NOpenScope)
              {
                int scopeEndIndex = nodes.FindNextSameScope(n => n is NCloseScope);
                scopeNodes = nodes.GetRange(1, scopeEndIndex - 1);
                nodes.RemoveRange(0, scopeEndIndex + 1);
              }
              else
              {
                int scopeEndIndex = nodes.FindNextSameScope(n => n is NLineEnd);
                scopeNodes = nodes.GetRange(0, scopeEndIndex + 1);
                nodes.RemoveRange(0, scopeEndIndex + 1);
              }

              Scope childScope = scope.GetChildScopeForConditional(afterIfElseLabel);

              CompileScope(childScope, scopeNodes, ref byteCodeState);
            }

            scope.instructions.Add(new CInstruction_Label(nextElseLabel, elseNode.file, elseNode.line));

            if (isLastElse)
              break;
          }

          scope.instructions.Add(new CInstruction_Label(afterIfElseLabel, ifNode.file, ifNode.line));
        }
        else if (nodes[0] is NWhileKeyword)
        {
          var whileNode = nodes[0];
          nodes.RemoveAt(0);

          if (nodes.Count < 4 || !(nodes[0] is NOpenParanthesis))
            Error($"Unexpected {(nodes.Count == 0 ? "end" : nodes[0].ToString())} in loop condition.", whileNode.file, whileNode.line);

          int endIndex = nodes.FindNextSameScope(n => n is NCloseParanthesis);

          if (endIndex == 1)
            Error($"Missing condition in loop condition.", nodes[1].file, nodes[1].line);

          var condition = nodes.GetRange(1, endIndex - 1);
          var value = GetRValue(scope, condition.ToList(), ref byteCodeState);
          nodes.RemoveRange(0, endIndex + 1);
          
          LLI_Label_PseudoInstruction afterWhileLabel = new LLI_Label_PseudoInstruction();
          LLI_Label_PseudoInstruction beforeWhileLabel = new LLI_Label_PseudoInstruction();
          LLI_Label_PseudoInstruction beforeWhileConsecutiveLabel = new LLI_Label_PseudoInstruction();

          scope.instructions.Add(new CInstruction_IfNonZeroJumpToLabel(value, afterWhileLabel, scope.maxRequiredStackSpace, true, whileNode.file, whileNode.line));
          scope.instructions.Add(new CInstruction_Label(beforeWhileLabel, whileNode.file, whileNode.line));

          // Inside while block.
          {
            List<Node> scopeNodes;

            if (nodes[0] is NOpenScope)
            {
              int scopeEndIndex = nodes.FindNextSameScope(n => n is NCloseScope);
              scopeNodes = nodes.GetRange(1, scopeEndIndex - 1);
              nodes.RemoveRange(0, scopeEndIndex + 1);
            }
            else
            {
              int scopeEndIndex = nodes.FindNextSameScope(n => n is NLineEnd);
              scopeNodes = nodes.GetRange(0, scopeEndIndex + 1);
              nodes.RemoveRange(0, scopeEndIndex + 1);
            }

            Scope childScope = scope.GetChildScopeForConditional(null);

            childScope.continueLabel = beforeWhileConsecutiveLabel;
            childScope.breakLabel = afterWhileLabel;

            CompileScope(childScope, scopeNodes, ref byteCodeState);
          }

          scope.instructions.Add(new CInstruction_Label(beforeWhileConsecutiveLabel, whileNode.file, whileNode.line));
          scope.instructions.Add(new CInstruction_IfNonZeroJumpToLabel(GetRValue(scope, condition.ToList(), ref byteCodeState), afterWhileLabel, scope.maxRequiredStackSpace, true, whileNode.file, whileNode.line));
          scope.instructions.Add(new CInstruction_GotoLabel(beforeWhileLabel, whileNode.file, whileNode.line));
          scope.instructions.Add(new CInstruction_Label(afterWhileLabel, whileNode.file, whileNode.line));
        }
        else if (nodes.NextIs(typeof(NPseudoFunction), typeof(NOpenScope), typeof(NName), typeof(NComma), typeof(NIntegerValue), typeof(NCloseScope)) && (nodes[0] as NPseudoFunction).type == NPseudoFunctionType.ToRegister)
        {
          scope.instructions.Add(new CInstruction_CustomAction(b => { b.MoveValueToPosition(scope.GetVariable((nodes[2] as NName).name), Position.Register((int)(nodes[4] as NIntegerValue).uint_value), scope.maxRequiredStackSpace, false); }, nodes[0].file, nodes[0].line));
          nodes.RemoveRange(0, 6);
        }
        else if (nodes.NextIs(typeof(NPseudoFunction), typeof(NOpenScope), typeof(NCloseScope)) && (nodes[0] as NPseudoFunction).type == NPseudoFunctionType.Exit)
        {
          scope.instructions.Add(new CInstruction_CustomAction(b => { b.instructions.Add(new LLI_Exit()); }, nodes[0].file, nodes[0].line));
          nodes.RemoveRange(0, 3);

          if (nodes.Count != 0)
            Warn("Unreachable Code.", nodes[0].file, nodes[0].line);
        }
        else
        {
          // Find first operator before ';'.
          int firstOperator = nodes.FindNextSameScope(n => n is NOperator || n is NLineEnd);

          if (nodes[firstOperator] is NLineEnd)
            firstOperator = -1;

          int nextEndLine = nodes.FindNextSameScope(firstOperator == -1 ? 0 : firstOperator, n => n is NLineEnd);

          if (nextEndLine == -1)
            Error($"Unexpected {nodes[0]}.", nodes[0].file, nodes[0].line);

          // only lvalue.
          if (firstOperator == -1)
          {
            var lnodes = nodes.GetRange(0, nextEndLine);
            nodes.RemoveRange(0, nextEndLine + 1);

            GetLValue(scope, lnodes, ref byteCodeState);
          }
          // `++`, `--`.
          else if (nextEndLine == firstOperator + 1)
          {
            var operatorNode = nodes[firstOperator] as NOperator;

            if (!new string[] { "++", "--" }.Contains(operatorNode.operatorType))
              Error($"Invalid use of operator {operatorNode}.", operatorNode.file, operatorNode.line);

            var lnodes = nodes.GetRange(0, firstOperator);
            nodes.RemoveRange(0, nextEndLine + 1);

            var lvalue = GetLValue(scope, lnodes, ref byteCodeState, null);
            lvalue.remainingReferences++;

            if (!(lvalue is CNamedValue))
              lvalue.description += " (lvalue)";

            if (!(lvalue.type is BuiltInCType) && !(lvalue.type is PtrCType || (lvalue.type as PtrCType).pointsTo is VoidCType))
              Error($"{operatorNode} cannot be applied to lvalue of type {lvalue.type}.", operatorNode.file, operatorNode.line);

            CValue resultingValue;

            if (lvalue.type is PtrCType)
            {
              scope.instructions.Add(new CInstruction_AddImm(lvalue, (operatorNode.operatorType == "++" ? 1 : -1) * (lvalue.type as PtrCType).pointsTo.GetSize(), scope.maxRequiredStackSpace, true, out resultingValue, operatorNode.file, operatorNode.line));
            }
            else
            {
              var type = lvalue.type as BuiltInCType;

              if (!type.IsFloat())
                scope.instructions.Add(new CInstruction_AddImm(lvalue, operatorNode.operatorType == "++" ? 1 : -1, scope.maxRequiredStackSpace, true, out resultingValue, operatorNode.file, operatorNode.line));
              else
                scope.instructions.Add(new CInstruction_AddImm(lvalue, operatorNode.operatorType == "++" ? 1.0 : -1.0, scope.maxRequiredStackSpace, true, out resultingValue, operatorNode.file, operatorNode.line));
            }
          }
          // lvalue = rvalue;
          else if ((nodes[firstOperator] as NOperator).operatorType == "=")
          {
            if (nodes[0] is NPseudoFunction && nodes[1] is NOpenParanthesis && nodes[firstOperator - 1] is NCloseParanthesis)
            {
              if ((nodes[0] as NPseudoFunction).type != NPseudoFunctionType.ValueOf)
                Error($"Unexpected '{nodes[0]}'.", nodes[0].file, nodes[0].line);

              var paramNodes = nodes.GetRange(2, firstOperator - 3);
              var equalsNode = nodes[firstOperator];
              var rnodes = nodes.GetRange(firstOperator + 1, nextEndLine - (firstOperator + 1));
              nodes.RemoveRange(0, nextEndLine + 1);

              var rvalue = GetRValue(scope, rnodes, ref byteCodeState);
              rvalue.remainingReferences++;

              if (!(rvalue is CNamedValue))
                rvalue.description += " (rvalue)";

              if (rvalue.type is VoidCType || rvalue.type is ArrayCType)
                Error($"Type '{rvalue.type}' is illegal for an rvalue.", rnodes[0].file, rnodes[1].line);

              var ptrValue = GetRValue(scope, paramNodes, ref byteCodeState);

              ptrValue.remainingReferences++;

              if (!(ptrValue is CNamedValue))
                ptrValue.description += " (rvalue)";

              if (!(ptrValue.type is PtrCType))
                Error($"Type '{ptrValue.type}' cannot be used as parameter for 'valueof'.", rnodes[0].file, rnodes[1].line);

              scope.instructions.Add(new CInstruction_SetValuePtrToValue(ptrValue, rvalue, equalsNode.file, equalsNode.line, scope.maxRequiredStackSpace));
            }
            else
            {
              var lnodes = nodes.GetRange(0, firstOperator);
              var equalsNode = nodes[firstOperator];
              var rnodes = nodes.GetRange(firstOperator + 1, nextEndLine - (firstOperator + 1));
              nodes.RemoveRange(0, nextEndLine + 1);

              var rvalue = GetRValue(scope, rnodes, ref byteCodeState);
              rvalue.remainingReferences++;

              if (!(rvalue is CNamedValue))
                rvalue.description += " (rvalue)";

              if (rvalue.type is VoidCType || rvalue.type is ArrayCType)
                Error($"Type '{rvalue.type}' is illegal for an rvalue.", rnodes[0].file, rnodes[1].line);

              var lvalue = GetLValue(scope, lnodes, ref byteCodeState, rvalue.type);
              lvalue.remainingReferences++;

              if (!(lvalue is CNamedValue))
                lvalue.description += " (lvalue)";

              scope.instructions.Add(new CInstruction_SetValueTo(lvalue, rvalue, equalsNode.file, equalsNode.line, scope.maxRequiredStackSpace));

              lvalue.isInitialized = true;
            }
          }
          else
          {
            throw new NotImplementedException();
          }
        }
      }

      if (scope.isFunction)
      {
        scope.instructions.Add(new CInstruction_EndFunction(scope.self));
      }
      else if (scope.parentScope == null) // is global scope.
      {
        scope.instructions.Add(new CInstruction_EndGlobalScope(scope.maxRequiredStackSpace, null, 0));
      }
      else if (scope.afterLabel != null)
      {
        scope.instructions.Add(new CInstruction_EndOfConditional_WipeAllRegisters(scope));
        scope.instructions.Add(new CInstruction_GotoLabel(scope.afterLabel, null, 0));
      }

      if (scope.parentScope == null)
      {
        foreach (var instruction in scope.instructions)
        {
          LLInstruction.currentFile = instruction.file;
          LLInstruction.currentLine = instruction.line;

          if (DetailedIntermediateOutput)
            byteCodeState.instructions.Add(new LLI_Comment_PseudoInstruction($"Intermediate Instruction Type: {instruction}"));

          instruction.GetLLInstructions(ref byteCodeState);
        }
      }
      else
      {
        scope.parentScope.instructions.AddRange(scope.instructions);
      }

      foreach (CFunction function in scope.GetLocalFunctions())
      {
        if (function is CBuiltInFunction)
          continue;

        Scope childScope = scope.GetChildScopeForFunction(function);

        CompileScope(childScope, function.nodes, ref byteCodeState);

        if (scope.parentScope != null)
        {
          scope.parentScope.instructions.AddRange(childScope.instructions);
        }
        else
        {
          foreach (var instruction in childScope.instructions)
          {
            LLInstruction.currentFile = instruction.file;
            LLInstruction.currentLine = instruction.line;

            if (DetailedIntermediateOutput)
              byteCodeState.instructions.Add(new LLI_Comment_PseudoInstruction($"Intermediate Instruction Type: {instruction}"));

            instruction.GetLLInstructions(ref byteCodeState);
          }
        }
      }
    }

    private static CValue GetLValue(Scope scope, List<Node> nodes, ref ByteCodeState byteCodeState, CType rValueType = null)
    {
      // variable definition.
      if (nodes.Count == 2 && nodes.NextIs(typeof(NType), typeof(NName)))
      {
        var type = nodes[0] as NType;
        var name = nodes[1] as NName;

        var value = new CNamedValue(name, type.type, false, false);
        scope.AddVariable(value);

        return value;
      }
      else if (nodes.Count == 2 && nodes.NextIs(typeof(NVarKeyword), typeof(NName)))
      {
        if (rValueType == null)
          Error($"Cannot deduct type for {nodes[1]}. {nodes[0]} can only be used when assigning a value.", nodes[0].file, nodes[0].line);

        if (rValueType.explicitCast != null)
          rValueType = rValueType.explicitCast;

        var name = nodes[1] as NName;

        var value = new CNamedValue(name, rValueType, false, false);
        scope.AddVariable(value);

        return value;
      }
      else if (nodes[0] is NName)
      {
        var value = scope.GetVariable((nodes[0] as NName).name);
        var nameNode = nodes[0];

        if (value != null)
        {
          if (nodes.Count > 1)
          {
            // Call to extern_func or func.
            if ((value.type is ExternFuncCType || value.type is FuncCType) && nodes[1] is NOpenParanthesis)
            {
              if (!((value.type as _FuncCTypeWrapper).returnType is VoidCType))
                Warn($"lvalue call to '{value}' will discard the return value of type '{(value.type as _FuncCTypeWrapper).returnType}'.", nameNode.file, nameNode.line);

              nodes.RemoveRange(0, 2);

              List<CValue> parameters = new List<CValue>();

              while (true)
              {
                if (nodes.Count == 0)
                {
                  Error($"Unexpected end of function call to '{value}'.", nameNode.file, nameNode.line);
                }
                else
                {
                  int nextCommaOrClosingParenthesis = nodes.FindNextSameScope(n => n is NComma || n is NCloseParanthesis);

                  if (nextCommaOrClosingParenthesis == -1)
                    Error($"Missing ',' or ')' whilst calling {value}.", nodes[0].file, nodes[0].line);

                  if (parameters.Count == 0 && nextCommaOrClosingParenthesis == 0 && nodes[0] is NCloseParanthesis)
                  {
                    nodes.RemoveAt(0);
                    break;
                  }

                  bool isLastParam = nodes[nextCommaOrClosingParenthesis] is NCloseParanthesis;

                  var parameterNodes = nodes.GetRange(0, nextCommaOrClosingParenthesis);
                  nodes.RemoveRange(0, nextCommaOrClosingParenthesis + 1);

                  var param = GetRValue(scope, parameterNodes, ref byteCodeState);

                  param.remainingReferences++;

                  if (!(param is CNamedValue))
                    param.description += " (rvalue)";

                  parameters.Add(param);

                  if (isLastParam)
                    break;
                }
              }

              CValue returnValue;

              scope.instructions.Add(new CInstruction_CallFunctionPtr(value, parameters, out returnValue, scope.maxRequiredStackSpace, nameNode.file, nameNode.line));

              return returnValue;
            }
            else
            {
              // TODO: '.', '->'
              throw new NotImplementedException();
            }
          }
          else
          {
            return value;
          }
        }
        else
        {
          var function = scope.GetFunction((nodes[0] as NName).name);
          
          if (function == null)
            Error($"Unknown identifer '{(nodes[0] as NName).name}'.", nameNode.file, nameNode.line);

          if (!(function.returnType is VoidCType))
            Warn($"lvalue call to {function} will discard the return value of type '{function.returnType}'.", nameNode.file, nameNode.line);

          if (nodes.Count == 1 || !(nodes[1] is NOpenParanthesis))
            Error($"Incomplete or invalid reference to function '{(nodes[0] as NName).name}'.", nameNode.file, nameNode.line);

          nodes.RemoveRange(0, 2);

          List<CValue> parameters = new List<CValue>();

          while (true)
          {
            if (nodes.Count == 0)
            {
              Error($"Unexpected end of function call to '{function}'.", nameNode.file, nameNode.line);
            }
            else
            {
              int nextCommaOrClosingParenthesis = nodes.FindNextSameScope(n => n is NComma || n is NCloseParanthesis);

              if (nextCommaOrClosingParenthesis == -1)
                Error($"Missing ',' or ')' whilst calling {function}.", nodes[0].file, nodes[0].line);

              if (parameters.Count == 0 && nextCommaOrClosingParenthesis == 0 && nodes[0] is NCloseParanthesis)
              {
                nodes.RemoveAt(0);
                break;
              }

              bool isLastParam = nodes[nextCommaOrClosingParenthesis] is NCloseParanthesis;

              var parameterNodes = nodes.GetRange(0, nextCommaOrClosingParenthesis);
              nodes.RemoveRange(0, nextCommaOrClosingParenthesis + 1);

              var param = GetRValue(scope, parameterNodes, ref byteCodeState);

              if (!(param is CNamedValue))
                param.description += " (rvalue)";

              param.remainingReferences++;

              parameters.Add(param);

              if (isLastParam)
                break;
            }
          }

          CValue returnValue;

          scope.instructions.Add(new CInstruction_CallFunction(function, parameters, out returnValue, scope.maxRequiredStackSpace, nameNode.file, nameNode.line));

          return returnValue;
        }
      }
      else
      {
        Error($"Unexpected {nodes[0]}.", nodes[0].file, nodes[0].line);
        return null; // Unreachable.
      }
    }

    private static CValue GetRValue(Scope scope, List<Node> nodes, ref ByteCodeState byteCodeState)
    {
      // TODO: Optional expected type?

      // Handle Operator Precedence.

      int nextOperator = nodes.FindNextSameScope(n => n is NOperator && new string[] { "=" }.Contains((n as NOperator).operatorType));

      if (nextOperator == -1)
        nextOperator = nodes.FindNextSameScope(n => n is NOperator && new string[] { "&&" }.Contains((n as NOperator).operatorType));

      if (nextOperator == -1)
        nextOperator = nodes.FindNextSameScope(n => n is NOperator && new string[] { "==", "<=", "!=", ">=" }.Contains((n as NOperator).operatorType));

      if (nextOperator == -1)
        nextOperator = nodes.FindNextSameScope(n => n is NOperator && new string[] { "*", "/", "%", "&", "^" }.Contains((n as NOperator).operatorType));

      if (nextOperator == -1)
        nextOperator = nodes.FindNextSameScope(n => n is NOperator && new string[] { "+", "-", "|", "<<", ">>" }.Contains((n as NOperator).operatorType));

      if (nextOperator == -1)
        nextOperator = nodes.FindNextSameScope(n => n is NOperator && new string[] { "~", "!" }.Contains((n as NOperator).operatorType));

      if (nextOperator == -1)
        nextOperator = nodes.FindNextSameScope(n => n is NOperator && !new string[] { "<", ">" }.Contains((n as NOperator).operatorType));

      if (nextOperator != -1)
      {
        var operatorNode = nodes[nextOperator] as NOperator;

        if (nextOperator == 0)
        {
          if (!new string[] { "~", "!" }.Contains(operatorNode.operatorType)) // TODO: How will we implement 'negate'?
            Error($"Unexpected {operatorNode} in rvalue.", operatorNode.file, operatorNode.line);

          //value.remainingReferences++;

          throw new NotImplementedException();
        }
        else
        {
          var lnodes = nodes.GetRange(0, nextOperator);
          var rnodes = nodes.GetRange(nextOperator + 1, nodes.Count - (nextOperator + 1));

          var left = GetRValue(scope, rnodes, ref byteCodeState);

          if (!(left is CNamedValue))
            left.description += " (left rvalue)";

          if (left.type is VoidCType || left.type is ArrayCType)
            Error($"Type '{left.type}' is illegal for an rvalue.", rnodes[0].file, rnodes[1].line);

          var right = GetRValue(scope, lnodes, ref byteCodeState);

          if (!(right is CNamedValue))
            right.description += " (right rvalue)";

          left.remainingReferences++;
          right.remainingReferences++;

          switch (operatorNode.operatorType)
          {
            case "=":
              {
                if (left.isConst)
                  Error($"Cannot assign '{right}' to constant value '{left}'.", operatorNode.file, operatorNode.line);

                scope.instructions.Add(new CInstruction_SetValueTo(left, right, operatorNode.file, operatorNode.line, scope.maxRequiredStackSpace));

                right.isInitialized = true;

                return right;
              }

            case "+":
              {
                CValue resultingValue;

                if (right is CConstIntValue)
                  scope.instructions.Add(new CInstruction_AddImm(left, (right as CConstIntValue).uvalue, scope.maxRequiredStackSpace, false, out resultingValue, operatorNode.file, operatorNode.line));
                else if (right is CConstFloatValue)
                  scope.instructions.Add(new CInstruction_AddImm(left, (right as CConstFloatValue).value, scope.maxRequiredStackSpace, false, out resultingValue, operatorNode.file, operatorNode.line));
                else
                  scope.instructions.Add(new CInstruction_Add(left, right, scope.maxRequiredStackSpace, false, out resultingValue, operatorNode.file, operatorNode.line));

                return resultingValue;
              }

            case "-":
              {
                CValue resultingValue;

                if (right is CConstIntValue)
                  unchecked { scope.instructions.Add(new CInstruction_AddImm(left, -(long)((right as CConstIntValue).uvalue), scope.maxRequiredStackSpace, false, out resultingValue, operatorNode.file, operatorNode.line)); }
                else if (right is CConstFloatValue)
                  scope.instructions.Add(new CInstruction_AddImm(left, -(right as CConstFloatValue).value, scope.maxRequiredStackSpace, false, out resultingValue, operatorNode.file, operatorNode.line));
                else
                  scope.instructions.Add(new CInstruction_Subtract(left, right, scope.maxRequiredStackSpace, false, out resultingValue, operatorNode.file, operatorNode.line));

                return resultingValue;
              }

            case "*":
              {
                CValue resultingValue;
                
                scope.instructions.Add(new CInstruction_Multiply(left, right, scope.maxRequiredStackSpace, false, out resultingValue, operatorNode.file, operatorNode.line));

                return resultingValue;
              }

            case "/":
              {
                CValue resultingValue;

                scope.instructions.Add(new CInstruction_Divide(left, right, scope.maxRequiredStackSpace, false, out resultingValue, operatorNode.file, operatorNode.line));

                return resultingValue;
              }

            case "%":
              {
                CValue resultingValue;

                scope.instructions.Add(new CInstruction_Modulo(left, right, scope.maxRequiredStackSpace, false, out resultingValue, operatorNode.file, operatorNode.line));

                return resultingValue;
              }

            case "&":
              {
                CValue resultingValue;

                scope.instructions.Add(new CInstruction_And(left, right, scope.maxRequiredStackSpace, false, out resultingValue, operatorNode.file, operatorNode.line));

                return resultingValue;
              }

            case "|":
              {
                CValue resultingValue;

                scope.instructions.Add(new CInstruction_Or(left, right, scope.maxRequiredStackSpace, false, out resultingValue, operatorNode.file, operatorNode.line));

                return resultingValue;
              }

            case "^":
              {
                CValue resultingValue;

                scope.instructions.Add(new CInstruction_XOr(left, right, scope.maxRequiredStackSpace, false, out resultingValue, operatorNode.file, operatorNode.line));

                return resultingValue;
              }

            case "<<":
              {
                CValue resultingValue;

                scope.instructions.Add(new CInstruction_BitShiftLeft(left, right, scope.maxRequiredStackSpace, false, out resultingValue, operatorNode.file, operatorNode.line));

                return resultingValue;
              }

            case ">>":
              {
                CValue resultingValue;

                scope.instructions.Add(new CInstruction_BitShiftRight(left, right, scope.maxRequiredStackSpace, false, out resultingValue, operatorNode.file, operatorNode.line));

                return resultingValue;
              }

            case "&&":
              {
                CValue resultingValue;

                scope.instructions.Add(new CInstruction_LogicalAnd(left, right, scope.maxRequiredStackSpace, false, out resultingValue, operatorNode.file, operatorNode.line));

                return resultingValue;
              }

            case "||":
              {
                CValue resultingValue;

                scope.instructions.Add(new CInstruction_LogicalOr(left, right, scope.maxRequiredStackSpace, false, out resultingValue, operatorNode.file, operatorNode.line));

                return resultingValue;
              }

            case "==":
              {
                CValue resultingValue;
                
                scope.instructions.Add(new CInstruction_Equals(left, right, scope.maxRequiredStackSpace, out resultingValue, operatorNode.file, operatorNode.line));

                return resultingValue;
              }

            case "!=":
              {
                CValue resultingValue;
                
                scope.instructions.Add(new CInstruction_NotEquals(left, right, scope.maxRequiredStackSpace, out resultingValue, operatorNode.file, operatorNode.line));

                return resultingValue;
              }

            default:
              {
                throw new NotImplementedException();
              }
          }
        }
      }
      else if (nodes[0] is NName)
      {
        var value = scope.GetVariable((nodes[0] as NName).name);
        var nameNode = nodes[0];

        if (value != null)
        {
          if (nodes.Count > 1)
          {
            // Call to extern_func or func.
            if ((value.type is ExternFuncCType || value.type is FuncCType) && nodes[1] is NOpenParanthesis)
            {
              if ((value.type as ExternFuncCType).returnType is VoidCType)
                Error($"Invalid return type '{(value.type as ExternFuncCType).returnType}' of rvalue function ptr call to '{value}'.", nameNode.file, nameNode.line);

              nodes.RemoveRange(0, 2);

              List<CValue> parameters = new List<CValue>();

              while (true)
              {
                if (nodes.Count == 0)
                {
                  Error($"Unexpected end of function call to '{value}'.", nameNode.file, nameNode.line);
                }
                else
                {
                  int nextCommaOrClosingParenthesis = nodes.FindNextSameScope(n => n is NComma || n is NCloseParanthesis);

                  if (nextCommaOrClosingParenthesis == -1)
                    Error($"Missing ',' or ')' whilst calling {value}.", nodes[0].file, nodes[0].line);

                  if (parameters.Count == 0 && nextCommaOrClosingParenthesis == 0 && nodes[0] is NCloseParanthesis)
                  {
                    nodes.RemoveAt(0);
                    break;
                  }

                  bool isLastParam = nodes[nextCommaOrClosingParenthesis] is NCloseParanthesis;

                  var parameterNodes = nodes.GetRange(0, nextCommaOrClosingParenthesis);
                  nodes.RemoveRange(0, nextCommaOrClosingParenthesis + 1);

                  var param = GetRValue(scope, parameterNodes, ref byteCodeState);

                  param.remainingReferences++;

                  if (!(param is CNamedValue))
                    param.description += " (rvalue)";

                  parameters.Add(param);

                  if (isLastParam)
                    break;
                }
              }

              CValue returnValue;

              scope.instructions.Add(new CInstruction_CallFunctionPtr(value, parameters, out returnValue, scope.maxRequiredStackSpace, nameNode.file, nameNode.line));

              return returnValue;
            }
            else if (nodes[1] is NOpenBracket)
            {
              var xvalue = GetXValue(scope, nodes, ref byteCodeState);

              CValue dereferencedValue;

              scope.instructions.Add(new CInstruction_DereferencePtr(xvalue, out dereferencedValue, scope.maxRequiredStackSpace, nodes[0].file, nodes[0].line));

              return dereferencedValue;
            }
            else
            {
              // TODO: '.', '->'
              throw new NotImplementedException();
            }
          }
          else
          {
            return value;
          }
        }
        else
        {
          var function = scope.GetFunction((nodes[0] as NName).name);

          if (function == null)
            Error($"Unknown identifer '{(nodes[0] as NName).name}'.", nameNode.file, nameNode.line);

          if (function.returnType is VoidCType)
            Error($"Invalid return type '{function.returnType}' of rvalue function call to '{function}'.", nameNode.file, nameNode.line);

          if (nodes.Count == 1)
          {
            if (function is CBuiltInFunction)
              Error($"Cannot retrieve the address of builtin function.", nameNode.file, nameNode.line);

            throw new NotImplementedException("Return ptr to function.");
          }

          if (!(nodes[1] is NOpenParanthesis))
            Error($"Incomplete or invalid reference to function '{(nodes[0] as NName).name}'.", nodes[0].file, nodes[0].line);

          nodes.RemoveRange(0, 2);

          List<CValue> parameters = new List<CValue>();

          while (true)
          {
            if (nodes.Count == 0)
            {
              Error($"Unexpected end of function call to '{function}'.", nameNode.file, nameNode.line);
            }
            else
            {
              int nextCommaOrClosingParenthesis = nodes.FindNextSameScope(n => n is NComma || n is NCloseParanthesis);

              if (nextCommaOrClosingParenthesis == -1)
                Error($"Missing ',' or ')' whilst calling {function}.", nodes[0].file, nodes[0].line);

              if (parameters.Count == 0 && nextCommaOrClosingParenthesis == 0 && nodes[0] is NCloseParanthesis)
              {
                nodes.RemoveAt(0);
                break;
              }

              bool isLastParam = nodes[nextCommaOrClosingParenthesis] is NCloseParanthesis;

              var parameterNodes = nodes.GetRange(0, nextCommaOrClosingParenthesis);
              nodes.RemoveRange(0, nextCommaOrClosingParenthesis + 1);
              
              var param = GetRValue(scope, parameterNodes, ref byteCodeState);

              param.remainingReferences++;

              if (!(param is CNamedValue))
                param.description += " (rvalue)";

              parameters.Add(param);

              if (isLastParam)
                break;
            }
          }

          CValue returnValue;

          scope.instructions.Add(new CInstruction_CallFunction(function, parameters, out returnValue, scope.maxRequiredStackSpace, nameNode.file, nameNode.line));

          return returnValue;
        }
      }
      else if (nodes[0] is NIntegerValue && nodes.Count == 1)
      {
        return new CConstIntValue(nodes[0] as NIntegerValue, BuiltInCType.Types["u64"]);
      }
      else if (nodes[0] is NFloatingPointValue && nodes.Count == 1)
      {
        return new CConstFloatValue(nodes[0] as NFloatingPointValue, BuiltInCType.Types["f64"]);
      }
      else if (nodes.NextIs(typeof(NCastKeyword), typeof(NOperator), typeof(NType), typeof(NOperator), typeof(NOpenParanthesis)) && (nodes[1] as NOperator).operatorType == "<" && (nodes[3] as NOperator).operatorType == ">" && nodes.Count > 5 && nodes.Last() is NCloseParanthesis)
      {
        var targetType = (nodes[2] as NType).type;
        var rValueToCast = GetRValue(scope, nodes.GetRange(5, nodes.Count - 6), ref byteCodeState);

        if (!(rValueToCast is CNamedValue))
          rValueToCast.description += " (rvalue to cast to '" + targetType + "')";

        if (rValueToCast.type.Equals(targetType))
          return rValueToCast;
        else if (!rValueToCast.type.CanExplicitCastTo(targetType) && !rValueToCast.type.CanImplicitCastTo(targetType))
          Error($"Explicit cast from type '{rValueToCast.type}' to type '{targetType}' is not possible for value '{rValueToCast}' (Defined in File '{rValueToCast.file ?? "?"}', Line {rValueToCast.line}).", nodes[0].file, nodes[0].line);
        else
        {
          rValueToCast.remainingReferences++;

          return rValueToCast.MakeCastableClone(targetType, scope, ref byteCodeState);
        }

        return null; // Unreachable.
      }
      else if (nodes.Count > 3 && nodes[0] is NPseudoFunction && nodes[1] is NOpenParanthesis && nodes[nodes.Count - 1] is NCloseParanthesis)
      {
        var pseudoFunction = nodes[0] as NPseudoFunction;
        nodes.RemoveAt(0);

        // is this a pair of parantheses?
        int parantesisLevel = 0;

        foreach (var node in nodes)
          if (node is NOpenParanthesis)
            parantesisLevel++;
          else if (node is NCloseParanthesis)
            if (--parantesisLevel == 0 && !object.ReferenceEquals(node, nodes[nodes.Count - 1]))
              Error($"Unexpected {node}.", node.file, node.line);
        
        switch (pseudoFunction.type)
        {
          case NPseudoFunctionType.SizeOf:
            {
              if (nodes.Count == 3 && nodes[1] is NType)
              {
                return new CConstIntValue((ulong)(nodes[1] as NType).type.GetSize(), BuiltInCType.Types["u64"], pseudoFunction.file, pseudoFunction.line);
              }
              else
              {
                // HACK: Ouch. This might actually move values around and generate instructions... :(
                var rvalue = GetRValue(scope, nodes.GetRange(1, nodes.Count - 2), ref byteCodeState);
                
                if (!(rvalue is CNamedValue))
                  rvalue.description += " (rvalue)";

                return new CConstIntValue((ulong)rvalue.type.GetSize(), BuiltInCType.Types["u64"], pseudoFunction.file, pseudoFunction.line);
              }
            }

          case NPseudoFunctionType.CountOf:
            {
              if (nodes.Count == 3 && nodes[1] is NType)
              {
                if (!((nodes[1] as NType).type is ArrayCType))
                  Error($"Invalid use of {nodes[1]} with {pseudoFunction}. Expected array or array type.", nodes[1].file, nodes[1].line);

                return new CConstIntValue((ulong)((nodes[1] as NType).type as ArrayCType).count, BuiltInCType.Types["u64"], pseudoFunction.file, pseudoFunction.line);
              }
              else
              {
                // HACK: Ouch. This might actually move values around and generate instructions... :(
                var rvalue = GetRValue(scope, nodes.GetRange(1, nodes.Count - 2), ref byteCodeState);
                
                if (!(rvalue is CNamedValue))
                  rvalue.description += " (rvalue)";

                if (!(rvalue.type is ArrayCType))
                  Error($"Invalid use of rvalue of type '{rvalue.type}' with {pseudoFunction}. Expected array or array type.", nodes[1].file, nodes[1].line);

                return new CConstIntValue((ulong)(rvalue.type as ArrayCType).count, BuiltInCType.Types["u64"], pseudoFunction.file, pseudoFunction.line);
              }
            }

          case NPseudoFunctionType.OffsetOf:
            {
              if (nodes.Count == 5 && nodes[1] is NName && nodes[2] is NComma && nodes[3] is NType)
              {
                if (!((nodes[3] as NType).type is StructCType))
                  Error($"Invalid use of {nodes[3]} with {pseudoFunction}. Expected struct type.", nodes[3].file, nodes[3].line);

                var structType = ((nodes[3] as NType).type as StructCType);

                foreach (var attribute in structType.attributes)
                {
                  if (attribute.name == (nodes[1] as NName).name)
                    return new CConstIntValue((ulong)attribute.offset, BuiltInCType.Types["u64"], pseudoFunction.file, pseudoFunction.line);
                }

                Error($"Invalid attribute name ({nodes[1]}) for {structType} with {pseudoFunction}.", nodes[1].file, nodes[1].line);
                return null; // Unreachable.
              }
              else
              {
                Error($"Unexpected {pseudoFunction}.", pseudoFunction.file, pseudoFunction.line);
                return null; // Unreachable.
              }
            }

          case NPseudoFunctionType.AddressOf:
            {
              var xvalue = GetXValue(scope, nodes.GetRange(1, nodes.Count - 2), ref byteCodeState);

              return xvalue;
            }

          case NPseudoFunctionType.ValueOf:
            {
              var rvalue = GetRValue(scope, nodes.GetRange(1, nodes.Count - 2), ref byteCodeState);

              if (!(rvalue is CNamedValue))
                rvalue.description += " (rvalue)";

              if (!(rvalue.type is PtrCType))
                Error($"Cannot dereference rvalue of type {rvalue.type}. Expected value of pointer type.", nodes[1].file, nodes[1].line);

              CValue derefValue;
              
              scope.instructions.Add(new CInstruction_DereferencePtr(rvalue, out derefValue, scope.maxRequiredStackSpace, nodes[0].file, nodes[0].line));

              return derefValue;
            }

          case NPseudoFunctionType.FromRegister:
            {
              if (nodes.Count != 4 || !(nodes[2] is NIntegerValue))
                Error($"Unexpected {nodes[2]} in {nodes[0]}.", nodes[2].file, nodes[2].line);

              var registerIndexNode = nodes[2] as NIntegerValue;

              if (registerIndexNode.isForcefullyNegative || registerIndexNode.uint_value >= (ulong)(IntegerRegisters + FloatRegisters))
                Error($"Invalid register index in {nodes[0]}: {nodes[2]}. Expected 0 .. {Compiler.IntegerRegisters + Compiler.FloatRegisters - 1}", nodes[2].file, nodes[2].line);

              if (registerIndexNode.uint_value < (ulong)IntegerRegisters)
                return new CValue(nodes[2].file, nodes[2].line, BuiltInCType.Types["u64"], true, true) { description = $"from {nodes[2]}", hasPosition = true, position = Position.Register((int)registerIndexNode.uint_value) };
              else
                return new CValue(nodes[2].file, nodes[2].line, BuiltInCType.Types["f64"], true, true) { description = $"from {nodes[2]}", hasPosition = true, position = Position.Register((int)registerIndexNode.uint_value) };
            }

          default:
            throw new NotImplementedException();
        }
      }
      else if (nodes[0] is NOpenParanthesis && nodes[nodes.Count - 1] is NCloseParanthesis)
      {
        // is this a pair of parantheses?
        int parantesisLevel = 0;

        foreach (var node in nodes)
          if (node is NOpenParanthesis)
            parantesisLevel++;
          else if (node is NCloseParanthesis)
            if (--parantesisLevel == 0 && !object.ReferenceEquals(node, nodes[nodes.Count - 1]))
              Error($"Unexpected {node}.", node.file, node.line);

        return GetRValue(scope, nodes.GetRange(1, nodes.Count - 1), ref byteCodeState);
      }
      else
      {
        Error($"Unexpected {nodes[0]}.", nodes[0].file, nodes[0].line);
        return null; // Unreachable.
      }
    }
    private static CValue GetXValue(Scope scope, List<Node> nodes, ref ByteCodeState byteCodeState)
    {
      if (nodes.Count == 0)
        Error("Missing xvalue.", null, -1);

      if (nodes[0] is NName)
      {
        var nameNode = nodes[0] as NName;

        if (nodes.Count == 1)
        {
          var variable = scope.GetVariable(nameNode.name);

          if (variable != null)
          {
            CGlobalValueReference addressOf;

            scope.instructions.Add(new CInstruction_AddressOfVariable(variable, out addressOf, scope.maxRequiredStackSpace, nodes[0].file, nodes[0].line));

            return addressOf;
          }
          else
          {
            throw new NotImplementedException();
          }
        }
        else
        {
          nodes.RemoveAt(0);

          if (nodes.Count > 2 && nodes[0] is NOpenBracket && nodes[nodes.Count - 1] is NCloseBracket)
          {
            // is this a pair of brackets?
            int bracketLevel = 0;

            foreach (var node in nodes)
              if (node is NOpenBracket)
                bracketLevel++;
              else if (node is NCloseBracket)
                if (--bracketLevel == 0 && !object.ReferenceEquals(node, nodes[nodes.Count - 1]))
                  Error($"Unexpected {node}.", node.file, node.line);
            
            var variable = scope.GetVariable(nameNode.name);

            if (variable == null)
              Error($"Invalid Token {nameNode}. Expected Variable Name.", nameNode.file, nameNode.line);

            CValue ptrWithoutOffset;

            if (variable.type is ArrayCType)
            {
              var type = variable.type as ArrayCType;

              if (type.type is VoidCType)
                Error($"Invalid array of type {type.type} ({variable}).", nameNode.file, nameNode.line);
              
              scope.instructions.Add(new CInstruction_GetPointerOfArrayStart(variable, out ptrWithoutOffset, scope.maxRequiredStackSpace, nameNode.file, nameNode.line));
            }
            else if (variable.type is PtrCType)
            {
              var type = variable.type as PtrCType;

              if (type.pointsTo is VoidCType)
                Error($"Cannot use array accessor on pointer of type {type.pointsTo} ({variable}).", nameNode.file, nameNode.line);

              ptrWithoutOffset = variable;
            }
            else
            {
              Error($"Cannot use array accessor on type {variable.type}. Expected Pointer or Array.", nameNode.file, nameNode.line);
              return null; // Unreachable.
            }

            ptrWithoutOffset.remainingReferences++;

            var index = GetRValue(scope, nodes.GetRange(1, nodes.Count - 2), ref byteCodeState);

            if (!(index is CNamedValue))
              index.description += " (rvalue)";

            if ((ptrWithoutOffset.type as PtrCType).pointsTo.GetSize() != 1)
            {
              index.remainingReferences++;
              CValue newIndex = null;
              scope.instructions.Add(new CInstruction_Multiply(index, new CConstIntValue((ulong)(ptrWithoutOffset.type as PtrCType).pointsTo.GetSize(), BuiltInCType.Types["u64"], null, -1), scope.maxRequiredStackSpace, false, out newIndex, nodes[0].file, nodes[0].line));
              index = newIndex;
            }

            CValue offsetPtr;
            index.remainingReferences++;
            scope.instructions.Add(new CInstruction_Add(ptrWithoutOffset, index, scope.maxRequiredStackSpace, false, out offsetPtr, nodes[0].file, nodes[0].line));

            return offsetPtr;
          }
          else
          {
            Error($"Unexpected {nodes[0]}.", nodes[0].file, nodes[0].line);
            return null; // Unreachable.
          }
        }
      }
      else
      {
        Error($"Unexpected {nodes[0]}.", nodes[0].file, nodes[0].line);
        return null; // Unreachable.
      }
    }
  }
}
