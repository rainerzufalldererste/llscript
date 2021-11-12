using System;
using System.Reflection;
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

  public class NCharKeyword : Node
  {
    public NCharKeyword(string file, int line) : base(file, line) { }

    public override string ToString() => "'char'";
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

  public class NReturnKeyword : Node
  {
    public NReturnKeyword(string file, int line) : base(file, line) { }

    public override string ToString() => "'return'";
  }

  public class NBreakKeyword : Node
  {
    public NBreakKeyword(string file, int line) : base(file, line) { }

    public override string ToString() => "'break'";
  }

  public class NContinueKeyword : Node
  {
    public NContinueKeyword(string file, int line) : base(file, line) { }

    public override string ToString() => "'continue'";
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
    Exit,
    Line,
    File
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

        case "__line":
          type = NPseudoFunctionType.Line;
          break;

        case "__file":
          type = NPseudoFunctionType.File;
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

    public NIntegerValue(bool forcefullyNegative, ulong uvalue, long value, string file, int line) : base(file, line)
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

  public class NNull : Node
  {
    public NNull(string file, int line) : base(file, line) { }

    public override string ToString() => $"null pointer";
  }
}
