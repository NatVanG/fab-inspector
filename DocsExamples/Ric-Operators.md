# Ric Operators — Quick Reference

> For in-depth explanations and advanced examples, see the [Fab Inspector wiki](https://github.com/NatVanG/PBI-InspectorV2/wiki).

Ric operators extend the [JSON Logic](https://json-everything.net/json-logic) engine used by Fab Inspector rules. They are available in the `test` (and `patch`) fields of any rule and work with both local and remote Fabric items.

All snippets below show the operator as it appears inside a rule's `test` array. For complete working rule files see the links in each section and the [Rule File Examples](../README.md#customrulesexamples) in the README.

---

## Contents

- [Navigation & Query](#navigation--query): `part`, `partinfo`, `path`, `query`, `drillvar`, `let`
- [Data Transformation](#data-transformation): `coalesce`, `tostring`, `torecord`, `typeof`, `keys`, `values`, `distinct`, `count`
- [String Operations](#string-operations): `strcontains`, `strsplit`, `strjoin`, `regexextract`
- [Array Operations](#array-operations): `slice`
- [Set Operations](#set-operations): `union`, `intersect`, `diff`, `symdiff`, `equalsets`
- [Date & Time](#date--time): `now`, `datediff`
- [Type & Null Checks](#type--null-checks): `hasprop`, `isnullorempty`
- [File System](#file-system): `filesize`, `filetextsearchcount`, `fromyamlfile`

---

## Navigation & Query

### `part`

Navigates to or queries a Fabric item file or folder. Returns the parsed JSON content of the matched path.

When used as the `part` rule property it acts as an **iterator** — the rule body runs once per matched file. When used as an operator inside `test` it performs a **one-off query** against the current item.

| Parameter | Type | Description |
|---|---|---|
| input | string | Regex path using `:` as the folder separator, **or** one of the Report part abstractions: `Report`, `ReportExtensions`, `Pages`, `PagesHeader`, `AllPages`, `Visuals`, `AllVisuals`, `MobileVisuals`, `AllMobileVisuals`, `Bookmarks`, `BookmarksHeader`, `AllBookmarks` |

**Returns:** Parsed JSON content of the matched file, or an array of items when the path matches multiple files.

```json
{ "part": "Pages" }
```

```json
{ "part": "folder1:.*:copyjob-content\\.json$" }
```

```json
{ "part": ".platform" }
```

---

### `partinfo`

Returns metadata about a Fabric item file or folder (name, size, whether it is a directory, etc.) without loading its full content.

| Parameter | Type | Description |
|---|---|---|
| input | string | Path expression (same format as `part`) |

**Returns:** Object with file/folder metadata properties (`name`, `fileSize`, `isDirectory`, etc.).

```json
{ "partinfo": "copyjob-content.json" }
```

---

### `path`

Evaluates a [JSONPath](https://goessner.net/articles/JsonPath/) expression against the current data context and returns all matching nodes as an array.

| Parameter | Type | Description |
|---|---|---|
| path | string | JSONPath expression (e.g. `$.visuals[*].name`) |

**Returns:** Array of matched nodes.

```json
{ "path": "$.themeCollection.baseTheme.name" }
```

```json
{ "path": "$.visuals[*].visual.visualType" }
```

See also: [Example-NewOperators-rules.json](Example-NewOperators-rules.json)

---

### `query`

Evaluates `input` first, then applies `rule` using the result as the data context. Useful for chaining operators where the output of one expression becomes the input of the next.

| Parameter | Type | Description |
|---|---|---|
| input | any | Expression evaluated first |
| rule | any | Expression applied with `input` result as the context |

**Returns:** Result of `rule` applied with `input` result as the context.

```json
{
  "query": [
    { "part": ".platform" },
    { "var": "0.metadata.displayName" }
  ]
}
```

---

### `drillvar`

Navigates nested data using a `>` delimited path. Each segment to the left of `>` is evaluated as a JSON Pointer against the current context; the segment to the right is evaluated recursively with the result.

| Parameter | Type | Description |
|---|---|---|
| path | string | `>` delimited navigation path (optional) |
| defaultValue | any | Returned when no match is found (optional) |

**Returns:** Matched nested value, the default value, or `null`.

```json
{ "drillvar": "artifacts>0>displayName" }
```

---

### `let`

Binds named variables to expressions and makes them available by name in a `body` expression. All bindings are evaluated in parallel against the original data — no binding sees another's result. Use `let` to avoid re-evaluating expensive expressions that are referenced multiple times.

| Parameter | Type | Description |
|---|---|---|
| bindings | object | Map of variable name → expression |
| body | any | Expression that references bound names via `{ "var": "<name>" }` |

**Returns:** Result of `body`.

```json
{
  "let": [
    { "items": { "path": "$.visuals[*].name" } },
    { "count": [{ "var": "items" }] }
  ]
}
```

```json
{
  "let": [
    {
      "themeName": { "var": "themeCollection.baseTheme.name" },
      "version": { "var": "version" }
    },
    { "strjoin": [[{ "var": "themeName" }, { "var": "version" }], " — "] }
  ]
}
```

See also: [Example-let-rule.json](Example-let-rule.json)

---

## Data Transformation

### `coalesce`

Returns the first non-null value from a list of expressions.

| Parameter | Type | Description |
|---|---|---|
| first | any | First expression |
| rest... | any | Additional expressions (variadic) |

**Returns:** First non-null result, or `null` if all are null.

```json
{ "coalesce": [{ "var": "optionalProp" }, "default-value"] }
```

---

### `tostring`

Converts any JSON value to its JSON string representation.

| Parameter | Type | Description |
|---|---|---|
| inputString | any | Value to convert |

**Returns:** String.

```json
{ "tostring": [{ "var": "version" }] }
```

---

### `torecord`

Builds a JSON object from alternating key–value expressions. Parameters must be provided in pairs: `[key1, value1, key2, value2, ...]`. The number of elements must be even.

| Parameter | Type | Description |
|---|---|---|
| pairs... | any | Alternating key and value expressions (minimum 2, must be even count) |

**Returns:** JSON object.

```json
{ "torecord": ["name", { "var": "displayName" }, "type", { "var": "itemType" }] }
```

---

### `typeof`

Returns the JSON type name of a value.

| Parameter | Type | Description |
|---|---|---|
| input | any | Value to inspect |

**Returns:** One of `"null"`, `"string"`, `"number"`, `"boolean"`, `"array"`, `"object"`.

```json
{ "typeof": [{ "var": "someProperty" }] }
```

---

### `keys`

Returns all property names of a JSON object as an array.

| Parameter | Type | Description |
|---|---|---|
| input | object | JSON object |

**Returns:** Array of property name strings.

```json
{ "keys": [{ "var": "" }] }
```

See also: [Example-NewOperators-rules.json](Example-NewOperators-rules.json)

---

### `values`

Returns all property values of a JSON object as an array.

| Parameter | Type | Description |
|---|---|---|
| input | object | JSON object |

**Returns:** Array of property values.

```json
{ "values": [{ "var": "" }] }
```

See also: [Example-NewOperators-rules.json](Example-NewOperators-rules.json)

---

### `distinct`

Removes duplicate elements from an array using deep equality comparison.

| Parameter | Type | Description |
|---|---|---|
| input | array | Array possibly containing duplicates |

**Returns:** Array with duplicates removed.

```json
{
  "distinct": [
    { "map": [{ "part": "Visuals" }, { "var": "visual.visualType" }] }
  ]
}
```

See also: [Example-NewOperators-rules.json](Example-NewOperators-rules.json)

---

### `count`

Returns the number of elements in an array.

| Parameter | Type | Description |
|---|---|---|
| input | array | Array to count |

**Returns:** Integer element count.

```json
{ "count": [{ "part": "Visuals" }] }
```

---

## String Operations

### `strcontains`

Counts the number of times a regex pattern appears in a string.

| Parameter | Type | Description |
|---|---|---|
| searchString | string | String to search in |
| containsString | string | Regex pattern to match |

**Returns:** Integer match count (0 if `searchString` is null).

```json
{ "strcontains": [{ "var": "description" }, "TODO"] }
```

---

### `strsplit`

Splits a string by a delimiter and returns the parts as an array.

| Parameter | Type | Description |
|---|---|---|
| inputString | string | String to split |
| delimiter | string | Delimiter string |

**Returns:** Array of string parts.

```json
{ "strsplit": [{ "var": "tags" }, ","] }
```

---

### `strjoin`

Joins an array of values into a single string using a separator.

| Parameter | Type | Description |
|---|---|---|
| input | array | Array of values to join |
| separator | string | Join separator |

**Returns:** Joined string.

```json
{ "strjoin": [{ "path": "$.items[*].name" }, ", "] }
```

---

### `regexextract`

Extracts all regex matches from a string. Optionally returns a specific capture group instead of the full match.

| Parameter | Type | Description |
|---|---|---|
| inputString | string | String to search |
| pattern | string | Regex pattern |
| group | number | Capture group index to return (optional; returns full matches if omitted) |

**Returns:** Array of matched strings (empty array if no matches).

```json
{ "regexextract": [{ "var": "sourceText" }, "\\b[A-Z]{2,}\\b"] }
```

```json
{ "regexextract": [{ "var": "sourceText" }, "(\\w+)@(\\w+)", 2] }
```

---

## Array Operations

### `slice`

Returns a subarray using JavaScript `Array.prototype.slice` semantics. Supports negative indices (counting from the end).

| Parameter | Type | Description |
|---|---|---|
| input | array | Array to slice |
| start | number | Start index (negative counts from end) |
| end | number | End index, exclusive (optional; defaults to array length) |

**Returns:** Array slice.

```json
{ "slice": [{ "part": "Pages" }, 0, 5] }
```

```json
{ "slice": [{ "part": "Pages" }, -3] }
```

---

## Set Operations

All set operators use deep equality comparison and remove duplicates from their results.

### `union`

Returns all items from both arrays with duplicates removed.

| Parameter | Type | Description |
|---|---|---|
| set1 | array | First set |
| set2 | array | Second set |

**Returns:** Array (union).

```json
{ "union": [{ "var": "allowedTypes" }, ["slicer", "card"]] }
```

---

### `intersect`

Returns items present in both arrays.

| Parameter | Type | Description |
|---|---|---|
| set1 | array | First set |
| set2 | array | Second set |

**Returns:** Array (intersection).

```json
{ "intersect": [{ "var": "activeTypes" }, { "var": "allowedTypes" }] }
```

---

### `diff`

Returns items from `set1` that are not in `set2` (set difference: `set1 − set2`).

| Parameter | Type | Description |
|---|---|---|
| set1 | array | First set |
| set2 | array | Second set |

**Returns:** Array.

```json
{ "diff": [{ "var": "activeTypes" }, { "var": "allowedTypes" }] }
```

---

### `symdiff`

Returns items that appear in exactly one of the two sets (symmetric difference: items in `set1` or `set2` but not both).

| Parameter | Type | Description |
|---|---|---|
| set1 | array | First set |
| set2 | array | Second set |

**Returns:** Array.

```json
{ "symdiff": [{ "var": "expected" }, { "var": "actual" }] }
```

---

### `equalsets`

Returns `true` if both arrays contain exactly the same elements (order-independent, duplicates ignored).

| Parameter | Type | Description |
|---|---|---|
| set1 | array | First set |
| set2 | array | Second set |

**Returns:** Boolean.

```json
{ "equalsets": [{ "var": "expected" }, { "var": "actual" }] }
```

---

## Date & Time

### `now`

Returns the current UTC date/time as an ISO 8601 string, optionally offset by a specified amount.

| Parameter | Type | Description |
|---|---|---|
| offset | number | Amount to add (optional) |
| unit | string | One of `"seconds"`, `"minutes"`, `"hours"`, `"days"` (default), `"months"`, `"years"` (optional) |

**Returns:** ISO 8601 date/time string.

```json
{ "now": [] }
```

```json
{ "now": [-30, "days"] }
```

```json
{ "now": [1, "years"] }
```

---

### `datediff`

Calculates the difference between two ISO 8601 date strings.

| Parameter | Type | Description |
|---|---|---|
| date1 | string | First date (ISO 8601) |
| date2 | string | Second date (ISO 8601) |
| unit | string | One of `"seconds"`, `"minutes"`, `"hours"`, `"days"` (default) (optional) |

**Returns:** Numeric difference (integer if whole number, decimal otherwise). Positive when `date2` is later than `date1`.

```json
{ "datediff": [{ "var": "createdDate" }, { "now": [] }, "days"] }
```

---

## Type & Null Checks

### `hasprop`

Returns `true` if a JSON object contains the specified property key.

| Parameter | Type | Description |
|---|---|---|
| obj | object | JSON object to check |
| key | string | Property name to look for |

**Returns:** Boolean.

```json
{ "hasprop": [{ "var": "" }, "optionalSetting"] }
```

---

### `isnullorempty`

Returns `true` if a value is null, an empty string, an empty array, or otherwise falsy.

| Parameter | Type | Description |
|---|---|---|
| value | any | Value to check |

**Returns:** Boolean.

```json
{ "isnullorempty": [{ "var": "description" }] }
```

---

## File System

### `filesize`

Returns the size (in bytes) of a local file or folder matched by a path expression.

| Parameter | Type | Description |
|---|---|---|
| inputString | string | Path expression (same format as `part`) |

**Returns:** File size as a number.

```json
{ "filesize": "report.json" }
```

---

### `filetextsearchcount`

Counts the number of times a regex pattern appears anywhere in the raw text content of a file.

| Parameter | Type | Description |
|---|---|---|
| filePath | string | Path to the file |
| patternString | string | Regex pattern to count |

**Returns:** Integer match count.

```json
{ "filetextsearchcount": [{ "var": "filePath" }, "customColor"] }
```

---

### `fromyamlfile`

Loads a YAML file and returns its content as a JSON node. If the file contains multiple YAML documents (separated by `---`), returns an array of documents.

| Parameter | Type | Description |
|---|---|---|
| inputString | string | Path to the YAML file |

**Returns:** Parsed JSON node, or array of nodes for multi-document YAML files.

```json
{ "fromyamlfile": "config.yaml" }
```

---

*For deeper examples and combined operator usage, see [Example-NewOperators-rules.json](Example-NewOperators-rules.json), [Example-let-rule.json](Example-let-rule.json), and the [Fab Inspector wiki](https://github.com/NatVanG/PBI-InspectorV2/wiki).*
