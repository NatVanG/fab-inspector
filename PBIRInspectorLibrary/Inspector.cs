using Json.Logic;
using Json.More;
using Json.Pointer;
using PBIRInspectorLibrary.Exceptions;
using PBIRInspectorLibrary.Output;
using PBIRInspectorLibrary.Part;
using System.Data;
using System.Text.Json.Nodes;

namespace PBIRInspectorLibrary
{
    /// <summary>
    /// Iterates through input rules and runs them against input PBI files
    /// </summary>
    public class Inspector : InspectorBase
    {
        private const string JSONPOINTERSTART = "/";
        private const string CONTEXTNODE = ".";
        internal const char DRILLCHAR = '>';

        private string? _rulesPath;
        private InspectionRules? _inspectionRules;

        public event EventHandler<MessageIssuedEventArgs>? MessageIssued;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="inspectionRules"></param>
        /// <param name="registries"></param>
        /// <param name="fileSystem"></param>
        public Inspector(InspectionRules inspectionRules, IEnumerable<JsonLogicOperatorRegistry> registries, IFabricFileSystem fileSystem) : base(inspectionRules, registries, fileSystem)
        {
            this._inspectionRules = inspectionRules;
        }

        public List<TestResult> Inspect()
        {
            var fileSystemPath = _fileSystem.RootPath;
            var rules = this._inspectionRules.Rules.Where(_ => !_.Disabled);
            var testResults = new List<TestResult>();

            if (rules != null && rules.Any())
            {
                if (!string.IsNullOrEmpty(fileSystemPath))
                {
                    if (_fileSystem.DirectoryExists(fileSystemPath))
                    {
                        //Run rules that apply across types ie. with attribute "itemtype" set to "*"
                        RunRulesByItemType(testResults, rules, "*", fileSystemPath);

                        //Run rules that apply to specific itemtypes
                        var fabricItems = _fileSystem.GetFabricItems(fileSystemPath);

                        if (fabricItems != null && fabricItems.Any())
                        {
                            foreach (var fabricItem in fabricItems)
                            {
                                ContextService.FabricItem = string.IsNullOrEmpty(fabricItem.FilePath) ? fabricItem.Id : fabricItem.FilePath; //TODO: improve this logic for setting ContextService.FabricItem
                                RunRulesByItemType(testResults, rules, fabricItem.Type, fabricItem.DirectoryPath);
                                RunDeprecatedRulesByItemType(testResults, rules, fabricItem.Type, fabricItem.DirectoryPath);
                            }
                        }
                        else
                        {
                            //LEGACY: support for report definition folder paths.
                            if (fileSystemPath.ToLowerInvariant().EndsWith("definition") || fileSystemPath.ToLowerInvariant().EndsWith(".report"))
                            {
                                OnMessageIssued(MessageTypeEnum.Information, string.Format("No platform files found in directory \"{0}\". Running legacy behaviour to support file system path ending in '\\definition' or '.report' and assuming fabric item type is report.", fileSystemPath));
                                RunRulesByItemType(testResults, rules, "report_deprecated", fileSystemPath);
                            }
                            else
                            {
                                OnMessageIssued(MessageTypeEnum.Information, string.Format("No legacy PBIP report definition folder nor Fabric .platform files found in directory \"{0}\".", fileSystemPath));
                            }
                        }
                    }
                    else if (_fileSystem.FileExists(fileSystemPath))
                    {
                        var fileExtension = _fileSystem.GetExtension(fileSystemPath).ToLowerInvariant();
                        switch (fileExtension)
                        {
                            case ".pbip":
                                //LEGACY: if _fabricItemPath is a pbip file, assume we want to test a report's metadata
                                RunRulesByItemType(testResults, rules, "report_deprecated", fileSystemPath);
                                break;
                            case ".json":
                                RunRulesByItemType(testResults, rules, "json", fileSystemPath);
                                break;
                            default:
                                throw new PBIRInspectorException(string.Format("Unsupported file itemType \"{0}\" for path \"{1}\".", fileExtension, fileSystemPath));
                        }

                    }
                    else
                    {
                        throw new PBIRInspectorException(string.Format("File or folder with path \"{0}\" not found. Running rules that don't pertain to any item type i.e. rules with itemType property set to \"{1}\".", fileSystemPath, "none"));
                    }
                }
                RunRulesByItemType(testResults, rules, "none", fileSystemPath);
            }
            else
            {
                OnMessageIssued(MessageTypeEnum.Information, "No rules found to run.");
            }

            return testResults;
        }

        protected void OnMessageIssued(MessageTypeEnum messageType, string message)
        {
            var args = new MessageIssuedEventArgs(message, messageType);
            OnMessageIssued(args);
        }

        protected virtual void OnMessageIssued(MessageIssuedEventArgs e)
        {
            EventHandler<MessageIssuedEventArgs>? handler = MessageIssued;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        #region private methods

        private MessageTypeEnum ConvertRuleLogType(string ruleLogType)
                {
                    if (string.IsNullOrEmpty(ruleLogType)) return MessageTypeEnum.Warning;

                    MessageTypeEnum logType;

                    switch (ruleLogType.ToLower().Trim())
                    {
                        case "error":
                            logType = MessageTypeEnum.Error;
                            break;
                        case "warning":
                            logType = MessageTypeEnum.Warning;
                            break;
                        default:
                            logType = MessageTypeEnum.Warning;
                            break;
                    }

                    return logType;
                }

        private void RunRulesByItemType(List<TestResult> testResults, IEnumerable<Rule> rules, string type, string fileSystemPath)
        {
            var rulesFilteredByItemType = rules.Where(_ => _.ItemType.Equals(type, StringComparison.InvariantCultureIgnoreCase));

            if (rulesFilteredByItemType == null || !rulesFilteredByItemType.Any())
            {
                OnMessageIssued(MessageTypeEnum.Information, string.Format("No rules found for item type \"{0}\".", type));
                return;
            }

            IPartQuery partQuery = PartQueryFactory.CreatePartQuery(type, fileSystemPath, _fileSystem);

            RunRules(testResults, rulesFilteredByItemType, partQuery);
        }

        private void RunDeprecatedRulesByItemType(List<TestResult> testResults, IEnumerable<Rule> rules, string type, string fileSystemPath)
        {
            const string DEPRECATED_SUFFIX = "_deprecated";
            var deprecatedRules = rules.Where(_ => _.ItemType.Contains(DEPRECATED_SUFFIX, StringComparison.InvariantCultureIgnoreCase));
            var rulesFilteredByItemType = deprecatedRules.Where(_ => _.ItemType.Replace(DEPRECATED_SUFFIX, string.Empty).Equals(type, StringComparison.InvariantCultureIgnoreCase));

            IPartQuery partQuery = PartQueryFactory.CreatePartQuery(string.Concat(type, DEPRECATED_SUFFIX), fileSystemPath, _fileSystem);

            RunRules(testResults, rulesFilteredByItemType, partQuery);
        }


        private void RunRules(List<TestResult> testResults, IEnumerable<Rule> rules, IPartQuery? partQuery)
        {
            foreach (var rule in rules)
            {
                var ruleLogType = ConvertRuleLogType(rule.LogType);
                ContextService.Current = new PartContext { PartQuery = partQuery, Part = partQuery.RootPart };

                OnMessageIssued(MessageTypeEnum.Information, string.Format("Running Rule \"{0}\".", rule.Name));
                Json.Logic.Rule? jrule = null;

                try
                {
                    jrule = System.Text.Json.JsonSerializer.Deserialize<Json.Logic.Rule>(rule.Test.Logic);
                }
                catch (System.Text.Json.JsonException e)
                {
                    OnMessageIssued(MessageTypeEnum.Error, string.Format("Parsing of logic for rule \"{0}\" failed, resuming to next rule.", rule.Name));
                    continue;
                }

                bool result = false;

                try
                {
                    var parts = new List<Part.Part>();

                    if (!string.IsNullOrEmpty(rule.Part))
                    {
                        var part = partQuery.Invoke(rule.Part, ContextService.Current.Part);

                        if (part != null && part
                            is List<Part.Part>)
                        {
                            parts.AddRange((List<Part.Part>)part);
                        }
                        else if (part != null && part is Part.Part)
                        {
                            parts.Add((Part.Part)part);
                        }
                        else
                        { 
                            var msgType = rule.PathErrorWhenNoMatch ? MessageTypeEnum.Error : MessageTypeEnum.Warning;
                            OnMessageIssued(msgType, (string.Format("Rule \"{0}\" - Part \"{1}\" not found.", rule.Name, rule.Part)));
                            //TODO: should we fail the test altogether here?
                            //TODO: document PathErrorWhenNoMatch in wiki
                            //OnMessageIssued(MessageTypeEnum.Error, (string.Format("Rule \"{0}\" - Part \"{1}\" not found, resuming to next rule.", rule.Name, rule.Part)));
                            //continue;
                        }
                    }
                    else
                    {
                        parts.Add(partQuery.RootPart);
                    }

                    foreach (var part in parts)
                    {
                        if (part == null)
                        {
                            MessageTypeEnum msgType = rule.PathErrorWhenNoMatch ? MessageTypeEnum.Error : MessageTypeEnum.Information;
                            var msg = string.Format("Rule \"{0}\" - Part(s) \"{1}\" not found.", rule.Name, rule.Part);
                            OnMessageIssued(msgType, msg);

                            if (rule.PathErrorWhenNoMatch)
                            {
                                testResults.Add(new TestResult { RuleId = rule.Id, RuleName = rule.Name, LogType = ruleLogType, RuleDescription = rule.Description, RuleItemType = rule.ItemType, ItemPath = null, ParentName = null, ParentDisplayName = "N/A", Pass = false, Message = msg, Expected = rule.Test.Expected, Actual = null });
                            }
                        }
                        else
                        {
                            ContextService.Current.Part = part;
                            var node = PartUtils.ToJsonNode(part);
                            var newdata = MapRuleDataPointersToValues(node, rule);

                            var itemPath = this._fileSystem.GetRelativePath(part.FileSystemPath);
                            //var itemPath = part.FileSystemPath.Substring(part.FileSystemPath.IndexOf(this._fileSystem.RootPath) + this._fileSystem.RootPath.Length);
                            itemPath = string.IsNullOrEmpty(itemPath) ? "root" : itemPath;
                            var parentPageName = part.FileSystemName.ToLowerInvariant().EndsWith("page.json") ? partQuery.PartName(part) : null;
                            var parentPageDisplayName = part.FileSystemName.ToLowerInvariant().EndsWith("page.json") ? partQuery.PartDisplayName(part) ?? partQuery.PartName(part) : "N/A";

                            try
                            {
                                var jruleresult = jrule.Apply(newdata);
                                result = rule.Test.Expected.IsEquivalentTo(jruleresult);

                                string resultString = string.Format("Rule \"{0}\" {1} with result: {2}, expected: {3}", rule != null ? rule.Name : string.Empty, result ? "PASSED" : "FAILED", jruleresult != null ? jruleresult.ToString() : string.Empty, rule.Test.Expected != null ? rule.Test.Expected.ToString() : string.Empty);
                                testResults.Add(new TestResult { RuleId = rule.Id, RuleName = rule.Name, LogType = ruleLogType, RuleDescription = rule.Description, RuleItemType = rule.ItemType, ItemPath = itemPath, ParentName = parentPageName, ParentDisplayName = parentPageDisplayName, Pass = result, Message = resultString, Expected = rule.Test.Expected, Actual = jruleresult });

                                //PATCH
                                if (!result && rule.ApplyPatch && rule.Patch != null && rule.Patch.Ops != null)
                                {
                                    if (jruleresult != null && jruleresult is JsonArray arr && arr.Count() > 0)
                                    {
                                        var allPatchParts = (List<Part.Part>)partQuery.Invoke(rule.Patch.PartName, part);

                                        //TODO: use another method to filter parts i.e. other than ToJSonString
                                        var filteredPatchParts = allPatchParts.Where(_ => arr.ToJsonString().Contains(partQuery.PartName(_)));
                                        foreach (var filteredPart in filteredPatchParts)
                                        {
                                            ApplyPatch(partQuery, rule, filteredPart);
                                        }
                                    }
                                    else
                                    {
                                        var patchPart = (Part.Part)partQuery.Invoke(rule.Patch.PartName, part);
                                        ApplyPatch(partQuery, rule, patchPart);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                OnMessageIssued(MessageTypeEnum.Error, (string.Format("Rule \"{0}\" - Part \"{1}\" execution failed. Inner exception: {2}", rule.Name, part.FileSystemName, ex.Message)));
                                continue;
                            }
                        }
                    }
                }
                catch (PBIRInspectorException e)
                {
                    testResults.Add(new TestResult { RuleId = rule.Id, RuleName = rule.Name, LogType = MessageTypeEnum.Error, RuleDescription = rule.Description, RuleItemType = rule.ItemType, Pass = false, Message = e.Message, Expected = rule.Test.Expected, Actual = null });
                    continue;
                }

            }
        }

        private void ApplyPatch(IPartQuery partQuery, Rule? rule, Part.Part? partToPatch)
        {
            var node = PartUtils.ToJsonNode(partToPatch);
            var patchResult = rule.Patch.Ops.Apply(node);
            if (patchResult.IsSuccess)
            {
                partToPatch.JsonContent = patchResult.Result;
                partToPatch.Save();
            }
            else
            {
                OnMessageIssued(MessageTypeEnum.Error, string.Format("Rule \"{0}\" - Patch failed for part \"{1}\". Error: \"{2}\".", rule.Id, partToPatch.FileSystemPath, patchResult.Error));
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="token"></param>
        /// <param name="rule"></param>
        /// <returns></returns>
        private JsonNode? MapRuleDataPointersToValues(JsonNode? target, Rule rule)
        {
            if (target == null || rule.Test.Data == null || rule.Test.Data is not JsonObject) return rule.Test.Data;
            if (rule.Test.Data is JsonObject && rule.Test.Data.AsObject().Count() == 0) return target;

            var newdata = new JsonObject();

            var olddata = rule.Test.Data.AsObject();

            try
            {
                foreach (var item in olddata)
                {
                    if (item.Value is JsonValue)
                    {
                        var value = item.Value.AsValue().Stringify();
                        if (value.StartsWith(JSONPOINTERSTART)) //check for JsonPointer syntax
                        {
                            try
                            {
                                var evalsuccess = EvalPath(value, target, out var newval);
                                if (evalsuccess)
                                {
                                    if (newval != null)
                                    {
                                        newdata.Add(new KeyValuePair<string, JsonNode?>(item.Key, newval?.DeepClone()));
                                    }
                                    else
                                    {
                                        //TODO: handle null value?
                                    }
                                }
                                else
                                {
                                    if (rule.PathErrorWhenNoMatch)
                                    {
                                        throw new PBIRInspectorException(string.Format("Rule \"{0}\" - Could not evaluate json pointer \"{1}\".", rule.Name, value));
                                    }
                                    else
                                    {
                                        OnMessageIssued(MessageTypeEnum.Information, string.Format("Rule \"{0}\" - Could not evaluate json pointer \"{1}\".", rule.Name, value));
                                        continue;
                                    }
                                }
                            }
                            catch (PointerParseException e)
                            {
                                if (rule.PathErrorWhenNoMatch)
                                {
                                    throw new PBIRInspectorException(string.Format("Rule \"{0}\" - Pointer parse exception for value \"{1}\".", rule.Name, value));
                                }
                                else
                                {
                                    OnMessageIssued(MessageTypeEnum.Error, string.Format("Rule \"{0}\" - Pointer parse exception for value \"{1}\". Inner Exception: \"{2}\".", rule.Name, value, e.Message));
                                    continue;
                                }
                            }
                        }
                        else if (value.Equals(CONTEXTNODE))
                        {
                            //context array token was used so pass in the parent array
                            newdata.Add(new KeyValuePair<string, JsonNode?>(item.Key, target?.DeepClone()));
                        }
                        else
                        {
                            //looks like a literal value
                            newdata.Add(new KeyValuePair<string, JsonNode?>(item.Key, item.Value?.DeepClone()));
                        }
                    }
                    else
                    {
                        //might be a JsonArray
                        newdata.Add(new KeyValuePair<string, JsonNode?>(item.Key, item.Value?.DeepClone()));
                    }
                }
            }
            catch (System.Text.Json.JsonException e)
            {
                throw new PBIRInspectorException("JsonException", e);
            }

            return newdata;
        }

        private bool EvalPath(string pathString, JsonNode? data, out JsonNode? result)
        {
            if (pathString.Contains(DRILLCHAR))
            {
                var leftString = pathString.Substring(0, pathString.IndexOf(DRILLCHAR));
                var rightString = pathString.Substring(pathString.IndexOf(DRILLCHAR) + 1);

                var leftStringPath = string.Concat(leftString.StartsWith(JSONPOINTERSTART) ? string.Empty : JSONPOINTERSTART, leftString.Replace('.', '/'));
                var pointer = JsonPointer.Parse(leftStringPath);
                if (pointer.TryEvaluate(data, out result))
                {
                    if (result is JsonValue val)
                    {
                        //remove single quotes from beginning and end of string if any.
                        string strVal;
                        if (val.ToString()!.StartsWith("'") && val.ToString()!.EndsWith("'"))
                        {
                            strVal = val.ToString()!.Substring(1, val.ToString()!.Length - 2);
                        }
                        else
                        {
                            strVal = val.ToString()!;
                        }

                        var pathEvalNode = JsonNode.Parse(strVal);
                        return EvalPath(rightString, pathEvalNode, out result);
                    }
                    else
                    {
                        return EvalPath(rightString, result, out result);
                    }
                }
            }
            else if (pathString.Trim().Equals(CONTEXTNODE))
            {
                result = data;
                return true;
            }
            else
            {
                var pathStringPath = string.Concat(pathString.StartsWith(JSONPOINTERSTART) ? string.Empty : JSONPOINTERSTART, pathString.Replace('.', '/'));
                var pointer = JsonPointer.Parse(pathStringPath);
                return pointer.TryEvaluate(data, out result);
            }

            result = null;
            return false;
        }

        #endregion
    }
}
