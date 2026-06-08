using System.Reflection;

namespace FabInspector.Tests
{
    [TestFixture]
    public class TMDLPartQueryTests
    {
        private static string SemanticModelFixturePath => Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "Files",
            "pbip",
            "Base-rules-passes.SemanticModel");

        [Test]
        public void PartQueryFactory_MapsSemanticModelToTMDLPartQuery()
        {
            var partAssembly = typeof(FabInspector.Core.Part.Part).Assembly;
            var factoryType = partAssembly.GetType("FabInspector.Core.Part.PartQueryFactory", throwOnError: true)!;

            var createMethod = factoryType.GetMethod(
                "CreatePartQuery",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(string), typeof(string), typeof(FabInspector.Core.IFabricFileSystem) },
                modifiers: null);

            Assert.That(createMethod, Is.Not.Null);

            var result = createMethod!.Invoke(null, new object?[] { "semanticmodel", SemanticModelFixturePath, null });

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.GetType().Name, Is.EqualTo("TMDLPartQuery"));
        }

        [Test]
        public void TMDLPartQuery_Helpers_ReturnExpectedParts_AndLenientForMissing()
        {
            var partAssembly = typeof(FabInspector.Core.Part.Part).Assembly;
            var queryType = partAssembly.GetType("FabInspector.Core.Part.TMDLPartQuery", throwOnError: true)!;

            Assert.That(queryType.GetMethod("AllTables", BindingFlags.Instance | BindingFlags.Public), Is.Null);
            Assert.That(queryType.GetMethod("AllCultures", BindingFlags.Instance | BindingFlags.Public), Is.Null);
            Assert.That(queryType.GetMethod("AllRoles", BindingFlags.Instance | BindingFlags.Public), Is.Null);
            Assert.That(queryType.GetMethod("AllPerspectives", BindingFlags.Instance | BindingFlags.Public), Is.Null);

            var query = Activator.CreateInstance(queryType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new object?[] { SemanticModelFixturePath, null }, null);
            Assert.That(query, Is.Not.Null);

            var rootPartProperty = queryType.GetProperty("RootPart");
            Assert.That(rootPartProperty, Is.Not.Null);

            var rootPart = rootPartProperty!.GetValue(query);
            Assert.That(rootPart, Is.Not.Null);

            object? definition = InvokeQueryMethod(queryType, query!, "Definition", rootPart!);
            object? database = InvokeQueryMethod(queryType, query!, "Database", rootPart!);
            object? model = InvokeQueryMethod(queryType, query!, "Model", rootPart!);
            object? expressions = InvokeQueryMethod(queryType, query!, "Expressions", rootPart!);
            object? relationships = InvokeQueryMethod(queryType, query!, "Relationships", rootPart!);
            object? dataSources = InvokeQueryMethod(queryType, query!, "DataSources", rootPart!);
            object? functions = InvokeQueryMethod(queryType, query!, "Functions", rootPart!);

            var tables = InvokeQueryMethod(queryType, query!, "Tables", rootPart!) as System.Collections.IEnumerable;
            var cultures = InvokeQueryMethod(queryType, query!, "Cultures", rootPart!) as System.Collections.IEnumerable;
            var roles = InvokeQueryMethod(queryType, query!, "Roles", rootPart!) as System.Collections.IEnumerable;
            var perspectives = InvokeQueryMethod(queryType, query!, "Perspectives", rootPart!) as System.Collections.IEnumerable;

            AssertPartFileName(definition, "definition.pbism");
            AssertPartFileName(database, "database.tmdl");
            AssertPartFileName(model, "model.tmdl");

            Assert.That(expressions, Is.Null);
            Assert.That(relationships, Is.Null);
            Assert.That(dataSources, Is.Null);
            Assert.That(functions, Is.Null);

            Assert.That(GetPartFileNames(tables!), Does.Contain("Inventory.tmdl"));
            Assert.That(GetPartFileNames(cultures!), Does.Contain("fr-FR.tmdl"));
            Assert.That(GetPartFileNames(roles!), Is.Empty);
            Assert.That(GetPartFileNames(perspectives!), Is.Empty);
        }

        private static object? InvokeQueryMethod(Type queryType, object query, string methodName, object context)
        {
            var method = queryType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(method, Is.Not.Null, $"Expected method '{methodName}' to exist.");

            return method!.Invoke(query, new[] { context });
        }

        private static void AssertPartFileName(object? partObject, string expectedFileName)
        {
            Assert.That(partObject, Is.Not.Null);

            var fileNameProperty = partObject!.GetType().GetProperty("FileSystemName");
            Assert.That(fileNameProperty, Is.Not.Null);

            var fileName = fileNameProperty!.GetValue(partObject) as string;
            Assert.That(fileName, Is.EqualTo(expectedFileName));
        }

        private static List<string> GetPartFileNames(System.Collections.IEnumerable parts)
        {
            var result = new List<string>();

            foreach (var part in parts)
            {
                var fileNameProperty = part!.GetType().GetProperty("FileSystemName");
                var fileName = fileNameProperty?.GetValue(part) as string;
                if (!string.IsNullOrWhiteSpace(fileName))
                {
                    result.Add(fileName);
                }
            }

            return result;
        }
    }
}
