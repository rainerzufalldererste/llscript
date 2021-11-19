using System;
using System.Collections.Generic;

namespace llsc
{
  public enum PositionType
  {
    Invalid,
    InRegister,
    OnStack,
    GlobalStackOffset,
    CodeBaseOffset,
  }

  public struct Position
  {
    private int _registerIndex;
    private long _stackOffsetForward;
    private long _globalStackBaseOffset;
    private LLI_Data_PseudoInstruction _codeBaseOffset;

    public PositionType type;

    public int registerIndex
    {
      get
      {
        if (type != PositionType.InRegister)
          throw new Exception("Invalid Position Type");

        return _registerIndex;
      }

      set 
      {
        if (type != PositionType.InRegister)
          throw new Exception("Invalid Position Type");

        _registerIndex = value;
      } 
    }

    public long stackOffsetForward
    {
      get
      {
        if (type != PositionType.OnStack)
          throw new Exception("Invalid Position Type");

        return _stackOffsetForward;
      }

      set
      {
        if (type != PositionType.OnStack)
          throw new Exception("Invalid Position Type");

        _stackOffsetForward = value;
      }
    }

    public long globalStackBaseOffset
    {
      get
      {
        if (type != PositionType.GlobalStackOffset)
          throw new Exception("Invalid Position Type");

        return _globalStackBaseOffset;
      }

      set
      {
        if (type != PositionType.GlobalStackOffset)
          throw new Exception("Invalid Position Type");

        _globalStackBaseOffset = value;
      }
    }

    public LLI_Data_PseudoInstruction codeBaseOffset
    {
      get
      {
        if (type != PositionType.CodeBaseOffset)
          throw new Exception("Invalid Position Type");

        return _codeBaseOffset;
      }

      set
      {
        if (type != PositionType.CodeBaseOffset)
          throw new Exception("Invalid Position Type");

        _codeBaseOffset = value;
      }
    }


    public static Position Register(int registerIndex)
    {
      Position ret = new Position();

      ret.type = PositionType.InRegister;
      ret.registerIndex = registerIndex;

      return ret;
    }

    public static Position StackOffset(long stackOffsetForward)
    {
      Position ret = new Position();

      ret.type = PositionType.OnStack;
      ret.stackOffsetForward = stackOffsetForward;

      return ret;
    }

    public static Position GlobalStackBaseOffset(long stackBaseOffset)
    {
      Position ret = new Position();

      ret.type = PositionType.GlobalStackOffset;
      ret.globalStackBaseOffset = stackBaseOffset;

      return ret;
    }

    public static Position CodeBaseOffset(CValue value, byte[] data, string file, int line)
    {
      Position ret = new Position();

      ret.type = PositionType.CodeBaseOffset;
      ret.codeBaseOffset = new LLI_Data_PseudoInstruction($"{value} defined in {value.file}:{value.line + 1} (assigned in {file}:{line + 1})", data);

      return ret;
    }

    public static bool operator ==(Position a, Position b)
    {
      if (a.type != b.type)
        return false;

      switch (a.type)
      {
        default:
          throw new Exception("Invalid Position Type.");

        case PositionType.InRegister:
          return a.registerIndex == b.registerIndex;

        case PositionType.OnStack:
          return a.stackOffsetForward == b.stackOffsetForward;

        case PositionType.GlobalStackOffset:
          return a.globalStackBaseOffset == b.globalStackBaseOffset;

        case PositionType.CodeBaseOffset:
          return a.codeBaseOffset == b.codeBaseOffset;
      }
    }

    public static bool operator !=(Position a, Position b)
    {
      return !(a == b);
    }

    public override string ToString()
    {
      switch (type)
      {
        case PositionType.InRegister:
          return $"r:{registerIndex}";

        case PositionType.OnStack:
          return $"stackOffsetForward:{stackOffsetForward}";

        case PositionType.GlobalStackOffset:
          return $"stackBaseOffset:{globalStackBaseOffset}";

        case PositionType.CodeBaseOffset:
          return $"codeBaseOffset:({codeBaseOffset.description})" + (codeBaseOffset.position != 0 ? $" @{codeBaseOffset.position}" : "");

        default:
          throw new Exception("Invalid Position Type. Internal Compiler Error.");
      }
    }

    public override bool Equals(object obj)
    {
      return obj is Position position && this == position;
    }

    public override int GetHashCode()
    {
      int hashCode = 91695821;
      hashCode = hashCode * -1521134295 + type.GetHashCode();

      switch (type)
      {
        case PositionType.InRegister:
          return hashCode * -1521134295 + _registerIndex.GetHashCode();

        case PositionType.OnStack:
          return hashCode * -1521134295 + _stackOffsetForward.GetHashCode();

        case PositionType.GlobalStackOffset:
          return hashCode * -1521134295 + _globalStackBaseOffset.GetHashCode();

        case PositionType.CodeBaseOffset:
          return hashCode * -1521134295 + EqualityComparer<LLI_Data_PseudoInstruction>.Default.GetHashCode(_codeBaseOffset);
          
        default:
          throw new Exception("Invalid Position Type. Internal Compiler Error.");
      }
    }
  }
}
