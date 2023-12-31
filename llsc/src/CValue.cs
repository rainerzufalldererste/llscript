﻿using System;
using System.Reflection;

namespace llsc
{
  public class CValue
  {
    public CType type { get; set; }

    public string description = "";

    public string file { get; protected set; }

    public int line { get; protected set; }

    public bool isInitialized = false;
    public Position position;
    public bool hasPosition = false;

    public int remainingReferences = 0;

    public int lastTouchedInstructionCount = 0;

    private static int lastIndex = 0;
    private readonly int index = ++lastIndex;

    protected CValue()
    {

    }

    public CValue(string file, int line, CType type, bool isInitialized) : base()
    {
      this.file = file;
      this.line = line;
      this.type = type;
      this.isInitialized = isInitialized;
    }

    public override string ToString() => $"unnamed value #{index} [" + type + (type.explicitCast != null ? $" (as '{type.explicitCast}') " : "") + (string.IsNullOrWhiteSpace(description) ? "" : $" '{description}'") + "]";

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
      ret.lastTouchedInstructionCount = lastTouchedInstructionCount;
      ret.description = description;

      scope.instructions.Add(new CInstruction_CopyPositionFromValueToValue(this, ret, scope.maxRequiredStackSpace, file, line));

      return ret;
    }

    public virtual CValue MakeCastableClone(CType targetType, Scope scope, ref ByteCodeState byteCodeState, string file, int line)
    {
      var ret = this.DeepClone(scope, ref byteCodeState);

      ret.type = type.MakeCastableClone(targetType);
      ret.description = $"(castable clone of {this})";

      return ret;
    }

    public static bool IsPowerOfTwo(ulong x)
    {
      return (x != 0) && ((x & (x - 1)) == 0);
    }
  }

  public class CConstIntValue : CValue
  {
    public ulong uvalue { get; set; }

    public long ivalue { get; set; }

    public CType smallestPossibleSignedType { get; protected set; } = null;

    protected CConstIntValue() : base() { }

    public CConstIntValue(ulong value, CType type, string file, int line) : base(file, line, type, true)
    {
      this.uvalue = value;
      unchecked { this.ivalue = (long)value; }

      base.type = base.type.MakeCastableClone(base.type);
      base.type.explicitCast = null;
      base.type.isConst = true;
    }

    public CConstIntValue(NIntegerValue value, CType type) : base(value.file, value.line, type, true)
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

      base.type = base.type.MakeCastableClone(base.type);
      base.type.explicitCast = null;
      base.type.isConst = true;
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

      if (!value.isForcefullyNegative)
        uvalue = value.uint_value;

      ivalue = value.int_value;

      base.file = value.file;
      base.line = value.line;
      base.type = _type;
      base.isInitialized = true;

      base.type = base.type.MakeCastableClone(base.type);
      base.type.explicitCast = null;
      base.type.isConst = true;

      if (smallestPossibleSignedType != null)
      {
        smallestPossibleSignedType = smallestPossibleSignedType.MakeCastableClone(smallestPossibleSignedType);
        smallestPossibleSignedType.explicitCast = null;
        smallestPossibleSignedType.isConst = true;
      }
    }

    public override string ToString() => "unnamed immediate value [" + type + (type.explicitCast != null ? $" (as '{type.explicitCast}') " : "") + "] (" + ((type as BuiltInCType).IsUnsigned() ? uvalue.ToString() : ivalue.ToString()) + ")" + (string.IsNullOrWhiteSpace(description) ? "" : $" ('{description}')");

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
      ret.lastTouchedInstructionCount = lastTouchedInstructionCount;
      ret.description = description;

      ret.uvalue = uvalue;
      ret.ivalue = ivalue;
      ret.smallestPossibleSignedType = smallestPossibleSignedType;

      if (ret.hasPosition)
        scope.instructions.Add(new CInstruction_CopyPositionFromValueToValue(this, ret, scope.maxRequiredStackSpace, file, line));

      return ret;
    }
  }

  public class CConstFloatValue : CValue
  {
    public double value { get; set; }

    protected CConstFloatValue() : base() { }

    public CConstFloatValue(double value, CType type, string file, int line) : base(file, line, type, true)
    {
      this.value = value;

      base.type = base.type.MakeCastableClone(base.type);
      base.type.explicitCast = null;
      base.type.isConst = true;
    }

    public CConstFloatValue(NFloatingPointValue value, CType type) : base(value.file, value.line, type, true)
    {
      if (!(type is BuiltInCType) || !(type as BuiltInCType).IsFloat())
        Compiler.Error($"Invalid Constant Value. Floating point value cannot be assigned to type '{ type.ToString() }'.", value.file, value.line);

      this.value = value.value;

      base.type = base.type.MakeCastableClone(base.type);
      base.type.explicitCast = null;
      base.type.isConst = true;
    }

    public override string ToString() => "unnamed immediate value [" + type + (type.explicitCast != null ? $" (as '{type.explicitCast}') " : "") + "] (" + value.ToString() + ")" + (string.IsNullOrWhiteSpace(description) ? "" : $" ('{description}')");

    public override CValue DeepClone(Scope scope, ref ByteCodeState byteCodeState)
    {
      var ret = new CConstFloatValue();
      ret.isInitialized = isInitialized;
      ret.position = position;
      ret.hasPosition = hasPosition;
      ret.remainingReferences = remainingReferences;
      ret.lastTouchedInstructionCount = lastTouchedInstructionCount;
      ret.description = description;

      ret.value = value;

      if (ret.hasPosition)
        scope.instructions.Add(new CInstruction_CopyPositionFromValueToValue(this, ret, scope.maxRequiredStackSpace, file, line));

      return ret;
    }
  }

  public class CNamedValue : CValue
  {
    public string name { get; protected set; }
    public bool hasHomePosition = false;
    public Position homePosition;
    public bool modifiedSinceLastHome = false;
    public bool isVolatile = false;
    public bool isStatic = false;

    protected CNamedValue() : base() { }

    public CNamedValue(NName name, CType type, bool isInitialized) : base(name.file, name.line, type, isInitialized)
    {
      this.name = name.name;
    }

    public CNamedValue(string name, CType type, bool isInitialized, string file, int line) : base(file, line, type, isInitialized)
    {
      this.name = name;
    }

    public override string ToString() => type + (type.explicitCast != null ? $" (as '{type.explicitCast}') " : "") + " " + name + (string.IsNullOrWhiteSpace(description) ? "" : $" ('{description}')");

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
      ret.lastTouchedInstructionCount = lastTouchedInstructionCount;
      ret.description = description;

      ret.name = name;
      ret.hasHomePosition = false;
      ret.modifiedSinceLastHome = false;

      scope.instructions.Add(new CInstruction_CopyPositionFromValueToValue(this, ret, scope.maxRequiredStackSpace, file, line));

      return ret;
    }

    public override CValue MakeCastableClone(CType targetType, Scope scope, ref ByteCodeState byteCodeState, string file, int line)
    {
      var ret = new CValue(file, line, type.MakeCastableClone(targetType), isInitialized)
      {
        description = $"castable clone of '{this}'",
      };

      scope.instructions.Add(new CInstruction_CopyPositionFromValueToValue(this, ret, scope.maxRequiredStackSpace, file, line));

      return ret;
    }
  }

  public class CGlobalValueReference : CValue
  {
    public bool isModified = false;

    public CGlobalValueReference(CType type, string file, int line) : base(file, line, type, true)
    {

    }

    public override string ToString() => "unnamed global (?) value [" + type + (type.explicitCast != null ? $" (as '{type.explicitCast}') " : "") + (string.IsNullOrWhiteSpace(description) ? "" : $" '{description}'") + "]";
  }

  public class CNullValue : CValue
  {
    public CNullValue(string file, int line) : base(file, line, new PtrCType(VoidCType.Instance) { isConst = true }, true)
    {

    }
  }
}
