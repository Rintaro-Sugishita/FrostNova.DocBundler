using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace FrostNova.DocBundler;

internal class Analyzer
{


    public static void Run(string[] args)
    {
#if DEBUG
        Debugger.Launch();
#endif
        // 1. 出力ディレクトリの準備
        string baseAppPath = AppDomain.CurrentDomain.BaseDirectory;
        string outputDir = Path.Combine(baseAppPath, "bundled_docs");
        Directory.CreateDirectory(outputDir);

        Console.WriteLine($"Output Directory: {outputDir}");

        if (args.Length == 0)
        {
            Console.WriteLine("Usage: fnb <file1.md> <file2.md> ...");
            return;
        }

        // 引数から対象ファイルをフラットなリストとして抽出
        //var allFiles = args
        //    .SelectMany(arg => Directory.GetFiles(Directory.GetCurrentDirectory(), arg))
        //    .Distinct()
        //    .ToList();
        var allFiles = args
            .SelectMany(arg =>
            {
                // 直接ファイルを指定（ドラッグ＆ドロップ含む）
                if (File.Exists(arg))
                {
                    return [Path.GetFullPath(arg)];
                }

                // ディレクトリを指定（中にある全 .md を走査し、条件に合うものだけ抽出）
                if (Directory.Exists(arg))
                {
                    return Directory.GetFiles(arg, "*.md")
                        .Where(file =>
                        {
                            try
                            {
                                // ファイルを開いて、どこかにレベル1見出しがあるか探す
                                using var reader = new StreamReader(file);
                                string? line;
                                // パフォーマンスのため、最初の100行程度を確認すれば十分
                                int lineCount = 0;
                                while ((line = reader.ReadLine()) != null && lineCount < 100)
                                {
                                    if (line.StartsWith("# ")) return true;
                                    lineCount++;
                                }
                                return false;
                            }
                            catch
                            {
                                return false;
                            }
                        })
                        .Select(Path.GetFullPath);
                }                
                // ワイルドカード指定
                try
                {
                    string? dir = Path.GetDirectoryName(arg);
                    string pattern = Path.GetFileName(arg);
                    string searchDir = string.IsNullOrEmpty(dir)
                        ? Directory.GetCurrentDirectory()
                        : Path.GetFullPath(dir);

                    if (Directory.Exists(searchDir))
                    {
                        return Directory.GetFiles(searchDir, pattern).Select(Path.GetFullPath);
                    }
                }
                catch { /* ignored */ }

                return Array.Empty<string>();
            })
            .Distinct()
            .ToList();
        // 並列実行 (CPUコアを有効活用)
        Parallel.ForEach(allFiles, file =>
        {
            ProcessRootFile(file, outputDir);
        });

        Console.WriteLine("All files processed.");
    }


    public static void ProcessRootFile(string targetFile, string outDir)
    {
        if (!File.Exists(targetFile) || Path.GetExtension(targetFile).ToLower() != ".md") return;

        string fullPath = Path.GetFullPath(targetFile);
        string root = FindRootPath(fullPath);


        // ファイル書き出しのみを担当
        string safeFileName = fullPath.Replace(root, "").TrimStart(Path.DirectorySeparatorChar).Replace(Path.DirectorySeparatorChar, '_');
        string outputPath = Path.Combine(outDir, safeFileName);
        Console.WriteLine($"Processing: {Path.GetFileName(targetFile)} -> {safeFileName}");
        // ロジックの呼び出し
        var result = Core.Resolve(fullPath, root, new HashSet<string>());

        File.WriteAllText(Path.Combine(outDir, safeFileName), result);
    }

    static string FindRootPath(string startPath)
    {
        var dir = new DirectoryInfo(Path.GetDirectoryName(startPath)!);
        while (dir != null)
        {
            if (dir.GetDirectories(".git").Any() || dir.GetDirectories(".vscode").Any() || dir.GetDirectories(".crossnote").Any())
                return dir.FullName;
            dir = dir.Parent;
        }
        return Path.GetDirectoryName(startPath)!;
    }
}



