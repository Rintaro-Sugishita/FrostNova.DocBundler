# FrostNova.DocBundler (`fnb`)

A high-performance, NativeAOT-compiled Markdown bundling tool designed for developers who use **VS Code + Markdown Preview Enhanced (MPE)**.

When writing technical specifications, we often use `@import` to keep documents DRY. However, LLMs (like ChatGPT or Claude) work best with a single, continuous context. `fnb` bridges this gap by merging your distributed documentation into a single, AI-ready Markdown file.

## Key Features

* **Recursive Bundling**: Resolves `@import` directives recursively, maintaining document hierarchy.
* **Root Path Discovery**: Automatically identifies the project root by searching for `.git`, `.vscode`, or `.crossnote` to resolve absolute paths (`/path/to/file`).
* **Smart Code Blocks**: Automatically wraps imported source files (`.cs`, `.ts`, `.sql`, etc.) in fenced code blocks with proper syntax highlighting.
* **Enhanced CSV Support**:
* Full support for double-quoted fields and escaped characters.
* **Auto-Alignment**: Detects numeric columns and automatically applies right-alignment in the generated Markdown table.


* **AI-Optimized Images**: Rewrites relative image paths to root-relative paths so that the context remains clear for both humans and AI agents.
* **High Performance**: Compiled with **.NET 9 NativeAOT**. It's a single, tiny executable with zero dependencies and near-instant startup.
* **Parallel Processing**: Blazing fast bundling of multiple files using multi-threaded execution.

## Installation

Download the latest `fnb.exe` from the releases page and place it in your PATH.

## Usage

### Command Line

Pass one or more Markdown files. Wildcards are supported.

```bash
# Bundle a single file
fnb index.md

# Bundle multiple files using wildcards
fnb docs/*.md specifications/**/*.md

```

### Drag & Drop

You can also drag and drop `.md` files directly onto `fnb.exe`.

### Output

The bundled files will be generated in a folder named `bundled_docs` located in the same directory as the `fnb.exe` application.

## Integration with MPE

This tool supports the standard MPE `@import` syntax:

```markdown
@import "sub_document.md"
@import "/absolute/path/from/root.md"
@import "data.csv"
@import "logic.cs"

```

## Build from Source

Requirements: **.NET 10 SDK**

```bash
git clone https://github.com/YourUsername/FrostNova.DocBundler.git
cd FrostNova.DocBundler/FrostNova.DocBundler
dotnet publish -r win-x64 -c Release

```
