using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace FrostNova.DocBundler;

internal class Analyzer
{


    public static void Run(string[] args)
    {
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
        var allFiles = args
            .SelectMany(arg => Directory.GetFiles(Directory.GetCurrentDirectory(), arg))
            .Distinct()
            .ToList();

        // 2. 並列実行 (CPUコアを有効活用)
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
            if (dir.GetDirectories(".git").Any() || dir.GetDirectories(".vscode").Any())
                return dir.FullName;
            dir = dir.Parent;
        }
        return Path.GetDirectoryName(startPath)!;
    }
}



