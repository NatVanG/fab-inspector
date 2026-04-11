using Json.Logic;
using Json.More;
using FabInspector.Operators;
using Ric.Operators;
using System.Text.Json;
using System.Text.Json.Nodes;
using NUnit.Framework.Constraints;

namespace PBIRInspectorTests
{
    /// <summary>
    /// Tests to ensure each operator can be successfully invoked from a JSON rule.
    /// </summary>
    [TestFixture]
    public class OperatorJsonInvocationTests
    {
        private static readonly JsonSerializerOptions _serializerOptions;

        static OperatorJsonInvocationTests()
        {
            // Register all operators before running tests
            var ricRegistry = new JsonLogicOperatorRegistry(
                new RicSerializerContext(),
                new IJsonLogicOperator[] {
                    new CountOperator(),
                    new DrillVariableOperator(),
                    new FileSizeOperator(),
                    new FileTextSearchCountOperator(),
                    new IsNullOrEmptyOperator(),
                    new PartInfoOperator(),
                    new PartOperator(),
                    new PathOperator(),
                    new QueryOperator(),
                    new SetDifferenceOperator(),
                    new SetEqualOperator(),
                    new SetIntersectionOperator(),
                    new SetSymmetricDifferenceOperator(),
                    new SetUnionOperator(),
                    new StringContainsOperator(),
                    new ToRecordOperator(),
                    new ToStringOperator(),
                    new FromYamlFileOperator()
                });
            ricRegistry.RegisterAll();

            var fabRegistry = new JsonLogicOperatorRegistry(
                new FabInspectorSerializerContext(),
                new IJsonLogicOperator[] {
                    new RectangleOverlapOperator(),
                    new DaxQueryOperator(),
                    new ApiGetOperator(),
                    new ScannerApiOperator()
                });
            fabRegistry.RegisterAll();

            _serializerOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
        }

        #region FabInspector.Operators Tests

        [Test]
        public void RectOverlap_CanBeDeserialized()
        {
            var jsonRule = @"{
                ""rectoverlap"": [
                    [
                        {""name"": ""rect1"", ""x"": 0, ""y"": 0, ""width"": 100, ""height"": 100},
                        {""name"": ""rect2"", ""x"": 50, ""y"": 50, ""width"": 100, ""height"": 100}
                    ]
                ]
            }";

            var rule = JsonSerializer.Deserialize<Rule>(jsonRule, _serializerOptions);
            Assert.That(rule, Is.Not.Null);
            Assert.That(rule, Is.InstanceOf<RectOverlapRule>());

            var result = rule!.Apply(null);
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.InstanceOf<JsonArray>());
            
            var resultArray = result as JsonArray;
            Assert.That(resultArray!.Count, Is.EqualTo(2));
        }

        [Test]
        public void DaxQuery_CanBeDeserialized()
        {
            var jsonRule = @"{""daxquery"": [""EVALUATE SUMMARIZECOLUMNS('Table'[Column])""]}";
            var rule = JsonSerializer.Deserialize<Rule>(jsonRule, _serializerOptions);
            
            Assert.That(rule, Is.Not.Null);
            Assert.That(rule, Is.InstanceOf<DaxQueryRule>());
        }

        [Test]
        public void ScannerApi_CanBeDeserialized()
        {
            var jsonRule = @"{""scannerapi"": [""ws_guid1"", ""ws_guid2""]}";
            var rule = JsonSerializer.Deserialize<Rule>(jsonRule, _serializerOptions);

            Assert.That(rule, Is.Not.Null);
            Assert.That(rule, Is.InstanceOf<ScannerApiRule>());
        }

        [Test]
        public void RectOverlap_WithMargin_CanBeDeserialized()
        {
            var jsonRule = @"{
                ""rectoverlap"": [
                    [
                        {""name"": ""rect1"", ""x"": 0, ""y"": 0, ""width"": 100, ""height"": 100},
                        {""name"": ""rect2"", ""x"": 110, ""y"": 110, ""width"": 100, ""height"": 100}
                    ],
                    10
                ]
            }";

            var rule = JsonSerializer.Deserialize<Rule>(jsonRule, _serializerOptions);
            Assert.That(rule, Is.Not.Null);
            Assert.That(rule, Is.InstanceOf<RectOverlapRule>());

            var result = rule!.Apply(null);
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.InstanceOf<JsonArray>());
        }

        #endregion

        #region Ric.Operators Tests

        [Test]
        public void Count_CanBeDeserialized()
        {
            var jsonRule = @"{""count"": [[""a"", ""b"", ""c""]]}";
            var rule = JsonSerializer.Deserialize<Rule>(jsonRule, _serializerOptions);
            
            Assert.That(rule, Is.Not.Null);
            Assert.That(rule, Is.InstanceOf<CountRule>());

            var result = rule!.Apply(null);
            Assert.That(result?.GetValue<int>(), Is.EqualTo(3));
        }

        [Test]
        public void DrillVariable_CanBeDeserialized()
        {
            var jsonRule = @"{""drillvar"": ""testString""}";
            var rule = JsonSerializer.Deserialize<Rule>(jsonRule, _serializerOptions);
            
            Assert.That(rule, Is.Not.Null);
            Assert.That(rule, Is.InstanceOf<DrillVariableRule>());

            //var result = rule!.Apply(null);
            //Assert.That(result, Is.Not.Null);
        }

        [Test]
        public void FileSize_CanBeDeserialized()
        {
            var jsonRule = @"{""filesize"": [""test.txt""]}";
            var rule = JsonSerializer.Deserialize<Rule>(jsonRule, _serializerOptions);
            
            Assert.That(rule, Is.Not.Null);
            Assert.That(rule, Is.InstanceOf<FileSizeRule>());

            // Note: This will return -1 if file doesn't exist, but the test is about deserialization
            //var result = rule!.Apply(null);
            //Assert.That(result, Is.Not.Null);
        }

        [Test]
        public void FileTextSearchCount_CanBeDeserialized()
        {
            var jsonRule = @"{""filetextsearchcount"": [""test.txt"", ""pattern""]}";
            var rule = JsonSerializer.Deserialize<Rule>(jsonRule, _serializerOptions);
            
            Assert.That(rule, Is.Not.Null);
            Assert.That(rule, Is.InstanceOf<FileTextSearchCountRule>());

            //var result = rule!.Apply(null);
            //Assert.That(result, Is.Not.Null);
        }

        [Test]
        public void IsNullOrEmpty_CanBeDeserialized()
        {
            var jsonRule = @"{""isnullorempty"": """"}";
            var rule = JsonSerializer.Deserialize<Rule>(jsonRule, _serializerOptions);
            
            Assert.That(rule, Is.Not.Null);
            Assert.That(rule, Is.InstanceOf<IsNullOrEmptyRule>());

            var result = rule!.Apply(null);
            Assert.That(result?.GetValue<bool>(), Is.True);
        }

        [Test]
        public void PartInfo_CanBeDeserialized()
        {
            var jsonRule = @"{""partinfo"": [""name""]}";
            var rule = JsonSerializer.Deserialize<Rule>(jsonRule, _serializerOptions);
            
            Assert.That(rule, Is.Not.Null);
            Assert.That(rule, Is.InstanceOf<PartInfoRule>());

            //var result = rule!.Apply(null);
            //Assert.That(result, Is.Not.Null);
        }

        [Test]
        public void Part_CanBeDeserialized()
        {
            var jsonRule = @"{""part"": [""testPart""]}";
            var rule = JsonSerializer.Deserialize<Rule>(jsonRule, _serializerOptions);
            
            Assert.That(rule, Is.Not.Null);
            Assert.That(rule, Is.InstanceOf<PartRule>());

            //var result = rule!.Apply(null);
            //Assert.That(result, Is.Not.Null);
        }

        [Test]
        public void Path_CanBeDeserialized()
        {
            var jsonRule = @"{""path"": [""$.test""]}";
            var rule = JsonSerializer.Deserialize<Rule>(jsonRule, _serializerOptions);
            
            Assert.That(rule, Is.Not.Null);
            Assert.That(rule, Is.InstanceOf<PathRule>());

            var testData = JsonNode.Parse(@"{""test"": ""value""}");
            var result = rule!.Apply(testData);
            Assert.That(result, Is.Not.Null);
        }

        [Test]
        public void Query_CanBeDeserialized()
        {
            var jsonRule = @"{""query"": [""testQuery"", {}]}";
            var rule = JsonSerializer.Deserialize<Rule>(jsonRule, _serializerOptions);
            
            Assert.That(rule, Is.Not.Null);
            Assert.That(rule, Is.InstanceOf<QueryRule>());

            var result = rule!.Apply(null);
            Assert.That(result, Is.Not.Null);
        }

        [Test]
        public void SetDifference_CanBeDeserialized()
        {
            var jsonRule = @"{""diff"": [[""a"", ""b"", ""c""], [""b"", ""c"", ""d""]]}";
            var rule = JsonSerializer.Deserialize<Rule>(jsonRule, _serializerOptions);
            
            Assert.That(rule, Is.Not.Null);
            Assert.That(rule, Is.InstanceOf<SetDifferenceRule>());

            var result = rule!.Apply(null);
            Assert.That(result, Is.Not.Null);
            JsonAssert.AreEquivalent(JsonNode.Parse(@"[""a""]"), result);
        }

        [Test]
        public void SetEqual_CanBeDeserialized()
        {
            var jsonRule = @"{""equalsets"": [[""a"", ""b""], [""b"", ""a""]]}";
            var rule = JsonSerializer.Deserialize<Rule>(jsonRule, _serializerOptions);
            
            Assert.That(rule, Is.Not.Null);
            Assert.That(rule, Is.InstanceOf<SetEqualRule>());

            var result = rule!.Apply(null);
            Assert.That(result?.GetValue<bool>(), Is.True);
        }

        [Test]
        public void SetIntersection_CanBeDeserialized()
        {
            var jsonRule = @"{""intersection"": [[""a"", ""b"", ""c""], [""b"", ""c"", ""d""]]}";
            var rule = JsonSerializer.Deserialize<Rule>(jsonRule, _serializerOptions);
            
            Assert.That(rule, Is.Not.Null);
            Assert.That(rule, Is.InstanceOf<SetIntersectionRule>());

            var result = rule!.Apply(null);
            Assert.That(result, Is.Not.Null);
            JsonAssert.AreEquivalent(JsonNode.Parse(@"[""b"", ""c""]"), result);
        }

        [Test]
        public void SetSymmetricDifference_CanBeDeserialized()
        {
            var jsonRule = @"{""symdiff"": [[""a"", ""b"", ""c""], [""b"", ""c"", ""d""]]}";
            var rule = JsonSerializer.Deserialize<Rule>(jsonRule, _serializerOptions);
            
            Assert.That(rule, Is.Not.Null);
            Assert.That(rule, Is.InstanceOf<SetSymmetricDifferenceRule>());

            var result = rule!.Apply(null);
            Assert.That(result, Is.Not.Null);
            JsonAssert.AreEquivalent(JsonNode.Parse(@"[""a"", ""d""]"), result);
        }

        [Test]
        public void SetUnion_CanBeDeserialized()
        {
            var jsonRule = @"{""union"": [[""a"", ""b""], [""c"", ""d""]]}";
            var rule = JsonSerializer.Deserialize<Rule>(jsonRule, _serializerOptions);
            
            Assert.That(rule, Is.Not.Null);
            Assert.That(rule, Is.InstanceOf<SetUnionRule>());

            var result = rule!.Apply(null);
            Assert.That(result, Is.Not.Null);
            JsonAssert.AreEquivalent(JsonNode.Parse(@"[""a"", ""b"", ""c"", ""d""]"), result);
        }

        [Test]
        public void StringContains_CanBeDeserialized()
        {
            var jsonRule = @"{""strcontains"": [""Hello world"", ""world""]}";
            var rule = JsonSerializer.Deserialize<Rule>(jsonRule, _serializerOptions);
            
            Assert.That(rule, Is.Not.Null);
            Assert.That(rule, Is.InstanceOf<StringContains>());

            var result = rule!.Apply(null);
            Assert.That(result?.GetValue<int>(), Is.GreaterThan(0));
        }

        [Test]
        public void ToRecord_CanBeDeserialized()
        {
            var jsonRule = @"{""torecord"": [[""key1"", ""key2""], [""value1"", ""value2""]]}";
            var rule = JsonSerializer.Deserialize<Rule>(jsonRule, _serializerOptions);
            
            Assert.That(rule, Is.Not.Null);
            Assert.That(rule, Is.InstanceOf<ToRecordRule>());

            var result = rule!.Apply(null);
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.InstanceOf<JsonObject>());
        }

        [Test]
        public void ToString_CanBeDeserialized()
        {
            var jsonRule = @"{""tostring"": [""value""]}";
            var rule = JsonSerializer.Deserialize<Rule>(jsonRule, _serializerOptions);
            
            Assert.That(rule, Is.Not.Null);
            Assert.That(rule, Is.InstanceOf<ToString>());

            //var result = rule!.Apply(null);
            //Assert.That(result, Is.Not.Null);
            //Assert.That(result.ToString(), Does.Contain("test"));
        }

        [Test]
        public void FromYamlFile_CanBeDeserialized()
        {
            var jsonRule = @"{""fromyamlfile"": [""test.yaml""]}";
            var rule = JsonSerializer.Deserialize<Rule>(jsonRule, _serializerOptions);
            
            Assert.That(rule, Is.Not.Null);
            Assert.That(rule, Is.InstanceOf<FromYamlFileRule>());
        }

        #endregion

        #region Complex Operator Tests

        [Test]
        public void ComplexRule_WithMultipleOperators_CanBeDeserialized()
        {
            var jsonRule = @"{
                ""and"": [
                    {""equalsets"": [[""a"", ""b""], [""b"", ""a""]]},
                    {"">"": [{""count"": [[1, 2, 3]]}, 2]}
                ]
            }";

            var rule = JsonSerializer.Deserialize<Rule>(jsonRule, _serializerOptions);
            Assert.That(rule, Is.Not.Null);

            var result = rule!.Apply(null);
            Assert.That(result?.GetValue<bool>(), Is.True);
        }

        [Test]
        public void NestedOperators_CanBeDeserialized()
        {
            var jsonRule = @"{
                ""count"": [
                    {""union"": [[""a"", ""b""], [""c"", ""d""]]}
                ]
            }";

            var rule = JsonSerializer.Deserialize<Rule>(jsonRule, _serializerOptions);
            Assert.That(rule, Is.Not.Null);

            var result = rule!.Apply(null);
            Assert.That(result?.GetValue<int>(), Is.EqualTo(4));
        }

        [Test]
        public void SetOperations_ChainedTogether_CanBeDeserialized()
        {
            var jsonRule = @"{
                ""count"": [
                    {""intersection"": [
                        {""union"": [[""a"", ""b""], [""b"", ""c""]]},
                        [""a"", ""b"", ""c"", ""d""]
                    ]}
                ]
            }";

            var rule = JsonSerializer.Deserialize<Rule>(jsonRule, _serializerOptions);
            Assert.That(rule, Is.Not.Null);

            var result = rule!.Apply(null);
            Assert.That(result?.GetValue<int>(), Is.EqualTo(3));
        }

        [Test]
        public void ConditionalWithCustomOperators_CanBeDeserialized()
        {
            var jsonRule = @"{
                ""if"": [
                    {""isnullorempty"": """"},
                    ""empty"",
                    ""not empty""
                ]
            }";

            var rule = JsonSerializer.Deserialize<Rule>(jsonRule, _serializerOptions);
            Assert.That(rule, Is.Not.Null);

            var result = rule!.Apply(null);
            Assert.That(result?.GetValue<string>(), Is.EqualTo("empty"));
        }

        #endregion
    }
}
