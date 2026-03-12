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
        //if (!System.Diagnostics.Debugger.IsAttached && Environment.UserInteractive)
        //{
        //    // ユニットテスト時は Environment.UserInteractive が false になることが多いですが、
        //    // より確実にテストを避けるなら、以下のチェックを組み合わせます。
        //    System.Diagnostics.Debugger.Launch();
        //}

        bool isTesting = AppDomain.CurrentDomain.GetAssemblies()
        .Any(a => a.FullName!.Contains("test", StringComparison.OrdinalIgnoreCase));

        if (!isTesting && !System.Diagnostics.Debugger.IsAttached)
        {
            System.Diagnostics.Debugger.Launch();
        }
#endif
        // 1. 出力ディレクトリの準備
        string baseAppPath = AppDomain.CurrentDomain.BaseDirectory;
        string outputDir = Path.Combine(baseAppPath, "bundled_docs");
        Directory.CreateDirectory(outputDir);

        Console.WriteLine($"Output Directory: {outputDir}");
        var pathArgs = new List<string>();
        bool embedImages = false;
        string customOutputDir = "bundled_docs"; // 将来用：出力先変更など
      
       var config = ParseArgs(args);

        // 2. 蓄積された pathArgs に対してのみ、既存のファイル/ディレクトリ走査を実行
        if (config.PathArgs.Count == 0)
        {
            Console.WriteLine("Usage: fnb <path> [--embed-images]");
            return;
        }

        // あとはこの pathArgs を SelectMany に渡すだけ
        var allFiles = config.PathArgs
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
                        .Where(IsRootMarkdown) // ← ここをメソッド参照にする
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
            ProcessRootFile(file, customOutputDir, embedImages);
        });

        Console.WriteLine("All files processed.");
    }


    // Analyzer.cs 内で抽出ロジックを分離
    internal static bool IsRootMarkdown(string filePath)
    {
        try
        {
            using var reader = new StreamReader(filePath);
            string? line;
            int lineCount = 0;
            while ((line = reader.ReadLine()) != null && lineCount < 100)
            {
                if (line.StartsWith("# ")) return true;
                lineCount++;
            }
            return false;
        }
        catch { return false; }
    }

    // Analyzer.cs 内でロジックを切り出し
    internal record RunConfig(List<string> PathArgs, bool EmbedImages, string CustomOutputDir);

    internal static RunConfig ParseArgs(string[] args)
    {
        var pathArgs = new List<string>();
        bool embedImages = false;
        string customOutputDir = "bundled_docs";

        var queue = new Queue<string>(args);
        while (queue.Count > 0)
        {
            var arg = queue.Dequeue();
            switch (arg.ToLower())
            {
                case "--embed-images": embedImages = true; break;
                case "-o" or "--output":
                    if (queue.TryDequeue(out var val)) customOutputDir = val;
                    break;
                default:
                    if (!arg.StartsWith("-")) pathArgs.Add(arg);
                    break;
            }
        }
        return new RunConfig(pathArgs, embedImages, customOutputDir);
    }

    internal static void ProcessRootFile(string targetFile, string outDir, bool embedImages)
    {
        if (!File.Exists(targetFile) || Path.GetExtension(targetFile).ToLower() != ".md") return;

        string fullPath = Path.GetFullPath(targetFile);
        string root = FindRootPath(fullPath);


        // ファイル書き出しのみを担当
        string safeFileName = fullPath.Replace(root, "").TrimStart(Path.DirectorySeparatorChar).Replace(Path.DirectorySeparatorChar, '_');
        string outputPath = Path.Combine(outDir, safeFileName);
        Console.WriteLine($"Processing: {Path.GetFileName(targetFile)} -> {safeFileName}");
        // ロジックの呼び出し
        var result = Core.Resolve(fullPath, root, new HashSet<string>(), embedImages);

        File.WriteAllText(Path.Combine(outDir, safeFileName), result);
    }

 internal   static string FindRootPath(string startPath)
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



