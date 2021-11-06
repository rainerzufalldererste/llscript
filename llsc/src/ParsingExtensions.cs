using System;
using System.Collections.Generic;

namespace llsc
{
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
      if (s.Length >= startIndex + next.Length)
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

    public static int FindNextSameScope(this List<Node> nodes, int start, Func<Node, bool> check)
    {
      int bracketLevel = 0;
      int parantesisLevel = 0;
      int braceLevel = 0;

      for (int i = start; i < nodes.Count; i++)
      {
        if (nodes[i] is NOpenParanthesis)
          ++parantesisLevel;
        else if (nodes[i] is NCloseParanthesis)
          --parantesisLevel;
        else if (nodes[i] is NOpenBracket)
          ++bracketLevel;
        else if (nodes[i] is NCloseBracket)
          --bracketLevel;
        else if (nodes[i] is NOpenScope)
          ++braceLevel;
        else if (nodes[i] is NCloseScope)
          --braceLevel;

        if (bracketLevel <= 0 && parantesisLevel <= 0 && braceLevel <= 0 && check(nodes[i]))
          return i;
      }

      return -1;
    }

    public static int FindNextSameScope(this List<Node> nodes, Func<Node, bool> check) => FindNextSameScope(nodes, 0, check);
    
    public static string ElementsToString(this IEnumerable<byte> list, string format)
    {
      string ret = "[";
      bool first = true;

      foreach (var x in list)
      {
        if (first)
          first = !first;
        else
          ret += ", ";

        ret += x.ToString(format);
      }

      return ret + "]";
    }
  }
}
