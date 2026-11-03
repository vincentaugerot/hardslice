using System.Text;
using HardSlice.Tool;

// The payload of a hook and its answers are UTF-8, as the CLIs write and read them, whatever the code page of the console: decoded
// with another, a path such as C:\Users\Clément\... would no longer be found in the solution and its protected file not refused.
var hook = args is ["hook", ..];
var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
var input = hook ? new StreamReader(Console.OpenStandardInput(), utf8) : Console.In;
var output = hook ? new StreamWriter(Console.OpenStandardOutput(), utf8) { AutoFlush = true } : Console.Out;
var error = hook ? new StreamWriter(Console.OpenStandardError(), utf8) { AutoFlush = true } : Console.Error;

return Cli.Run(
    args, Environment.CurrentDirectory, new ProcessRunner(Environment.GetEnvironmentVariable), output, error, input,
    Environment.GetEnvironmentVariable, Cli.HookStateDirectory);
