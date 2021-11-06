using System;
using System.Reflection;

namespace llsc
{
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
}
