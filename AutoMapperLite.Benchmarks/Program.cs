using System.Reflection;
using AutoMapperLite.Benchmarks;
using BenchmarkDotNet.Running;

if (args.Contains("--verify"))
{
    Verify.Run();
    return;
}

BenchmarkSwitcher.FromAssembly(Assembly.GetExecutingAssembly()).Run(args);
