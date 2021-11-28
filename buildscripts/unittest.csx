#r "System"

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

void Fail(string error)
{
  Console.WriteLine(error);
  Environment.Exit(-1);
}

string CallProcess(string processName, string args, out int exitCode)
{
  StringBuilder outputBuilder;
  System.Diagnostics.ProcessStartInfo processStartInfo;
  System.Diagnostics.Process process;

  outputBuilder = new StringBuilder();

  processStartInfo = new System.Diagnostics.ProcessStartInfo();
  processStartInfo.CreateNoWindow = true;
  processStartInfo.RedirectStandardOutput = true;
  processStartInfo.RedirectStandardInput = true;
  processStartInfo.UseShellExecute = false;
  processStartInfo.Arguments = args;
  processStartInfo.FileName = processName;

  process = new System.Diagnostics.Process();
  process.StartInfo = processStartInfo;
  process.EnableRaisingEvents = true;

  process.OutputDataReceived += new System.Diagnostics.DataReceivedEventHandler
  (
    delegate(object sender, System.Diagnostics.DataReceivedEventArgs e)
    {
      outputBuilder.Append(e.Data);
    }
  );
  
  process.Start();
  process.BeginOutputReadLine();
  process.WaitForExit();

  System.Threading.Thread.Sleep(200);

  process.CancelOutputRead();

  exitCode = process.ExitCode;

  return outputBuilder.ToString();
}

string scriptFile = "tmp/code.lls";

void TestWithParams(string test, string param)
{
  Console.WriteLine("\t" + (string.IsNullOrEmpty(param) ? "(default)" : param));
 
  string byteCodeFile = "tmp/bytecode.lls";

  string output = CallProcess("..\\builds\\bin\\llsc.exe", $"{scriptFile}{param} -o=\"{byteCodeFile}\"", out int exitCode);
 
  if (exitCode != 0)
    Fail($"Failed to compile test {test} (error code 0x{exitCode:X})\n\n{output}");
  
  output = CallProcess("..\\builds\\bin\\llscript_exec.exe", byteCodeFile, out exitCode);
 
  if (exitCode != 0)
    Fail($"Failed to execute test {test} (error code 0x{exitCode:X})\n\n{output}");
 
  var expected = File.ReadAllText(test.Replace("lls", "txt"));
  
  if (expected != output)
    Fail($"Invalid output for test {test}\n\nExpected:  ({expected.Length} chars) '{expected}'\nRetrieved: ({output.Length} chars) '{output}'");
}

try
{
  Directory.CreateDirectory("tmp");

  var tests = Directory.GetFiles("tests/", "*.lls");

  Console.WriteLine($"\nRunning {tests.Length} UnitTest(s)...\n");

  foreach (var test in tests)
  {
    Console.WriteLine($"Testing {test} ...\n");

    File.Copy(test, scriptFile, true);

    string[] ps = new string[]{ " -S+", " -O0", " -dbgdb", " -assume=ByteCodeMutable" };

    for (int i = 0; i < (2 << (ps.Length - 1)); i++)
    {
      string param = "";

      for (int j = 0; j < ps.Length; j++)
        if ((i & (1 << j)) != 0)
          param += ps[j];
      
      TestWithParams(test, param);
    }

    Console.WriteLine("\n");
  }

  Console.WriteLine("\nAll UnitTests succeeded!");
}
catch (Exception e)
{
  Console.WriteLine($"Failed with error: {e.Message}\n{e}");
}

Directory.Delete("tmp", true);