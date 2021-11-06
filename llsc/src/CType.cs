using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace llsc
{
  public abstract class CType
  {
    public CType explicitCast = null;

    public abstract long GetSize();

    public override bool Equals(object obj) => false;

    public override int GetHashCode() => base.GetHashCode();

    public static bool operator ==(CType a, CType b)
    {
      if (object.ReferenceEquals(a, null) ^ object.ReferenceEquals(b, null))
        return false;

      if (object.ReferenceEquals(a, b))
        return true;

      return a.Equals(b);
    }

    public static bool operator !=(CType a, CType b)
    {
      if (object.ReferenceEquals(a, null) ^ object.ReferenceEquals(b, null))
        return true;

      if (object.ReferenceEquals(a, b))
        return false;

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
          case BuiltInTypes.i8:
            unchecked { return new byte[] { (byte)value.uint_value }; }

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
            Compiler.Error($"In Struct '{this.name}' (File: '{this.file}', Line {this.line + 1}) member {(i + 1)} (of type {this.attributes[i].type}) and {(j + 1)} (of type {this.attributes[j].type}) have the name '{this.attributes[j].name}'.", this.attributes[j].file, this.attributes[j].line);

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
}
