using System;

namespace llsc
{
  public enum PositionType
  {
    OnStack,
    InRegister,
    GlobalStackOffset,
    CodeBaseOffset,
  }

  public struct Position
  {
    public PositionType type;
    public int registerIndex;
    public long stackOffsetForward;
    public long globalStackBaseOffset;
    public LLI_Data_PseudoInstruction codeBaseOffset;

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
  }
}
