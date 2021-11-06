using System;
using System.Collections.Generic;

namespace llsc
{
  public class FileContents
  {
    public string filename;
    public string[] lines;

    public List<Node> nodes = new List<Node>();
  }
}
