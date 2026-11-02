using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace SqlApp.Services
{
    public class AssetBundle
    {
        public string Name { get; set; } = string.Empty;
        public string DirectoryPath { get; set; } = string.Empty;
        public string ModelPath { get; set; } = string.Empty;
        public string DbPath { get; set; } = string.Empty;
        public string? EmbeddingPath { get; set; }
        public int Stage { get; set; }
        public int Contract { get; set; }
        public bool IsExactContract { get; set; }
    }

    public class AssetDiscoveryService
    {
        private static readonly Regex StageRegex = new(@"stage(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex ContractRegex = new(@"contract(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public string FindAssetsRoot()
        {
            // Search upwards from current directory and AppContext.BaseDirectory to find the project root "assets" folder
            string[] startDirs = { Directory.GetCurrentDirectory(), AppContext.BaseDirectory };

            foreach (var start in startDirs)
            {
                try
                {
                    var dir = new DirectoryInfo(start);
                    while (dir != null)
                    {
                        string candidate = Path.Combine(dir.FullName, "assets");
                        if (Directory.Exists(candidate))
                        {
                            // Verify it has content or subdirectories
                            if (Directory.EnumerateFileSystemEntries(candidate).Any())
                            {
                                return Path.GetFullPath(candidate);
                            }
                        }
                        dir = dir.Parent;
                    }
                }
                catch { }
            }

            return Path.GetFullPath("assets");
        }

        public AssetBundle? CreateBundleFromDirectory(string dir)
        {
            if (!Directory.Exists(dir)) return null;

            string modelPath = Path.Combine(dir, "model.gguf");
            if (!File.Exists(modelPath))
            {
                modelPath = Directory.GetFiles(dir, "*.gguf").FirstOrDefault() ?? string.Empty;
            }

            string dbPath = Path.Combine(dir, "shop.db");
            if (!File.Exists(dbPath))
            {
                dbPath = Directory.GetFiles(dir, "*.db").FirstOrDefault() ?? string.Empty;
            }

            if (string.IsNullOrEmpty(modelPath) || !File.Exists(modelPath) ||
                string.IsNullOrEmpty(dbPath) || !File.Exists(dbPath))
            {
                return null;
            }

            string? embeddingPath = Directory.GetFiles(dir, "semantic_dict_*.bin").FirstOrDefault()
                ?? Directory.GetFiles(dir, "*.bin").FirstOrDefault();

            string dirName = Path.GetFileName(dir);
            if (string.IsNullOrEmpty(dirName)) dirName = dir;

            var bundle = new AssetBundle
            {
                Name = dirName,
                DirectoryPath = Path.GetFullPath(dir),
                ModelPath = Path.GetFullPath(modelPath),
                DbPath = Path.GetFullPath(dbPath),
                EmbeddingPath = embeddingPath != null ? Path.GetFullPath(embeddingPath) : null
            };

            var stageMatch = StageRegex.Match(dirName);
            if (stageMatch.Success && int.TryParse(stageMatch.Groups[1].Value, out int s))
            {
                bundle.Stage = s;
            }

            var contractMatch = ContractRegex.Match(dirName);
            if (contractMatch.Success && int.TryParse(contractMatch.Groups[1].Value, out int c))
            {
                bundle.Contract = c;
            }

            bundle.IsExactContract = !dirName.Contains("-rc", StringComparison.OrdinalIgnoreCase);

            return bundle;
        }

        public List<AssetBundle> DiscoverBundles()
        {
            var bundles = new List<AssetBundle>();
            string assetsRoot = FindAssetsRoot();

            if (!Directory.Exists(assetsRoot))
            {
                return bundles;
            }

            foreach (var dir in Directory.GetDirectories(assetsRoot))
            {
                var bundle = CreateBundleFromDirectory(dir);
                if (bundle != null)
                {
                    bundles.Add(bundle);
                }
            }

            // Sort: 1) Stage DESC, 2) Contract DESC, 3) Exact contract first
            return bundles
                .OrderByDescending(b => b.Stage)
                .ThenByDescending(b => b.Contract)
                .ThenByDescending(b => b.IsExactContract)
                .ToList();
        }

        public List<string> GetAllModels()
        {
            string root = FindAssetsRoot();
            if (!Directory.Exists(root)) return new();
            return Directory.GetFiles(root, "*.gguf", SearchOption.AllDirectories).ToList();
        }

        public List<string> GetAllDbs()
        {
            string root = FindAssetsRoot();
            if (!Directory.Exists(root)) return new();
            return Directory.GetFiles(root, "*.db", SearchOption.AllDirectories).ToList();
        }

        public List<string> GetAllEmbeddings()
        {
            string root = FindAssetsRoot();
            if (!Directory.Exists(root)) return new();
            return Directory.GetFiles(root, "*.bin", SearchOption.AllDirectories).ToList();
        }
    }
}
