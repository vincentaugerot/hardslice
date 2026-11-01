using HardSlice.Tool;

return Cli.Run(args, Environment.CurrentDirectory, new ProcessRunner(), Console.Out, Console.Error, Console.In, Environment.GetEnvironmentVariable, Cli.HookStateDirectory);
