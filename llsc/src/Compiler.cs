using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace llsc
{
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
    public readonly int length;

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

      this.length = Encoding.UTF8.GetByteCount(this.value) + 1;
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
    public abstract ulong GetSize();

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

    public override ulong GetSize() => 0;

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

    public override ulong GetSize()
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
    
    public override string ToString() => type.ToString();
  }

  public class PtrCType : CType
  {
    public readonly CType pointsTo;

    public PtrCType(CType pointsTo)
    {
      this.pointsTo = pointsTo;
    }

    public override ulong GetSize() => 8;
    public override bool Equals(object obj) => (obj is PtrCType && (obj as PtrCType).pointsTo == pointsTo);

    public override int GetHashCode() => -(pointsTo.GetHashCode() + 1);

    public override string ToString() => "ptr<" + pointsTo.ToString() + ">";
  }

  public class ArrayCType : CType
  {
    public readonly CType type;
    public readonly ulong count;

    public ArrayCType(CType type, ulong count)
    {
      this.type = type;
      this.count = count;
    }

    public override ulong GetSize() => count * type.GetSize();

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

    public override ulong GetSize() => 8;

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
    private readonly ulong size;

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

    public override ulong GetSize()
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
    public ulong offset;

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

    public int remainingReferences = 0;
    public readonly bool isConst;

    public CValue(string file, int line, CType type, bool isConst)
    {
      this.file = file;
      this.line = line;
      this.type = type;
      this.isConst = isConst;
    }
  }

  public class CConstValue : CValue
  {
    public readonly ulong uvalue;
    public readonly long ivalue;

    public CConstValue(NIntegerValue value, CType type) : base(value.file, value.line, type, true)
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
  }

  public class CNamedValue : CValue
  {
    public readonly string name;
    public int stackOffset;

    public CNamedValue(NName name, CType type, bool isConst) : base(name.file, name.line, type, isConst)
    {
      this.name = name.name;
    }
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
  }

  public class CFunction
  {
    public readonly string file;
    public readonly int line;
    public readonly string name;

    public readonly CType returnType;
    public readonly Tuple<string, CType>[] parameters;

    public CFunction(string name, CType returnType, IEnumerable<Tuple<string, CType>> parameters, string file, int line)
    {
      this.name = name;
      this.returnType = returnType;
      this.parameters = parameters.ToArray();
      this.file = file;
      this.line = line;
    }
  }

  public struct LabelReference
  {
    public readonly int patchValueIndex;
    public readonly int byteCodeIndexPosition;

    public LabelReference(int patchValueIndex, int byteCodeIndexPosition)
    {
      this.patchValueIndex = patchValueIndex;
      this.byteCodeIndexPosition = byteCodeIndexPosition;
    }
  }

  public class ByteCodeState
  {
    public Dictionary<int, ulong> patchValues = new Dictionary<int, ulong>();
    public List<LabelReference> valuesToPatch = new List<LabelReference>();
    public List<byte> byteCode = new List<byte>();

    public CValue[] iregisters = new CValue[8];
    public CValue[] fregisters = new CValue[8];
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

    public abstract void AppendBytecode(ref ByteCodeState byteCodeState);
  }

  public class Scope
  {
    public int maxRequiredStackSpace = 0;

    public readonly Scope parentScope;
    public readonly bool isFunction;

    private Dictionary<string, StructCType> definedStructs;
    private Dictionary<string, CFunction> definedFunctions;
    private Dictionary<string, CNamedValue> definedVariables;

    public List<CInstruction> instructions = new List<CInstruction>();

    /// <summary>
    /// Create Global Scope.
    /// </summary>
    public Scope()
    {
      isFunction = false;
      parentScope = null;
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
  }

  static class Compiler
  {
    static bool WarningsAsErrors = false;
    static bool ShowWarnings = true;

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

        // TODO: Patch ByteCode.

        File.WriteAllBytes(outFileName, byteCodeState.byteCode.ToArray());
      }
      catch (CompileFailureException)
      {
        Console.WriteLine("Compilation Failed.");

        Environment.Exit(1);
      }
      catch (Exception e)
      {
        Console.WriteLine("Internal Compiler Error.\n\n" + e.ToString());

        Environment.Exit(-1);
      }
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
                  if (" .,;=!<>()[]{}+-*/%^&|~".Contains(lineString[start]))
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

            case "text":
              file.nodes[i] = new NType(new PtrCType(BuiltInCType.Types["i8"]), node.file, node.line);
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
          nodes.Insert(i, new NType(new ArrayCType(type.type, size.uint_value), start.file, start.line));
        }
      }
    }

    private static void CompileScope(Scope scope, List<Node> nodes, ref ByteCodeState byteCodeState)
    {
      while (nodes.Count > 0)
      {
        // Struct.
        if (nodes[0] is NLineEnd)
        {
          nodes.RemoveAt(0);
        }
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
        // Unsized Array.
        else if (nodes.NextIs(typeof(NArrayKeyword), typeof(NOperator), typeof(NType), typeof(NOperator), typeof(NName), typeof(NOperator), typeof(NOpenScope)) && ((nodes[1] as NOperator).operatorType == "<" && (nodes[3] as NOperator).operatorType == ">" && (nodes[5] as NOperator).operatorType == "="))
        {
          if (!((nodes[2] as NType).type is BuiltInCType))
            Error($"Invalid Type '{(nodes[2] as NType).type.ToString()}'. Dynamically Sized Arrays can only contain builtin types.", nodes[2].file, nodes[2].line);

          // TODO: Implement.
        }
        else
        {
          Error($"Unexpected {nodes[0]}.", nodes[0].file, nodes[1].line);
        }
      }
    }
  }
}
