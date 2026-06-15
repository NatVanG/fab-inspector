using Json.Logic;
using Json.More;
using FabInspector.Core;
using Ric.Operators;
using System.Text.Json;
using System.Text.Json.Nodes;
using Rule = Json.Logic.Rule;

namespace FabInspector.Tests
{
    /// <summary>
    /// Unit tests for the new Ric.Operators: keys, values, distinct, typeof, hasprop,
    /// strsplit, strjoin, regexextract, coalesce, slice, now, datediff.
    /// </summary>
    [TestFixture]
    public class RicOperatorTests
    {
        private static readonly JsonSerializerOptions _serializerOptions;

        static RicOperatorTests()
        {
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
                    new SetIntersectOperator(),
                    new SetSymmetricDifferenceOperator(),
                    new SetUnionOperator(),
                    new StringContainsOperator(),
                    new ToRecordOperator(),
                    new ToStringOperator(),
                    new FromYamlFileOperator(),
                    new KeysOperator(),
                    new ValuesOperator(),
                    new DistinctOperator(),
                    new TypeOfOperator(),
                    new HasPropOperator(),
                    new StringSplitOperator(),
                    new StringJoinOperator(),
                    new RegexExtractOperator(),
                    new CoalesceOperator(),
                    new SliceOperator(),
                    new NowOperator(),
                    new DateDiffOperator()
                });
            ricRegistry.RegisterAll();

            _serializerOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
        }

        #region Keys

        [Test]
        public void Keys_ReturnsPropertyNames()
        {
            var rule = JsonSerializer.Deserialize<Rule>(
                @"{""keys"": [{""var"": """"}]}",
                _serializerOptions);

            var data = JsonNode.Parse(@"{""a"": 1, ""b"": 2, ""c"": 3}");
            var result = rule!.Apply(data) as JsonArray;

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Count, Is.EqualTo(3));
            JsonAssert.AreEquivalent(JsonNode.Parse(@"[""a"",""b"",""c""]"), result);
        }

        [Test]
        public void Keys_EmptyObject_ReturnsEmptyArray()
        {
            var rule = JsonSerializer.Deserialize<Rule>(
                @"{""keys"": [{""var"": """"}]}",
                _serializerOptions);

            var data = JsonNode.Parse(@"{}");
            var result = rule!.Apply(data) as JsonArray;

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Count, Is.EqualTo(0));
        }

        [Test]
        public void Keys_NonObject_Throws()
        {
            var rule = JsonSerializer.Deserialize<Rule>(
                @"{""keys"": [""not an object""]}",
                _serializerOptions);

            Assert.That(() => rule!.Apply(null), Throws.InstanceOf<JsonLogicException>());
        }

        #endregion

        #region Values

        [Test]
        public void Values_ReturnsPropertyValues()
        {
            var rule = JsonSerializer.Deserialize<Rule>(
                @"{""values"": [{""var"": """"}]}",
                _serializerOptions);

            var data = JsonNode.Parse(@"{""a"": 1, ""b"": 2, ""c"": 3}");
            var result = rule!.Apply(data) as JsonArray;

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Count, Is.EqualTo(3));
            JsonAssert.AreEquivalent(JsonNode.Parse(@"[1,2,3]"), result);
        }

        [Test]
        public void Values_EmptyObject_ReturnsEmptyArray()
        {
            var rule = JsonSerializer.Deserialize<Rule>(
                @"{""values"": [{""var"": """"}]}",
                _serializerOptions);

            var data = JsonNode.Parse(@"{}");
            var result = rule!.Apply(data) as JsonArray;

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Count, Is.EqualTo(0));
        }

        #endregion

        #region Distinct

        [Test]
        public void Distinct_RemovesDuplicates()
        {
            var rule = JsonSerializer.Deserialize<Rule>(
                @"{""distinct"": [[""a"", ""b"", ""a"", ""c"", ""b""]]}",
                _serializerOptions);

            var result = rule!.Apply(null) as JsonArray;

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Count, Is.EqualTo(3));
            JsonAssert.AreEquivalent(JsonNode.Parse(@"[""a"",""b"",""c""]"), result);
        }

        [Test]
        public void Distinct_AlreadyUnique_ReturnsSame()
        {
            var rule = JsonSerializer.Deserialize<Rule>(
                @"{""distinct"": [[1, 2, 3]]}",
                _serializerOptions);

            var result = rule!.Apply(null) as JsonArray;

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Count, Is.EqualTo(3));
        }

        [Test]
        public void Distinct_EmptyArray_ReturnsEmpty()
        {
            var rule = JsonSerializer.Deserialize<Rule>(
                @"{""distinct"": [[]]}",
                _serializerOptions);

            var result = rule!.Apply(null) as JsonArray;

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Count, Is.EqualTo(0));
        }

        #endregion

        #region TypeOf

        [Test]
        public void TypeOf_String_ReturnsString()
        {
            var rule = JsonSerializer.Deserialize<Rule>(
                @"{""typeof"": [""hello""]}",
                _serializerOptions);

            var result = rule!.Apply(null);
            Assert.That(result?.GetValue<string>(), Is.EqualTo("string"));
        }

        [Test]
        public void TypeOf_Number_ReturnsNumber()
        {
            var rule = JsonSerializer.Deserialize<Rule>(
                @"{""typeof"": [42]}",
                _serializerOptions);

            var result = rule!.Apply(null);
            Assert.That(result?.GetValue<string>(), Is.EqualTo("number"));
        }

        [Test]
        public void TypeOf_Boolean_ReturnsBoolean()
        {
            var rule = JsonSerializer.Deserialize<Rule>(
                @"{""typeof"": [true]}",
                _serializerOptions);

            var result = rule!.Apply(null);
            Assert.That(result?.GetValue<string>(), Is.EqualTo("boolean"));
        }

        [Test]
        public void TypeOf_Array_ReturnsArray()
        {
            var rule = JsonSerializer.Deserialize<Rule>(
                @"{""typeof"": [[1,2,3]]}",
                _serializerOptions);

            var result = rule!.Apply(null);
            Assert.That(result?.GetValue<string>(), Is.EqualTo("array"));
        }

        [Test]
        public void TypeOf_Object_ReturnsObject()
        {
            var rule = JsonSerializer.Deserialize<Rule>(
                @"{""typeof"": [{""var"": """"}]}",
                _serializerOptions);

            var data = JsonNode.Parse(@"{""a"": 1}");
            var result = rule!.Apply(data);
            Assert.That(result?.GetValue<string>(), Is.EqualTo("object"));
        }

        [Test]
        public void TypeOf_Null_ReturnsNull()
        {
            var rule = JsonSerializer.Deserialize<Rule>(
                @"{""typeof"": [null]}",
                _serializerOptions);

            var result = rule!.Apply(null);
            Assert.That(result?.GetValue<string>(), Is.EqualTo("null"));
        }

        #endregion

        #region HasProp

        [Test]
        public void HasProp_ExistingProperty_ReturnsTrue()
        {
            var rule = JsonSerializer.Deserialize<Rule>(
                @"{""hasprop"": [{""var"": """"}, ""name""]}",
                _serializerOptions);

            var data = JsonNode.Parse(@"{""name"": ""test""}");
            var result = rule!.Apply(data);
            Assert.That(result?.GetValue<bool>(), Is.True);
        }

        [Test]
        public void HasProp_MissingProperty_ReturnsFalse()
        {
            var rule = JsonSerializer.Deserialize<Rule>(
                @"{""hasprop"": [{""var"": """"}, ""missing""]}",
                _serializerOptions);

            var data = JsonNode.Parse(@"{""name"": ""test""}");
            var result = rule!.Apply(data);
            Assert.That(result?.GetValue<bool>(), Is.False);
        }

        [Test]
        public void HasProp_NullValue_ReturnsTrue()
        {
            var rule = JsonSerializer.Deserialize<Rule>(
                @"{""hasprop"": [{""var"": """"}, ""name""]}",
                _serializerOptions);

            var data = JsonNode.Parse(@"{""name"": null}");
            var result = rule!.Apply(data);
            Assert.That(result?.GetValue<bool>(), Is.True);
        }

        #endregion

        #region StringSplit

        [Test]
        public void StringSplit_SplitsByDelimiter()
        {
            var rule = JsonSerializer.Deserialize<Rule>(
                @"{""strsplit"": [""a,b,c"", "",""]}",
                _serializerOptions);

            var result = rule!.Apply(null) as JsonArray;

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Count, Is.EqualTo(3));
            JsonAssert.AreEquivalent(JsonNode.Parse(@"[""a"",""b"",""c""]"), result);
        }

        [Test]
        public void StringSplit_NoDelimiterFound_ReturnsSingleElement()
        {
            var rule = JsonSerializer.Deserialize<Rule>(
                @"{""strsplit"": [""abc"", ""-""]}",
                _serializerOptions);

            var result = rule!.Apply(null) as JsonArray;

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Count, Is.EqualTo(1));
            Assert.That(result[0]?.GetValue<string>(), Is.EqualTo("abc"));
        }

        [Test]
        public void StringSplit_WithVar_SplitsDataValue()
        {
            var rule = JsonSerializer.Deserialize<Rule>(
                @"{""strsplit"": [{""var"": ""path""}, ""/""]}",
                _serializerOptions);

            var data = JsonNode.Parse(@"{""path"": ""folder/subfolder/file.txt""}");
            var result = rule!.Apply(data) as JsonArray;

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Count, Is.EqualTo(3));
            Assert.That(result[2]?.GetValue<string>(), Is.EqualTo("file.txt"));
        }

        #endregion

        #region StringJoin

        [Test]
        public void StringJoin_JoinsWithSeparator()
        {
            var rule = JsonSerializer.Deserialize<Rule>(
                @"{""strjoin"": [[""a"", ""b"", ""c""], ""-""]}",
                _serializerOptions);

            var result = rule!.Apply(null);
            Assert.That(result?.GetValue<string>(), Is.EqualTo("a-b-c"));
        }

        [Test]
        public void StringJoin_EmptyArray_ReturnsEmptyString()
        {
            var rule = JsonSerializer.Deserialize<Rule>(
                @"{""strjoin"": [[], "",""]}",
                _serializerOptions);

            var result = rule!.Apply(null);
            Assert.That(result?.GetValue<string>(), Is.EqualTo(""));
        }

        [Test]
        public void StringJoin_MixedTypes_StringifiesAll()
        {
            var rule = JsonSerializer.Deserialize<Rule>(
                @"{""strjoin"": [[""a"", 1, true], "", ""]}",
                _serializerOptions);

            var result = rule!.Apply(null);
            Assert.That(result?.GetValue<string>(), Does.Contain("a"));
            Assert.That(result?.GetValue<string>(), Does.Contain("1"));
        }

        #endregion

        #region RegexExtract

        [Test]
        public void RegexExtract_FindsMatches()
        {
            var rule = JsonSerializer.Deserialize<Rule>(
                @"{""regexextract"": [""abc123def456"", ""\\d+""]}",
                _serializerOptions);

            var result = rule!.Apply(null) as JsonArray;

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Count, Is.EqualTo(2));
            Assert.That(result[0]?.GetValue<string>(), Is.EqualTo("123"));
            Assert.That(result[1]?.GetValue<string>(), Is.EqualTo("456"));
        }

        [Test]
        public void RegexExtract_NoMatch_ReturnsEmptyArray()
        {
            var rule = JsonSerializer.Deserialize<Rule>(
                @"{""regexextract"": [""abcdef"", ""\\d+""]}",
                _serializerOptions);

            var result = rule!.Apply(null) as JsonArray;

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Count, Is.EqualTo(0));
        }

        [Test]
        public void RegexExtract_WithCaptureGroup()
        {
            var rule = JsonSerializer.Deserialize<Rule>(
                @"{""regexextract"": [""name=John age=30"", ""(\\w+)=(\\w+)"", 1]}",
                _serializerOptions);

            var result = rule!.Apply(null) as JsonArray;

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Count, Is.EqualTo(2));
            Assert.That(result[0]?.GetValue<string>(), Is.EqualTo("name"));
            Assert.That(result[1]?.GetValue<string>(), Is.EqualTo("age"));
        }

        [Test]
        public void RegexExtract_WithVar_ExtractsFromData()
        {
            var rule = JsonSerializer.Deserialize<Rule>(
                @"{""regexextract"": [{""var"": ""text""}, ""[A-Z]{2,}""]}",
                _serializerOptions);

            var data = JsonNode.Parse(@"{""text"": ""Hello WORLD foo BAR""}");
            var result = rule!.Apply(data) as JsonArray;

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Count, Is.EqualTo(2));
            Assert.That(result[0]?.GetValue<string>(), Is.EqualTo("WORLD"));
            Assert.That(result[1]?.GetValue<string>(), Is.EqualTo("BAR"));
        }

        #endregion

        #region Coalesce

        [Test]
        public void Coalesce_ReturnsFirstNonNull()
        {
            var rule = JsonSerializer.Deserialize<Rule>(
                @"{""coalesce"": [null, null, ""found""]}",
                _serializerOptions);

            var result = rule!.Apply(null);
            Assert.That(result?.GetValue<string>(), Is.EqualTo("found"));
        }

        [Test]
        public void Coalesce_KeepsFalsyNonNullValues()
        {
            var rule = JsonSerializer.Deserialize<Rule>(
                @"{""coalesce"": [null, 0]}",
                _serializerOptions);

            var result = rule!.Apply(null);
            Assert.That(result?.GetValue<int>(), Is.EqualTo(0));
        }

        [Test]
        public void Coalesce_KeepsEmptyString()
        {
            var rule = JsonSerializer.Deserialize<Rule>(
                @"{""coalesce"": [null, """"]}",
                _serializerOptions);

            var result = rule!.Apply(null);
            Assert.That(result?.GetValue<string>(), Is.EqualTo(""));
        }

        [Test]
        public void Coalesce_KeepsFalse()
        {
            var rule = JsonSerializer.Deserialize<Rule>(
                @"{""coalesce"": [null, false]}",
                _serializerOptions);

            var result = rule!.Apply(null);
            Assert.That(result?.GetValue<bool>(), Is.False);
        }

        [Test]
        public void Coalesce_AllNull_ReturnsNull()
        {
            var rule = JsonSerializer.Deserialize<Rule>(
                @"{""coalesce"": [null, null, null]}",
                _serializerOptions);

            var result = rule!.Apply(null);
            Assert.That(result, Is.Null);
        }

        #endregion

        #region Slice

        [Test]
        public void Slice_FirstN()
        {
            var rule = JsonSerializer.Deserialize<Rule>(
                @"{""slice"": [[""a"",""b"",""c"",""d"",""e""], 0, 3]}",
                _serializerOptions);

            var result = rule!.Apply(null) as JsonArray;

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Count, Is.EqualTo(3));
            JsonAssert.AreEquivalent(JsonNode.Parse(@"[""a"",""b"",""c""]"), result);
        }

        [Test]
        public void Slice_MiddleRange()
        {
            var rule = JsonSerializer.Deserialize<Rule>(
                @"{""slice"": [[""a"",""b"",""c"",""d"",""e""], 1, 4]}",
                _serializerOptions);

            var result = rule!.Apply(null) as JsonArray;

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Count, Is.EqualTo(3));
            JsonAssert.AreEquivalent(JsonNode.Parse(@"[""b"",""c"",""d""]"), result);
        }

        [Test]
        public void Slice_NegativeStart_CountsFromEnd()
        {
            var rule = JsonSerializer.Deserialize<Rule>(
                @"{""slice"": [[""a"",""b"",""c"",""d"",""e""], -2]}",
                _serializerOptions);

            var result = rule!.Apply(null) as JsonArray;

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Count, Is.EqualTo(2));
            JsonAssert.AreEquivalent(JsonNode.Parse(@"[""d"",""e""]"), result);
        }

        [Test]
        public void Slice_NegativeEnd()
        {
            var rule = JsonSerializer.Deserialize<Rule>(
                @"{""slice"": [[""a"",""b"",""c"",""d"",""e""], 1, -1]}",
                _serializerOptions);

            var result = rule!.Apply(null) as JsonArray;

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Count, Is.EqualTo(3));
            JsonAssert.AreEquivalent(JsonNode.Parse(@"[""b"",""c"",""d""]"), result);
        }

        [Test]
        public void Slice_NoEnd_SlicesToEnd()
        {
            var rule = JsonSerializer.Deserialize<Rule>(
                @"{""slice"": [[""a"",""b"",""c"",""d"",""e""], 2]}",
                _serializerOptions);

            var result = rule!.Apply(null) as JsonArray;

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Count, Is.EqualTo(3));
            JsonAssert.AreEquivalent(JsonNode.Parse(@"[""c"",""d"",""e""]"), result);
        }

        #endregion

        #region Now

        [Test]
        public void Now_NoParams_ReturnsCurrentTimestamp()
        {
            var rule = JsonSerializer.Deserialize<Rule>(
                @"{""now"": []}",
                _serializerOptions);

            var before = DateTimeOffset.UtcNow;
            var result = rule!.Apply(null);
            var after = DateTimeOffset.UtcNow;

            Assert.That(result, Is.Not.Null);
            var dateStr = result!.GetValue<string>();
            Assert.That(dateStr, Does.Match(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{7}Z$"));
            var parsed = DateTimeOffset.Parse(dateStr);
            Assert.That(parsed, Is.GreaterThanOrEqualTo(before.AddSeconds(-1)));
            Assert.That(parsed, Is.LessThanOrEqualTo(after.AddSeconds(1)));
        }

        [Test]
        public void Now_NegativeDays_ReturnsPastDate()
        {
            var rule = JsonSerializer.Deserialize<Rule>(
                @"{""now"": [-2, ""days""]}",
                _serializerOptions);

            var result = rule!.Apply(null);
            Assert.That(result, Is.Not.Null);

            var dateStr = result!.GetValue<string>();
            Assert.That(dateStr, Does.Match(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{7}Z$"));
            var parsed = DateTimeOffset.Parse(dateStr);
            var expected = DateTimeOffset.UtcNow.AddDays(-2);
            Assert.That(parsed, Is.EqualTo(expected).Within(TimeSpan.FromSeconds(2)));
        }

        [Test]
        public void Now_NegativeHours_ReturnsPastDate()
        {
            var rule = JsonSerializer.Deserialize<Rule>(
                @"{""now"": [-5, ""hours""]}",
                _serializerOptions);

            var result = rule!.Apply(null);
            Assert.That(result, Is.Not.Null);

            var dateStr = result!.GetValue<string>();
            Assert.That(dateStr, Does.Match(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{7}Z$"));
            var parsed = DateTimeOffset.Parse(dateStr);
            var expected = DateTimeOffset.UtcNow.AddHours(-5);
            Assert.That(parsed, Is.EqualTo(expected).Within(TimeSpan.FromSeconds(2)));
        }

        [Test]
        public void Now_PositiveMinutes_ReturnsFutureDate()
        {
            var rule = JsonSerializer.Deserialize<Rule>(
                @"{""now"": [30, ""minutes""]}",
                _serializerOptions);

            var result = rule!.Apply(null);
            Assert.That(result, Is.Not.Null);

            var dateStr = result!.GetValue<string>();
            Assert.That(dateStr, Does.Match(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{7}Z$"));
            var parsed = DateTimeOffset.Parse(dateStr);
            var expected = DateTimeOffset.UtcNow.AddMinutes(30);
            Assert.That(parsed, Is.EqualTo(expected).Within(TimeSpan.FromSeconds(2)));
        }

        #endregion

        #region DateDiff

        [Test]
        public void DateDiff_DefaultDays()
        {
            var rule = JsonSerializer.Deserialize<Rule>(
                @"{""datediff"": [""2026-04-14T00:00:00Z"", ""2026-04-16T00:00:00Z""]}",
                _serializerOptions);

            var result = rule!.Apply(null);
            Assert.That(result?.GetValue<int>(), Is.EqualTo(2));
        }

        [Test]
        public void DateDiff_InHours()
        {
            var rule = JsonSerializer.Deserialize<Rule>(
                @"{""datediff"": [""2026-04-16T10:00:00Z"", ""2026-04-16T15:30:00Z"", ""hours""]}",
                _serializerOptions);

            var result = rule!.Apply(null);
            Assert.That(result?.GetValue<double>(), Is.EqualTo(5.5));
        }

        [Test]
        public void DateDiff_InMinutes()
        {
            var rule = JsonSerializer.Deserialize<Rule>(
                @"{""datediff"": [""2026-04-16T10:00:00Z"", ""2026-04-16T10:45:00Z"", ""minutes""]}",
                _serializerOptions);

            var result = rule!.Apply(null);
            Assert.That(result?.GetValue<int>(), Is.EqualTo(45));
        }

        [Test]
        public void DateDiff_NegativeResult_Date1AfterDate2()
        {
            var rule = JsonSerializer.Deserialize<Rule>(
                @"{""datediff"": [""2026-04-16T00:00:00Z"", ""2026-04-14T00:00:00Z""]}",
                _serializerOptions);

            var result = rule!.Apply(null);
            Assert.That(result?.GetValue<int>(), Is.EqualTo(-2));
        }

        [Test]
        public void DateDiff_InSeconds()
        {
            var rule = JsonSerializer.Deserialize<Rule>(
                @"{""datediff"": [""2026-04-16T10:00:00Z"", ""2026-04-16T10:00:30Z"", ""seconds""]}",
                _serializerOptions);

            var result = rule!.Apply(null);
            Assert.That(result?.GetValue<int>(), Is.EqualTo(30));
        }

        [Test]
        public void DateDiff_WithNow_ComputesRelativeDifference()
        {
            // datediff between "7 days ago" and "now" should be ~7 days
            var rule = JsonSerializer.Deserialize<Rule>(
                @"{""datediff"": [{""now"": [-7, ""days""]}, {""now"": []}]}",
                _serializerOptions);

            var result = rule!.Apply(null);
            Assert.That(result?.GetValue<double>(), Is.EqualTo(7.0).Within(0.01));
        }

        #endregion

        #region Combined / Integration

        [Test]
        public void Distinct_Count_Combined()
        {
            var rule = JsonSerializer.Deserialize<Rule>(
                @"{""count"": [{""distinct"": [[""a"",""b"",""a"",""c"",""b"",""a""]]}]}",
                _serializerOptions);

            var result = rule!.Apply(null);
            Assert.That(result?.GetValue<int>(), Is.EqualTo(3));
        }

        [Test]
        public void Keys_Count_Combined()
        {
            var rule = JsonSerializer.Deserialize<Rule>(
                @"{""count"": [{""keys"": [{""var"": """"}]}]}",
                _serializerOptions);

            var data = JsonNode.Parse(@"{""a"": 1, ""b"": 2, ""c"": 3}");
            var result = rule!.Apply(data);
            Assert.That(result?.GetValue<int>(), Is.EqualTo(3));
        }

        [Test]
        public void Split_Then_Slice()
        {
            var rule = JsonSerializer.Deserialize<Rule>(
                @"{""slice"": [{""strsplit"": [""a/b/c/d"", ""/""]}, 1, 3]}",
                _serializerOptions);

            var result = rule!.Apply(null) as JsonArray;

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Count, Is.EqualTo(2));
            JsonAssert.AreEquivalent(JsonNode.Parse(@"[""b"",""c""]"), result);
        }

        [Test]
        public void Split_Then_Join()
        {
            var rule = JsonSerializer.Deserialize<Rule>(
                @"{""strjoin"": [{""strsplit"": [""a,b,c"", "",""]}, "" - ""]}",
                _serializerOptions);

            var result = rule!.Apply(null);
            Assert.That(result?.GetValue<string>(), Is.EqualTo("a - b - c"));
        }

        [Test]
        public void TypeOf_Coalesce_Combined()
        {
            var rule = JsonSerializer.Deserialize<Rule>(
                @"{""typeof"": [{""coalesce"": [null, ""hello""]}]}",
                _serializerOptions);

            var result = rule!.Apply(null);
            Assert.That(result?.GetValue<string>(), Is.EqualTo("string"));
        }

        #endregion

        #region FileTextSearchCount

        [Test]
        public void FileTextSearchCount_CountsPatternOccurrences()
        {
            var tempFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tempFile, "foo bar foo baz foo");
                var escapedPath = tempFile.Replace(@"\", @"\\");
                var rule = JsonSerializer.Deserialize<Rule>(
                    $@"{{""filetextsearchcount"": [""{escapedPath}"", ""foo""]}}",
                    _serializerOptions);

                var result = rule!.Apply(null);
                Assert.That(result?.GetValue<int>(), Is.EqualTo(3));
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Test]
        public void FileTextSearchCount_NoMatch_ReturnsZero()
        {
            var tempFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tempFile, "hello world");
                var escapedPath = tempFile.Replace(@"\", @"\\");
                var rule = JsonSerializer.Deserialize<Rule>(
                    $@"{{""filetextsearchcount"": [""{escapedPath}"", ""notpresent""]}}",
                    _serializerOptions);

                var result = rule!.Apply(null);
                Assert.That(result?.GetValue<int>(), Is.EqualTo(0));
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Test]
        public void FileTextSearchCount_RegexPattern_CountsMatches()
        {
            var tempFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tempFile, "color1 colour2 color3");
                var escapedPath = tempFile.Replace(@"\", @"\\");
                var rule = JsonSerializer.Deserialize<Rule>(
                    $@"{{""filetextsearchcount"": [""{escapedPath}"", ""colou?r\\d""]}}",
                    _serializerOptions);

                var result = rule!.Apply(null);
                Assert.That(result?.GetValue<int>(), Is.EqualTo(3));
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Test]
        public void FileTextSearchCount_SingleMatch_ReturnsOne()
        {
            var tempFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tempFile, "the quick brown fox");
                var escapedPath = tempFile.Replace(@"\", @"\\");
                var rule = JsonSerializer.Deserialize<Rule>(
                    $@"{{""filetextsearchcount"": [""{escapedPath}"", ""fox""]}}",
                    _serializerOptions);

                var result = rule!.Apply(null);
                Assert.That(result?.GetValue<int>(), Is.EqualTo(1));
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Test]
        public void FileTextSearchCount_FileNotFound_Throws()
        {
            var rule = JsonSerializer.Deserialize<Rule>(
                @"{""filetextsearchcount"": [""/nonexistent/path/file.txt"", ""pattern""]}",
                _serializerOptions);

            Assert.That(() => rule!.Apply(null), Throws.InstanceOf<JsonLogicException>());
        }

        [Test]
        public void FileTextSearchCount_EmptyPatternString_Throws()
        {
            var tempFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tempFile, "some content here");
                var escapedPath = tempFile.Replace(@"\", @"\\");
                var rule = JsonSerializer.Deserialize<Rule>(
                    $@"{{""filetextsearchcount"": [""{escapedPath}"", """"]}}",
                    _serializerOptions);

                Assert.That(() => rule!.Apply(null), Throws.InstanceOf<JsonLogicException>());
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Test]
        public void FileTextSearchCount_WithVar_UsesDataValue()
        {
            var tempFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tempFile, "alpha beta alpha gamma alpha");
                var escapedPath = tempFile.Replace(@"\", @"\\");
                var rule = JsonSerializer.Deserialize<Rule>(
                    $@"{{""filetextsearchcount"": [""{escapedPath}"", {{""var"": ""pattern""}}]}}",
                    _serializerOptions);

                var data = JsonNode.Parse(@"{""pattern"": ""alpha""}");
                var result = rule!.Apply(data);
                Assert.That(result?.GetValue<int>(), Is.EqualTo(3));
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        #endregion
    }
}
