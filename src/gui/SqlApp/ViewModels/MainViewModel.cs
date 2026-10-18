using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SqlApp.Models;
using SqlApp.Services;

namespace SqlApp.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly AssetDiscoveryService _assetDiscoveryService;
        private readonly SqlExecutionService _sqlExecutionService;
        private readonly InferenceService _inferenceService;
        private readonly SessionStorageService _sessionStorageService;

        public ObservableCollection<ConversationSession> Sessions { get; } = new();

        [ObservableProperty]
        public partial ConversationSession? SelectedSession { get; set; }

        partial void OnSelectedSessionChanged(ConversationSession? value)
        {
            NotifyBundleProperties();
        }

        [ObservableProperty]
        public partial string InputText { get; set; } = string.Empty;

        [ObservableProperty]
        public partial bool IsGenerating { get; set; }

        [ObservableProperty]
        public partial string StatusMessage { get; set; } = string.Empty;

        public ObservableCollection<AssetBundle> AvailableBundles { get; } = new();

        public AssetBundle? CurrentSessionBundle
        {
            get
            {
                if (SelectedSession == null) return null;

                if (!string.IsNullOrEmpty(SelectedSession.AssetBundleName))
                {
                    var matchByName = AvailableBundles.FirstOrDefault(b =>
                        string.Equals(b.Name, SelectedSession.AssetBundleName, StringComparison.OrdinalIgnoreCase));
                    if (matchByName != null) return matchByName;
                }

                var matchByPaths = AvailableBundles.FirstOrDefault(b =>
                    string.Equals(b.ModelPath, SelectedSession.ModelPath, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(b.DbPath, SelectedSession.DbPath, StringComparison.OrdinalIgnoreCase));
                if (matchByPaths != null) return matchByPaths;

                if (!string.IsNullOrEmpty(SelectedSession.ModelPath))
                {
                    string? dir = Path.GetDirectoryName(SelectedSession.ModelPath);
                    if (!string.IsNullOrEmpty(dir))
                    {
                        var matchByDir = AvailableBundles.FirstOrDefault(b =>
                            string.Equals(b.DirectoryPath, dir, StringComparison.OrdinalIgnoreCase));
                        if (matchByDir != null) return matchByDir;
                    }
                }

                return AvailableBundles.FirstOrDefault();
            }
            set
            {
                SelectBundle(value);
            }
        }

        public void SelectBundle(AssetBundle? bundle)
        {
            if (bundle == null || SelectedSession == null) return;
            if (ReferenceEquals(CurrentSessionBundle, bundle)) return;

            SelectedSession.AssetBundleName = bundle.Name;
            SelectedSession.ModelPath = bundle.ModelPath;
            SelectedSession.DbPath = bundle.DbPath;
            SelectedSession.EmbeddingPath = bundle.EmbeddingPath;

            NotifyBundleProperties();
            _ = LoadSchemaAsync();
            _ = _sessionStorageService.SaveSessionsAsync(Sessions);
        }

        public void NotifyBundleProperties()
        {
            OnPropertyChanged(nameof(CurrentSessionBundle));
            OnPropertyChanged(nameof(CurrentSessionBundleHasEmbedding));
            OnPropertyChanged(nameof(SelectedModelFileName));
            OnPropertyChanged(nameof(SelectedDbFileName));
            OnPropertyChanged(nameof(SelectedEmbeddingFileName));
            OnPropertyChanged(nameof(SelectedModelPath));
            OnPropertyChanged(nameof(SelectedDbPath));
            OnPropertyChanged(nameof(SelectedEmbeddingPath));
        }

        public string SelectedModelFileName =>
            !string.IsNullOrEmpty(CurrentSessionBundle?.ModelPath)
                ? Path.GetFileName(CurrentSessionBundle.ModelPath)
                : "(없음)";

        public string SelectedDbFileName =>
            !string.IsNullOrEmpty(CurrentSessionBundle?.DbPath)
                ? Path.GetFileName(CurrentSessionBundle.DbPath)
                : "(없음)";

        public string SelectedEmbeddingFileName =>
            !string.IsNullOrEmpty(CurrentSessionBundle?.EmbeddingPath)
                ? Path.GetFileName(CurrentSessionBundle.EmbeddingPath)
                : "(없음)";

        public string SelectedModelPath => CurrentSessionBundle?.ModelPath ?? string.Empty;
        public string SelectedDbPath => CurrentSessionBundle?.DbPath ?? string.Empty;
        public string SelectedEmbeddingPath => CurrentSessionBundle?.EmbeddingPath ?? string.Empty;

        public bool CurrentSessionBundleHasEmbedding => !string.IsNullOrEmpty(CurrentSessionBundle?.EmbeddingPath);

        public ObservableCollection<string> AvailableModels { get; } = new();
        public ObservableCollection<string> AvailableDbs { get; } = new();
        public ObservableCollection<string> AvailableEmbeddings { get; } = new();
        public ObservableCollection<TableSchemaInfo> CurrentSchema { get; } = new();

        [ObservableProperty]
        public partial bool IsLoadingSchema { get; set; }

        public MainViewModel()
        {
            _assetDiscoveryService = new AssetDiscoveryService();
            _sqlExecutionService = new SqlExecutionService();
            _inferenceService = new InferenceService();
            _sessionStorageService = new SessionStorageService();

            InitializeAssets();
            CreateNewSession();
            _ = LoadInitialSessionsAsync();
        }

        private async Task LoadInitialSessionsAsync()
        {
            var bestBundle = AvailableBundles.FirstOrDefault() ?? _assetDiscoveryService.DiscoverBundles().FirstOrDefault();

            var saved = await _sessionStorageService.LoadSessionsAsync();
            if (saved.Count > 0)
            {
                Sessions.Clear();
                foreach (var s in saved)
                {
                    if (string.IsNullOrWhiteSpace(s.ModelPath) || !File.Exists(s.ModelPath))
                    {
                        s.ModelPath = bestBundle?.ModelPath ?? AvailableModels.FirstOrDefault() ?? string.Empty;
                    }
                    if (string.IsNullOrWhiteSpace(s.DbPath) || !File.Exists(s.DbPath))
                    {
                        s.DbPath = bestBundle?.DbPath ?? AvailableDbs.FirstOrDefault() ?? string.Empty;
                    }
                    if (string.IsNullOrWhiteSpace(s.EmbeddingPath) || !File.Exists(s.EmbeddingPath))
                    {
                        s.EmbeddingPath = bestBundle?.EmbeddingPath ?? AvailableEmbeddings.FirstOrDefault();
                    }
                    if (string.IsNullOrEmpty(s.AssetBundleName))
                    {
                        s.AssetBundleName = bestBundle?.Name;
                    }

                    Sessions.Add(s);
                }
                SelectedSession = Sessions.FirstOrDefault();
                OnPropertyChanged(nameof(CurrentSessionBundle));
                OnPropertyChanged(nameof(CurrentSessionBundleHasEmbedding));
                _ = LoadSchemaAsync();
            }
        }

        private void InitializeAssets()
        {
            var bundles = _assetDiscoveryService.DiscoverBundles();
            var models = _assetDiscoveryService.GetAllModels();
            var dbs = _assetDiscoveryService.GetAllDbs();
            var embeddings = _assetDiscoveryService.GetAllEmbeddings();

            AvailableBundles.Clear();
            foreach (var b in bundles) AvailableBundles.Add(b);

            AvailableModels.Clear();
            foreach (var m in models) AvailableModels.Add(m);

            AvailableDbs.Clear();
            foreach (var d in dbs) AvailableDbs.Add(d);

            AvailableEmbeddings.Clear();
            foreach (var e in embeddings) AvailableEmbeddings.Add(e);
        }

        [RelayCommand]
        public void CreateNewSession()
        {
            var bestBundle = AvailableBundles.FirstOrDefault() ?? _assetDiscoveryService.DiscoverBundles().FirstOrDefault();

            var session = new ConversationSession
            {
                Title = $"새 대화 {Sessions.Count + 1}",
                AssetBundleName = bestBundle?.Name,
                ModelPath = bestBundle?.ModelPath ?? AvailableModels.FirstOrDefault() ?? string.Empty,
                DbPath = bestBundle?.DbPath ?? AvailableDbs.FirstOrDefault() ?? string.Empty,
                EmbeddingPath = bestBundle?.EmbeddingPath ?? AvailableEmbeddings.FirstOrDefault(),
                Strategy = "none",
                MaxTokens = 64
            };

            Sessions.Insert(0, session);
            SelectedSession = session;
            NotifyBundleProperties();
            _ = LoadSchemaAsync();
            _ = _sessionStorageService.SaveSessionsAsync(Sessions);
        }

        [RelayCommand]
        public void DeleteSession(ConversationSession? session)
        {
            if (session == null) return;

            int index = Sessions.IndexOf(session);
            Sessions.Remove(session);

            if (SelectedSession == session)
            {
                if (Sessions.Count > 0)
                {
                    SelectedSession = Sessions[Math.Max(0, index - 1)];
                }
                else
                {
                    CreateNewSession();
                }
            }
            _ = _sessionStorageService.SaveSessionsAsync(Sessions);
        }

        [RelayCommand]
        public void SelectSession(ConversationSession? session)
        {
            if (session == null) return;
            SelectedSession = session;
            NotifyBundleProperties();
            _ = LoadSchemaAsync();
        }

        public void AddCustomAssetFolder(string folderPath)
        {
            var bundle = _assetDiscoveryService.CreateBundleFromDirectory(folderPath);
            if (bundle == null)
            {
                StatusMessage = "선택한 폴더에 유효한 .gguf 및 .db 파일이 없습니다. (.gguf, .db 필수)";
                return;
            }

            var existing = AvailableBundles.FirstOrDefault(b =>
                string.Equals(b.DirectoryPath, bundle.DirectoryPath, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(b.Name, bundle.Name, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                CurrentSessionBundle = existing;
                StatusMessage = $"에셋 번들 '{existing.Name}'을(를) 선택했습니다.";
            }
            else
            {
                AvailableBundles.Insert(0, bundle);
                CurrentSessionBundle = bundle;
                StatusMessage = $"새 에셋 번들 '{bundle.Name}'을(를) 추가하고 적용했습니다.";
            }
        }

        [RelayCommand]
        public void StartEditTitle(ConversationSession? session)
        {
            if (session != null)
            {
                session.IsEditingTitle = true;
            }
        }

        [RelayCommand]
        public void FinishEditTitle(ConversationSession? session)
        {
            if (session != null)
            {
                session.IsEditingTitle = false;
                _ = _sessionStorageService.SaveSessionsAsync(Sessions);
            }
        }

        public void AddCustomModel(string path)
        {
            if (!string.IsNullOrWhiteSpace(path) && !AvailableModels.Contains(path))
            {
                AvailableModels.Insert(0, path);
            }
            if (SelectedSession != null)
            {
                SelectedSession.ModelPath = path;
                _ = _sessionStorageService.SaveSessionsAsync(Sessions);
            }
        }

        public void AddCustomDb(string path)
        {
            if (!string.IsNullOrWhiteSpace(path) && !AvailableDbs.Contains(path))
            {
                AvailableDbs.Insert(0, path);
            }
            if (SelectedSession != null)
            {
                SelectedSession.DbPath = path;
                _ = LoadSchemaAsync();
                _ = _sessionStorageService.SaveSessionsAsync(Sessions);
            }
        }

        public void AddCustomEmbedding(string path)
        {
            if (!string.IsNullOrWhiteSpace(path) && !AvailableEmbeddings.Contains(path))
            {
                AvailableEmbeddings.Insert(0, path);
            }
            if (SelectedSession != null)
            {
                SelectedSession.EmbeddingPath = path;
                _ = _sessionStorageService.SaveSessionsAsync(Sessions);
            }
        }

        [RelayCommand]
        public void SetMode(string modeStr)
        {
            if (SelectedSession == null) return;
            if (Enum.TryParse<AppMode>(modeStr, true, out var mode))
            {
                SelectedSession.CurrentMode = mode;
            }
        }

        [RelayCommand]
        public void SetStrategy(string strategy)
        {
            if (SelectedSession == null || string.IsNullOrWhiteSpace(strategy)) return;
            SelectedSession.Strategy = strategy;
        }

        [RelayCommand]
        public async Task LoadSchemaAsync()
        {
            if (SelectedSession == null || string.IsNullOrWhiteSpace(SelectedSession.DbPath))
            {
                CurrentSchema.Clear();
                return;
            }

            IsLoadingSchema = true;
            try
            {
                var tables = await _sqlExecutionService.GetDatabaseSchemaAsync(SelectedSession.DbPath);
                CurrentSchema.Clear();
                foreach (var t in tables)
                {
                    CurrentSchema.Add(t);
                }
            }
            finally
            {
                IsLoadingSchema = false;
            }
        }

        [RelayCommand]
        public void UsePromptSuggestion(string? suggestion)
        {
            if (!string.IsNullOrWhiteSpace(suggestion))
            {
                InputText = suggestion;
            }
        }

        [RelayCommand]
        public async Task SendMessageAsync()
        {
            if (SelectedSession == null || string.IsNullOrWhiteSpace(InputText) || IsGenerating)
            {
                return;
            }

            string question = InputText.Trim();
            InputText = string.Empty;

            // Auto-rename session title on first message
            if (SelectedSession.Messages.Count == 0 || SelectedSession.Title.StartsWith("새 대화", StringComparison.Ordinal))
            {
                SelectedSession.Title = question.Length > 20 ? question[..20] + "..." : question;
            }

            // User Message
            var userMsg = new ChatMessage
            {
                Sender = MessageSender.User,
                Text = question
            };
            SelectedSession.Messages.Add(userMsg);

            // Assistant placeholder message
            var assistantMsg = new ChatMessage
            {
                Sender = MessageSender.Assistant
            };
            SelectedSession.Messages.Add(assistantMsg);

            IsGenerating = true;
            StatusMessage = "SQL 생성 및 교정 처리 중...";

            try
            {
                var sw = Stopwatch.StartNew();

                if (SelectedSession.CurrentMode == AppMode.Query)
                {
                    var result = await _inferenceService.RunQueryAsync(
                        SelectedSession.ModelPath,
                        SelectedSession.DbPath,
                        SelectedSession.EmbeddingPath,
                        SelectedSession.Strategy,
                        SelectedSession.MaxTokens,
                        SelectedSession.Threshold,
                        SelectedSession.Dim,
                        question);

                    sw.Stop();
                    assistantMsg.Sql = result.Sql;
                    assistantMsg.RawSql = result.RawSql;
                    assistantMsg.StopReason = result.StopReason;
                    assistantMsg.InferenceTimeMs = sw.ElapsedMilliseconds;
                }
                else
                {
                    // Postprocess mode
                    string cleanSql = await _inferenceService.RunPostprocessAsync(
                        question,
                        SelectedSession.Strategy,
                        SelectedSession.DbPath,
                        SelectedSession.Dim,
                        SelectedSession.Threshold);

                    sw.Stop();
                    assistantMsg.Sql = cleanSql;
                    assistantMsg.RawSql = question;
                    assistantMsg.InferenceTimeMs = sw.ElapsedMilliseconds;
                }

                StatusMessage = $"완료 ({assistantMsg.InferenceTimeMs}ms)";
            }
            catch (Exception ex)
            {
                assistantMsg.ErrorMessage = $"오류 발생: {ex.Message}";
                StatusMessage = "추론 중 오류가 발생했습니다.";
            }
            finally
            {
                IsGenerating = false;
                _ = _sessionStorageService.SaveSessionsAsync(Sessions);
            }
        }

        [RelayCommand]
        public async Task RunSqlAsync(ChatMessage? message)
        {
            if (message == null || string.IsNullOrWhiteSpace(message.Sql) || SelectedSession == null)
            {
                return;
            }

            message.IsRunningQuery = true;
            try
            {
                var result = await _sqlExecutionService.ExecuteQueryAsync(SelectedSession.DbPath, message.Sql);
                ApplyQueryResult(message, result);
            }
            catch (Exception ex)
            {
                var errorResult = new QueryResultData
                {
                    ErrorMessage = $"실행 실패: {ex.Message}"
                };
                ApplyQueryResult(message, errorResult);
            }
            finally
            {
                message.IsRunningQuery = false;
                _ = _sessionStorageService.SaveSessionsAsync(Sessions);
            }
        }

        private void ApplyQueryResult(ChatMessage message, QueryResultData result)
        {
            if (SelectedSession == null) return;

            // 1. 이미 생성된 결과 말풍선이 있는지 확인 (ID 매칭 또는 바로 다음 메시지 확인)
            ChatMessage? existingResultMsg = null;
            if (message.ResultMessageId.HasValue)
            {
                existingResultMsg = SelectedSession.Messages.FirstOrDefault(m => m.Id == message.ResultMessageId.Value);
            }

            if (existingResultMsg == null)
            {
                int currentIdx = SelectedSession.Messages.IndexOf(message);
                if (currentIdx >= 0 && currentIdx + 1 < SelectedSession.Messages.Count &&
                    SelectedSession.Messages[currentIdx + 1].Sender == MessageSender.QueryResult)
                {
                    existingResultMsg = SelectedSession.Messages[currentIdx + 1];
                    message.ResultMessageId = existingResultMsg.Id;
                }
            }

            // 2. 이미 존재하면 기존 말풍선에 결과만 갱신
            if (existingResultMsg != null)
            {
                existingResultMsg.QueryResult = result;
                existingResultMsg.IsQueryResultExpanded = true;
            }
            else
            {
                // 3. 존재하지 않으면 새 말풍선 생성 후 해당 모델 메시지 바로 아래에 삽입
                var newResultMsg = new ChatMessage
                {
                    Sender = MessageSender.QueryResult,
                    Sql = message.Sql,
                    QueryResult = result,
                    IsQueryResultExpanded = true
                };
                message.ResultMessageId = newResultMsg.Id;

                int index = SelectedSession.Messages.IndexOf(message);
                if (index >= 0 && index + 1 <= SelectedSession.Messages.Count)
                {
                    SelectedSession.Messages.Insert(index + 1, newResultMsg);
                }
                else
                {
                    SelectedSession.Messages.Add(newResultMsg);
                }
            }
        }

        [RelayCommand]
        public void CopySql(string? sql)
        {
            if (string.IsNullOrWhiteSpace(sql)) return;

            try
            {
                var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
                dataPackage.SetText(sql);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
                StatusMessage = "SQL이 클립보드에 복사되었습니다.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"복사 실패: {ex.Message}";
            }
        }

        [RelayCommand]
        public async Task ImportJsonFileAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath) || SelectedSession == null)
            {
                return;
            }

            try
            {
                string json = await File.ReadAllTextAsync(filePath);
                using var doc = JsonDocument.Parse(json);

                var questions = new List<string>();

                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var elem in doc.RootElement.EnumerateArray())
                    {
                        if (elem.TryGetProperty("question", out var q) && !string.IsNullOrWhiteSpace(q.GetString()))
                        {
                            questions.Add(q.GetString()!);
                        }
                        else if (elem.TryGetProperty("sql", out var s) && !string.IsNullOrWhiteSpace(s.GetString()))
                        {
                            questions.Add(s.GetString()!);
                        }
                    }
                }
                else if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    if (doc.RootElement.TryGetProperty("question", out var q) && !string.IsNullOrWhiteSpace(q.GetString()))
                    {
                        questions.Add(q.GetString()!);
                    }
                    else if (doc.RootElement.TryGetProperty("sql", out var s) && !string.IsNullOrWhiteSpace(s.GetString()))
                    {
                        questions.Add(s.GetString()!);
                    }
                }

                if (questions.Count > 0)
                {
                    InputText = questions[0];
                    StatusMessage = $"JSON 파일에서 {questions.Count}개 항목 로드됨. 첫 번째 질문이 입력창에 배치되었습니다.";
                }
                else
                {
                    StatusMessage = "JSON 파일에서 question 또는 sql 키를 찾지 못했습니다.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"JSON 읽기 오류: {ex.Message}";
            }
        }
    }
}
