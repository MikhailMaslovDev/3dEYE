# 3dEYE Test Assignment

Windows desktop solution for generating and externally sorting large text files.

The task consists of two C# applications:

- `FileGenerator` creates reproducible UTF-8 test files of a requested minimum size.
- `FileSorter` will sort files that are too large to fit in memory by using external stable merge sort.

## Input and output format

Every valid line has the following format:

```text
<Number>.<String>
```

Examples:

```text
415.Apple
415. Apple
12.Version 1.2
```

`Number` is a non-negative `Int32`. The separator is the first dot immediately after the number; all later dots and spaces belong to `String`.

Sorting order:

1. string part, using ordinal case-sensitive comparison;
2. number, ascending;
3. original line order when both keys are identical.

Invalid lines will be skipped and reported in `invalid-lines.log` beside the sorted output.

## Current status

| Project | Status |
| --- | --- |
| `FileGenerator` | Implemented: UI, validation, cancellation, progress, reproducible random data and duplicate text values. |
| `FileSorter` | Planned: parser, invalid-line log, chunk sorting, stable multi-pass merge and UI. |

## FileGenerator

The generator creates a file at least as large as the entered size. It writes to a temporary `.partial` file first and moves it to the selected output path only after successful completion. Cancelling the operation removes the temporary file.

The size input is expressed in binary megabytes:

```text
1 MB = 1024 × 1024 = 1,048,576 bytes
```

The random seed makes data generation reproducible. The generator builds random strings of varying word count and length, and with a fixed probability chooses one of several shared strings so duplicate string values are guaranteed in a sufficiently large file.
## Planned FileSorter design

The sorter will avoid loading the whole input file into memory:

```text
Input file
  -> read a chunk
  -> stable in-memory sort
  -> write a sorted temporary file
  -> merge sorted temporary files in bounded groups
  -> final sorted output
```

The number of workers, in-memory chunk size and maximum number of simultaneously merged files will be configurable in the application UI.
