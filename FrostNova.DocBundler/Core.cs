using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using static System.Net.Mime.MediaTypeNames;

namespace FrostNova.DocBundler
{
    public static partial class Core
    {
        // Source Generators for Regex (NativeAOT friendly)
        [GeneratedRegex(@"!\[(.*?)\]\((.*?)\)")]
        private static partial Regex ImageRegex();

        [GeneratedRegex(@"^@import\s+""(.+?)""")]
        private static partial Regex ImportRegex();

        private static readonly object _consoleLock = new();

        private static void WriteError(string message)
        {
            lock (_consoleLock)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine($"[ERR] {message}");
                Console.ResetColor();
            }
        }
        // ヘルパーメソッド
        private static void WriteWarning(string message)
        {
            lock (_consoleLock)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Error.WriteLine($"[WARN] {message}");
                Console.ResetColor();
            }
        }


        /// <summary>
        /// Resolves @import directives and rewrites image paths in a markdown file.
        /// </summary>
        /// <param name="filePath">Target markdown file path.</param>
        /// <param name="root">Project root path for absolute path resolution.</param>
        /// <param name="visited">Set of paths currently in the recursion stack.</param>
        /// <returns>Bundled markdown content.</returns>
        public static string Resolve(string filePath, string root, HashSet<string> visited)
        {
            var fullPath = Path.GetFullPath(filePath);

            // 再帰呼び出しの深さ方向で同じファイルを読もうとした場合はスキップ            
            if (visited.Contains(fullPath))
            {
                var msg = $"Recursive Import Skip: {Path.GetFileName(filePath)}";
                WriteWarning(msg); // 色付きで出力
                return $"\n[{msg}]\n";
            }

            if (!File.Exists(fullPath))
            {
                var msg = $"File Not Found: {Path.GetFileName(filePath)}";
                WriteError(msg); // 色付きで出力
                return $"\n> [!CAUTION] {msg}\n";
            }

            // 現在のファイルをスタックに追加
            visited.Add(fullPath);

            try
            {
                var lines = File.ReadAllLines(fullPath);
                var sb = new StringBuilder();
                var currentDir = Path.GetDirectoryName(fullPath)!;

                foreach (var line in lines)
                {
                    // 1. まず、画像パスの書き換えを試みる（インポート行かどうかにかかわらず実行）
                    var processedLine = ImageRegex().Replace(line, m =>
                    {
                        var alt = m.Groups[1].Value;
                        var path = m.Groups[2].Value;

                        // 外部URLまたはルート相対パスはそのまま
                        if (path.StartsWith("http") || path.StartsWith("/")) return m.Value;

                        var absImagePath = Path.GetFullPath(Path.Combine(currentDir, path));
                        var relToRoot = Path.GetRelativePath(root, absImagePath);

                        // WindowsパスをURL/Markdown互換のスラッシュに統一
                        return $"![{alt}](/{relToRoot.Replace('\\', '/')})";
                    });

                    // 2. 処理済みの行が @import 構文か判定
                    var match = ImportRegex().Match(processedLine.Trim());
                    if (match.Success)
                    {
                        var importPathStr = match.Groups[1].Value;
                        var fullImportPath = importPathStr.StartsWith("/")
                            ? Path.GetFullPath(importPathStr.TrimStart('/'), root)
                            : Path.GetFullPath(Path.Combine(currentDir, importPathStr));

                        var ext = Path.GetExtension(fullImportPath).ToLower();

                        // インポート先の内容を取得
                        var importedContent = ProcessContent(fullImportPath, ext, root, visited);
                        sb.Append(importedContent);

                        // インポートされた内容の末尾に改行がない場合、構造維持のため改行を追加
                        if (importedContent.Length > 0 && !importedContent.EndsWith('\n'))
                        {
                            sb.Append('\n');
                        }
                    }
                    else
                    {
                        // 3. 通常の行（画像置換済み）を追加
                        sb.AppendLine(processedLine);
                    }
                }
                return sb.ToString();
            }
            finally
            {
                // 他のルートからのインポートを許可するため、メソッド終了時にスタックから解除
                visited.Remove(fullPath);
            }
        }


        private static string ProcessContent(string path, string ext, string root, HashSet<string> visited)
        {
            return ext switch
            {
                ".md" => Resolve(path, root, visited),
                ".csv" => ConvertCsvToMdTable(File.ReadAllLines(path)),
                ".cs" or ".ts" or ".sql" or ".json" => $"```{ext.TrimStart('.')}\n{File.ReadAllText(path)}\n```",
                _ => Resolve(path, root, visited)
            };
        }

        public static string ConvertCsvToMdTable(string[] lines)
        {
            //var lines = File.ReadAllLines(path);
            if (lines.Length == 0) return "";

            // 1. 全データを二次元配列（リストのリスト）としてパース
            var tableData = lines.Select(ParseCsvLine).ToList();
            if (tableData.Count == 0) return "";

            var sb = new StringBuilder();
            int columnCount = tableData.Max(row => row.Count);

            // 2. 各列が数値かどうかを判定（ヘッダー以外の1行目以降で判定）
            var isNumericColumn = new bool[columnCount];
            if (tableData.Count > 1)
            {
                var firstDataRow = tableData[1];
                for (int i = 0; i < columnCount; i++)
                {
                    if (i < firstDataRow.Count && double.TryParse(firstDataRow[i], out _))
                    {
                        isNumericColumn[i] = true;
                    }
                }
            }

            // 3. Markdown出力
            for (int rowIndex = 0; rowIndex < tableData.Count; rowIndex++)
            {
                var row = tableData[rowIndex];
                // 足りない列を埋める
                var cells = row.Concat(Enumerable.Repeat("", Math.Max(0, columnCount - row.Count))).ToList();

                sb.AppendLine("| " + string.Join(" | ", cells) + " |");

                // ヘッダー直後のセパレーター行
                if (rowIndex == 0)
                {
                    var separators = isNumericColumn.Select(isNum => isNum ? "---:" : "---");
                    sb.AppendLine("| " + string.Join(" | ", separators) + " |");
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// ダブルクォーテーションを考慮したCSVパース（NativeAOT互換）
        /// </summary>
        static List<string> ParseCsvLine(string line)
        {
            var fields = new List<string>();
            var currentField = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        // エスケープされたダブルクォート ("")
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            currentField.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        currentField.Append(c);
                    }
                }
                else
                {
                    if (c == '"')
                    {
                        inQuotes = true;
                    }
                    else if (c == ',')
                    {
                        fields.Add(currentField.ToString().Trim());
                        currentField.Clear();
                    }
                    else
                    {
                        currentField.Append(c);
                    }
                }
            }
            fields.Add(currentField.ToString().Trim());
            return fields;
        }


    }



}
