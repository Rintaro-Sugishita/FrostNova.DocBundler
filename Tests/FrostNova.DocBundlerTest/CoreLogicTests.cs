using System;
using System.Collections.Generic;
using System.Text;
using FrostNova.DocBundler;

namespace FrostNova.DocBundlerTest
{
    public class CoreLogicTests
    {
        [Fact]
        public void ConvertCsvToMdTable_ShouldAlignNumericColumnsRight()
        {
            // Arrange
            string[] csvLines = {
            "Item,Price,Quantity",
            "Apple,150,5",
            "Banana,80,10"
        };

            // Act
            var result = Core.ConvertCsvToMdTable(csvLines);

            // Assert
            // 2行目のセパレーター行を確認
            // Price(150)とQuantity(5)が数値なので右寄せ(---:)、Itemは左寄せ(---)を期待
            Assert.Contains("| --- | ---: | ---: |", result);
        }


        [Fact]
        public void ConvertCsvToMdTable_MatchesExactly()
        {
            // Arrange
            string[] csvLines = {
                "Item,Price",
                "Apple,150",
                "Banana,80"
            };

            // 期待される完全なMarkdown文字列
            // ヒアドキュメント内の改行に注意
            var expected =
                "| Item | Price |\n" +
                "| --- | ---: |\n" +
                "| Apple | 150 |\n" +
                "| Banana | 80 |\n";

            // Act
            var result = Core.ConvertCsvToMdTable(csvLines);

            // Assert
            // 改行コードを統一して完全一致比較
            Assert.Equal(Normalize(expected), Normalize(result));
        }

        private string Normalize(string input) => input.Replace("\r\n", "\n").Trim();
        [Fact]
        public void Resolve_ShouldMergeRecursiveImports()
        {
            // Arrange
            using var temp = new TempTestDirectory();
            var root = temp.Path;

            // ファイル構成: index.md -> sub/page.md -> common.md
            temp.CreateFile("index.md", "# Index\n@import \"sub/page.md\"");
            temp.CreateFile("sub/page.md", "## Page\n@import \"../common.md\"");
            temp.CreateFile("common.md", "Shared Content");

            // Act
            var visited = new HashSet<string>();
            var result = Core.Resolve(Path.Combine(root, "index.md"), root, visited, false);

            // Assert
            Assert.Contains("# Index", result);
            Assert.Contains("## Page", result);
            Assert.Contains("Shared Content", result);
        }

        [Fact]
        public void Resolve_ShouldFixImagePathsToRootRelative()
        {
            // Arrange
            using var temp = new TempTestDirectory();
            var root = temp.Path;

            // sub/doc.md から ../img/pic.png を参照
            var docPath = temp.CreateFile("sub/doc.md", "![pic](../img/pic.png)");
            temp.CreateFile("img/pic.png", ""); // ダミーファイル

            // Act
            var result = Core.Resolve(docPath, root, new HashSet<string>(), false);

            // Assert
            // ルート相対パス /img/pic.png に書き換わっていることを確認
            Assert.Contains("![pic](/img/pic.png)", result);
        }

        [Fact]
        public void Resolve_ShouldHandleCsvWithQuotes()
        {
            // Arrange
            using var temp = new TempTestDirectory();
            var csvPath = temp.CreateFile("data.csv", "City,\"Location, Info\"\nTokyo,\"Minato, Roppongi\"");
            var mdPath = temp.CreateFile("index.md", "@import \"data.csv\"");

            // Act
            var result = Core.Resolve(mdPath, temp.Path, new HashSet<string>(), false);

            // Assert
            // カンマを含むフィールドが正しく分割されているか
            Assert.Contains("| Location, Info |", result);
            Assert.Contains("| Minato, Roppongi |", result);
        }

        [Fact]
        public void Resolve_ShouldDetectRecursiveLoop()
        {
            // Arrange
            using var temp = new TempTestDirectory();
            temp.CreateFile("A.md", "@import \"B.md\"");
            temp.CreateFile("B.md", "@import \"A.md\"");
            var startPath = Path.Combine(temp.Path, "A.md");

            // Act
            var result = Core.Resolve(startPath, temp.Path, new HashSet<string>(), false);

            // Assert
            Assert.Contains("[Recursive Import Skip: A.md]", result);
        }

        [Fact]
        public void Resolve_MultipleImportsAndTextPreservation_FullMatch()
        {
            // Arrange
            using var temp = new TempTestDirectory();
            temp.CreateFile("main.md",
                "Introduction text.\n" +
                "@import \"part1.md\"\n" +
                "Middle text.\n" +
                "@import \"part2.md\"\n" +
                "Ending text.");

            temp.CreateFile("part1.md", "Content 1");
            temp.CreateFile("part2.md", "Content 2");

            var expected =
                "Introduction text.\n" +
                "Content 1\n" +
                "Middle text.\n" +
                "Content 2\n" +
                "Ending text.\n";

            // Act
            var result = Core.Resolve(Path.Combine(temp.Path, "main.md"), temp.Path, new HashSet<string>(), false);

            // Assert
            Assert.Equal(Normalize(expected), Normalize(result));
        }

        [Fact]
        public void Resolve_RootRelativePath_FullMatch()
        {
            // Arrange
            using var temp = new TempTestDirectory();
            // ルートの目印を作成
            temp.CreateFile(".git", "");

            // 深い階層のファイルから、ルート直下のファイルを絶対パスでインポート
            temp.CreateFile("common/footer.md", "Global Footer");
            var deepMd = temp.CreateFile("project/sub/doc.md", "@import \"/common/footer.md\"");

            var expected = "Global Footer\n";

            // Act
            // 第2引数に temp.Path を渡すことでそこをルートとして認識させる
            var result = Core.Resolve(deepMd, temp.Path, new HashSet<string>(), false);

            // Assert
            Assert.Equal(Normalize(expected), Normalize(result));
        }

        [Fact]
        public void Resolve_ImportVariations_FullMatch()
        {
            // Arrange
            using var temp = new TempTestDirectory();
            temp.CreateFile("main.md",
                "  @import \"sub.md\"  \n" + // 前後にスペースがあっても反応すべきか？（現在の仕様はTrim前提）
                "@import \"sub.md\"");      // 連続

            temp.CreateFile("sub.md", "Sub");

            var expected = "Sub\nSub\n";

            // Act
            var result = Core.Resolve(Path.Combine(temp.Path, "main.md"), temp.Path, new HashSet<string>(), false);

            // Assert
            Assert.Equal(Normalize(expected), Normalize(result));
        }

        [Fact]
        public void Resolve_FileNotFound_ShouldEmbedWarning()
        {
            using var temp = new TempTestDirectory();
            var main = temp.CreateFile("main.md", "@import \"missing.md\"");

            var result = Core.Resolve(main, temp.Path, new HashSet<string>(), false);

            // 以前実装した > [!CAUTION] が出ているか
            Assert.Contains("File Not Found: missing.md", result);
        }

        [Fact]
        public void Resolve_EmptyFile_ShouldNotCrash()
        {
            using var temp = new TempTestDirectory();
            temp.CreateFile("main.md", "@import \"empty.md\"");
            temp.CreateFile("empty.md", ""); // 空ファイル

            var result = Core.Resolve(Path.Combine(temp.Path, "main.md"), temp.Path, new HashSet<string>(), false);

            // クラッシュせず、空文字（または元の行を除去した結果）が返ってくるか
            Assert.NotNull(result);
        }

        [Fact]
        public void Resolve_PathWithSpaces_ShouldWork()
        {
            using var temp = new TempTestDirectory();
            // スペース入りのディレクトリ
            var subDir = Path.Combine(temp.Path, "my docs");
            Directory.CreateDirectory(subDir);

            File.WriteAllText(Path.Combine(subDir, "target.md"), "Space Content");
            var main = temp.CreateFile("main.md", "@import \"my docs/target.md\"");

            var result = Core.Resolve(main, temp.Path, new HashSet<string>(), false);

            Assert.Contains("Space Content", result);
        }

        [Theory]
        [InlineData("100", true)]    // 整数
        [InlineData("1.23", true)]   // 小数
        [InlineData("-50", true)]    // 負数
        [InlineData("1,000", true)] // カンマ区切り（デフォルトのTryParseでは文字列扱い）
        [InlineData("v1.0", false)]   // バージョン表記
        [InlineData("2026/03/11", false)] // 日付
        public void CsvNumericDetection_Patterns(string dataValue, bool expectedIsNumeric)
        {
            // Arrange
            // ヘッダー行と、テスト対象のデータ行を作成
            string[] csvLines = {
                "Header",
                dataValue
            };

            // Act
            var result = Core.ConvertCsvToMdTable(csvLines);

            // Assert
            if (expectedIsNumeric)
            {
                // 右寄せ (---:) が含まれているべき
                Assert.Contains("| ---: |", result);
            }
            else
            {
                // 左寄せ (---) であるべき
                Assert.Contains("| --- |", result);
            }
        }



    }
}
