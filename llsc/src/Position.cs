using System;

namespace llsc
{
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
}
