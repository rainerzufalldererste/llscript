using System;
using System.Reflection;

namespace llsc
{
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

    public CType smallestPossibleSignedType { get; protected set; } = null;

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

    public CConstIntValue(NIntegerValue value) : base()
    {
      BuiltInCType _type = null;

      if (value.isForcefullyNegative)
      {
        if (value.int_value >= (long)sbyte.MinValue)
          smallestPossibleSignedType = _type = BuiltInCType.Types["i8"];
        else if (value.int_value >= (long)short.MinValue)
          smallestPossibleSignedType = _type = BuiltInCType.Types["i16"];
        else if (value.int_value >= (long)int.MinValue)
          smallestPossibleSignedType = _type = BuiltInCType.Types["i32"];
        else
          smallestPossibleSignedType = _type = BuiltInCType.Types["i64"];
      }
      else
      {
        if (value.uint_value <= (ulong)byte.MaxValue)
        {
          _type = BuiltInCType.Types["u8"];
          smallestPossibleSignedType = value.uint_value <= (ulong)byte.MaxValue ? BuiltInCType.Types["i8"] : BuiltInCType.Types["i16"];
        }
        else if (value.uint_value <= (ulong)ushort.MaxValue)
        {
          _type = BuiltInCType.Types["u16"];
          smallestPossibleSignedType = value.uint_value <= (ulong)short.MaxValue ? BuiltInCType.Types["i16"] : BuiltInCType.Types["i32"];
        }
        else if (value.uint_value <= (ulong)uint.MaxValue)
        {
          _type = BuiltInCType.Types["u32"];
          smallestPossibleSignedType = value.uint_value <= (ulong)int.MaxValue ? BuiltInCType.Types["i32"] : BuiltInCType.Types["i64"];
        }
        else
        {
          _type = BuiltInCType.Types["u64"];
          smallestPossibleSignedType = value.uint_value <= (ulong)long.MaxValue ? BuiltInCType.Types["i64"] : null;
        }
      }

      base.file = value.file;
      base.line = value.line;
      base.type = _type;
      base.isConst = true;
      base.isInitialized = true;
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
      ret.smallestPossibleSignedType = smallestPossibleSignedType;

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
}
