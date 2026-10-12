using System;
using System.IO;
using SkillRuntimeChecks;

try
{
    Phase3BChecks.Run(Console.WriteLine);
    Phase3BChecks.RealExport(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../Assets/Resources/SkillConfigs/Character_miyabi_Skills.json")), Console.WriteLine);
    Phase3BChecks.Measure(Console.WriteLine);
    Phase3CChecks.Run(Console.WriteLine);
    Phase3CChecks.Measure(Console.WriteLine);
    Phase4BChecks.Run(Console.WriteLine, Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../")));
    Console.WriteLine("PHASE3C_ALL_PASS standalone .NET assertions; NOT Unity or battle regression");
    Console.WriteLine("PHASE3B_ALL_PASS standalone .NET assertions; NOT Unity or battle regression");
    return 0;
}
catch (Exception ex) { Console.Error.WriteLine("SKILL_RUNTIME_CHECKS_FAIL " + ex); return 1; }
