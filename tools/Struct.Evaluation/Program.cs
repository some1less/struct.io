using Struct.Evaluation;

// Run from the repo root: dotnet run -c Release --project tools/Struct.Evaluation [catalog.json]
var harness = EvaluationSetup.FromRepo(Directory.GetCurrentDirectory(), args.FirstOrDefault() ?? EvaluationSetup.DefaultCatalog);
harness.WriteAll(Path.Combine("data", "eval"));
