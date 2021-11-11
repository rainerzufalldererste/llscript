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
    DT_OtherPtr,
    DT_U8Ptr,
    DT_U16Ptr,
    DT_U32Ptr,
    DT_U64Ptr,
    DT_I8Ptr,
    DT_I16Ptr,
    DT_I32Ptr,
    DT_I64Ptr,
    DT_F32Ptr,
    DT_F64Ptr,
    DT_OtherArray,
    DT_U8Array,
    DT_U16Array,
    DT_U32Array,
    DT_U64Array,
    DT_I8Array,
    DT_I16Array,
    DT_I32Array,
    DT_I64Array,
    DT_F32Array,
    DT_F64Array
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
        else
          type = DbgType.DT_OtherPtr;
      }
      else if (value.type is ArrayCType)
      {
        type = DbgTypeHelper((value.type as ArrayCType).type);

        if (type >= DbgType.DT_U8 && type <= DbgType.DT_F64)
          type = type + (DbgType.DT_U8Array - DbgType.DT_U8);
        else
          type = DbgType.DT_OtherArray;
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
      bytes.Add(isVariable ? (byte)1 : (byte)0) ;

      if (position.inRegister)
        bytes.AddRange(BitConverter.GetBytes((ulong)position.registerIndex));
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

  public class DbgDatabaseEntry
  {
    public ulong offset;
    public string line = "";
    public List<string> comments = new List<string>();
    public List<DbgLocationInfo> locationInfo = new List<DbgLocationInfo>();

    public DbgDatabaseEntry(ulong offset)
    {
      this.offset = offset;
    }

    public List<byte> GetBytes()
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

        return header;
      }

      return null;
    }
  }

  public class DbgDatabase
  {
    public static void Write(string outFileName, IEnumerable<FileContents> files, ByteCodeState byteCodeState)
    {
      List<DbgDatabaseEntry> entries = new List<DbgDatabaseEntry>();
      DbgDatabaseEntry current = new DbgDatabaseEntry(0);
      DbgDatabaseEntry last = current;

      string lastFile = null;
      int lastLine = -1;
      
      foreach (var instruction in byteCodeState.instructions)
      {
        if (instruction is LLI_Location_PseudoInstruction /* && (instruction as LLI_Location_PseudoInstruction).locationInfo.isVariable && (instruction as LLI_Location_PseudoInstruction).locationInfo.type != DbgType.DT_Other */)
        {
          var info = (instruction as LLI_Location_PseudoInstruction).locationInfo;

          if (info != null)
            last.locationInfo.Add(info);

          continue;
        }
        else if (instruction is LLI_PseudoInstruction)
        {
          current.comments.Add(instruction.ToString());

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
          current.line += "File: " + (lastFile == null ? "<compiler internal>" : lastFile);
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
            current.line += ((printedFile ? "\r\n" : "") + $"{lastLine + 1:   0}: {file[lastLine]}\r\n");
        }

        if (instruction.bytecodeSize != 0)
        {
          current.offset = instruction.position;

          entries.Add(current);
          last = current;
          current = new DbgDatabaseEntry(instruction.position + instruction.bytecodeSize);
        }
      }

      // Write File Contents.
      {
        List<byte> header = new List<byte>();
        List<byte> body = new List<byte>();

        header.AddRange(BitConverter.GetBytes((ulong)1)); // Debug Info Version.

        ulong count = 0;

        foreach (var entry in entries)
        {
          if (!string.IsNullOrEmpty(entry.line) || entry.comments.Count != 0 || entry.locationInfo.Count != 0)
            count++;
        }

        header.AddRange(BitConverter.GetBytes(count));

        foreach (var entry in entries)
        {
          var bytes = entry.GetBytes();

          if (bytes == null)
            continue;

          header.AddRange(BitConverter.GetBytes(entry.offset));
          header.AddRange(BitConverter.GetBytes((ulong)body.Count));

          body.AddRange(bytes);
        }

        header.AddRange(body);

        File.WriteAllBytes(outFileName + ".dbg", header.ToArray());
        Console.WriteLine($"Successfully wrote debug database to '{outFileName}.dbg'.");
      }
    }
  }
}
