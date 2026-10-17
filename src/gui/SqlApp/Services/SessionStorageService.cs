using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using SqlApp.Models;

namespace SqlApp.Services
{
    public class SessionStorageDto
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? AssetBundleName { get; set; }
        public string ModelPath { get; set; } = string.Empty;
        public string DbPath { get; set; } = string.Empty;
        public string? EmbeddingPath { get; set; }
        public AppMode CurrentMode { get; set; }
        public string Strategy { get; set; } = "typo,ngram";
        public int MaxTokens { get; set; } = 64;
        public double Threshold { get; set; }
        public int Dim { get; set; }
        public List<ChatMessageDto> Messages { get; set; } = new();
    }

    public class ChatMessageDto
    {
        public Guid Id { get; set; }
        public MessageSender Sender { get; set; }
        public string Text { get; set; } = string.Empty;
        public string Sql { get; set; } = string.Empty;
        public string? RawSql { get; set; }
        public string? StopReason { get; set; }
        public long InferenceTimeMs { get; set; }
        public DateTime Timestamp { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class SessionStorageService
    {
        private readonly string _storagePath;

        public SessionStorageService()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string dir = Path.Combine(appData, "capstone_sql_gui");
            Directory.CreateDirectory(dir);
            _storagePath = Path.Combine(dir, "sessions.json");
        }

        public async Task SaveSessionsAsync(IEnumerable<ConversationSession> sessions)
        {
            try
            {
                var dtos = new List<SessionStorageDto>();
                foreach (var s in sessions)
                {
                    var dto = new SessionStorageDto
                    {
                        Id = s.Id,
                        Title = s.Title,
                        CreatedAt = s.CreatedAt,
                        UpdatedAt = s.UpdatedAt,
                        AssetBundleName = s.AssetBundleName,
                        ModelPath = s.ModelPath,
                        DbPath = s.DbPath,
                        EmbeddingPath = s.EmbeddingPath,
                        CurrentMode = s.CurrentMode,
                        Strategy = s.Strategy,
                        MaxTokens = s.MaxTokens,
                        Threshold = s.Threshold,
                        Dim = s.Dim
                    };

                    foreach (var m in s.Messages)
                    {
                        dto.Messages.Add(new ChatMessageDto
                        {
                            Id = m.Id,
                            Sender = m.Sender,
                            Text = m.Text,
                            Sql = m.Sql,
                            RawSql = m.RawSql,
                            StopReason = m.StopReason,
                            InferenceTimeMs = m.InferenceTimeMs,
                            Timestamp = m.Timestamp,
                            ErrorMessage = m.ErrorMessage
                        });
                    }
                    dtos.Add(dto);
                }

                string json = JsonSerializer.Serialize(dtos, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(_storagePath, json);
            }
            catch { }
        }

        public async Task<List<ConversationSession>> LoadSessionsAsync()
        {
            var list = new List<ConversationSession>();
            if (!File.Exists(_storagePath)) return list;

            try
            {
                string json = await File.ReadAllTextAsync(_storagePath);
                var dtos = JsonSerializer.Deserialize<List<SessionStorageDto>>(json);
                if (dtos == null) return list;

                foreach (var dto in dtos)
                {
                    var s = new ConversationSession
                    {
                        Id = dto.Id,
                        Title = dto.Title,
                        CreatedAt = dto.CreatedAt,
                        UpdatedAt = dto.UpdatedAt,
                        AssetBundleName = dto.AssetBundleName,
                        ModelPath = dto.ModelPath,
                        DbPath = dto.DbPath,
                        EmbeddingPath = dto.EmbeddingPath,
                        CurrentMode = dto.CurrentMode,
                        Strategy = dto.Strategy,
                        MaxTokens = dto.MaxTokens,
                        Threshold = dto.Threshold,
                        Dim = dto.Dim
                    };

                    foreach (var m in dto.Messages)
                    {
                        s.Messages.Add(new ChatMessage
                        {
                            Id = m.Id,
                            Sender = m.Sender,
                            Text = m.Text,
                            Sql = m.Sql,
                            RawSql = m.RawSql,
                            StopReason = m.StopReason,
                            InferenceTimeMs = m.InferenceTimeMs,
                            Timestamp = m.Timestamp,
                            ErrorMessage = m.ErrorMessage
                        });
                    }
                    list.Add(s);
                }
            }
            catch { }

            return list;
        }
    }
}
