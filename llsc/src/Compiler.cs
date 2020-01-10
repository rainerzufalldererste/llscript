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

    LLS_OP_UADD_IMM,
    LLS_OP_UADD_REGISTER,
    LLS_OP_USUB_IMM,
    LLS_OP_USUB_REGISTER,
    LLS_OP_UMUL_IMM,
    LLS_OP_UMUL_REGISTER,
    LLS_OP_UDIV_IMM,
    LLS_OP_UDIV_REGISTER,
    LLS_OP_UMOD_IMM,
    LLS_OP_UMOD_REGISTER,

    LLS_OP_ADD_IMM,
    LLS_OP_ADD_REGISTER,
    LLS_OP_SUB_IMM,
    LLS_OP_SUB_REGISTER,
    LLS_OP_MUL_IMM,
    LLS_OP_MUL_REGISTER,
    LLS_OP_DIV_IMM,
    LLS_OP_DIV_REGISTER,
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

    LLS_OP_INV,
    LLS_OP_NOT,

    LLS_OP_INEGATE,
    LLS_OP_FNEGATE,

    LLS_OP_CMP_EQ_IMM,
    LLS_OP_CMP_GT_IMM,
    LLS_OP_CMP_LT_IMM,

    LLS_OP_CMP_EQ_REGISTER,
    LLS_OP_CMP_GT_REGISTER,
    LLS_OP_CMP_LT_REGISTER,

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

  public abstract class CType
  {
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
  }

  public class VoidCType : CType
  {
    public static VoidCType Instance = new VoidCType();

    private VoidCType() { }

    public override long GetSize() => 0;

    public override string ToString() => "void";

    public override bool Equals(object obj) => obj is VoidCType;

    public override int GetHashCode() => 0x13579bdf;
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
  }

  public class ExternFuncCType : CType
  {
    public readonly CType[] parameterTypes;
    public readonly CType returnType;

    public ExternFuncCType(CType returns, IEnumerable<CType> parameters)
    {
      returnType = returns;
      parameterTypes = parameters.ToArray();
    }

    public override long GetSize() => 8;

    public override bool Equals(object obj)
    {
      if (obj is ExternFuncCType)
      {
        ExternFuncCType other = (obj as ExternFuncCType);

        if (other.returnType != returnType || other.parameterTypes.Length != parameterTypes.Length)
          return false;
        
        for (int i = 0; i < parameterTypes.Length; i++)
          if (!other.parameterTypes[i].Equals(parameterTypes[i]))
            return false;

        return true;
      }

      return false;
    }

    public override int GetHashCode()
    {
      int hashCode = ~returnType.GetHashCode();
      int parameterIndex = 1;

      foreach (var type in parameterTypes)
        hashCode ^= (type.GetHashCode() + parameterIndex++);

      return hashCode;
    }

    public override string ToString()
    {
      string ret = "extern_func<" + returnType.ToString() + " (";

      for (int i = 0; i < parameterTypes.Length; i++)
      {
        ret += parameterTypes[i].ToString();

        if (i + 1 < parameterTypes.Length)
          ret += ", ";
      }

      return ret;
    }
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
    public readonly CType type;
    public readonly string file;
    public readonly int line;
    public bool isInitialized;
    public Position position;
    public bool hasPosition = false;

    public int remainingReferences = 0;
    public readonly bool isConst;
    public int lastTouchedInstructionCount = 0;

    public CValue(string file, int line, CType type, bool isConst, bool isInitialized)
    {
      this.file = file;
      this.line = line;
      this.type = type;
      this.isConst = isConst;
      this.isInitialized = isInitialized;
    }

    public override string ToString() => "unnamed value [" + (isConst ? "const " : "") + type + "]";
  }

  public class CConstValue : CValue
  {
    public readonly ulong uvalue;
    public readonly long ivalue;

    public CConstValue(ulong value, CType type, string file, int line) : base(file, line, type, true, true)
    {
      this.uvalue = value;
      unchecked { this.ivalue = (long)value; }
    }

    public CConstValue(NIntegerValue value, CType type) : base(value.file, value.line, type, true, true)
    {
      if (!(type is BuiltInCType))
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

    public override string ToString() => "unnamed immediate value [" + (isConst ? "const " : "") + type + "] (" + ((type as BuiltInCType).IsUnsigned() ? uvalue.ToString() : ivalue.ToString()) + ")";
  }

  public class CNamedValue : CValue
  {
    public readonly string name;
    public bool hasStackOffset = false;
    public long homeStackOffset;
    public bool modifiedSinceLastHome = false;

    public CNamedValue(NName name, CType type, bool isConst, bool isInitialized) : base(name.file, name.line, type, isConst, isInitialized)
    {
      this.name = name.name;
    }

    public CNamedValue(string name, CType type, bool isConst, bool isInitialized, string file, int line) : base(file, line, type, isConst, isInitialized)
    {
      this.name = name;
    }

    public override string ToString() => (isConst ? "const " : "") + type + " " + name;
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

    public static int FindNextSelfScope(this List<Node> nodes, int start, Func<Node, bool> check)
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

    public static int FindNextSelfScope(this List<Node> nodes, Func<Node, bool> check) => FindNextSelfScope(nodes, 0, check);
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

    public readonly Value<long> minStackSize = new Value<long>(0);
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

      minStackSize.value = 8; // Return Address.
      
      // + Return Value Ptr if not void or in register.
      if (!(returnType is VoidCType || returnType is BuiltInCType || returnType is PtrCType))
        minStackSize.value += 8;

      int intRegistersTaken = (this is CBuiltInFunction ? 1 : 0);
      int floatRegistersTaken = 0;

      if (this is CBuiltInFunction)
        minStackSize.value = 0;

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
        param.value.position = Position.StackOffset(minStackSize.value);
        param.value.hasPosition = true;
        minStackSize.value += param.type.GetSize();
      }

      callStackSize = minStackSize.value;
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

    public int GetFreeIntegerRegister(Value<long> stackSize)
    {
      for (int i = Compiler.IntegerRegisters - 1; i >= 0; i--)
        if (registers[i] == null)
          return i;

      for (int i = Compiler.IntegerRegisters - 1; i >= 0; i--)
        if (registers[i].remainingReferences == 0)
          return i;

      int oldestIndex = -1;
      int oldestValue = 0;

      // With Home.

      for (int i = Compiler.IntegerRegisters - 1; i >= 0; i--)
      {
        if (registers[i] is CNamedValue && (registers[i] as CNamedValue).hasStackOffset)
        {
          if (!(registers[i] as CNamedValue).modifiedSinceLastHome)
            return i;

          if (registers[i].lastTouchedInstructionCount < oldestValue)
          {
            oldestValue = registers[i].lastTouchedInstructionCount;
            oldestIndex = i;
          }
        }
      }

      if (oldestIndex < 0)
      {
        oldestIndex = 0;

        for (int i = Compiler.IntegerRegisters - 1; i >= 0; i--)
        {
          if (registers[i].lastTouchedInstructionCount < oldestValue)
          {
            oldestValue = registers[i].lastTouchedInstructionCount;
            oldestIndex = i;
          }
        }
      }

      FreeUsedRegister(oldestIndex, stackSize);

      return oldestIndex;
    }

    public int GetTriviallyFreeIntegerRegister()
    {
      for (int i = Compiler.IntegerRegisters - 1; i >= 0; i--)
        if (registers[i] == null)
          return i;

      for (int i = Compiler.IntegerRegisters - 1; i >= 0; i--)
        if (registers[i].remainingReferences == 0)
          return i;

      for (int i = Compiler.IntegerRegisters - 1; i >= 0; i--)
        if (registers[i] is CNamedValue && (registers[i] as CNamedValue).hasStackOffset)
          if (!(registers[i] as CNamedValue).modifiedSinceLastHome)
            return i;

      return -1;
    }

    public int GetTriviallyFreeFloatRegister()
    {
      for (int i = Compiler.IntegerRegisters + Compiler.FloatRegisters - 1; i >= Compiler.IntegerRegisters; i--)
        if (registers[i] == null)
          return i;

      for (int i = Compiler.IntegerRegisters + Compiler.FloatRegisters - 1; i >= Compiler.IntegerRegisters; i--)
        if (registers[i].remainingReferences == 0)
          return i;

      for (int i = Compiler.IntegerRegisters + Compiler.FloatRegisters - 1; i >= Compiler.IntegerRegisters; i--)
        if (registers[i] is CNamedValue && (registers[i] as CNamedValue).hasStackOffset)
          if (!(registers[i] as CNamedValue).modifiedSinceLastHome)
            return i;

      return -1;
    }

    public void FreeRegister(int register, Value<long> stackSize)
    {
      if (registers[register] == null || registers[register].remainingReferences == 0)
        return;

      if (registers[register] is CNamedValue && (registers[register] as CNamedValue).hasStackOffset && !(registers[register] as CNamedValue).modifiedSinceLastHome)
        return;

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

    private void FreeUsedRegister(int register, Value<long> stackSize)
    {
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
        }
        else
        {
          if (size == 8)
            instructions.Add(new LLI_MovRegisterToStackOffset(register, stackSize, stackSize.value));
          else
            instructions.Add(new LLI_MovRegisterToStackOffset_NBytes(register, stackSize, stackSize.value, (byte)size));

          namedValue.hasStackOffset = true;
          namedValue.homeStackOffset = stackSize.value;

          namedValue.position.inRegister = false;
          namedValue.position.stackOffsetForward = namedValue.homeStackOffset;
          namedValue.modifiedSinceLastHome = false;

          stackSize.value += size;
        }
      }
      else
      {
        if (size == 8)
          instructions.Add(new LLI_MovRegisterToStackOffset(register, stackSize, stackSize.value));
        else
          instructions.Add(new LLI_MovRegisterToStackOffset_NBytes(register, stackSize, stackSize.value, (byte)size));

        registers[register].position.inRegister = false;
        registers[register].position.stackOffsetForward = stackSize.value;

        stackSize.value += size;
      }
    }

    public void CompileInstructionsToBytecode()
    {
      ulong position = 0;

      foreach (var instruction in instructions)
      {
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

    public void MoveValueToPosition(CValue sourceValue, Position targetPosition, Value<long> stackSize, bool addReference)
    {
      // TODO: Work out the reference count...

      if (targetPosition.inRegister && sourceValue.type.GetSize() > 8)
        throw new Exception($"Internal Compiler Error: Value '{sourceValue}' cannot be moved to a register, because it's > 8 bytes.");

      if (sourceValue is CConstValue)
      {
        if (!(sourceValue.hasPosition && targetPosition.inRegister && sourceValue.position.inRegister && sourceValue.position.registerIndex == targetPosition.registerIndex) || (targetPosition.inRegister && registers[targetPosition.registerIndex] is CConstValue && (registers[targetPosition.registerIndex] as CConstValue).uvalue == (sourceValue as CConstValue).uvalue))
        {
          if (addReference)
            registers[targetPosition.registerIndex].remainingReferences++;

          registers[targetPosition.registerIndex].lastTouchedInstructionCount = instructions.Count;
        }
        else if (targetPosition.inRegister)
        {
          FreeRegister(targetPosition.registerIndex, stackSize);
          instructions.Add(new LLI_MovImmToRegister(targetPosition.registerIndex, BitConverter.GetBytes((sourceValue as CConstValue).uvalue)));
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
              if (registers[j] is CConstValue && (registers[j] as CConstValue).uvalue == (sourceValue as CConstValue).uvalue)
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
            instructions.Add(new LLI_MovImmToRegister(registerIndex, BitConverter.GetBytes((sourceValue as CConstValue).uvalue)));
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
      else
      {
        if (sourceValue.hasPosition)
          throw new Exception("Internal Compiler Error: Move To Position, but source value has no origin and is not constant.");

        if (sourceValue.position.inRegister == targetPosition.inRegister && ((targetPosition.inRegister && sourceValue.position.registerIndex == targetPosition.registerIndex) || (!targetPosition.inRegister && sourceValue.position.stackOffsetForward == targetPosition.stackOffsetForward)))
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
        else if (targetPosition.inRegister)
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
    }
  }
  
  public class Value<T>
  {
    public T value;

    public Value(T value) => this.value = value;
  }

  public abstract class LLInstruction
  {
    public ulong position;

    public ulong bytecodeSize { get; protected set; }

    public LLInstruction(ulong bytecodeSize) => this.bytecodeSize = bytecodeSize;

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
  }

  public class LLI_Label_PseudoInstruction : LLInstruction
  {
    public LLI_Label_PseudoInstruction() : base(0) { }

    public override void AppendBytecode(ref List<byte> byteCode) { }
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
  }

  public class LLI_StackIncrementImm : LLInstruction
  {
    readonly Value<long> value;
    long offset = 0;

    public LLI_StackIncrementImm(long value) : base(9)
    {
      this.value = new Value<long>(value);
    }

    public LLI_StackIncrementImm(Value<long> value, long offset = 0) : base(9)
    {
      this.value = value;
      this.offset = offset;
    }

    public override void AfterCodeGen()
    {
      if (((long)this.value.value + offset) == 0)
        base.bytecodeSize = 0;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_STACK_INC_IMM);
      byteCode.AddRange(BitConverter.GetBytes(value.value - offset));
    }
  }

  public class LLI_StackDecrementImm : LLInstruction
  {
    readonly Value<long> value;
    long offset = 0;

    public LLI_StackDecrementImm(long value) : base(9)
    {
      this.value = new Value<long>(value);
    }

    public LLI_StackDecrementImm(Value<long> value, long offset = 0) : base(9)
    {
      this.value = value;
      this.offset = offset;
    }

    public override void AfterCodeGen()
    {
      if (((long)this.value.value + offset) == 0)
        base.bytecodeSize = 0;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_STACK_DEC_IMM);
      byteCode.AddRange(BitConverter.GetBytes(value.value - offset));
    }
  }

  public class LLI_LoadEffectiveAddress_StackOffsetToRegister : LLInstruction
  {
    readonly Value<long> value;
    readonly Value<long> offset;
    readonly int register;

    public LLI_LoadEffectiveAddress_StackOffsetToRegister(long value, long offset, int register) : base(10)
    {
      this.value = new Value<long>(value);
      this.offset = new Value<long>(offset);
      this.register = register;
    }

    public LLI_LoadEffectiveAddress_StackOffsetToRegister(Value<long> value, long offset, int register) : base(10)
    {
      this.value = value;
      this.offset = new Value<long>(offset);
      this.register = register;
    }

    public LLI_LoadEffectiveAddress_StackOffsetToRegister(Value<long> value, Value<long> offset, int register) : base(10)
    {
      this.value = value;
      this.offset = offset;
      this.register = register;
    }
    
    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_LEA_STACK_TO_REGISTER);
      byteCode.Add((byte)register);
      byteCode.AddRange(BitConverter.GetBytes(value.value - offset.value));
    }
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
  }

  public class LLI_MovRegisterToStackOffset : LLInstruction
  {
    readonly int register;
    readonly Value<long> stackSize;
    readonly long offset;

    public LLI_MovRegisterToStackOffset(int register, Value<long> stackSize, long offset) : base(1 + 8 + 1)
    {
      this.register = register;
      this.stackSize = stackSize;
      this.offset = offset;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_MOV_REGISTER_STACK);
      byteCode.AddRange(BitConverter.GetBytes(stackSize.value - offset));
      byteCode.Add((byte)register);
    }
  }

  public class LLI_MovRegisterToStackOffset_NBytes : LLInstruction
  {
    readonly int register;
    readonly Value<long> stackSize;
    readonly long offset;
    readonly int bytes;

    public LLI_MovRegisterToStackOffset_NBytes(int register, Value<long> stackSize, long offset, int bytes) : base(1 + 8 + 1 + 1)
    {
      this.register = register;
      this.stackSize = stackSize;
      this.offset = offset;
      this.bytes = bytes;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_MOV_REGISTER_STACK_N_BYTES);
      byteCode.AddRange(BitConverter.GetBytes(stackSize.value - offset));
      byteCode.Add((byte)register);
      byteCode.Add((byte)bytes);
    }
  }

  public class LLI_MovStackOffsetToRegister : LLInstruction
  {
    readonly int register;
    readonly Value<long> stackSize;
    readonly long offset;

    public LLI_MovStackOffsetToRegister(Value<long> stackSize, long offset, int register) : base(1 + 8 + 1)
    {
      this.register = register;
      this.stackSize = stackSize;
      this.offset = offset;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_MOV_STACK_REGISTER);
      byteCode.Add((byte)register);
      byteCode.AddRange(BitConverter.GetBytes(stackSize.value - offset));
    }
  }

  public class LLI_MovStackOffsetToStackOffset : LLInstruction
  {
    Value<long> sourceStackSize;
    long sourceOffset;

    Value<long> targetStackSize;
    long targetOffset;

    public LLI_MovStackOffsetToStackOffset(Value<long> sourceStackSize, long sourceOffset, Value<long> targetStackSize, long targetOffset) : base(1 + 8 + 8)
    {
      this.sourceStackSize = sourceStackSize;
      this.sourceOffset = sourceOffset;

      this.targetStackSize = targetStackSize;
      this.targetOffset = targetOffset;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_MOV_STACK_STACK);
      byteCode.AddRange(BitConverter.GetBytes(targetStackSize.value - targetOffset));
      byteCode.AddRange(BitConverter.GetBytes(sourceStackSize.value - sourceOffset));
    }
  }

  public class LLI_MovStackOffsetToStackOffset_NBytes : LLInstruction
  {
    Value<long> sourceStackSize;
    long sourceOffset;

    Value<long> targetStackSize;
    long targetOffset;

    byte bytes;

    public LLI_MovStackOffsetToStackOffset_NBytes(Value<long> sourceStackSize, long sourceOffset, Value<long> targetStackSize, long targetOffset, byte bytes) : base(1 + 8 + 8 + 1)
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
      byteCode.AddRange(BitConverter.GetBytes(targetStackSize.value - targetOffset));
      byteCode.AddRange(BitConverter.GetBytes(sourceStackSize.value - sourceOffset));
      byteCode.Add(bytes);
    }
  }

  public class LLI_MovPtrInRegisterFromRegister : LLInstruction
  {
    int targetPtrRegister, sourceValueRegister;

    public LLI_MovPtrInRegisterFromRegister(int targetPtrRegister, int sourceValueRegister) : base(3)
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
  }

  public class LLI_MovPtrInRegisterFromRegister_NBytes : LLInstruction
  {
    int targetPtrRegister, sourceValueRegister, bytes;

    public LLI_MovPtrInRegisterFromRegister_NBytes(int targetPtrRegister, int sourceValueRegister, int bytes) : base(4)
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
  }

  public class LLI_UAddImm : LLInstruction
  {
    int register;
    byte[] value;
    public LLI_UAddImm(int register, byte[] value) : base(1 + 1 + 8)
    {
      this.register = register;
      this.value = value;

      if (this.value.Length != 8)
        throw new Exception("Internal Compiler Error!");
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_UADD_IMM);
      byteCode.Add((byte)register);
      byteCode.AddRange(value);
    }
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
      function.ResetRegisterPositions();

      byteCodeState.instructions.Add(function.functionStartLabel);

      for (int i = 0; i < byteCodeState.registers.Length; i++)
        byteCodeState.registers[i] = null;

      foreach (var parameter in function.parameters)
        if (parameter.value.position.inRegister)
          byteCodeState.registers[parameter.value.position.registerIndex] = parameter.value;

      byteCodeState.instructions.Add(new LLI_StackIncrementImm(function.minStackSize, -(long)function.callStackSize));
    }
  }

  public class CInstruction_BeginGlobalScope : CInstruction
  {
    private readonly Value<long> stackSize;

    public CInstruction_BeginGlobalScope(Value<long> stackSize, string file, int line) : base(file, line)
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
    }
  }

  public class CInstruction_EndGlobalScope : CInstruction
  {
    private readonly Value<long> stackSize;

    public CInstruction_EndGlobalScope(Value<long> stackSize, string file, int line) : base(file, line)
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
    private readonly CNamedValue value;
    private readonly byte[] data;
    private readonly Value<long> stackSize;

    public CInstruction_SetArray(CNamedValue value, byte[] data, string file, int line, Value<long> stackSize) : base(file, line)
    {
      this.data = data;
      this.value = value;
      this.stackSize = stackSize;

      if (!value.hasStackOffset)
        throw new Exception("Internal Compiler Error.");
    }

    public override void GetLLInstructions(ref ByteCodeState byteCodeState)
    {
      long dataSizeRemaining = data.LongLength;
      int stackPtrRegister = byteCodeState.GetFreeIntegerRegister(stackSize);
      byteCodeState.registers[stackPtrRegister] = new CValue(file, line, new PtrCType(BuiltInCType.Types["u64"]), true, true) { remainingReferences = 1, lastTouchedInstructionCount = byteCodeState.instructions.Count };

      byteCodeState.instructions.Add(new LLI_LoadEffectiveAddress_StackOffsetToRegister(stackSize, value.homeStackOffset, stackPtrRegister));
      
      int valueRegister = byteCodeState.GetFreeIntegerRegister(stackSize);
      byteCodeState.registers[stackPtrRegister] = new CValue(file, line, BuiltInCType.Types["u64"], true, true) { remainingReferences = 1, lastTouchedInstructionCount = byteCodeState.instructions.Count };

      long offset = 0;

      byte[] _data = new byte[8];

      while (dataSizeRemaining >= 8)
      {
        for (int i = 0; i < 8; i++)
          _data[i] = data[offset + i];

        byteCodeState.instructions.Add(new LLI_MovImmToRegister(valueRegister, _data.ToArray())); // move value to register.
        byteCodeState.instructions.Add(new LLI_MovPtrInRegisterFromRegister(stackPtrRegister, valueRegister)); // move register to ptr.

        dataSizeRemaining -= 8;
        offset += 8;

        if (dataSizeRemaining > 0)
          byteCodeState.instructions.Add(new LLI_UAddImm(stackPtrRegister, BitConverter.GetBytes((ulong)8))); // inc ptr by 8.
      }

      if (dataSizeRemaining > 0)
      {
        for (int i = 0; i < 8 && offset + i < data.LongLength; i++)
          _data[i] = data[offset + i];

        byteCodeState.instructions.Add(new LLI_MovImmToRegister(valueRegister, _data.ToArray())); // move value to register.
        byteCodeState.instructions.Add(new LLI_MovPtrInRegisterFromRegister_NBytes(stackPtrRegister, valueRegister, (byte)dataSizeRemaining)); // move register to ptr.
      }

      byteCodeState.registers[stackPtrRegister] = null;
      byteCodeState.registers[valueRegister] = null;
    }
  }

  public class CInstruction_CallFunction : CInstruction
  {
    private readonly CFunction function;
    private readonly List<CValue> arguments;
    private readonly CValue returnValue;
    private readonly Value<long> stackSize;

    public CInstruction_CallFunction(CFunction function, List<CValue> arguments, out CValue returnValue, Value<long> stackSize, string file, int line) : base(file, line)
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

        if (arguments[i].type != function.parameters[i].type)
          Compiler.Error($"In function call to '{function}': Argument {(i + 1)} '{arguments[i]}' for parameter {function.parameters[i].type} {function.parameters[i].name} is of mismatching type '{arguments[i].type}'. Defined in File '{arguments[i].file ?? "?"}', Line: {arguments[i].line}.", file, line);
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
      // In case it's a recursive call: backup the parameter positions.
      var originalParameters = function.parameters;
      function.ResetRegisterPositions();

      for (int i = arguments.Count - 1; i >= 0; i--)
      {
        var targetPosition = function.parameters[i].value.position;
        var sourceValue = arguments[i];

        byteCodeState.MoveValueToPosition(sourceValue, targetPosition, stackSize, true);
      }

      if (function is CBuiltInFunction)
      {
        Position targetPosition = new Position() { inRegister = true, registerIndex = 0 };

        byteCodeState.MoveValueToPosition(new CConstValue((function as CBuiltInFunction).builtinFunctionIndex, BuiltInCType.Types["u8"], file, line), targetPosition, stackSize, true);

        if (!(function.returnType is VoidCType))
        {
          returnValue.hasPosition = true;
          returnValue.position.inRegister = true;
          returnValue.position.registerIndex = 0;
        }

        byteCodeState.instructions.Add(new LLI_CallBuiltInFunction_IDFromRegister_ResultToRegister(0, 0));

        byteCodeState.registers[0] = returnValue;
      }
      else
      {
        throw new NotImplementedException();
      }

      function.parameters = originalParameters;
    }
  }


  public class Scope
  {
    public Value<long> maxRequiredStackSpace = new Value<long>(0);

    public readonly Scope parentScope;
    public readonly bool isFunction;
    public readonly CFunction self;
    public int stackSpaceAllocationByteCodeIndex;
    public bool isConditional = false;

    private Dictionary<string, StructCType> definedStructs;
    private Dictionary<string, CFunction> definedFunctions;
    private Dictionary<string, CNamedValue> definedVariables;

    public readonly LLI_Label_PseudoInstruction continueLabel, breakLabel, afterLabel;

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

    public Scope GetChildScope(CFunction function)
    {
      return new Scope(this, function);
    }
  }

  static class Compiler
  {
    static bool WarningsAsErrors = false;
    static bool ShowWarnings = true;

    public static readonly int IntegerRegisters = 8;
    public static readonly int FloatRegisters = 8;

    public static void Error(string error, string file, int line)
    {
      Console.ForegroundColor = ConsoleColor.Red;

      Console.Write("Error");

      if (file != null)
        Console.Write($" (in '{file}', Line {line + 1})");

      Console.WriteLine(":\n\t" + error);
      Console.ResetColor();

      throw new CompileFailureException();
    }

    public static void Warn(string warning, string file, int line)
    {
      if (ShowWarnings)
      {
        Console.ForegroundColor = ConsoleColor.Yellow;

        Console.Write("Warning");

        if (file != null)
          Console.Write($" (in '{file}', Line {line + 1})");

        Console.WriteLine(":\n\t" + warning);
        Console.ResetColor();
      }

      if (WarningsAsErrors)
        Error($"Warning treated as error: '{warning}'", file, line);
    }

    [STAThread]
    static void Main(string[] args)
    {
      string outFileName = "bytecode.lls";

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

            default:
              if (argument.StartsWith("-o="))
                outFileName = argument.Substring(3).Trim('\'', '\"');
              else
                Error($"Invalid Parameter '{argument}'.", null, 0);
              break;
          }
        }

        bool anyFilesCompiled = false;

        var files = (from file in args where !file.StartsWith("-") select new FileContents() { filename = file, lines = File.ReadAllLines(file) });
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

        files = null;

        var globalScope = new Scope();
        var byteCodeState = new ByteCodeState();

        CompileScope(globalScope, allNodes, ref byteCodeState);
        byteCodeState.CompileInstructionsToBytecode();

        File.WriteAllBytes(outFileName, byteCodeState.byteCode.ToArray());
      }
      catch (CompileFailureException e)
      {
        Console.WriteLine("Compilation Failed.");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("\n" + e.ToString());
        Console.ResetColor();

        Environment.Exit(1);
      }
      catch (Exception e)
      {
        Console.WriteLine("Internal Compiler Error.\n\n" + e.ToString());

        Environment.Exit(-1);
      }

      Console.WriteLine("Compilation Succeeded.");
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
              else if (lineString.NextIs(start, "==") || lineString.NextIs(start, "!=") || lineString.NextIs(start, "<=") || lineString.NextIs(start, ">=") || lineString.NextIs(start, "<<") || lineString.NextIs(start, ">>") || lineString.NextIs(start, "++") || lineString.NextIs(start, "--") || lineString.NextIs(start, "&&") || lineString.NextIs(start, "||"))
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
            Console.WriteLine($"Internal Compiler Error Parsing File '{file.filename}', Line {line + 1}.\n\n" + e.ToString());

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
        Console.WriteLine($"Internal Compiler Error Parsing File '{file.filename}'.\n\n" + e.ToString());

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

            case "array":
              file.nodes[i] = new NArrayKeyword(node.file, node.line);
              break;

            case "func":
              file.nodes[i] = new NFuncKeyword(node.file, node.line);
              break;

            case "extern_func":
              file.nodes[i] = new NExternFuncKeyword(node.file, node.line);
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
            value.homeStackOffset = scope.maxRequiredStackSpace.value;
            value.hasStackOffset = true;
            scope.maxRequiredStackSpace.value += arrayType.GetSize();

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
            value.homeStackOffset = scope.maxRequiredStackSpace.value;
            value.hasStackOffset = true;
            scope.maxRequiredStackSpace.value += arrayType.GetSize();

            scope.AddVariable(value);
            scope.instructions.Add(new CInstruction_SetArray(value, data.ToArray(), startNode.file, startNode.line, scope.maxRequiredStackSpace));
          }
        }
        else
        {
          // Find first '=' before ';'.
          // TODO: Care about scope!
          int firstEquals = -1;
          int nextEndLine = -1;

          for (int i = 0; i < nodes.Count; i++)
          {
            if (firstEquals == -1 && nodes[i] is NOperator && (nodes[i] as NOperator).operatorType == "=")
            {
              firstEquals = i;
            }
            else if (nodes[i] is NLineEnd)
            {
              nextEndLine = i;
              break;
            }
          }

          if (nextEndLine == -1 || nextEndLine == firstEquals + 1)
            Error($"Unexpected {nodes[0]}.", nodes[0].file, nodes[0].line);

          // only lvalue.
          if (firstEquals == -1)
          {
            var lnodes = nodes.GetRange(0, nextEndLine);
            nodes.RemoveRange(0, nextEndLine + 1);

            GetLValue(scope, lnodes, ref byteCodeState);
          }
          // lvalue = rvalue;
          else
          {
            var lnodes = nodes.GetRange(0, firstEquals);
            var rnodes = nodes.GetRange(firstEquals + 1, nextEndLine - (firstEquals + 1));
            nodes.RemoveRange(0, nextEndLine + 1);

            var rvalue = GetRValue(scope, rnodes, ref byteCodeState);

            if (rvalue.type is VoidCType || rvalue.type is ArrayCType)
              Error($"Type '{rvalue.type}' is illegal for an rvalue.", rnodes[0].file, rnodes[1].line);

            var lvalue = GetLValue(scope, lnodes, ref byteCodeState, rvalue.type);

            // TODO: Assign value.

            rvalue.isInitialized = true;
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
        scope.instructions.Add(new CInstruction_GotoLabel(scope.afterLabel, null, 0));
      }

      foreach (var instruction in scope.instructions)
        instruction.GetLLInstructions(ref byteCodeState);

      foreach (CFunction function in scope.GetLocalFunctions())
      {
        if (function is CBuiltInFunction)
          continue;

        Scope childScope = scope.GetChildScope(function);

        CompileScope(childScope, function.nodes, ref byteCodeState);
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
      else if (nodes[0] is NName)
      {
        var value = scope.GetVariable((nodes[0] as NName).name);

        if (value != null)
        {
          if (nodes.Count > 1)
          {
            // TODO: '.', '->', '('
            throw new NotImplementedException();
          }
          else
          {
            return value;
          }
        }
        else
        {
          var function = scope.GetFunction((nodes[0] as NName).name);
          var nameNode = nodes[0];

          if (function == null)
            Error($"Unknown identifer '{(nodes[0] as NName).name}'.", nameNode.file, nameNode.line);

          if (!(function.returnType is VoidCType))
            Warn($"lvalue call to {function} will discard the return value.", nameNode.file, nameNode.line);

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
              int nextCommaOrClosingParenthesis = nodes.FindNextSelfScope(n => n is NComma || n is NCloseParanthesis);

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

              parameters.Add(GetRValue(scope, parameterNodes, ref byteCodeState));

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
      if (nodes[0] is NName)
      {
        var value = scope.GetVariable((nodes[0] as NName).name);

        if (value != null)
        {
          if (nodes.Count > 1)
          {
            // TODO: '.', '->', '('
            throw new NotImplementedException();
          }
          else
          {
            return value;
          }
        }
        else
        {
          var function = scope.GetFunction((nodes[0] as NName).name);
          var nameNode = nodes[0];

          if (function == null)
            Error($"Unknown identifer '{(nodes[0] as NName).name}'.", nameNode.file, nameNode.line);

          if (function.returnType is VoidCType)
            Error($"Invalid return type '{function.returnType}' of rvalue function call to '{function}'.", nameNode.file, nameNode.line);

          if (nodes.Count == 1 || !(nodes[1] is NOpenParanthesis))
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
              int nextCommaOrClosingParenthesis = nodes.FindNextSelfScope(n => n is NComma || n is NCloseParanthesis);

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

              parameters.Add(GetRValue(scope, parameterNodes, ref byteCodeState));

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
  }
}
