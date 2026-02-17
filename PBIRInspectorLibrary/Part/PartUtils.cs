using Json.Path;
using Json.Pointer;
using System.Text.Json.Nodes;

namespace PBIRInspectorLibrary.Part
{
    public class PartUtils
    {
        const string PROP_FILESYSTEMNAME = "filesystemname";
        const string PROP_FILESYSTEMPATH = "filesystempath";
        const string PROP_FILESYSTEMTYPE = "partfilesystemtype";
        const string PROP_FILESIZE = "filesize";
        const string PROP_FILECOUNT = "filecount";

        public static JsonNode ToJsonNode(object? value)
        {
            if (value == null) return null;

            if (value is List<Part>)
            {
                return ToJsonArray(value as List<Part>);
            }
            else if (value is Part)
            {
                return ToJsonNode((Part)value);
            }
            else
            {
                //if the value is not a Part or List<Part>, we return it as a JsonValue
                return JsonValue.Create(value);
            }
        }

        public static JsonArray ToJsonArray(List<Part> parts)
        {
            if (parts == null || parts.Count == 0) return new JsonArray();
            return new JsonArray(parts.Select(_ => ToJsonNode(_)?.DeepClone()).ToArray());
        }

        //returns null if the file does not exist or is not a json file or if it's a directory
        public static JsonNode? ToJsonNode(Part context)
        {
            if (context == null) return null;
            if (context.JsonContent != null) return context.JsonContent;

            JsonNode? node = null;


            try
            {
                if (File.Exists(context.FileSystemPath))
                {
                    node = JsonNode.Parse(File.ReadAllText(context.FileSystemPath));
                }

                if (Directory.Exists(context.FileSystemPath))
                {
                    //if the path is a directory, we cannot parse it as JSON, so we return an annotation with the file system path
                    node = Annotations(context);
                }
            }
            catch (System.Text.Json.JsonException)
            {
                //this is not a json file or not a valid json file so add annotation with the file system path; this is so JsonLogic rules can still be applied
                
                //TODO: introduce a breaking change to return a JsonValue with the contents of the file instead of annotations?
                node = Annotations(context);
            }
            finally
            {
                context.JsonContent = node;
            }
            

            return node;
        }

        private static JsonNode Annotations(Part part)
        {
            const string deprecated_AnnotationPrefix = "pbiri_";
            const string annotationPrefix = "part_";
            PartInfo partInfo = new PartInfo(part);

            var node = new JsonObject();

            if (node is JsonObject jsonObject)
            {
                var annotations = new JsonArray
                        {
                            //TODO: Legacy, deprecate.
                            new JsonObject { ["name"] = deprecated_AnnotationPrefix + PROP_FILESYSTEMPATH, ["value"] =  partInfo.FileSystemPath },
                            new JsonObject { ["name"] = deprecated_AnnotationPrefix + PROP_FILESYSTEMNAME, ["value"] =  partInfo.FileSystemName },
                            new JsonObject { ["name"] = annotationPrefix + PROP_FILESYSTEMPATH, ["value"] =  partInfo.FileSystemPath },
                            new JsonObject { ["name"] = annotationPrefix + PROP_FILESYSTEMNAME, ["value"] =  partInfo.FileSystemName },
                            new JsonObject { ["name"] = annotationPrefix + PROP_FILESYSTEMTYPE, ["value"] =  partInfo.PartFileSystemType.ToString() },
                            new JsonObject { ["name"] = annotationPrefix + PROP_FILESIZE, ["value"] =  partInfo.FileSize.ToString() },
                            new JsonObject { ["name"] = annotationPrefix + PROP_FILECOUNT, ["value"] =  partInfo.FileCount.ToString() }
                };

                jsonObject["annotations"] = annotations;
            }

            return node;
        }

        public static PartInfo? TryGetPartInfo(JsonNode node, bool setAdvancedProperties = false)
        {
            var fileSystemPath = TryGetFileSystemPath(node);
            if (fileSystemPath != null)
            {
                return new PartInfo(fileSystemPath, setAdvancedProperties);
            }
            else
            {
                return null;
            }
        }

        private static string? TryGetFileSystemPath(JsonNode node)
        {            
            if (node is JsonValue filePathValue && filePathValue.TryGetValue(out string? stringFilePath) && File.Exists(stringFilePath))
            {
                return stringFilePath;
            }
            else
            {
                return TryGetFilePropertyValue(node, PROP_FILESYSTEMPATH);
            }       
        }

        private static string? TryGetFilePropertyValue(JsonNode node, string propertyName)
        {
            //TODO: allow getting file properties via annotations?
            //return TryGetAnnotationValue(node, propertyName) ?? TryGetPartInfoPropertyValue(node, propertyName);
            return TryGetPartInfoPropertyValue(node, propertyName);
        }

        private static string? TryGetAnnotationValue(JsonNode node, string annotationKey)
        {
            const string annotationPrefix = "part_";

            annotationKey = annotationKey.ToLowerInvariant();

            if (!annotationKey.StartsWith(annotationPrefix))
            {
                annotationKey = annotationPrefix + annotationKey;
            }

            var jsonPathString = string.Format("$.annotations[?@.name == '{0}'].value", annotationKey);

            JsonPath pa = JsonPath.Parse(jsonPathString);

            var result = pa.Evaluate(node);
            if (result.Matches != null && result.Matches.Count > 0)
            {
                //TODO: return array?
                var resultNode = result.Matches.SingleOrDefault()!.Value;
                return resultNode!.ToString();
            }
            else
            {
                return null;
            }
        }

        private static string? TryGetPartInfoPropertyValue(JsonNode node, string propertyName)
        {
            propertyName = propertyName.ToLowerInvariant();
            var jsonPointerStr = !propertyName.StartsWith("/") ? "/" + propertyName : propertyName;
            return TryGetJsonNodeStringValue(node, jsonPointerStr);
        }

        public static string? TryGetJsonNodeStringValue(JsonNode node, string query)
        {
            JsonPointer pt = JsonPointer.Parse(query);

            if (pt.TryEvaluate(node, out var result))
            {
                if (result is JsonValue val)
                {
                    return val.ToString();
                }
            }

            return null;
        }

        #region partInfo
        public static JsonNode PartInfoToJsonNode(object? value)
        {
            if (value == null) return null;

            if (value is List<Part>)
            {
                return PartInfoToJsonNode(value as List<Part>);
            }
            else if (value is Part)
            {
                return PartInfoToJsonNode(value as Part);
            }

            return null;
        }

        private static JsonNode PartInfoToJsonNode(List<Part> parts)
        {
            if (parts == null || parts.Count == 0) return new JsonArray();
            return new JsonArray(parts.Select(PartInfoToJsonNode).ToArray());
        }

        private static JsonNode PartInfoToJsonNode(Part part)
        {
            if (part == null) return null;

            var partInfo = new PartInfo(part);
            return PartInfoToJsonNode(partInfo);

        }

        private static JsonNode PartInfoToJsonNode(PartInfo partInfo)
        {
            if (partInfo == null) return null;
            var jsonObject = new JsonObject
            {
                [PROP_FILESYSTEMNAME] = partInfo.FileSystemName,
                [PROP_FILESYSTEMPATH] = partInfo.FileSystemPath,
                [PROP_FILESYSTEMTYPE] = partInfo.PartFileSystemType.ToString(),
                [PROP_FILESIZE] = partInfo.FileSize,
                [PROP_FILECOUNT] = partInfo.FileCount
            };
            return jsonObject;
        }
        #endregion
    }
}
