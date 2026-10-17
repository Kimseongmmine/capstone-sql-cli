using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using SqlEngine.Core;

namespace SqlApp.Services
{
    public class InferenceService : IDisposable
    {
        private readonly ConcurrentDictionary<string, SqlPipeline> _pipelines = new();
        private readonly object _lock = new();

        public Task<SqlResult> RunQueryAsync(
            string modelPath,
            string? dbPath,
            string? embeddingPath,
            string strategy,
            int maxTokens,
            double threshold,
            int dim,
            string question)
        {
            return Task.Run(() =>
            {
                lock (_lock)
                {
                    if (string.IsNullOrWhiteSpace(modelPath) || !File.Exists(modelPath))
                    {
                        throw new FileNotFoundException($"모델 파일(.gguf)을 찾을 수 없습니다: {modelPath}");
                    }

                    string key = $"{modelPath}|{dbPath ?? ""}";
                    if (!_pipelines.TryGetValue(key, out var pipeline))
                    {
                        pipeline = SqlPipeline.Create(modelPath, dbPath);
                        _pipelines[key] = pipeline;
                    }

                    if (!string.IsNullOrWhiteSpace(embeddingPath) && File.Exists(embeddingPath))
                    {
                        pipeline.SetEmbeddingPath(embeddingPath);
                    }

                    pipeline.SetStrategy(strategy, dim, threshold);

                    return pipeline.Run(question, maxTokens);
                }
            });
        }

        public Task<string> RunPostprocessAsync(
            string sql,
            string strategy,
            string? dbPath,
            int dim,
            double threshold)
        {
            return Task.Run(() =>
            {
                if (string.IsNullOrWhiteSpace(sql))
                {
                    throw new ArgumentException("교정할 SQL 쿼리가 비어 있습니다.", nameof(sql));
                }

                return SqlPipeline.Postprocess(sql, strategy, dbPath, dim, threshold);
            });
        }

        public void Dispose()
        {
            lock (_lock)
            {
                foreach (var p in _pipelines.Values)
                {
                    p.Dispose();
                }
                _pipelines.Clear();
            }
        }
    }
}
