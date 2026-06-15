# Fab Inspector — Operators Overview

Fab Inspector extends the [JSON Logic](https://jsonlogic.com/operations.html) built-in operators with two sets of custom operators. This page explains when to use each set and the shared conventions that apply to both.

---

## Contents

- [Which operators to use](#which-operators-to-use)
- [Shared conventions](#shared-conventions)
- [Operator quick-reference](#operator-quick-reference)

---

## Which operators to use

| Capability | Use | Operator family |
|---|---|---|
| Navigate or iterate Fabric item files | Parse and query a part of the item being tested | [Ric Operators](ric-operators.md) — `part`, `partinfo`, `path` |
| Transform or reshape data | Convert, join, split, count, or filter data already loaded | [Ric Operators](ric-operators.md) — `coalesce`, `tostring`, `count`, `distinct`, `keys`, `values`, etc. |
| String and regex operations | Match patterns, split, join, or extract via regex | [Ric Operators](ric-operators.md) — `strcontains`, `strsplit`, `strjoin`, `regexextract` |
| Set and array operations | Compute unions, intersections, differences | [Ric Operators](ric-operators.md) — `union`, `intersect`, `diff`, `symdiff`, `equalsets`, `slice` |
| Layout analysis | Detect overlapping visuals on a page | [Ric Operators](ric-operators.md) — `rectoverlap` |
| Date/time arithmetic | Compare or offset timestamps | [Ric Operators](ric-operators.md) — `now`, `datediff` |
| Local file system | Read file sizes or search raw text | [Ric Operators](ric-operators.md) — `filesize`, `filetextsearchcount`, `fromyamlfile` |
| Call Fabric or Power BI REST API | Authenticated GET against any Fabric/Power BI endpoint | [Fab Inspector Operators](fabinspector-operators.md) — `apiget` |
| Read from OneLake DFS | Fetch a file from an OneLake endpoint | [Fab Inspector Operators](fabinspector-operators.md) — `dfsget` |
| Execute DAX | Query a published semantic model | [Fab Inspector Operators](fabinspector-operators.md) — `daxquery` |
| Execute T-SQL | Query a Fabric Lakehouse SQL endpoint | [Fab Inspector Operators](fabinspector-operators.md) — `sqlquery` |
| Workspace metadata scan | Retrieve workspace info via Power BI admin API | [Fab Inspector Operators](fabinspector-operators.md) — `scannerapi` |

**In short:**
- **Ric operators** work entirely with data already available locally (item files, in-memory data, local filesystem). They have no network or authentication requirements.
- **Fab Inspector operators** make authenticated outbound calls (REST, DFS, DAX, SQL). They require a non-`local` `-authmethod`.

---

## Shared conventions

### Where operators appear

Both operator families are available in the `test` field of any rule. Ric operators are also available in the `patch` field.

### URL placeholder tokens (Fab Inspector operators only)

Fab Inspector operators that accept URLs resolve the following tokens automatically at runtime:

| Token | Resolved to |
|---|---|
| `{context-fabricworkspace}` | The workspace ID from the `-fabricworkspace` CLI parameter |
| `{context-fabricitem}` | The item ID from the `-fabricitem` CLI parameter |

Additional positional placeholders (e.g. `{type}`, `{folder}`, `{fileName}`) are filled from the `urlParameters` array in the order they appear in the URL.

### Authentication (Fab Inspector operators only)

All Fab Inspector operators require a non-`local` `-authmethod`. See the [CLI reference](cli-reference.md#parameters) for available authentication methods and environment variable support.

### Parallel execution caution

The `scannerapi` operator polls for up to 5 minutes per call. Using it with `-parallel true` is not recommended as long-running polls will occupy worker threads.

Other Fab Inspector operators may hit service throttling sooner under parallel fan-out.

---

## Operator quick-reference

- **[Ric Operators](ric-operators.md)** — `part`, `partinfo`, `path`, `query`, `drillvar`, `let`, `coalesce`, `tostring`, `torecord`, `typeof`, `keys`, `values`, `distinct`, `count`, `strcontains`, `strsplit`, `strjoin`, `regexextract`, `slice`, `union`, `intersect`, `diff`, `symdiff`, `equalsets`, `rectoverlap`, `now`, `datediff`, `hasprop`, `isnullorempty`, `filesize`, `filetextsearchcount`, `fromyamlfile`

- **[Fab Inspector Operators](fabinspector-operators.md)** — `apiget`, `dfsget`, `daxquery`, `sqlquery`, `scannerapi`
