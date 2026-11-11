# C# formatting conventions

These conventions apply to C# files in this repository, including tests.
The initial configuration is intended for demonstration and team review.

## Shared settings

The root .editorconfig defines four-space indentation, brace placement,
basic spacing, a final newline and removal of trailing whitespace.

Existing single-line blocks and statements are preserved.

## Using directives

Sort using directives alphabetically, without putting System namespaces
first and without blank lines between groups.

Use the editor's organise-usings or code-cleanup action to sort imports.
Do not assume Format Document also sorts them.

## Line wrapping

Keep short expressions and service registrations on one line where they
remain readable. Wrap longer expressions at natural logical breaks when
this improves readability.

There is no mandatory character limit. Editor word wrapping can be used
to accommodate screen width.

The shared settings do not automatically join previously wrapped calls.

## Visual Studio

Visual Studio supports .editorconfig directly.

Use Quick Actions and Refactorings > Sort Usings for import ordering.

## Formatting on save

Formatting on save was tested in both editors and corrected deliberately
introduced spacing errors.

In Visual Studio, use a Code Cleanup profile containing only Format
document when formatting-only behaviour is required. Other fixers can
remove imports or apply additional changes.

In VS Code, enable Editor: Format On Save for C# files when desired.

Formatting on save remains a developer setting for this initial proposal.
Review the diff after saving, particularly when editing existing files.

## Visual Studio Code

Use Microsoft's C# extension (ms-dotnettools.csharp).
C# Dev Kit (ms-dotnettools.csdevkit) provides additional .NET tooling.

Install EditorConfig for VS Code (EditorConfig.EditorConfig) for general
EditorConfig properties such as indentation and file whitespace.

Use Microsoft's C# formatter for C# files. Place the cursor on a using
directive and press Ctrl+. > Sort Usings for import ordering.

## Existing code

Apply the conventions to new or modified code. Avoid bulk reformatting.

Inspect the diff after formatting or saving. Whole-file formatting,
trailing-whitespace cleanup and final-newline insertion can affect
untouched lines. Keep unrelated formatting changes out of feature PRs.

## Scope and follow-up

This baseline covers the API repository and its test projects.

Member ordering is deferred to phase 2. CI formatting checks and a wider
rollout to other repositories remain subject to team agreement.
