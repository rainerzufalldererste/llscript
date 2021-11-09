using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace llsc
{
  public enum DbgType
  {
    DT_Other,
    DT_U8,
    DT_U16,
    DT_U32,
    DT_U64,
    DT_I8,
    DT_I16,
    DT_I32,
    DT_I64,
    DT_F32,
    DT_F64,
    DT_U8Ptr,
    DT_U16Ptr,
    DT_U32Ptr,
    DT_U64Ptr,
    DT_I8Ptr,
    DT_I16Ptr,
    DT_I32Ptr,
    DT_I64Ptr,
    DT_F32Ptr,
    DT_F64Ptr
  }

  public class DbgLocationInfo
  {
    public string name;
    public DbgType type;
    public bool isVariable;
    public Position position;
    public SharedValue<long> stackSize;

    public DbgLocationInfo(CValue value, SharedValue<long> stackSize)
    {
      if (!value.hasPosition)
        throw new Exception("Internal Compiler Error!");

      isVariable = value is CNamedValue;

      if (isVariable)
        name = (value as CNamedValue).name;
      else
        name = value.ToString();

      if (value.type is PtrCType)
      {
        type = DbgTypeHelper((value.type as PtrCType).pointsTo);

        if (type >= DbgType.DT_U8 && type <= DbgType.DT_F64)
          type = type + (DbgType.DT_U8Ptr - DbgType.DT_U8);
      }
      else if (value.type is ArrayCType)
      {
        type = DbgTypeHelper((value.type as ArrayCType).type);

        if (type >= DbgType.DT_U8 && type <= DbgType.DT_F64)
          type = type + (DbgType.DT_U8Ptr - DbgType.DT_U8);
      }
      else
      {
        type = DbgTypeHelper(value.type);
      }

      position = value.position;
      this.stackSize = stackSize;
    }

    public List<byte> GetBytes()
    {
      List<byte> bytes = new List<byte>();

      bytes.Add((byte)type);
      bytes.Add(position.inRegister ? (byte)1 : (byte)0);

      if (position.inRegister)
        bytes.AddRange(BitConverter.GetBytes(position.inRegister ? (long)1 : (long)0));
      else
        bytes.AddRange(BitConverter.GetBytes(stackSize.Value - position.stackOffsetForward));

      bytes.AddRange(Encoding.UTF8.GetBytes(name));
      bytes.Add(0);

      return bytes;
    }

    static DbgType DbgTypeHelper(CType type)
    {
      if (!(type is BuiltInCType))
      {
        return DbgType.DT_Other;
      }
      else
      {
        BuiltInCType t = (BuiltInCType)type;

        if (t.IsFloat())
        {
          switch (t.GetSize())
          {
            case 4:
              return DbgType.DT_F32;

            case 8:
              return DbgType.DT_F64;

            default: throw new Exception("Unexpected Type.");
          }
        }
        else if (t.IsUnsigned())
        {
          switch (t.GetSize())
          {
            case 1:
              return DbgType.DT_U8;

            case 2:
              return DbgType.DT_U16;

            case 4:
              return DbgType.DT_U32;

            case 5:
              return DbgType.DT_U64;

            default: throw new Exception("Unexpected Type.");
          }
        }
        else
        {
          switch (t.GetSize())
          {
            case 1:
              return DbgType.DT_I8;

            case 2:
              return DbgType.DT_I16;

            case 4:
              return DbgType.DT_I32;

            case 5:
              return DbgType.DT_I64;

            default: throw new Exception("Unexpected Type.");
          }
        }
      }
    }
  }

  public class DbgDatabase
  {
    public static void Write(string outFileName, IEnumerable<FileContents> files, ByteCodeState byteCodeState)
    {
      List<Tuple<ulong, byte[]>> entries = new List<Tuple<ulong, byte[]>>();

      string lastFile = null;
      int lastLine = -1;
      
      string line = "";
      List<string> comments = new List<string>();
      List<DbgLocationInfo> locationInfo = new List<DbgLocationInfo>();

      foreach (var instruction in byteCodeState.instructions)
      {
        if (instruction is LLI_Location_PseudoInstruction && (instruction as LLI_Location_PseudoInstruction).locationInfo.isVariable && (instruction as LLI_Location_PseudoInstruction).locationInfo.type != DbgType.DT_Other)
        {
          var info = (instruction as LLI_Location_PseudoInstruction).locationInfo;

          if (info != null)
            locationInfo.Add(info);

          continue;
        }
        else if (instruction is LLI_PseudoInstruction)
        {
          comments.Add(instruction.ToString());

          continue;
        }

        bool printLine = false;
        bool printedFile = false;

        if (lastFile != instruction.file)
        {
          printLine = true;
          printedFile = true;
          lastFile = instruction.file;
          lastLine = -1;
          line += "File: " + (lastFile == null ? "<compiler internal>" : lastFile);
        }

        if (lastLine != instruction.line)
        {
          printLine = true;
          lastLine = instruction.line;
        }

        if (printLine)
        {
          var file = (from x in files where x.filename == lastFile select x.lines).FirstOrDefault();

          if (file != null && file.Length > lastLine && lastLine >= 0)
            line += ((printedFile ? "\r\n" : "") + $"{lastLine + 1:   0}: {file[lastLine]}\r\n");
        }

        if (instruction.bytecodeSize != 0)
        {
          if (!string.IsNullOrEmpty(line) || comments.Count != 0 || locationInfo.Count != 0)
          {
            List<byte> header = new List<byte>();
            List<byte> contents = new List<byte>();

            header.AddRange(BitConverter.GetBytes((ulong)(string.IsNullOrEmpty(line) ? 0 : 1)));
            header.AddRange(BitConverter.GetBytes((ulong)comments.Count));
            header.AddRange(BitConverter.GetBytes((ulong)locationInfo.Count));

            if (!string.IsNullOrEmpty(line))
            {
              header.AddRange(BitConverter.GetBytes((ulong)contents.Count));
              contents.AddRange(Encoding.UTF8.GetBytes(line));
              contents.Add(0);
            }

            foreach (var comment in comments)
            {
              header.AddRange(BitConverter.GetBytes((ulong)contents.Count));
              contents.AddRange(Encoding.UTF8.GetBytes(comment));
              contents.Add(0);
            }

            foreach (var info in locationInfo)
            {
              header.AddRange(BitConverter.GetBytes((ulong)contents.Count));
              contents.AddRange(info.GetBytes());
            }

            header.AddRange(contents);
            entries.Add(Tuple.Create(instruction.position, header.ToArray()));

            line = "";
            comments = new List<string>();
            locationInfo = new List<DbgLocationInfo>();
          }
        }
      }

      // Write File Contents.
      {
        List<byte> header = new List<byte>();
        List<byte> body = new List<byte>();

        header.AddRange(BitConverter.GetBytes((ulong)0));
        header.AddRange(BitConverter.GetBytes((ulong)entries.Count));

        foreach (var entry in entries)
        {
          body.AddRange(entry.Item2);

          header.AddRange(BitConverter.GetBytes(entry.Item1));
          header.AddRange(BitConverter.GetBytes((ulong)body.Count));
        }

        header.AddRange(body);

        File.WriteAllBytes(outFileName + ".dbg", header.ToArray());
        Console.WriteLine($"Successfully wrote debug database to '{outFileName}.dbg'.");
      }
    }
  }
}
