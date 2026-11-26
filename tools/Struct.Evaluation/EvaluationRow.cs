using System.Globalization;
using Struct.BLL.Core;

namespace Struct.Evaluation;

/// <summary>
/// One solver run on one scenario. <see cref="TopK"/> is set on exact rows only. <see cref="PctOfOptimal"/> is
/// empty unless the run filled as many slots as the best known build: objectives of different slot sets do not compare.
/// </summary>
public sealed record EvaluationRow(
    decimal Budget, Purpose Purpose, string Solver, int? TopK, double Lambda,
    double Objective, double? PctOfOptimal, decimal Price, bool Complete, int Filled, bool TimedOut, double Ms,
    string Cpu, string Gpu)
{
    public const string Header =
        "budget,purpose,solver,topk,lambda,objective,pct_of_optimal,price,budget_used_pct,complete,filled,timed_out,ms,cpu,gpu";

    public string ToCsv()
    {
        string topK = TopK switch { null => "", EvaluationHarness.AllCandidates => "all", var k => k.Value.ToString() };
        return string.Create(CultureInfo.InvariantCulture,
            $"{Budget},{Purpose},{Solver},{topK},{Lambda},{Objective:F4},{PctOfOptimal:F2},{Price},{Price / Budget * 100:F2}," +
            $"{(Complete ? "true" : "false")},{Filled},{(TimedOut ? "true" : "false")},{Ms:F2},{Quote(Cpu)},{Quote(Gpu)}");
    }

    private static string Quote(string s) => $"\"{s.Replace("\"", "\"\"")}\"";
}
