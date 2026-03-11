using System;
using System.Collections.Generic;
using System.Text;

namespace FrostNova.DocBundlerTest;

public class TempTestDirectory : IDisposable
{
    public string Path { get; }

    public TempTestDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "fnb_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string CreateFile(string relativePath, string content)
    {
        var fullPath = System.IO.Path.Combine(Path, relativePath);
        var dir = System.IO.Path.GetDirectoryName(fullPath);
        if (dir != null) Directory.CreateDirectory(dir);
        File.WriteAllText(fullPath, content);
        return fullPath;
    }

    public void Dispose()
    {
        if (Directory.Exists(Path)) Directory.Delete(Path, true);
    }

}
