using Mapster;

namespace AutoMapperLite.Benchmarks
{
    /// <summary>
    /// Dev-only correctness check ("dotnet run -- --verify"): confirms every library configured
    /// in the shared benchmark setup actually produces equivalent output before trusting a
    /// multi-minute BenchmarkDotNet run's numbers. Not itself a benchmark.
    /// </summary>
    internal static class Verify
    {
        private sealed class Harness : ComparisonBenchmarkBase
        {
            public new void Setup() => base.Setup();
        }

        public static void Run()
        {
            var h = new Harness();
            h.Setup();

            int fails = 0;
            void Check(string label, bool condition, string detail = "")
            {
                if (condition) Console.WriteLine($"PASS: {label}");
                else { Console.WriteLine($"FAIL: {label} {detail}"); fails++; }
            }

            var simpleAml = h.AutoMapperLite.Map<SimpleDestination>(h.SimpleSource);
            var simpleAmlTyped = h.AutoMapperLite.Map<SimpleSource, SimpleDestination>(h.SimpleSource);
            var simpleAm = h.AutoMapper.Map<SimpleDestination>(h.SimpleSource);
            var simpleMs = h.SimpleSource.Adapt<SimpleDestination>(h.MapsterConfig);
            Check("Simple: AutoMapperLite", simpleAml.Id == 1 && simpleAml.Name == "Benchmark");
            Check("Simple: AutoMapperLite (typed API)", simpleAmlTyped.Id == 1 && simpleAmlTyped.Name == "Benchmark");
            Check("Simple: AutoMapper", simpleAm.Id == 1 && simpleAm.Name == "Benchmark");
            Check("Simple: Mapster", simpleMs.Id == 1 && simpleMs.Name == "Benchmark");

            var medAml = h.AutoMapperLite.Map<MediumDestination>(h.MediumSource);
            var medAm = h.AutoMapper.Map<MediumDestination>(h.MediumSource);
            var medMs = h.MediumSource.Adapt<MediumDestination>(h.MapsterConfig);
            Check("Medium: AutoMapperLite", medAml.Email == "benchmark@example.com" && medAml.Balance == 1234.56m && medAml.ExternalId == 123456789L);
            Check("Medium: AutoMapper", medAm.Email == "benchmark@example.com" && medAm.Balance == 1234.56m && medAm.ExternalId == 123456789L);
            Check("Medium: Mapster", medMs.Email == "benchmark@example.com" && medMs.Balance == 1234.56m && medMs.ExternalId == 123456789L);

            var nestAml = h.AutoMapperLite.Map<OrganizationViewModel>(h.OrganizationSource);
            var nestAm = h.AutoMapper.Map<OrganizationViewModel>(h.OrganizationSource);
            var nestMs = h.OrganizationSource.Adapt<OrganizationViewModel>(h.MapsterConfig);
            Check("Nested: AutoMapperLite", nestAml.CountryViewModel.Name == "Wonderland");
            Check("Nested: AutoMapper", nestAm.CountryViewModel.Name == "Wonderland");
            Check("Nested: Mapster", nestMs.CountryViewModel.Name == "Wonderland");

            var deepAml = h.AutoMapperLite.Map<Level1Dto>(h.DeepNestedSource);
            var deepAmlTyped = h.AutoMapperLite.Map<Level1, Level1Dto>(h.DeepNestedSource);
            var deepAm = h.AutoMapper.Map<Level1Dto>(h.DeepNestedSource);
            var deepMs = h.DeepNestedSource.Adapt<Level1Dto>(h.MapsterConfig);
            Check("DeepNested: AutoMapperLite", deepAml.Child.Child.Child.Value == "leaf", $"got '{deepAml.Child?.Child?.Child?.Value}'");
            Check("DeepNested: AutoMapperLite (typed API)", deepAmlTyped.Child.Child.Child.Value == "leaf", $"got '{deepAmlTyped.Child?.Child?.Child?.Value}'");
            Check("DeepNested: AutoMapper", deepAm.Child.Child.Child.Value == "leaf", $"got '{deepAm.Child?.Child?.Child?.Value}'");
            Check("DeepNested: Mapster", deepMs.Child.Child.Child.Value == "leaf", $"got '{deepMs.Child?.Child?.Child?.Value}'");

            var custAml = h.AutoMapperLite.Map<PersonDestination>(h.PersonSource);
            var custAm = h.AutoMapper.Map<PersonDestination>(h.PersonSource);
            var custMs = h.PersonSource.Adapt<PersonDestination>(h.MapsterConfig);
            var expectedAge = DateTime.UtcNow.Year - 1990;
            Check("Custom: AutoMapperLite", custAml.FullName == "Ada Lovelace" && custAml.Age == expectedAge, $"got '{custAml.FullName}', age={custAml.Age}");
            Check("Custom: AutoMapper", custAm.FullName == "Ada Lovelace" && custAm.Age == expectedAge, $"got '{custAm.FullName}', age={custAm.Age}");
            Check("Custom: Mapster", custMs.FullName == "Ada Lovelace" && custMs.Age == expectedAge, $"got '{custMs.FullName}', age={custMs.Age}");

            var listAml = h.AutoMapperLite.Map<List<SimpleDestination>>(h.SourceList100);
            var listAmlTyped = h.AutoMapperLite.Map<List<SimpleSource>, List<SimpleDestination>>(h.SourceList100);
            var listAm = h.AutoMapper.Map<List<SimpleDestination>>(h.SourceList100);
            var listMs = h.SourceList100.Adapt<List<SimpleDestination>>(h.MapsterConfig);
            Check("Collection100: AutoMapperLite", listAml.Count == 100 && listAml[99].Name == "Item 99");
            Check("Collection100: AutoMapperLite (typed API)", listAmlTyped.Count == 100 && listAmlTyped[99].Name == "Item 99");
            Check("Collection100: AutoMapper", listAm.Count == 100 && listAm[99].Name == "Item 99");
            Check("Collection100: Mapster", listMs.Count == 100 && listMs[99].Name == "Item 99");

            Console.WriteLine();
            Console.WriteLine(fails == 0 ? "ALL SCENARIOS VERIFIED CORRECT" : $"{fails} SCENARIO(S) FAILED");
            Environment.Exit(fails == 0 ? 0 : 1);
        }
    }
}
