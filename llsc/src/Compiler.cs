using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;

[assembly: AssemblyVersion("1.0.*")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace llsc
{
  static class Compiler
  {
    static bool WarningsAsErrors = false;
    static bool ShowWarnings = true;

    public static int OptimizationLevel { get; private set; } = 1;
    public static bool EmitAsm { get; private set; } = false;
    public static bool EmitDbgDatabse { get; private set; } = false;

    public static bool DetailedIntermediateOutput { get; private set; } = false;

    public static readonly int IntegerRegisters = 8;
    public static readonly int FloatRegisters = 8;

    public static Scope GlobalScope = new Scope();

    public static class Assumptions
    {
      public static bool ByteCodeMutable = false;
    }

    public static void Error(string error, string file, int line)
    {
      Console.ForegroundColor = ConsoleColor.Red;

      Console.Write("\nError");

      if (file != null)
        Console.Write($" (in '{file}', Line {line + 1})");

      Console.WriteLine(":\n\t" + error + "\n");
      Console.ResetColor();

      throw new CompileFailureException();
    }

    public static void Warn(string warning, string file, int line)
    {
      if (ShowWarnings)
      {
        Console.ForegroundColor = ConsoleColor.Yellow;

        Console.Write("\nWarning");

        if (file != null)
          Console.Write($" (in '{file}', Line {line + 1})");

        Console.WriteLine(":\n\t" + warning + "\n");
        Console.ResetColor();
      }

      if (WarningsAsErrors)
        Error($"Warning treated as error: '{warning}'", file, line);
    }

    [STAThread]
    static void Main(string[] args)
    {
      Console.WriteLine($"llsc - LLS Bytecode Compiler (Build Version: {Assembly.GetExecutingAssembly().GetName().Version})\n");

      string outFileName = "bytecode.lls";
      IEnumerable<FileContents> files = null;
      var byteCodeState = new ByteCodeState();

      try
      {
        foreach (var argument in (from arg in args where arg.StartsWith("-") select arg))
        {
          switch (argument)
          {
            case "-NoWarn":
              ShowWarnings = false;
              break;

            case "-FatalWarnings":
              WarningsAsErrors = true;
              break;

            case "-O0":
              OptimizationLevel = 0;
              break;

            case "-S":
              EmitAsm = true;
              break;

            case "-S+":
              EmitAsm = true;
              DetailedIntermediateOutput = true;
              break;

            case "-dbgdb":
              EmitDbgDatabse = true;
              break;

            default:
              if (argument.StartsWith("-o="))
              {
                outFileName = argument.Substring(3).Trim('\'', '\"');
              }
              else if (argument.StartsWith("-assume="))
              {
                var assumption = argument.Substring("-assume=".Length).Trim('\'', '\"');

                var field = typeof(Compiler.Assumptions).GetField(assumption);

                if (field != null)
                {
                  field.SetValue(null, true);
                }
                else
                {
                  Console.WriteLine($"Invalid assumption '{assumption}'.\n\nAvailable Assumptions:");

                  foreach (var x in typeof(Compiler.Assumptions).GetFields())
                    Console.WriteLine(x.Name);

                  Error($"Aborting.", null, 0);
                }
              }
              else
              {
                Error($"Invalid Parameter '{argument}'.", null, 0);
              }
              break;
          }
        }

        bool anyFilesCompiled = false;

        files = (from file in args where !file.StartsWith("-") select new FileContents() { filename = file, lines = File.ReadAllLines(file) });
        var allNodes = new List<Node>();

        foreach (var file in files)
        {
          anyFilesCompiled = true;

          Parse(file);
          ResolveKeywords(file);

          if (file.nodes.Count == 0)
            Warn($"[P] File '{file.filename}' didn't produce any parsed nodes.", file.filename, 0);
          else if (!(file.nodes.Last() is NLineEnd) && !(file.nodes.Last() is NCloseScope))
            Error($"File '{file}' doesn't end on either ';' or '}}'.", file.filename, file.nodes.Last().line);

          allNodes.AddRange(file.nodes);
        }

        if (!anyFilesCompiled)
          Error("No Files have been compiled.", null, 0);

        Console.WriteLine($"Parsing Succeeded. ({allNodes.Count} Nodes parsed from {files.Count()} Files)");

        if (!EmitAsm && !EmitDbgDatabse)
          files = null;

        CompileScope(GlobalScope, allNodes, ref byteCodeState);
        Console.WriteLine($"Instruction Generation Succeeded. ({byteCodeState.instructions.Count} Instructions & Pseudo-Instructions generated.)");

        byteCodeState.CompileInstructionsToBytecode();
        Console.WriteLine($"Code Generation Succeeded. ({byteCodeState.byteCode.Count} Bytes)");

        File.WriteAllBytes(outFileName, byteCodeState.byteCode.ToArray());
        Console.WriteLine($"Successfully wrote byte code to '{outFileName}'.");

        if (EmitAsm)
        {
          try
          {
            WriteDisasm(outFileName, files, byteCodeState);
          }
          catch (Exception e)
          {
            Console.WriteLine($"Failed to write disasm with internal compiler error '{e.Message}'.\n{e}");
          }
        }

        if (EmitDbgDatabse)
        {
          try
          {
            DbgDatabase.Write(outFileName, files, byteCodeState);
          }
          catch (Exception e)
          {
            Console.WriteLine($"Failed to write debug database with internal compiler error '{e.Message}'.\n{e}");
          }
        }

      }
      catch (CompileFailureException e)
      {
        try
        {
          byteCodeState.instructions.Add(new LLI_Comment_PseudoInstruction("Compilation Failed!"));

          if (EmitAsm)
            WriteDisasm(outFileName, files, byteCodeState);
        }
        catch { }

        Console.WriteLine("Compilation Failed.");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("\n" + e.ToString());
        Console.ResetColor();

        Environment.Exit(1);
      }
      catch (Exception e)
      {
        try
        {
          byteCodeState.instructions.Add(new LLI_Comment_PseudoInstruction("Compilation Failed!"));

          if (EmitAsm)
            WriteDisasm(outFileName, files, byteCodeState);
        }
        catch { }

        Console.WriteLine("Internal Compiler Error.");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("\n" + e.ToString());

        Environment.Exit(-1);
      }

      Console.WriteLine("\nCompilation Succeeded.");
    }

    private static void WriteDisasm(string outFileName, IEnumerable<FileContents> files, ByteCodeState byteCodeState)
    {
      string lastFile = null;
      int lastLine = -1;

      List<string> lines = new List<string>();

      lines.Add("# llsc bytecode disassembly output");
      lines.Add("# ================================");

      foreach (var instruction in byteCodeState.instructions)
      {
        if (instruction.bytecodeSize == 0 && !(instruction is LLI_PseudoInstruction))
          continue;

        bool printLine = false;
        bool printedFile = false;

        if (lastFile != instruction.file)
        {
          printLine = true;
          printedFile = true;
          lastFile = instruction.file;
          lastLine = -1;
          lines.Add("\r\n# File: " + (lastFile == null ? "<compiler internal>" : lastFile));
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
            lines.Add((printedFile ? "" : "\r\n") + $"# Line {lastLine + 1:   0}: {file[lastLine]}");
        }

        lines.Add($"{instruction.ToString().PadRight(90)} # 0x{instruction.position:X}");
      }

      File.WriteAllLines(outFileName + ".asm", lines);
      Console.WriteLine($"Successfully wrote disasm to '{outFileName}.asm'.");
    }

    private static void Parse(FileContents file)
    {
      try
      {
        List<Node> nodes = file.nodes;

        bool inMultilineComment = false;

        for (int line = 0; line < file.lines.Length; line++)
        {
          try
          {
            var lineString = file.lines[line];
            int start = 0;

            if (inMultilineComment)
            {
              if (lineString.FindString("*/", out start))
              {
                inMultilineComment = false;
                start += 2;
              }
              else
              {
                continue;
              }
            }

            while (start < lineString.Length)
            {
              if (lineString[start] == ' ')
              {
                start++;
              }
              else if (lineString.NextIs(start, "//"))
              {
                break;
              }
              else if (lineString.NextIs(start, "/*"))
              {
                start += 2;

                if (lineString.Substring(start).FindString("*/", out int nextStart))
                {
                  inMultilineComment = false;
                  start += 2 + nextStart;
                }
                else
                {
                  inMultilineComment = true;
                  break;
                }
              }
              else if (lineString.NextIs(start, "\""))
              {
                start++;

                bool endFound = false;

                for (int i = start; i < lineString.Length; i++)
                {
                  if (lineString[i] == '\"' && (i == start || lineString[i - 1] != '#'))
                  {
                    nodes.Add(new NStringValue(lineString.Substring(start, i - start), file.filename, line));
                    start = i + 1;
                    endFound = true;
                    break;
                  }
                }

                if (!endFound)
                  Error($"Missing end of string '{lineString.Substring(start)}'", file.filename, line);
              }
              else if (lineString[start] == '.')
              {
                nodes.Add(new NAttributeOperator(file.filename, line));
                start++;
              }
              else if (lineString.NextIs(start, "->"))
              {
                nodes.Add(new NDereferenceAttributeOperator(file.filename, line));
                start += 2;
              }
              else if (lineString.NextIs(start, "#<#") || lineString.NextIs(start, "#>#"))
              {
                nodes.Add(new NOperator(lineString.Substring(start, 3), file.filename, line));
                start += 3;
              }
              else if (lineString.NextIs(start, "==") || lineString.NextIs(start, "!=") || lineString.NextIs(start, "<=") || lineString.NextIs(start, ">=") || lineString.NextIs(start, "++") || lineString.NextIs(start, "--") || lineString.NextIs(start, "+=") || lineString.NextIs(start, "-=") || lineString.NextIs(start, "*=") || lineString.NextIs(start, "/=") || lineString.NextIs(start, "|=") || lineString.NextIs(start, "&=") || lineString.NextIs(start, "^=") || lineString.NextIs(start, "&&") || lineString.NextIs(start, "||"))
              {
                nodes.Add(new NOperator(lineString.Substring(start, 2), file.filename, line));
                start += 2;
              }
              else if (lineString[start] == '=' || lineString[start] == '!' || lineString[start] == '<' || lineString[start] == '>' || lineString[start] == '+' || lineString[start] == '-' || lineString[start] == '*' || lineString[start] == '/' || lineString[start] == '%' || lineString[start] == '&' || lineString[start] == '|' || lineString[start] == '^' || lineString[start] == '~')
              {
                nodes.Add(new NOperator(lineString.Substring(start, 1), file.filename, line));
                start++;
              }
              else if (lineString[start] == ',')
              {
                nodes.Add(new NComma(file.filename, line));
                start++;
              }
              else if (lineString[start] == '{')
              {
                nodes.Add(new NOpenScope(file.filename, line));
                start++;
              }
              else if (lineString[start] == '}')
              {
                nodes.Add(new NCloseScope(file.filename, line));
                start++;
              }
              else if (lineString[start] == '[')
              {
                nodes.Add(new NOpenBracket(file.filename, line));
                start++;
              }
              else if (lineString[start] == ']')
              {
                nodes.Add(new NCloseBracket(file.filename, line));
                start++;
              }
              else if (lineString[start] == '(')
              {
                nodes.Add(new NOpenParanthesis(file.filename, line));
                start++;
              }
              else if (lineString[start] == ')')
              {
                nodes.Add(new NCloseParanthesis(file.filename, line));
                start++;
              }
              else if (lineString[start] == ';')
              {
                nodes.Add(new NLineEnd(file.filename, line));
                start++;
              }
              else // some sort of name or value.
              {
                int originalStart = start;

                for (; start < lineString.Length; start++)
                  if (" .,;=!<>()[]{}+-*/%^&|~:".Contains(lineString[start]))
                    break;

                string foundString = lineString.Substring(originalStart, start - originalStart);

                Node node = null;

                if (null != (node = NIntegerValue.GetIntegerValue(foundString, file.filename, line)))
                {
                  nodes.Add(node);
                }
                else
                {
                  nodes.Add(new NName(foundString, file.filename, line));
                }
              }
            }
          }
          catch (CompileFailureException e)
          {
            throw e;
          }
          catch (Exception e)
          {
            Console.WriteLine($"Internal Compiler Error Parsing File '{file.filename}', Line {line + 1}.");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("\n" + e.ToString());

            Environment.Exit(-1);
          }
        }

        if (inMultilineComment)
          Warn("[P] Missing end of multiline comment ('*/').", file.filename, file.lines.Length - 1);
      }
      catch (CompileFailureException e)
      {
        throw e;
      }
      catch (Exception e)
      {
        Console.WriteLine($"Internal Compiler Error Parsing File '{file.filename}'.");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("\n" + e.ToString());

        Environment.Exit(-1);
      }
    }

    private static void ResolveKeywords(FileContents file)
    {
      for (int i = 0; i < file.nodes.Count; i++)
      {
        var node = file.nodes[i];

        if (node is NName)
        {
          switch (((NName)node).name)
          {
            case "struct":
              file.nodes[i] = new NStructKeyword(node.file, node.line);
              break;

            case "function":
              file.nodes[i] = new NFunctionKeyword(node.file, node.line);
              break;

            case "ptr":
              file.nodes[i] = new NPtrKeyword(node.file, node.line);
              break;

            case "var":
              file.nodes[i] = new NVarKeyword(node.file, node.line);
              break;

            case "cast":
              file.nodes[i] = new NCastKeyword(node.file, node.line);
              break;

            case "array":
              file.nodes[i] = new NArrayKeyword(node.file, node.line);
              break;

            case "func":
              file.nodes[i] = new NFuncKeyword(node.file, node.line);
              break;

            case "extern_func":
              file.nodes[i] = new NExternFuncKeyword(node.file, node.line);
              break;

            case "float":
              file.nodes[i] = new NFloatKeyword(node.file, node.line);
              break;

            case "char":
              file.nodes[i] = new NCharKeyword(node.file, node.line);
              break;

            case "if":
              file.nodes[i] = new NIfKeyword(node.file, node.line);
              break;

            case "else":
              file.nodes[i] = new NElseKeyword(node.file, node.line);
              break;

            case "while":
              file.nodes[i] = new NWhileKeyword(node.file, node.line);
              break;

            case "return":
              file.nodes[i] = new NReturnKeyword(node.file, node.line);
              break;

            case "break":
              file.nodes[i] = new NBreakKeyword(node.file, node.line);
              break;

            case "continue":
              file.nodes[i] = new NContinueKeyword(node.file, node.line);
              break;

            case "const":
              file.nodes[i] = new NConstKeyword(node.file, node.line);
              break;

            case "sizeof":
            case "countof":
            case "addressof":
            case "valueof":
            case "offsetof":
            case "__from_register":
            case "__to_register":
            case "__exit":
            case "__line":
            case "__file":
              file.nodes[i] = new NPseudoFunction(((NName)node).name, node.file, node.line);
              break;

            case "u64":
            case "i64":
            case "f64":
            case "u32":
            case "i32":
            case "f32":
            case "u16":
            case "i16":
            case "u8":
            case "i8":
              file.nodes[i] = new NType(BuiltInCType.Types[((NName)node).name], node.file, node.line);
              break;

            case "void":
              file.nodes[i] = new NType(VoidCType.Instance, node.file, node.line);
              break;

            case "text":
              if (file.nodes.NextIs(i + 1, typeof(NName), typeof(NOperator)) && (file.nodes[i + 2] as NOperator).operatorType == "=")
              {
                var originalNode = file.nodes[i];
                file.nodes[i] = new NArrayKeyword(originalNode.file, originalNode.line);
                file.nodes.Insert(i + 1, new NOperator("<", originalNode.file, originalNode.line));
                file.nodes.Insert(i + 2, new NType(BuiltInCType.Types["i8"], originalNode.file, originalNode.line));
                file.nodes.Insert(i + 3, new NOperator(">", originalNode.file, originalNode.line));
              }
              else
              {
                file.nodes[i] = new NType(new PtrCType(BuiltInCType.Types["i8"]), node.file, node.line);
              }
              break;

            case "voidptr":
              file.nodes[i] = new NType(new PtrCType(VoidCType.Instance), node.file, node.line);
              break;

            case "nullptr":
            case "null":
            case "NULL":
              file.nodes[i] = new NNull(node.file, node.line);
              break;
          }
        }
      }
    }

    private static void PatchTypes(Scope scope, ref List<Node> nodes)
    {
      for (int i = nodes.Count - 1; i >= 0; i--)
      {
        // Const Type.
        if (nodes.NextIs(i, typeof(NConstKeyword), typeof(NType)))
        {
          var typeNode = nodes[i + 1];
          var type = (typeNode as NType).type;

          if (type is PtrCType)
          {
            var t = (type as PtrCType).pointsTo;
            t = t.MakeCastableClone(t);
            t.explicitCast = null;
            t.isConst = true;

            type = new PtrCType(t) { isConst = true };
          }
          else if (type is ArrayCType)
          {
            var t = (type as ArrayCType).type;
            t = t.MakeCastableClone(t);
            t.explicitCast = null;
            t.isConst = true;

            type = new ArrayCType(t, (type as ArrayCType).count) { isConst = true };
          }
          else
          {
            type = type.MakeCastableClone(type);
            type.explicitCast = null;
            type.isConst = true;
          }

          nodes.RemoveRange(i, 2);
          nodes.Insert(i, new NType(type, typeNode.file, typeNode.line));
        }
        // Ptr.
        else if (nodes.NextIs(i, typeof(NPtrKeyword), typeof(NOperator), typeof(NType), typeof(NOperator)) && ((nodes[i + 1] as NOperator).operatorType == "<" && (nodes[i + 3] as NOperator).operatorType == ">"))
        {
          var start = nodes[i];
          NType type = nodes[i + 2] as NType;

          nodes.RemoveRange(i, 4);
          nodes.Insert(i, new NType(new PtrCType(type.type), start.file, start.line));
        }
        // Sized Array.
        else if (nodes.NextIs(i, typeof(NArrayKeyword), typeof(NOperator), typeof(NType), typeof(NComma), typeof(NIntegerValue), typeof(NOperator)) && ((nodes[i + 1] as NOperator).operatorType == "<" && (nodes[i + 5] as NOperator).operatorType == ">"))
        {
          if ((nodes[i + 4] as NIntegerValue).isForcefullyNegative || (nodes[i + 4] as NIntegerValue).uint_value == 0)
            Error($"Size of Array can not be less than 1. Given: '{(nodes[i + 4] as NIntegerValue).int_value}'.", nodes[i + 4].file, nodes[i + 4].line);

          var start = nodes[i];
          var type = nodes[i + 2] as NType;
          var size = nodes[i + 4] as NIntegerValue;

          nodes.RemoveRange(i, 6);
          nodes.Insert(i, new NType(new ArrayCType(type.type, (long)size.uint_value), start.file, start.line));
        }
        // extern_func / func.
        else if ((nodes[i] is NFuncKeyword || nodes[i] is NExternFuncKeyword) && nodes.NextIs(i + 1, typeof(NOperator), typeof(NType), typeof(NOpenParanthesis)) && (nodes[i + 1] as NOperator).operatorType == "<")
        {
          var funcTypeNode = nodes[i];
          var returnType = (nodes[i + 2] as NType).type;

          int j = i + 4;
          bool aborted = false;
          List<CType> parameters = new List<CType>();

          while (true)
          {
            if (nodes.Count <= j)
            {
              Error($"Unexpected end of function type definition.", funcTypeNode.file, funcTypeNode.line);
            }
            else if (nodes[j] is NOperator && (nodes[j] as NOperator).operatorType == ">")
            {
              j++;
              break;
            }
            else if (nodes[j] is NType)
            {
              parameters.Add((nodes[j] as NType).type);
              j++;

              if (nodes.Count <= j)
              {
                Error($"Unexpected end of function type definition.", funcTypeNode.file, funcTypeNode.line);
              }
              else if (nodes[j] is NComma)
              {
                j++;
              }
              else if (nodes[j] is NCloseParanthesis)
              {
                j++;
                break;
              }
              else
              {
                Error($"Unexpected '{nodes[j]}' in function type definition.", funcTypeNode.file, funcTypeNode.line);
              }
            }
            else if (nodes[j] is NName)
            {
              aborted = true;
              break;
            }
            else
            {
              Error($"Unexpected '{nodes[j]}' in function type definition.", funcTypeNode.file, funcTypeNode.line);
            }
          }

          if (nodes.Count <= j)
          {
            Error($"Unexpected end of function type definition.", funcTypeNode.file, funcTypeNode.line);
          }
          else if (nodes[j] is NOperator && (nodes[j] as NOperator).operatorType == ">")
          {
            j++;
          }
          else
          {
            Error($"Unexpected '{nodes[j]}' in function type definition.", funcTypeNode.file, funcTypeNode.line);
          }

          if (!aborted)
          {
            nodes.RemoveRange(i, j - i);

            if (funcTypeNode is NFuncKeyword)
              nodes.Insert(i, new NType(new FuncCType(returnType, parameters), funcTypeNode.file, funcTypeNode.line));
            else if (funcTypeNode is NExternFuncKeyword)
              nodes.Insert(i, new NType(new ExternFuncCType(returnType, parameters), funcTypeNode.file, funcTypeNode.line));
            else
              throw new Exception("Internal Compiler Error");
          }
        }
        else if (nodes.NextIs(i, typeof(NFloatKeyword), typeof(NOpenParanthesis), typeof(NStringValue), typeof(NCloseParanthesis)))
        {
          var start = nodes[i];
          var value = nodes[i + 2];
          double floatValue = 0;

          if (!double.TryParse((value as NStringValue).value, out floatValue))
            Error($"Invalid floating point value '{(value as NStringValue).value}'.", value.file, value.line);

          nodes.RemoveRange(i, 4);
          nodes.Insert(i, new NFloatingPointValue(floatValue, start.file, start.line));
        }
        else if (nodes.NextIs(i, typeof(NCharKeyword), typeof(NOpenParanthesis), typeof(NStringValue), typeof(NCloseParanthesis)))
        {
          var start = nodes[i];
          var value = nodes[i + 2] as NStringValue;

          if (value.value.Length != 1)
            Error($"Invalid character value '{(value as NStringValue).value}' must be of length 1.", value.file, value.line);

          nodes.RemoveRange(i, 4);
          nodes.Insert(i, new NIntegerValue(false, value.value[0], value.value[0], start.file, start.line));
        }
      }
    }

    private static void CompileScope(Scope scope, List<Node> nodes, ref ByteCodeState byteCodeState)
    {
      if (scope.IsFunction())
      {
        scope.maxRequiredStackSpace = scope.self.minStackSize;

        foreach (var parameter in scope.self.parameters)
          scope.AddVariable(parameter.value);

        scope.instructions.Add(new CInstruction_BeginFunction(scope.self));
      }
      else if (scope.parentScope == null) // if it's the global scope.
      {
        if (nodes.Count == 0)
          throw new Exception("Internal Compiler Error: The Global Scope does not contain any nodes, but CompileScope was called on it.");

        PatchTypes(scope, ref nodes);

        // Add Builtin Functions.
        foreach (var function in CBuiltInFunction.Functions)
          scope.AddFunction(function);

        scope.instructions.Add(new CInstruction_BeginGlobalScope(scope.maxRequiredStackSpace, nodes[0].file, nodes[0].line));
      }

      while (nodes.Count > 0)
      {
        // Floating Line End.
        if (nodes[0] is NLineEnd)
        {
          nodes.RemoveAt(0);
        }
        // Struct.
        else if (nodes.NextIs(typeof(NStructKeyword), typeof(NName), typeof(NOpenScope)))
        {
          // Parse Struct.
          NName nameNode = nodes[1] as NName;
          string name = nameNode.name;
          List<StructAttribute> attributes = new List<StructAttribute>();

          nodes.RemoveRange(0, 3);

          while (true)
          {
            if (nodes.Count == 0)
            {
              Error($"Invalid Struct Definition for '{name}'.", nameNode.file, nameNode.line);
            }
            else if (nodes[0] is NType && nodes.Count > 1 && nodes[1] is NName)
            {
              CType type = (nodes[0] as NType).type;
              nodes.RemoveAt(0);

              while (nodes[0] is NName)
              {
                attributes.Add(new StructAttribute((nodes[0] as NName).name, type, nodes[0].file, nodes[0].line));

                if (nodes.Count == 1)
                {
                  Error($"Invalid Struct Definition for '{name}'.", nameNode.file, nameNode.line);
                }
                else if (nodes[1] is NComma)
                {
                  nodes.RemoveRange(0, 2);
                }
                else if (nodes[1] is NLineEnd)
                {
                  nodes.RemoveRange(0, 2);
                  break;
                }
                else
                {
                  Error($"Unexpected {nodes[1]} in struct declaration. Expected: ',' or ';'.", nodes[1].file, nodes[1].line);
                }
              }
            }
            else if (nodes[0] is NCloseScope)
            {
              nodes.RemoveAt(0);
              break;
            }
            else
            {
              Error($"Unexpected {nodes[0]} in struct declaration.", nodes[0].file, nodes[0].line);
            }
          }

          StructCType structType = new StructCType(name, attributes, nameNode.file, nameNode.line);

          // Add Struct to scope.
          scope.AddStruct(structType);

          // Patch Types.
          for (int i = 0; i < nodes.Count; i++)
            if (nodes[i] is NName && (nodes[i] as NName).name == structType.name)
              nodes[i] = new NType(structType, nodes[i].file, nodes[i].line);

          PatchTypes(scope, ref nodes);
        }
        // Function.
        else if (nodes.NextIs(typeof(NFunctionKeyword), typeof(NType), typeof(NName), typeof(NOpenParanthesis)))
        {
          var startNode = nodes[0];
          var type = (nodes[1] as NType).type;
          var nameNode = nodes[2] as NName;

          nodes.RemoveRange(0, 4);

          // Function already defined?
          {
            CFunction existingFunction = scope.GetFunction(nameNode.name);

            if (null != existingFunction)
              Error($"Duplicate function definition for identifier '{nameNode.name}'. A function with the same name has already been defined in File '{existingFunction.file}', Line {existingFunction.line + 1}: {existingFunction}", nameNode.file, nameNode.line);
          }

          var parameters = new List<FunctionParameter>();

          // Get Parameters.
          while (true)
          {
            if (nodes.Count == 0)
            {
              Error("Unexpected end of function definition.", startNode.file, startNode.line);
            }
            else if (nodes.NextIs(typeof(NType), typeof(NName)))
            {
              parameters.Add(new FunctionParameter((nodes[0] as NType).type, (nodes[1] as NName).name, nodes[1].file, nodes[1].line));
              nodes.RemoveRange(0, 2);

              if (nodes.Count == 0)
              {
                Error("Unexpected end of function definition.", startNode.file, startNode.line);
              }
              else if (nodes[0] is NComma)
              {
                nodes.RemoveAt(0);
              }
              else if (nodes[0] is NCloseParanthesis)
              {
                nodes.RemoveAt(0);
                break;
              }
              else
              {
                Error($"Unexpected {nodes[0]} in function definition.", nodes[0].file, nodes[0].line);
              }
            }
            else if (nodes[0] is NCloseParanthesis)
            {
              nodes.RemoveAt(0);
              break;
            }
            else
            {
              Error($"Unexpected {nodes[0]} in function definition.", nodes[0].file, nodes[0].line);
            }
          }

          // Add Function to scope.
          var function = new CFunction(nameNode.name, type, parameters, startNode.file, startNode.line);
          scope.AddFunction(function);

          if (nodes.Count < 2)
            Error("Unexpected end of function.", startNode.file, startNode.line);
          else if (!(nodes[0] is NOpenScope))
            Error($"Unexpected {nodes[0]}. Expected function definition.", nodes[0].file, nodes[0].line);

          nodes.RemoveAt(0);

          // Find End.
          int openScopes = 1;
          int endIndex = 0;

          for (; endIndex < nodes.Count; endIndex++)
          {
            if (nodes[endIndex] is NOpenScope)
            {
              openScopes++;
            }
            else if (nodes[endIndex] is NCloseScope)
            {
              openScopes--;

              if (openScopes == 0)
                break;
            }
          }

          if (openScopes != 0)
            Error($"Missing end of function at File '{startNode.file}', Line {startNode.line + 1}. Expected '}}'.", nodes.Last().file, nodes.Last().line);

          // Extract Nodes.
          var functionNodes = nodes.GetRange(0, endIndex);
          nodes.RemoveRange(0, endIndex + 1);

          function.nodes = functionNodes;
        }
        // Dynamic Array.
        else if (nodes.NextIs(typeof(NArrayKeyword), typeof(NOperator), typeof(NType), typeof(NOperator), typeof(NName), typeof(NOperator)) && ((nodes[1] as NOperator).operatorType == "<" && (nodes[3] as NOperator).operatorType == ">" && (nodes[5] as NOperator).operatorType == "="))
        {
          ParseDynamicArrayInitialization(scope, ref nodes, false);
        }
        // Fixed size array.
        else if (nodes.NextIs(typeof(NType), typeof(NName), typeof(NOperator), typeof(NOpenScope)) && ((nodes[0] as NType).type is ArrayCType && (nodes[2] as NOperator).operatorType == "="))
        {
          ParseFixedSizeArrayInitialization(scope, ref nodes, false);
        }
        // Const Dynamic Array.
        else if (nodes.NextIs(typeof(NConstKeyword), typeof(NArrayKeyword), typeof(NOperator), typeof(NType), typeof(NOperator), typeof(NName), typeof(NOperator)) && ((nodes[2] as NOperator).operatorType == "<" && (nodes[4] as NOperator).operatorType == ">" && (nodes[6] as NOperator).operatorType == "="))
        {
          nodes.RemoveAt(0); // Remove `const`.

          ParseDynamicArrayInitialization(scope, ref nodes, true);
        }
        // Const Fixed size array.
        else if (nodes.NextIs(typeof(NConstKeyword), typeof(NType), typeof(NName), typeof(NOperator), typeof(NOpenScope)) && ((nodes[1] as NType).type is ArrayCType && (nodes[3] as NOperator).operatorType == "="))
        {
          nodes.RemoveAt(0); // Remove `const`.

          ParseFixedSizeArrayInitialization(scope, ref nodes, true);
        }
        else if (nodes[0] is NIfKeyword)
        {
          var ifNode = nodes[0];
          nodes.RemoveAt(0);

          if (nodes.Count < 4 || !(nodes[0] is NOpenParanthesis))
            Error($"Unexpected {(nodes.Count == 0 ? "end" : nodes[0].ToString())} in conditional expression.", ifNode.file, ifNode.line);

          var conditionRange = nodes.GetRange(1, nodes.Count - 1);
          int endIndex = conditionRange.FindNextSameScope(n => n is NCloseParanthesis);

          if (endIndex == 0)
            Error($"Missing condition in conditional expression.", nodes[1].file, nodes[1].line);

          var condition = conditionRange.GetRange(0, endIndex);
          var value = GetRValue(scope, condition.ToList(), ref byteCodeState);
          nodes.RemoveRange(0, endIndex + 2);

          var afterIfElseLabel = new LLI_Label_PseudoInstruction($"After if-else '{ifNode}' in {ifNode.file}:{ifNode.line + 1}");

          // Handle Conditional Block.
          {
            var nextElseLabel = new LLI_Label_PseudoInstruction($"First next else label for '{ifNode}' in {ifNode.file}:{ifNode.line + 1}");
            scope.instructions.Add(new CInstruction_IfZeroJumpToLabel(value, nextElseLabel, scope.maxRequiredStackSpace, true, ifNode.file, ifNode.line));

            // Inside If Block.
            {
              List<Node> scopeNodes;

              if (nodes[0] is NOpenScope)
              {
                int scopeEndIndex = nodes.FindNextSameScope(n => n is NCloseScope);
                scopeNodes = nodes.GetRange(1, scopeEndIndex - 1);
                nodes.RemoveRange(0, scopeEndIndex + 1);
              }
              else
              {
                int scopeEndIndex = nodes.FindNextSameScope(n => n is NLineEnd);
                scopeNodes = nodes.GetRange(0, scopeEndIndex + 1);
                nodes.RemoveRange(0, scopeEndIndex + 1);
              }

              Scope childScope = scope.GetChildScopeForConditional(afterIfElseLabel);

              CompileScope(childScope, scopeNodes, ref byteCodeState);
            }

            scope.instructions.Add(new CInstruction_Label(nextElseLabel, ifNode.file, ifNode.line));
          }

          int elseCount = 1;

          // Handle Else and Else-If blocks.
          while (true)
          {
            if (nodes.Count == 0 || !(nodes[0] is NElseKeyword))
              break;

            bool isLastElse = nodes.Count >= 1 && !(nodes[1] is NIfKeyword);

            var elseNode = nodes[0];
            nodes.RemoveAt(0);

            LLI_Label_PseudoInstruction nextElseLabel = new LLI_Label_PseudoInstruction($"Next else label #{elseCount++} for '{ifNode}' in {ifNode.file}:{ifNode.line + 1}");

            if (!isLastElse)
            {
              nodes.RemoveAt(0);

              if (nodes.Count < 4 || !(nodes[0] is NOpenParanthesis))
                Error($"Unexpected {(nodes.Count == 0 ? "end" : nodes[0].ToString())} in conditional expression.", ifNode.file, ifNode.line);

              int elseIfConditionEndIndex = nodes.FindNextSameScope(n => n is NCloseParanthesis);

              if (elseIfConditionEndIndex == 1)
                Error($"Missing condition in conditional expression.", nodes[1].file, nodes[1].line);

              var elseIfCondition = nodes.GetRange(1, elseIfConditionEndIndex - 1);
              var elseIfConditionValue = GetRValue(scope, elseIfCondition.ToList(), ref byteCodeState);
              nodes.RemoveRange(0, elseIfConditionEndIndex + 1);

              scope.instructions.Add(new CInstruction_IfZeroJumpToLabel(elseIfConditionValue, nextElseLabel, scope.maxRequiredStackSpace, false, ifNode.file, ifNode.line));
            }

            // Inside Else Block.
            {
              List<Node> scopeNodes;

              if (nodes[0] is NOpenScope)
              {
                int scopeEndIndex = nodes.FindNextSameScope(n => n is NCloseScope);
                scopeNodes = nodes.GetRange(1, scopeEndIndex - 1);
                nodes.RemoveRange(0, scopeEndIndex + 1);
              }
              else
              {
                int scopeEndIndex = nodes.FindNextSameScope(n => n is NLineEnd);
                scopeNodes = nodes.GetRange(0, scopeEndIndex + 1);
                nodes.RemoveRange(0, scopeEndIndex + 1);
              }

              Scope childScope = scope.GetChildScopeForConditional(afterIfElseLabel);

              CompileScope(childScope, scopeNodes, ref byteCodeState);
            }

            scope.instructions.Add(new CInstruction_Label(nextElseLabel, elseNode.file, elseNode.line));

            if (isLastElse)
              break;
          }

          scope.instructions.Add(new CInstruction_Label(afterIfElseLabel, ifNode.file, ifNode.line));
        }
        else if (nodes[0] is NWhileKeyword)
        {
          var whileNode = nodes[0];
          nodes.RemoveAt(0);

          if (nodes.Count < 4 || !(nodes[0] is NOpenParanthesis))
            Error($"Unexpected {(nodes.Count == 0 ? "end" : nodes[0].ToString())} in loop condition.", whileNode.file, whileNode.line);

          var conditionRange = nodes.GetRange(1, nodes.Count - 1);
          int endIndex = conditionRange.FindNextSameScope(n => n is NCloseParanthesis);

          if (endIndex <= 0)
            Error($"Missing condition in loop condition.", nodes[1].file, nodes[1].line);

          var condition = conditionRange.GetRange(0, endIndex);
          var value = GetRValue(scope, condition.ToList(), ref byteCodeState);
          nodes.RemoveRange(0, endIndex + 2);
          
          LLI_Label_PseudoInstruction afterWhileLabel = new LLI_Label_PseudoInstruction($"After {whileNode} in {whileNode.file}:{whileNode.line + 1}");
          LLI_Label_PseudoInstruction beforeWhileLabel = new LLI_Label_PseudoInstruction($"Before {whileNode} in {whileNode.file}:{whileNode.line + 1}");
          LLI_Label_PseudoInstruction beforeWhileConsecutiveLabel = new LLI_Label_PseudoInstruction($"Before consecutive executions of {whileNode} in {whileNode.file}:{whileNode.line + 1}");

          scope.instructions.Add(new CInstruction_IfZeroJumpToLabel(value, afterWhileLabel, scope.maxRequiredStackSpace, true, whileNode.file, whileNode.line));
          scope.instructions.Add(new CInstruction_Label(beforeWhileLabel, whileNode.file, whileNode.line));

          // Inside while block.
          {
            List<Node> scopeNodes;

            if (nodes[0] is NOpenScope)
            {
              var scopeRange = nodes.GetRange(1, nodes.Count - 1);
              int scopeEndIndex = scopeRange.FindNextSameScope(n => n is NCloseScope);

              if (scopeEndIndex == -1)
                Error($"Failed to find end of loop statement '{whileNode}'.", whileNode.file, whileNode.line);

              scopeNodes = scopeRange.GetRange(0, scopeEndIndex);
              nodes = scopeRange;
              nodes.RemoveRange(0, scopeEndIndex + 1);
            }
            else
            {
              int scopeEndIndex = nodes.FindNextSameScope(n => n is NLineEnd);
              scopeNodes = nodes.GetRange(0, scopeEndIndex + 1);
              nodes.RemoveRange(0, scopeEndIndex + 1);
            }

            Scope childScope = scope.GetChildScopeForConditional(null);

            childScope.continueLabel = beforeWhileConsecutiveLabel;
            childScope.breakLabel = afterWhileLabel;

            CompileScope(childScope, scopeNodes, ref byteCodeState);
          }

          scope.instructions.Add(new CInstruction_Label(beforeWhileConsecutiveLabel, whileNode.file, whileNode.line));
          scope.instructions.Add(new CInstruction_IfZeroJumpToLabel(GetRValue(scope, condition.ToList(), ref byteCodeState), afterWhileLabel, scope.maxRequiredStackSpace, true, whileNode.file, whileNode.line));
          scope.instructions.Add(new CInstruction_GotoLabel(beforeWhileLabel, whileNode.file, whileNode.line));
          scope.instructions.Add(new CInstruction_Label(afterWhileLabel, whileNode.file, whileNode.line));
        }
        else if (nodes.NextIs(typeof(NPseudoFunction), typeof(NOpenParanthesis), typeof(NName), typeof(NComma), typeof(NIntegerValue), typeof(NCloseParanthesis), typeof(NLineEnd)) && (nodes[0] as NPseudoFunction).type == NPseudoFunctionType.ToRegister)
        {
          var nameNode = nodes[2] as NName;
          var indexNode = nodes[4] as NIntegerValue;
          var variable = scope.GetVariable(nameNode.name);
          scope.instructions.Add(new CInstruction_CustomAction(b => {
            b.CopyValueToPosition(variable, Position.Register((int)indexNode.uint_value), scope.maxRequiredStackSpace, variable.type.GetSize());
          }, nameNode.file, nameNode.line));
          nodes.RemoveRange(0, 7);
        }
        else if (nodes.NextIs(typeof(NPseudoFunction), typeof(NOpenParanthesis), typeof(NCloseParanthesis), typeof(NLineEnd)) && (nodes[0] as NPseudoFunction).type == NPseudoFunctionType.Exit)
        {
          var exitNode = nodes[0] as NPseudoFunction;
          scope.instructions.Add(new CInstruction_CustomAction(b => { b.instructions.Add(new LLI_Exit()); }, exitNode.file, exitNode.line));
          nodes.RemoveRange(0, 4);

          if (nodes.Count != 0)
            Warn("Unreachable Code.", nodes[0].file, nodes[0].line);
        }
        else if (nodes.NextIs(typeof(NBreakKeyword), typeof(NLineEnd)))
        {
          var breakLabel = scope.GetBreakLabel();

          if (breakLabel == null)
            Error($"Cannot use {nodes[0]} when not inside a loop.", nodes[0].file, nodes[0].line);

          scope.instructions.Add(new CInstruction_GotoLabel(breakLabel, nodes[0].file, nodes[0].line));
          nodes.RemoveRange(0, 2);

          if (nodes.Count != 0)
            Warn("Unreachable Code.", nodes[0].file, nodes[0].line);
        }
        else if (nodes.NextIs(typeof(NContinueKeyword), typeof(NLineEnd)))
        {
          var continueLabel = scope.GetContinueLabel();

          if (continueLabel == null)
            Error($"Cannot use {nodes[0]} when not inside a loop.", nodes[0].file, nodes[0].line);

          scope.instructions.Add(new CInstruction_GotoLabel(continueLabel, nodes[0].file, nodes[0].line));
          nodes.RemoveRange(0, 2);

          if (nodes.Count != 0)
            Warn("Unreachable Code.", nodes[0].file, nodes[0].line);
        }
        else if (nodes[0] is NReturnKeyword)
        {
          var parentFunction = scope.GetCurrentFunction();

          if (parentFunction == null)
            Error($"Cannot use {nodes[0]} in global scope.", nodes[0].file, nodes[0].line);

          if (parentFunction.returnType is VoidCType)
          {
            if (!(nodes[1] is NLineEnd))
              Error($"Unexpected {nodes[1]} in {nodes[0]} statement.", nodes[1].file, nodes[1].line);
            
            scope.instructions.Add(new CInstruction_CustomAction(b => { b.instructions.Add(new LLI_StackDecrementImm(scope.maxRequiredStackSpace)); b.instructions.Add(new LLI_Return()); }, nodes[0].file, nodes[0].line));
            nodes.RemoveRange(0, 2);

            if (nodes.Count != 0)
              Warn("Unreachable Code.", nodes[0].file, nodes[0].line);
          }
          else
          {
            throw new NotImplementedException();
          }
        }
        else
        {
          // Find first operator before ';'.
          int firstOperator = nodes.FindNextSameScope(n => n is NOperator || n is NLineEnd);

          if (firstOperator < 0)
            Error($"Failed to find operator. Unexpected token '{nodes[0]}'", nodes[0].file, nodes[0].line);

          if (nodes[firstOperator] is NLineEnd)
            firstOperator = -1;

          int nextEndLine = nodes.FindNextSameScope(firstOperator == -1 ? 0 : firstOperator, n => n is NLineEnd);

          if (nextEndLine == -1)
            Error($"Unexpected {nodes[0]}.", nodes[0].file, nodes[0].line);

          // only lvalue.
          if (firstOperator == -1)
          {
            var lnodes = nodes.GetRange(0, nextEndLine);
            nodes.RemoveRange(0, nextEndLine + 1);

            var value = GetLValue(scope, lnodes, ref byteCodeState);

            if (value != null && value.type != null && value.type is ArrayCType)
              scope.instructions.Add(new CInstruction_InitializeArray(value, new byte[value.type.GetSize()], lnodes[0].file, lnodes[0].line, scope.maxRequiredStackSpace));
          }
          // `++`, `--`.
          else if (nextEndLine == firstOperator + 1)
          {
            var operatorNode = nodes[firstOperator] as NOperator;

            if (!new string[] { "++", "--" }.Contains(operatorNode.operatorType))
              Error($"Invalid use of operator {operatorNode}.", operatorNode.file, operatorNode.line);

            var lnodes = nodes.GetRange(0, firstOperator);
            nodes.RemoveRange(0, nextEndLine + 1);

            var lvalue = GetLValue(scope, lnodes, ref byteCodeState, null);

            if (!(lvalue is CNamedValue))
              lvalue.description += " (lvalue)";

            if (!(lvalue.type is BuiltInCType) && !(lvalue.type is PtrCType || (lvalue.type as PtrCType).pointsTo is VoidCType))
              Error($"{operatorNode} cannot be applied to lvalue of type {lvalue.type}.", operatorNode.file, operatorNode.line);

            CValue resultingValue;

            if (lvalue.type is PtrCType)
            {
              scope.instructions.Add(new CInstruction_AddImm(lvalue, (operatorNode.operatorType == "++" ? 1 : -1), scope.maxRequiredStackSpace, true, out resultingValue, operatorNode.file, operatorNode.line));
            }
            else
            {
              var type = lvalue.type as BuiltInCType;

              if (!type.IsFloat())
                scope.instructions.Add(new CInstruction_AddImm(lvalue, operatorNode.operatorType == "++" ? 1 : -1, scope.maxRequiredStackSpace, true, out resultingValue, operatorNode.file, operatorNode.line));
              else
                scope.instructions.Add(new CInstruction_AddImm(lvalue, operatorNode.operatorType == "++" ? 1.0 : -1.0, scope.maxRequiredStackSpace, true, out resultingValue, operatorNode.file, operatorNode.line));
            }
          }
          // lvalue = rvalue;
          else if ((nodes[firstOperator] as NOperator).operatorType == "=")
          {
            if (nodes[0] is NPseudoFunction && nodes[1] is NOpenParanthesis && nodes[firstOperator - 1] is NCloseParanthesis)
            {
              if ((nodes[0] as NPseudoFunction).type != NPseudoFunctionType.ValueOf)
                Error($"Unexpected '{nodes[0]}'.", nodes[0].file, nodes[0].line);

              var paramNodes = nodes.GetRange(2, firstOperator - 3);
              var equalsNode = nodes[firstOperator];
              var rnodes = nodes.GetRange(firstOperator + 1, nextEndLine - (firstOperator + 1));
              nodes.RemoveRange(0, nextEndLine + 1);

              var rvalue = GetRValue(scope, rnodes, ref byteCodeState);

              if (!(rvalue is CNamedValue))
                rvalue.description += " (rvalue)";

              if (rvalue.type is VoidCType || rvalue.type is ArrayCType)
                Error($"Type '{rvalue.type}' is illegal for an rvalue.", paramNodes[0].file, paramNodes[0].line);

              var ptrValue = GetRValue(scope, paramNodes, ref byteCodeState);

              if (!(ptrValue is CNamedValue))
                ptrValue.description += " (rvalue)";

              if (!(ptrValue.type is PtrCType))
                Error($"Type '{ptrValue.type}' cannot be used as parameter for 'valueof'.", paramNodes[0].file, paramNodes[0].line);

              scope.instructions.Add(new CInstruction_SetValuePtrToValue(ptrValue, rvalue, equalsNode.file, equalsNode.line, scope.maxRequiredStackSpace));
            }
            else if (nodes[0] is NName && nodes[1] is NOpenBracket && nodes[firstOperator - 1] is NCloseBracket)
            {
              // is this a pair of brackets?
              int bracketLevel = 0;

              var lvaluenodes = nodes.GetRange(0, firstOperator); // last node should be the potential close bracket.

              foreach (var node in lvaluenodes)
                if (node is NOpenBracket)
                  bracketLevel++;
                else if (node is NCloseBracket)
                  if (--bracketLevel == 0 && !object.ReferenceEquals(node, lvaluenodes[lvaluenodes.Count - 1]))
                    Error($"Unexpected {node}.", node.file, node.line);

              CValue variable = scope.GetVariable((nodes[0] as NName).name);

              if (variable == null)
                Error($"Unexpected token '{nodes[0]}'. Can't resolve identifer to variable.", nodes[0].file, nodes[0].line);

              if (!(variable.type is ArrayCType) && !(variable.type is PtrCType))
                Error($"Illegal subscript on variable {variable}. Expected pointer or array.", nodes[1].file, nodes[1].line);

              if (variable.type is ArrayCType)
              {
                CGlobalValueReference reference;

                scope.instructions.Add(new CInstruction_ArrayVariableToPtr(variable as CNamedValue, out reference, scope.maxRequiredStackSpace, nodes[1].file, nodes[1].line));

                variable = reference;
              }

              var subScriptNodes = nodes.GetRange(2, firstOperator - 3);
              var equalsNode = nodes[firstOperator];
              var rnodes = nodes.GetRange(firstOperator + 1, nextEndLine - (firstOperator + 1));
              nodes.RemoveRange(0, nextEndLine + 1);

              var rvalue = GetRValue(scope, rnodes, ref byteCodeState);

              if (!(rvalue is CNamedValue))
                rvalue.description += " (rvalue)";

              if (rvalue.type is VoidCType || rvalue.type is ArrayCType)
                Error($"Type '{rvalue.type}' is illegal for an rvalue.", rnodes[0].file, rnodes[0].line);

              var subScriptValue = GetRValue(scope, subScriptNodes, ref byteCodeState);

              if (!(subScriptValue is CNamedValue))
                subScriptValue.description += " (rvalue)";

              if (!(subScriptValue.type is BuiltInCType && !(subScriptValue.type as BuiltInCType).IsFloat()))
                Error($"Type '{subScriptValue}' cannot be used as subscript. Expected an integer type.", subScriptNodes[0].file, subScriptNodes[0].line);

              CValue lvalue;
              scope.instructions.Add(new CInstruction_Add(variable, subScriptValue, scope.maxRequiredStackSpace, false, out lvalue, subScriptNodes[0].file, subScriptNodes[0].line));

              scope.instructions.Add(new CInstruction_SetValuePtrToValue(lvalue, rvalue, equalsNode.file, equalsNode.line, scope.maxRequiredStackSpace));
            }
            else
            {
              var lnodes = nodes.GetRange(0, firstOperator);
              var equalsNode = nodes[firstOperator];
              var rnodes = nodes.GetRange(firstOperator + 1, nextEndLine - (firstOperator + 1));
              nodes.RemoveRange(0, nextEndLine + 1);

              var rvalue = GetRValue(scope, rnodes, ref byteCodeState);

              if (!(rvalue is CNamedValue))
                rvalue.description += " (rvalue)";

              if (rvalue.type is VoidCType)
                Error($"Type '{rvalue.type}' is illegal for an rvalue.", rnodes[0].file, rnodes[0].line);

              if (rvalue.type is ArrayCType && rvalue is CNamedValue)
              {
                CGlobalValueReference addressOf;

                scope.instructions.Add(new CInstruction_ArrayVariableToPtr((CNamedValue)rvalue, out addressOf, scope.maxRequiredStackSpace, equalsNode.file, equalsNode.line));

                rvalue = addressOf;
              }

              var lvalue = GetLValue(scope, lnodes, ref byteCodeState, rvalue.type);

              if (!(lvalue is CNamedValue))
                lvalue.description += " (lvalue)";

              scope.instructions.Add(new CInstruction_SetValueTo(lvalue, rvalue, equalsNode.file, equalsNode.line, scope.maxRequiredStackSpace));
            }
          }
          else
          {
            Error($"Unexpected Token or no valid operator found at parser head: {nodes[0]}.", nodes[0].file, nodes[0].line);
          }
        }
      }

      if (scope.IsFunction())
      {
        scope.instructions.Add(new CInstruction_EndFunction(scope.self));
      }
      else if (scope.parentScope == null) // is global scope.
      {
        scope.instructions.Add(new CInstruction_EndGlobalScope(scope.maxRequiredStackSpace, null, 0));
      }
      else if (scope.afterLabel != null)
      {
        scope.instructions.Add(new CInstruction_EndOfConditional_WipeAllRegisters(scope));
        scope.instructions.Add(new CInstruction_GotoLabel(scope.afterLabel, null, 0));
      }

      if (scope.parentScope == null)
      {
        int index = 0;

        foreach (var instruction in scope.instructions)
        {
          LLInstruction.currentFile = instruction.file;
          LLInstruction.currentLine = instruction.line;

          if (DetailedIntermediateOutput)
            byteCodeState.instructions.Add(new LLI_Comment_PseudoInstruction($"Intermediate Instruction Type: {instruction} (#{index++})"));

          try
          {
            instruction.GetLLInstructions(ref byteCodeState);
          }
          catch (Exception e)
          {
            Console.WriteLine($"Exception thrown on Instruction: {instruction}.");

            byteCodeState.instructions.Add(new LLI_Comment_PseudoInstruction($"\nCompilation Failed with the following exception:\n\n{e}"));

            ExceptionDispatchInfo.Capture(e).Throw();
          }
        }
      }
      else
      {
        scope.parentScope.instructions.AddRange(scope.instructions);
      }

      foreach (CFunction function in scope.GetLocalFunctions())
      {
        if (function is CBuiltInFunction)
          continue;

        Scope childScope = scope.GetChildScopeForFunction(function);

        CompileScope(childScope, function.nodes, ref byteCodeState);

        if (scope.parentScope != null)
        {
          scope.parentScope.instructions.AddRange(childScope.instructions);
        }
        else
        {
          int index = 0;

          foreach (var instruction in childScope.instructions)
          {
            LLInstruction.currentFile = instruction.file;
            LLInstruction.currentLine = instruction.line;

            if (DetailedIntermediateOutput)
              byteCodeState.instructions.Add(new LLI_Comment_PseudoInstruction($"Intermediate Instruction Type: {instruction} (#{function}:{index++})"));

            instruction.GetLLInstructions(ref byteCodeState);
          }
        }
      }
    }

    private static void ParseFixedSizeArrayInitialization(Scope scope, ref List<Node> nodes, bool isConst)
    {
      if (!(((nodes[0] as NType).type as ArrayCType).type is BuiltInCType))
        Error($"Invalid Type '{((nodes[0] as NType).type as ArrayCType).type}'. Fixed Size Array Initializers can only contain builtin types.", nodes[0].file, nodes[0].line);

      var nameNode = nodes[1] as NName;
      var startNode = nodes[0] as NType;
      var arrayType = startNode.type as ArrayCType;
      var builtinType = arrayType.type as BuiltInCType;
      bool isStatic = isConst || !scope.InFunction();

      if (isConst)
      {
        builtinType = builtinType.MakeCastableClone(builtinType) as BuiltInCType;
        builtinType.explicitCast = null;
        builtinType.isConst = true;
      }

      nodes.RemoveRange(0, 4);

      var valueCount = 0;

      List<byte> data = new List<byte>();

      while (true)
      {
        if (nodes.Count == 0)
        {
          Error("Unexpected end of array definition.", startNode.file, startNode.line);
        }
        else if (nodes[0] is NIntegerValue)
        {
          valueCount++;

          if (valueCount > arrayType.count)
            Error($"Number of array values in initializer exceeds specified count ({arrayType.count}) for {nameNode}.", nodes[0].file, nodes[0].line);

          data.AddRange(builtinType.GetAsBytes(nodes[0] as NIntegerValue));
          nodes.RemoveAt(0);

          if (nodes.Count == 0)
          {
            Error("Unexpected end of array definition.", startNode.file, startNode.line);
          }
          else if (nodes.NextIs(typeof(NCloseScope), typeof(NLineEnd)))
          {
            nodes.RemoveRange(0, 2);
            break;
          }
          else if (nodes.NextIs(typeof(NComma)))
          {
            nodes.RemoveAt(0);
          }
          else
          {
            Error($"Unexpected {nodes[0]} in array definition.", nodes[0].file, nodes[0].line);
          }
        }
        else if (nodes.NextIs(typeof(NCloseScope), typeof(NLineEnd)))
        {
          nodes.RemoveRange(0, 2);
          break;
        }
        else
        {
          Error($"Unexpected {nodes[0]} in array definition.", nodes[0].file, nodes[0].line);
        }
      }

      if (data.Count < arrayType.GetSize())
        data.AddRange(new byte[arrayType.GetSize() - data.Count]);

      if (isConst)
        arrayType.isConst = true;

      var value = new CNamedValue(nameNode, arrayType, true);
      value.isStatic = isStatic || isConst;

      scope.AddVariable(value);
      scope.instructions.Add(new CInstruction_InitializeArray(value, data.ToArray(), startNode.file, startNode.line, scope.maxRequiredStackSpace));
    }

    private static void ParseDynamicArrayInitialization(Scope scope, ref List<Node> nodes, bool isConst)
    {
      if (!((nodes[2] as NType).type is BuiltInCType))
        Error($"Invalid Type '{(nodes[2] as NType).type.ToString()}'. Dynamically Sized Arrays can only contain builtin types.", nodes[2].file, nodes[2].line);

      var builtinType = (nodes[2] as NType).type as BuiltInCType;
      bool isStatic = isConst || !scope.InFunction();

      if (isConst)
      {
        builtinType = builtinType.MakeCastableClone(builtinType) as BuiltInCType;
        builtinType.explicitCast = null;
        builtinType.isConst = true;
      }

      if (builtinType.type == BuiltInTypes.i8 && nodes.Count > 6 && nodes[6] is NStringValue && nodes[7] is NLineEnd)
      {
        var stringValue = nodes[6] as NStringValue;
        var arrayType = new ArrayCType(builtinType, stringValue.length) { isConst = isConst };
        var value = new CNamedValue(nodes[4] as NName, arrayType, true) { isStatic = isStatic || isConst };

        scope.AddVariable(value);
        scope.instructions.Add(new CInstruction_InitializeArray(value, stringValue.bytes, nodes[0].file, nodes[0].line, scope.maxRequiredStackSpace));
        nodes.RemoveRange(0, 8);
      }
      else if (nodes.Count > 6 && nodes[6] is NOpenScope)
      {
        long valueCount = 0;
        List<byte> data = new List<byte>();

        var startNode = nodes[0];
        var nameNode = (nodes[4] as NName);

        nodes.RemoveRange(0, 7);

        while (true)
        {
          if (nodes.Count == 0)
          {
            Error("Unexpected end of array definition.", startNode.file, startNode.line);
          }
          else if (nodes[0] is NIntegerValue)
          {
            valueCount++;
            data.AddRange(builtinType.GetAsBytes(nodes[0] as NIntegerValue));
            nodes.RemoveAt(0);

            if (nodes.Count == 0)
            {
              Error("Unexpected end of array definition.", startNode.file, startNode.line);
            }
            else if (nodes.NextIs(typeof(NCloseScope), typeof(NLineEnd)))
            {
              nodes.RemoveRange(0, 2);
              break;
            }
            else if (nodes.NextIs(typeof(NComma)))
            {
              nodes.RemoveAt(0);
            }
            else
            {
              Error($"Unexpected {nodes[0]} in array definition.", nodes[0].file, nodes[0].line);
            }
          }
          else if (nodes.NextIs(typeof(NCloseScope), typeof(NLineEnd)))
          {
            nodes.RemoveRange(0, 2);
            break;
          }
          else
          {
            Error($"Unexpected {nodes[0]} in array definition.", nodes[0].file, nodes[0].line);
          }
        }

        var arrayType = new ArrayCType(builtinType, valueCount);

        if (isConst)
          arrayType.isConst = true;

        var value = new CNamedValue(nameNode, arrayType, true);
        value.isStatic = isStatic || isConst;

        scope.AddVariable(value);
        scope.instructions.Add(new CInstruction_InitializeArray(value, data.ToArray(), startNode.file, startNode.line, scope.maxRequiredStackSpace));
      }
      else
      {
        Error($"Unexpected Token combination in dynamic array initialization: '{nodes[0]}' ...", nodes[0].file, nodes[0].line);
      }
    }

    private static CValue GetLValue(Scope scope, List<Node> nodes, ref ByteCodeState byteCodeState, CType rValueType = null)
    {
      // variable definition.
      if (nodes.Count == 2 && nodes.NextIs(typeof(NType), typeof(NName)))
      {
        var type = nodes[0] as NType;
        var name = nodes[1] as NName;

        var ctype = type.type.MakeCastableClone(type.type);
        ctype.explicitCast = null;

        var value = new CNamedValue(name, ctype, false) { isStatic = !scope.InFunction() };

        scope.AddVariable(value);

        return value;
      }
      if (nodes.Count == 3 && nodes.NextIs(typeof(NConstKeyword), typeof(NType), typeof(NName)))
      {
        if (rValueType == null)
          Error($"Cannot declare const value {nodes[2]}. {nodes[0]} can only be used when assigning a value.", nodes[0].file, nodes[0].line);

        var type = nodes[1] as NType;
        var name = nodes[2] as NName;

        var ctype = type.type.MakeCastableClone(type.type);
        ctype.explicitCast = null;
        ctype.isConst = true;

        var value = new CNamedValue(name, ctype, false) { isStatic = !scope.InFunction() };

        scope.AddVariable(value);

        return value;
      }
      else if (nodes.Count == 2 && nodes.NextIs(typeof(NVarKeyword), typeof(NName)))
      {
        if (rValueType == null)
          Error($"Cannot deduct type for {nodes[1]}. {nodes[0]} can only be used when assigning a value.", nodes[0].file, nodes[0].line);

        var type = (rValueType.explicitCast == null ? rValueType : rValueType.explicitCast);
        type.MakeCastableClone(type);
        type.explicitCast = null;

        var name = nodes[1] as NName;
        var value = new CNamedValue(name, type, false) { isStatic = !scope.InFunction() };

        scope.AddVariable(value);

        return value;
      }
      else if (nodes.Count == 3 && nodes.NextIs(typeof(NConstKeyword), typeof(NVarKeyword), typeof(NName)))
      {
        if (rValueType == null)
          Error($"Cannot declare const value {nodes[2]}. {nodes[0]} and {nodes[1]} can only be used when assigning a value.", nodes[0].file, nodes[0].line);

        var type = (rValueType.explicitCast == null ? rValueType : rValueType.explicitCast);
        type = type.MakeCastableClone(type);
        type.explicitCast = null;
        type.isConst = true;

        var name = nodes[2] as NName;
        var value = new CNamedValue(name, type, false) { isStatic = !scope.InFunction() };

        scope.AddVariable(value);

        return value;
      }
      else if (nodes[0] is NName)
      {
        var value = scope.GetVariable((nodes[0] as NName).name);
        var nameNode = nodes[0];

        if (value != null)
        {
          if (nodes.Count > 1)
          {
            // Call to extern_func or func.
            if ((value.type is ExternFuncCType || value.type is FuncCType) && nodes[1] is NOpenParanthesis)
            {
              if (!((value.type as _FuncCTypeWrapper).returnType is VoidCType))
                Warn($"lvalue call to '{value}' will discard the return value of type '{(value.type as _FuncCTypeWrapper).returnType}'.", nameNode.file, nameNode.line);

              nodes.RemoveRange(0, 2);

              List<CValue> parameters = new List<CValue>();

              while (true)
              {
                if (nodes.Count == 0)
                {
                  Error($"Unexpected end of function call to '{value}'.", nameNode.file, nameNode.line);
                }
                else
                {
                  int nextCommaOrClosingParenthesis = nodes.FindNextSameScope(n => n is NComma || n is NCloseParanthesis);

                  if (nextCommaOrClosingParenthesis == -1)
                    Error($"Missing ',' or ')' whilst calling {value}.", nodes[0].file, nodes[0].line);

                  if (parameters.Count == 0 && nextCommaOrClosingParenthesis == 0 && nodes[0] is NCloseParanthesis)
                  {
                    nodes.RemoveAt(0);
                    break;
                  }

                  bool isLastParam = nodes[nextCommaOrClosingParenthesis] is NCloseParanthesis;

                  var parameterNodes = nodes.GetRange(0, nextCommaOrClosingParenthesis);
                  nodes.RemoveRange(0, nextCommaOrClosingParenthesis + 1);

                  var param = GetRValue(scope, parameterNodes, ref byteCodeState);

                  if (!(param is CNamedValue))
                    param.description += " (rvalue)";

                  parameters.Add(param);

                  if (isLastParam)
                    break;
                }
              }

              CValue returnValue;

              scope.instructions.Add(new CInstruction_CallFunctionPtr(value, parameters, out returnValue, scope.maxRequiredStackSpace, nameNode.file, nameNode.line));

              return returnValue;
            }
            else
            {
              // TODO: '.', '->'
              Error($"Unexpected Token: '{nodes[0]}'", nodes[0].file, nodes[0].line);
              return null;
            }
          }
          else
          {
            return value;
          }
        }
        else
        {
          var function = scope.GetFunction((nodes[0] as NName).name);
          
          if (function == null)
            Error($"Unknown identifer '{(nodes[0] as NName).name}'.", nameNode.file, nameNode.line);

          if (!(function.returnType is VoidCType))
            Warn($"lvalue call to {function} will discard the return value of type '{function.returnType}'.", nameNode.file, nameNode.line);

          if (nodes.Count == 1 || !(nodes[1] is NOpenParanthesis) || !(nodes[nodes.Count - 1] is NCloseParanthesis))
            Error($"Incomplete or invalid reference to function '{(nodes[0] as NName).name}'.", nameNode.file, nameNode.line);

          List<CValue> parameters = new List<CValue>();
          nodes.RemoveRange(0, 2);

          while (true)
          {
            if (nodes.Count == 0)
            {
              Error($"Unexpected end of function call to '{function}'.", nameNode.file, nameNode.line);
            }
            else
            {
              int nextCommaOrClosingParenthesis = nodes.FindNextSameScope(n => n is NComma);

              if (nextCommaOrClosingParenthesis == -1)
                nextCommaOrClosingParenthesis = nodes.Count - 1;

              if (parameters.Count == 0 && nextCommaOrClosingParenthesis == 0 && nodes[0] is NCloseParanthesis)
              {
                nodes.RemoveAt(0);
                break;
              }

              bool isLastParam = nodes[nextCommaOrClosingParenthesis] is NCloseParanthesis;

              var parameterNodes = nodes.GetRange(0, nextCommaOrClosingParenthesis);
              nodes.RemoveRange(0, nextCommaOrClosingParenthesis + 1);

              var param = GetRValue(scope, parameterNodes, ref byteCodeState);

              if (!(param is CNamedValue))
                param.description += " (rvalue)";

              parameters.Add(param);

              if (isLastParam)
                break;
            }
          }

          CValue returnValue;

          scope.instructions.Add(new CInstruction_CallFunction(function, parameters, out returnValue, scope.maxRequiredStackSpace, nameNode.file, nameNode.line));

          return returnValue;
        }
      }
      else
      {
        Error($"Unexpected {nodes[0]}.", nodes[0].file, nodes[0].line);
        return null; // Unreachable.
      }
    }

    private static CValue GetRValue(Scope scope, List<Node> nodes, ref ByteCodeState byteCodeState)
    {
      // TODO: Optional expected type?

      // Handle Operator Precedence.

      int nextOperator = nodes.FindNextSameScope(n => n is NOperator && new string[] { "=" }.Contains((n as NOperator).operatorType));

      if (nextOperator == -1)
        nextOperator = nodes.FindNextSameScope(n => n is NOperator && new string[] { "&&" }.Contains((n as NOperator).operatorType));

      if (nextOperator == -1)
        nextOperator = nodes.FindNextSameScope(n => n is NOperator && new string[] { "==", "<=", "!=", ">=" }.Contains((n as NOperator).operatorType));

      if (nextOperator == -1)
      {
        bool lastWasCastOrType = false;

        nextOperator = nodes.FindNextSameScope(n =>
        {
          bool ret = !lastWasCastOrType && n is NOperator && new string[] { "<", ">" }.Contains((n as NOperator).operatorType);

          lastWasCastOrType = !lastWasCastOrType && (n is NCastKeyword || n is NType || (n is NOperator && ((n as NOperator).operatorType == "<" || (n as NOperator).operatorType == ">")));

          return ret;
        });
      }

      if (nextOperator == -1)
        nextOperator = nodes.FindNextSameScope(n => n is NOperator && new string[] { "~", "!" }.Contains((n as NOperator).operatorType));

      if (nextOperator == -1)
        nextOperator = nodes.FindNextSameScope(n => n is NOperator && new string[] { "#<#", "#>#" }.Contains((n as NOperator).operatorType));

      if (nextOperator == -1)
        nextOperator = nodes.FindNextSameScope(n => n is NOperator && new string[] { "+", "-", "|" }.Contains((n as NOperator).operatorType));

      if (nextOperator == -1)
        nextOperator = nodes.FindNextSameScope(n => n is NOperator && new string[] { "*", "/", "%", "&", "^" }.Contains((n as NOperator).operatorType));

      if (nextOperator != -1)
      {
        var operatorNode = nodes[nextOperator] as NOperator;

        if (nextOperator == 0)
        {
          var value = GetRValue(scope, nodes.GetRange(1, nodes.Count - 1), ref byteCodeState);
          
          switch (operatorNode.operatorType)
          {
            case "~":
              {
                CValue resultingValue;

                scope.instructions.Add(new CInstruction_Inverse(value, scope.maxRequiredStackSpace, out resultingValue, operatorNode.file, operatorNode.line));

                return resultingValue;
              }

            case "!":
              {
                CValue resultingValue;

                scope.instructions.Add(new CInstruction_LogicalNot(value, scope.maxRequiredStackSpace, out resultingValue, operatorNode.file, operatorNode.line));

                return resultingValue;
              }

            case "-":
              {
                if (value is CConstIntValue)
                {
                  var _value = (value as CConstIntValue);

                  if ((_value.type as BuiltInCType).IsUnsigned())
                  {
                    if (_value.smallestPossibleSignedType == null)
                      Error($"Attempting to negate value '{value}' that cannot be represented by a signed value.", operatorNode.file, operatorNode.line);

                    CValue resultingValue = _value.MakeCastableClone(_value.smallestPossibleSignedType, scope, ref byteCodeState, operatorNode.file, operatorNode.line);
                    resultingValue.description += $" (negated '{value}')";

                    (resultingValue as CConstIntValue).ivalue = -(resultingValue as CConstIntValue).ivalue;
                    unchecked { (resultingValue as CConstIntValue).uvalue = (ulong)(resultingValue as CConstIntValue).ivalue; }

                    return resultingValue;
                  }
                  else
                  {
                    _value.description += " (negated)";
                    _value.ivalue = -_value.ivalue;
                    unchecked { _value.uvalue = (ulong)_value.ivalue; }

                    return _value;
                  }
                }
                else if (value is CConstFloatValue)
                {
                  var _value = (value as CConstFloatValue);

                  _value.description += " (negated)";
                  _value.value = -_value.value;

                  return _value;
                }
                else
                {
                  CValue resultingValue;

                  scope.instructions.Add(new CInstruction_Negate(value, scope.maxRequiredStackSpace, out resultingValue, operatorNode.file, operatorNode.line));

                  return resultingValue;
                }
              }

            default:
              {
                Error($"Unexpected {operatorNode} in rvalue.", operatorNode.file, operatorNode.line);
                return null; // Unreachable.
              }
          }
        }
        else
        {
          var lnodes = nodes.GetRange(0, nextOperator);
          var rnodes = nodes.GetRange(nextOperator + 1, nodes.Count - (nextOperator + 1));

          var left = GetRValue(scope, lnodes, ref byteCodeState);

          if (!(left is CNamedValue))
            left.description += " (left rvalue)";

          if (left.type is VoidCType)
            Error($"Type '{left.type}' is illegal for an rvalue.", operatorNode.file, operatorNode.line);

          if (left.type is ArrayCType)
          {
            if (!(left is CNamedValue))
              Error($"Type '{left.type}' cannot be used to perform operations on, since it's not named.", operatorNode.file, operatorNode.line);

            CGlobalValueReference tmp;

            scope.instructions.Add(new CInstruction_ArrayVariableToPtr(left as CNamedValue, out tmp, scope.maxRequiredStackSpace, operatorNode.file, operatorNode.line));

            left = tmp;
          }

          var right = GetRValue(scope, rnodes, ref byteCodeState);

          if (!(right is CNamedValue))
            right.description += " (right rvalue)";

          if (right.type is VoidCType)
            Error($"Type '{left.type}' is illegal for an rvalue.", operatorNode.file, operatorNode.line);

          if (right.type is ArrayCType)
          {
            if (!(right is CNamedValue))
              Error($"Type '{right.type}' cannot be used to perform operations on, since it's not named.", operatorNode.file, operatorNode.line);

            CGlobalValueReference tmp;

            scope.instructions.Add(new CInstruction_ArrayVariableToPtr(right as CNamedValue, out tmp, scope.maxRequiredStackSpace, operatorNode.file, operatorNode.line));

            right = tmp;
          }

          switch (operatorNode.operatorType)
          {
            case "=":
              {
                if (left.type.isConst)
                  Error($"Cannot assign '{right}' to constant value '{left}'.", operatorNode.file, operatorNode.line);

                scope.instructions.Add(new CInstruction_SetValueTo(left, right, operatorNode.file, operatorNode.line, scope.maxRequiredStackSpace));

                return right;
              }

            case "+":
              {
                CValue resultingValue;

                if (right is CConstIntValue)
                  scope.instructions.Add(new CInstruction_AddImm(left, (right as CConstIntValue).uvalue, scope.maxRequiredStackSpace, false, out resultingValue, operatorNode.file, operatorNode.line));
                else if (right is CConstFloatValue)
                  scope.instructions.Add(new CInstruction_AddImm(left, (right as CConstFloatValue).value, scope.maxRequiredStackSpace, false, out resultingValue, operatorNode.file, operatorNode.line));
                else
                  scope.instructions.Add(new CInstruction_Add(left, right, scope.maxRequiredStackSpace, false, out resultingValue, operatorNode.file, operatorNode.line));

                return resultingValue;
              }

            case "-":
              {
                CValue resultingValue;

                if (right is CConstIntValue)
                  unchecked { scope.instructions.Add(new CInstruction_AddImm(left, -(long)((right as CConstIntValue).uvalue), scope.maxRequiredStackSpace, false, out resultingValue, operatorNode.file, operatorNode.line)); }
                else if (right is CConstFloatValue)
                  scope.instructions.Add(new CInstruction_AddImm(left, -(right as CConstFloatValue).value, scope.maxRequiredStackSpace, false, out resultingValue, operatorNode.file, operatorNode.line));
                else
                  scope.instructions.Add(new CInstruction_Subtract(left, right, scope.maxRequiredStackSpace, false, out resultingValue, operatorNode.file, operatorNode.line));

                return resultingValue;
              }

            case "*":
              {
                CValue resultingValue;
                
                scope.instructions.Add(new CInstruction_Multiply(left, right, scope.maxRequiredStackSpace, false, out resultingValue, operatorNode.file, operatorNode.line));

                return resultingValue;
              }

            case "/":
              {
                CValue resultingValue;

                scope.instructions.Add(new CInstruction_Divide(left, right, scope.maxRequiredStackSpace, false, out resultingValue, operatorNode.file, operatorNode.line));

                return resultingValue;
              }

            case "%":
              {
                CValue resultingValue;

                scope.instructions.Add(new CInstruction_Modulo(left, right, scope.maxRequiredStackSpace, false, out resultingValue, operatorNode.file, operatorNode.line));

                return resultingValue;
              }

            case "&":
              {
                CValue resultingValue;

                scope.instructions.Add(new CInstruction_And(left, right, scope.maxRequiredStackSpace, false, out resultingValue, operatorNode.file, operatorNode.line));

                return resultingValue;
              }

            case "|":
              {
                CValue resultingValue;

                scope.instructions.Add(new CInstruction_Or(left, right, scope.maxRequiredStackSpace, false, out resultingValue, operatorNode.file, operatorNode.line));

                return resultingValue;
              }

            case "^":
              {
                CValue resultingValue;

                scope.instructions.Add(new CInstruction_XOr(left, right, scope.maxRequiredStackSpace, false, out resultingValue, operatorNode.file, operatorNode.line));

                return resultingValue;
              }

            case "#<#":
              {
                CValue resultingValue;

                scope.instructions.Add(new CInstruction_BitShiftLeft(left, right, scope.maxRequiredStackSpace, false, out resultingValue, operatorNode.file, operatorNode.line));

                return resultingValue;
              }

            case "#>#":
              {
                CValue resultingValue;

                scope.instructions.Add(new CInstruction_BitShiftRight(left, right, scope.maxRequiredStackSpace, false, out resultingValue, operatorNode.file, operatorNode.line));

                return resultingValue;
              }

            case "&&":
              {
                CValue resultingValue;

                scope.instructions.Add(new CInstruction_LogicalAnd(left, right, scope.maxRequiredStackSpace, false, out resultingValue, operatorNode.file, operatorNode.line));

                return resultingValue;
              }

            case "||":
              {
                CValue resultingValue;

                scope.instructions.Add(new CInstruction_LogicalOr(left, right, scope.maxRequiredStackSpace, false, out resultingValue, operatorNode.file, operatorNode.line));

                return resultingValue;
              }

            case "==":
              {
                CValue resultingValue;
                
                scope.instructions.Add(new CInstruction_Equals(left, right, scope.maxRequiredStackSpace, out resultingValue, operatorNode.file, operatorNode.line));

                return resultingValue;
              }

            case "!=":
              {
                CValue resultingValue;
                
                scope.instructions.Add(new CInstruction_NotEquals(left, right, scope.maxRequiredStackSpace, out resultingValue, operatorNode.file, operatorNode.line));

                return resultingValue;
              }

            case "<=":
              {
                CValue resultingValue;

                scope.instructions.Add(new CInstruction_LessOrEqual(left, right, scope.maxRequiredStackSpace, out resultingValue, operatorNode.file, operatorNode.line));

                return resultingValue;
              }

            case ">=":
              {
                CValue resultingValue;

                scope.instructions.Add(new CInstruction_GreaterOrEqual(left, right, scope.maxRequiredStackSpace, out resultingValue, operatorNode.file, operatorNode.line));

                return resultingValue;
              }

            case "<":
              {
                CValue resultingValue;

                scope.instructions.Add(new CInstruction_Less(left, right, scope.maxRequiredStackSpace, out resultingValue, operatorNode.file, operatorNode.line));

                return resultingValue;
              }

            case ">":
              {
                CValue resultingValue;

                scope.instructions.Add(new CInstruction_Greater(left, right, scope.maxRequiredStackSpace, out resultingValue, operatorNode.file, operatorNode.line));

                return resultingValue;
              }

            default:
              {
                throw new NotImplementedException();
              }
          }
        }
      }
      else if (nodes[0] is NName)
      {
        var value = scope.GetVariable((nodes[0] as NName).name);
        var nameNode = nodes[0];

        if (value != null)
        {
          if (nodes.Count > 1)
          {
            // Call to extern_func or func.
            if ((value.type is ExternFuncCType || value.type is FuncCType) && nodes[1] is NOpenParanthesis)
            {
              if ((value.type as ExternFuncCType).returnType is VoidCType)
                Error($"Invalid return type '{(value.type as ExternFuncCType).returnType}' of rvalue function ptr call to '{value}'.", nameNode.file, nameNode.line);

              nodes.RemoveRange(0, 2);

              List<CValue> parameters = new List<CValue>();

              while (true)
              {
                if (nodes.Count == 0)
                {
                  Error($"Unexpected end of function call to '{value}'.", nameNode.file, nameNode.line);
                }
                else
                {
                  int nextCommaOrClosingParenthesis = nodes.FindNextSameScope(n => n is NComma || n is NCloseParanthesis);

                  if (nextCommaOrClosingParenthesis == -1)
                    Error($"Missing ',' or ')' whilst calling {value}.", nodes[0].file, nodes[0].line);

                  if (parameters.Count == 0 && nextCommaOrClosingParenthesis == 0 && nodes[0] is NCloseParanthesis)
                  {
                    nodes.RemoveAt(0);
                    break;
                  }

                  bool isLastParam = nodes[nextCommaOrClosingParenthesis] is NCloseParanthesis;

                  var parameterNodes = nodes.GetRange(0, nextCommaOrClosingParenthesis);
                  nodes.RemoveRange(0, nextCommaOrClosingParenthesis + 1);

                  var param = GetRValue(scope, parameterNodes, ref byteCodeState);

                  if (!(param is CNamedValue))
                    param.description += " (rvalue)";

                  parameters.Add(param);

                  if (isLastParam)
                    break;
                }
              }

              CValue returnValue;

              scope.instructions.Add(new CInstruction_CallFunctionPtr(value, parameters, out returnValue, scope.maxRequiredStackSpace, nameNode.file, nameNode.line));

              return returnValue;
            }
            else if (nodes[1] is NOpenBracket)
            {
              var xvalue = GetXValue(scope, nodes, ref byteCodeState);

              CValue dereferencedValue;

              scope.instructions.Add(new CInstruction_DereferencePtr(xvalue, out dereferencedValue, scope.maxRequiredStackSpace, nodes[0].file, nodes[0].line));

              return dereferencedValue;
            }
            else
            {
              Error($"Unexpected Token {nodes[0]}.", nodes[0].file, nodes[0].line);

              // TODO: '.', '->'
              throw new NotImplementedException();
            }
          }
          else
          {
            return value;
          }
        }
        else
        {
          var function = scope.GetFunction((nodes[0] as NName).name);

          if (function == null)
            Error($"Unknown identifer '{(nodes[0] as NName).name}'.", nameNode.file, nameNode.line);

          if (function.returnType is VoidCType)
            Error($"Invalid return type '{function.returnType}' of rvalue function call to '{function}'.", nameNode.file, nameNode.line);

          if (nodes.Count == 1)
          {
            if (function is CBuiltInFunction)
              Error($"Cannot retrieve the address of builtin function.", nameNode.file, nameNode.line);

            throw new NotImplementedException("Return ptr to function.");
          }

          if (!(nodes[1] is NOpenParanthesis))
            Error($"Incomplete or invalid reference to function '{(nodes[0] as NName).name}'.", nodes[0].file, nodes[0].line);

          nodes.RemoveRange(0, 2);

          List<CValue> parameters = new List<CValue>();

          while (true)
          {
            if (nodes.Count == 0)
            {
              Error($"Unexpected end of function call to '{function}'.", nameNode.file, nameNode.line);
            }
            else
            {
              int nextCommaOrClosingParenthesis = nodes.FindNextSameScope(n => n is NComma || n is NCloseParanthesis);

              if (nextCommaOrClosingParenthesis == -1)
                Error($"Missing ',' or ')' whilst calling {function}.", nodes[0].file, nodes[0].line);

              if (parameters.Count == 0 && nextCommaOrClosingParenthesis == 0 && nodes[0] is NCloseParanthesis)
              {
                nodes.RemoveAt(0);
                break;
              }

              bool isLastParam = nodes[nextCommaOrClosingParenthesis] is NCloseParanthesis;

              var parameterNodes = nodes.GetRange(0, nextCommaOrClosingParenthesis);
              nodes.RemoveRange(0, nextCommaOrClosingParenthesis + 1);
              
              var param = GetRValue(scope, parameterNodes, ref byteCodeState);

              if (!(param is CNamedValue))
                param.description += " (rvalue)";

              parameters.Add(param);

              if (isLastParam)
                break;
            }
          }

          CValue returnValue;

          scope.instructions.Add(new CInstruction_CallFunction(function, parameters, out returnValue, scope.maxRequiredStackSpace, nameNode.file, nameNode.line));

          return returnValue;
        }
      }
      else if (nodes[0] is NIntegerValue && nodes.Count == 1)
      {
        return new CConstIntValue(nodes[0] as NIntegerValue);
      }
      else if (nodes[0] is NFloatingPointValue && nodes.Count == 1)
      {
        return new CConstFloatValue(nodes[0] as NFloatingPointValue, BuiltInCType.Types["f64"]);
      }
      else if (nodes.NextIs(typeof(NCastKeyword), typeof(NOperator), typeof(NType), typeof(NOperator), typeof(NOpenParanthesis)) && (nodes[1] as NOperator).operatorType == "<" && (nodes[3] as NOperator).operatorType == ">" && nodes.Count > 5 && nodes.Last() is NCloseParanthesis)
      {
        var targetType = (nodes[2] as NType).type;
        var rValueToCast = GetRValue(scope, nodes.GetRange(5, nodes.Count - 6), ref byteCodeState);

        if (rValueToCast.type is ArrayCType && rValueToCast is CNamedValue)
        {
          CGlobalValueReference addressOf;

          scope.instructions.Add(new CInstruction_ArrayVariableToPtr((CNamedValue)rValueToCast, out addressOf, scope.maxRequiredStackSpace, nodes[2].file, nodes[2].line));

          rValueToCast = addressOf;
        }

        if (rValueToCast.type.Equals(targetType))
        {
          return rValueToCast;
        }
        else if (!rValueToCast.type.CanExplicitCastTo(targetType) && !rValueToCast.type.CanImplicitCastTo(targetType))
        {
          Error($"Explicit cast from type '{rValueToCast.type}' to type '{targetType}' is not possible for value '{rValueToCast}' (Defined in File '{rValueToCast.file ?? "?"}', Line {rValueToCast.line + 1}).", nodes[0].file, nodes[0].line);
        }
        else
        {
          var ret = rValueToCast.MakeCastableClone(targetType, scope, ref byteCodeState, nodes[2].file, nodes[2].line);
          ret.description += " (rvalue to cast to '" + targetType + "')";
          
          return ret;
        }

        return null; // Unreachable.
      }
      else if (nodes.Count >= 3 && nodes[0] is NPseudoFunction && nodes[1] is NOpenParanthesis && nodes[nodes.Count - 1] is NCloseParanthesis)
      {
        var pseudoFunction = nodes[0] as NPseudoFunction;
        nodes.RemoveAt(0);

        // is this a pair of parantheses?
        int parantesisLevel = 0;

        foreach (var node in nodes)
          if (node is NOpenParanthesis)
            parantesisLevel++;
          else if (node is NCloseParanthesis)
            if (--parantesisLevel == 0 && !object.ReferenceEquals(node, nodes[nodes.Count - 1]))
              Error($"Unexpected {node}.", node.file, node.line);
        
        switch (pseudoFunction.type)
        {
          case NPseudoFunctionType.SizeOf:
            {
              if (nodes.Count == 3 && nodes[1] is NType)
              {
                return new CConstIntValue(new NIntegerValue(false, (ulong)(nodes[1] as NType).type.GetSize(), (long)(nodes[1] as NType).type.GetSize(), pseudoFunction.file, pseudoFunction.line)) { description = $"'{pseudoFunction}'('{nodes[1]}')" };
              }
              else
              {
                // HACK: Ouch. This might actually move values around and generate instructions... :(
                var rvalue = GetRValue(scope, nodes.GetRange(1, nodes.Count - 2), ref byteCodeState);
                
                if (!(rvalue is CNamedValue))
                  rvalue.description += " (rvalue)";

                return new CConstIntValue(new NIntegerValue(false, (ulong)rvalue.type.GetSize(), (long)rvalue.type.GetSize(), pseudoFunction.file, pseudoFunction.line)) { description = $"'{pseudoFunction}'('{rvalue}')" };
              }
            }

          case NPseudoFunctionType.CountOf:
            {
              if (nodes.Count == 3 && nodes[1] is NType)
              {
                if (!((nodes[1] as NType).type is ArrayCType))
                  Error($"Invalid use of {nodes[1]} with {pseudoFunction}. Expected array or array type.", nodes[1].file, nodes[1].line);

                return new CConstIntValue(new NIntegerValue(false, (ulong)((nodes[1] as NType).type as ArrayCType).count, (long)((nodes[1] as NType).type as ArrayCType).count, pseudoFunction.file, pseudoFunction.line)) { description = $"'{pseudoFunction}'('{nodes[1]}')" };
              }
              else
              {
                // HACK: Ouch. This might actually move values around and generate instructions... :(
                var rvalue = GetRValue(scope, nodes.GetRange(1, nodes.Count - 2), ref byteCodeState);
                
                if (!(rvalue is CNamedValue))
                  rvalue.description += " (rvalue)";

                if (!(rvalue.type is ArrayCType))
                  Error($"Invalid use of rvalue of type '{rvalue.type}' with {pseudoFunction}. Expected array or array type.", nodes[1].file, nodes[1].line);

                return new CConstIntValue(new NIntegerValue(false, (ulong)(rvalue.type as ArrayCType).count, (long)(rvalue.type as ArrayCType).count, pseudoFunction.file, pseudoFunction.line)) { description = $"'{pseudoFunction}'('{rvalue}')" };
              }
            }

          case NPseudoFunctionType.OffsetOf:
            {
              if (nodes.Count == 5 && nodes[1] is NName && nodes[2] is NComma && nodes[3] is NType)
              {
                if (!((nodes[3] as NType).type is StructCType))
                  Error($"Invalid use of {nodes[3]} with {pseudoFunction}. Expected struct type.", nodes[3].file, nodes[3].line);

                var structType = ((nodes[3] as NType).type as StructCType);

                foreach (var attribute in structType.attributes)
                {
                  if (attribute.name == (nodes[1] as NName).name)
                    return new CConstIntValue(new NIntegerValue(false, (ulong)attribute.offset, (long)attribute.offset, pseudoFunction.file, pseudoFunction.line)) { description = $"'{pseudoFunction}'('{attribute} of {structType}')" };
                }

                Error($"Invalid attribute name ({nodes[1]}) for {structType} with {pseudoFunction}.", nodes[1].file, nodes[1].line);
                return null; // Unreachable.
              }
              else
              {
                Error($"Unexpected {pseudoFunction}.", pseudoFunction.file, pseudoFunction.line);
                return null; // Unreachable.
              }
            }

          case NPseudoFunctionType.AddressOf:
            {
              var xvalue = GetXValue(scope, nodes.GetRange(1, nodes.Count - 2), ref byteCodeState);

              return xvalue;
            }

          case NPseudoFunctionType.ValueOf:
            {
              var rvalue = GetRValue(scope, nodes.GetRange(1, nodes.Count - 2), ref byteCodeState);

              if (!(rvalue is CNamedValue))
                rvalue.description += " (rvalue)";

              if (!(rvalue.type is PtrCType))
                Error($"Cannot dereference rvalue of type {rvalue.type}. Expected value of pointer type.", nodes[1].file, nodes[1].line);

              CValue derefValue;
              
              scope.instructions.Add(new CInstruction_DereferencePtr(rvalue, out derefValue, scope.maxRequiredStackSpace, nodes[0].file, nodes[0].line));

              return derefValue;
            }

          case NPseudoFunctionType.FromRegister:
            {
              if (nodes.Count != 3 || !(nodes[1] is NIntegerValue))
                Error($"Unexpected {nodes[1]} in {pseudoFunction}.", nodes[1].file, nodes[1].line);

              var registerIndexNode = nodes[1] as NIntegerValue;

              if (registerIndexNode.isForcefullyNegative || registerIndexNode.uint_value >= (ulong)(IntegerRegisters + FloatRegisters))
                Error($"Invalid register index in {pseudoFunction}: {nodes[1]}. Expected 0 .. {Compiler.IntegerRegisters + Compiler.FloatRegisters - 1}", nodes[1].file, nodes[1].line);

              CValue value = null;

              if (registerIndexNode.uint_value < (ulong)IntegerRegisters)
                value = new CValue(nodes[1].file, nodes[1].line, BuiltInCType.Types["u64"], true) { description = $"from {nodes[1]}", hasPosition = true, position = Position.Register((int)registerIndexNode.uint_value) };
              else
                value = new CValue(nodes[1].file, nodes[1].line, BuiltInCType.Types["f64"], true) { description = $"from {nodes[1]}", hasPosition = true, position = Position.Register((int)registerIndexNode.uint_value) };

              value.type = value.type.MakeCastableClone(value.type);
              value.type.explicitCast = null;
              value.type.isConst = true;

              scope.instructions.Add(new CInstruction_CustomAction(e => { e.registers[value.position.registerIndex] = value; }, nodes[1].file, nodes[1].line));

              return value;
            }

          case NPseudoFunctionType.Line:
            {
              return new CConstIntValue((ulong)(nodes[0].line + 1), BuiltInCType.Types["u64"], nodes[0].file, nodes[0].line);
            }

          case NPseudoFunctionType.File:
            {
              throw new NotImplementedException();
            }

          default:
            throw new NotImplementedException();
        }
      }
      else if (nodes[0] is NOpenParanthesis && nodes[nodes.Count - 1] is NCloseParanthesis)
      {
        // is this a pair of parantheses?
        int parantesisLevel = 0;

        foreach (var node in nodes)
          if (node is NOpenParanthesis)
            parantesisLevel++;
          else if (node is NCloseParanthesis)
            if (--parantesisLevel == 0 && !object.ReferenceEquals(node, nodes[nodes.Count - 1]))
              Error($"Unexpected {node}.", node.file, node.line);

        return GetRValue(scope, nodes.GetRange(1, nodes.Count - 2), ref byteCodeState);
      }
      else if (nodes[0] is NStringValue && nodes.Count == 1)
      {
        byte[] bytes = (nodes[0] as NStringValue).bytes;

        CType internalType = BuiltInCType.Types["i8"];
        internalType = internalType.MakeCastableClone(internalType);
        internalType.explicitCast = null;
        internalType.isConst = true;

        CValue inlineString = new CValue(nodes[0].file, nodes[0].line, new ArrayCType(internalType, bytes.LongLength) { isConst = true }, true) { description = $"inline string '{(nodes[0] as NStringValue).value}'" };

        scope.instructions.Add(new CInstruction_InitializeArray(inlineString, bytes, nodes[0].file, nodes[0].line, scope.maxRequiredStackSpace));

        return inlineString;
      }
      else if (nodes[0] is NNull && nodes.Count == 1)
      {
        return new CNullValue(nodes[0].file, nodes[0].line);
      }
      else
      {
        Error($"Unexpected {nodes[0]}. Expected lvalue.", nodes[0].file, nodes[0].line);
        return null; // Unreachable.
      }
    }
    private static CValue GetXValue(Scope scope, List<Node> nodes, ref ByteCodeState byteCodeState)
    {
      if (nodes.Count == 0)
        Error("Missing xvalue.", null, -1);

      if (nodes[0] is NName)
      {
        var nameNode = nodes[0] as NName;

        if (nodes.Count == 1)
        {
          var variable = scope.GetVariable(nameNode.name);

          if (variable != null)
          {
            CGlobalValueReference addressOf;

            scope.instructions.Add(new CInstruction_AddressOfVariable(variable, out addressOf, scope.maxRequiredStackSpace, nodes[0].file, nodes[0].line));

            return addressOf;
          }
          else
          {
            Error($"Unknown token '{nameNode}'. Expected variable name.", nameNode.file, nameNode.line);
            throw new NotImplementedException();
          }
        }
        else
        {
          nodes.RemoveAt(0);

          if (nodes.Count > 2 && nodes[0] is NOpenBracket && nodes[nodes.Count - 1] is NCloseBracket)
          {
            // is this a pair of brackets?
            int bracketLevel = 0;

            foreach (var node in nodes)
              if (node is NOpenBracket)
                bracketLevel++;
              else if (node is NCloseBracket)
                if (--bracketLevel == 0 && !object.ReferenceEquals(node, nodes[nodes.Count - 1]))
                  Error($"Unexpected {node}.", node.file, node.line);
            
            var variable = scope.GetVariable(nameNode.name);

            if (variable == null)
              Error($"Invalid Token {nameNode}. Expected Variable Name.", nameNode.file, nameNode.line);

            CValue ptrWithoutOffset;

            if (variable.type is ArrayCType)
            {
              var type = variable.type as ArrayCType;

              if (type.type is VoidCType)
                Error($"Invalid array of type {type.type} ({variable}).", nameNode.file, nameNode.line);

              CGlobalValueReference reference;

              scope.instructions.Add(new CInstruction_ArrayVariableToPtr(variable, out reference, scope.maxRequiredStackSpace, nameNode.file, nameNode.line));

              ptrWithoutOffset = reference;
            }
            else if (variable.type is PtrCType)
            {
              var type = variable.type as PtrCType;

              if (type.pointsTo is VoidCType)
                Error($"Cannot use array accessor on pointer of type {type.pointsTo} ({variable}).", nameNode.file, nameNode.line);

              ptrWithoutOffset = variable;
            }
            else
            {
              Error($"Cannot use array accessor on type {variable.type}. Expected Pointer or Array.", nameNode.file, nameNode.line);
              return null; // Unreachable.
            }

            var index = GetRValue(scope, nodes.GetRange(1, nodes.Count - 2), ref byteCodeState);

            if (!(index is CNamedValue))
              index.description += " (rvalue)";

            CValue offsetPtr;
            scope.instructions.Add(new CInstruction_Add(ptrWithoutOffset, index, scope.maxRequiredStackSpace, false, out offsetPtr, nodes[0].file, nodes[0].line));

            return offsetPtr;
          }
          else
          {
            Error($"Unexpected {nodes[0]}.", nodes[0].file, nodes[0].line);
            return null; // Unreachable.
          }
        }
      }
      else
      {
        Error($"Unexpected {nodes[0]}.", nodes[0].file, nodes[0].line);
        return null; // Unreachable.
      }
    }
  }
}
