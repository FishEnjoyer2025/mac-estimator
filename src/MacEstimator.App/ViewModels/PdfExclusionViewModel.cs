using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MacEstimator.App.Models;
using MacEstimator.App.Services;
using Microsoft.Win32;

namespace MacEstimator.App.ViewModels;

public partial class PdfExclusionViewModel : ObservableObject
{
    private readonly PdfTextExtractor _extractor;
    private readonly KeywordScoringService _scorer;
    private readonly KeywordConfigService _configService;
    private KeywordConfig _config = new();

    [ObservableProperty]
    private string _loadedFileName = string.Empty;

    [ObservableProperty]
    private bool _hasResults;

    [ObservableProperty]
    private int _fitnessScore;

    [ObservableProperty]
    private string _fitnessScoreColor = "#a0a0a0";

    [ObservableProperty]
    private string _fitnessLabel = string.Empty;

    [ObservableProperty]
    private int _totalPages;

    [ObservableProperty]
    private string _extractedText = string.Empty;

    [ObservableProperty]
    private bool _showExtractedText;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private bool _isAnalyzing;

    [ObservableProperty]
    private string _newGoodKeyword = string.Empty;

    [ObservableProperty]
    private string _newBadKeyword = string.Empty;

    public ObservableCollection<string> GoodKeywords { get; } = [];
    public ObservableCollection<string> BadKeywords { get; } = [];
    public ObservableCollection<FoundKeyword> FoundGoodKeywords { get; } = [];
    public ObservableCollection<FoundKeyword> FoundBadKeywords { get; } = [];
    public ObservableCollection<string> MissingGoodKeywords { get; } = [];

    private string? _currentPdfPath;

    public PdfExclusionViewModel(PdfTextExtractor extractor, KeywordScoringService scorer, KeywordConfigService configService)
    {
        _extractor = extractor;
        _scorer = scorer;
        _configService = configService;
    }

    [RelayCommand]
    private async Task Initialize()
    {
        _config = await _configService.LoadAsync();
        SyncKeywordLists();
    }

    [RelayCommand]
    private async Task BrowsePdf()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "PDF Files (*.pdf)|*.pdf",
            Title = "Select Architectural PDF"
        };

        if (dialog.ShowDialog() == true)
            await AnalyzePdf(dialog.FileName);
    }

    public async Task HandleFileDrop(string filePath)
    {
        if (Path.GetExtension(filePath).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            await AnalyzePdf(filePath);
        else
            StatusText = "Please drop a PDF file";
    }

    private async Task AnalyzePdf(string filePath)
    {
        _currentPdfPath = filePath;
        LoadedFileName = Path.GetFileName(filePath);
        IsAnalyzing = true;
        StatusText = "Analyzing PDF...";

        try
        {
            // Always reload keywords from Excel before scanning
            _config = await _configService.LoadAsync();
            SyncKeywordLists();

            var pageTexts = await Task.Run(() => _extractor.ExtractText(filePath));
            var totalPages = await Task.Run(() => _extractor.GetPageCount(filePath));

            if (pageTexts.Count == 0)
            {
                StatusText = "No extractable text found -- this may be a scanned/raster PDF";
                HasResults = false;
                IsAnalyzing = false;
                return;
            }

            var result = _scorer.Analyze(pageTexts, _config);
            result.TotalPages = totalPages;

            // Update UI
            FitnessScore = result.FitnessScore;
            FitnessLabel = result.FitnessScore switch
            {
                >= 70 => "Strong Fit",
                >= 40 => "Review Needed",
                _ => "Weak Fit"
            };
            FitnessScoreColor = result.FitnessScore switch
            {
                >= 70 => "#13a10e",  // green
                >= 40 => "#ffc83d",  // yellow
                _ => "#d13438"       // red
            };
            TotalPages = result.TotalPages;
            ExtractedText = result.ExtractedText;

            FoundGoodKeywords.Clear();
            foreach (var k in result.FoundGood)
                FoundGoodKeywords.Add(k);

            FoundBadKeywords.Clear();
            foreach (var k in result.FoundBad)
                FoundBadKeywords.Add(k);

            MissingGoodKeywords.Clear();
            foreach (var k in result.MissingGood)
                MissingGoodKeywords.Add(k);

            HasResults = true;
            StatusText = $"Analyzed {totalPages} pages -- {pageTexts.Count} had extractable text";
        }
        catch (Exception ex)
        {
            StatusText = $"Error reading PDF: {ex.Message}";
            HasResults = false;
        }
        finally
        {
            IsAnalyzing = false;
        }
    }

    [RelayCommand]
    private async Task AddGoodKeyword()
    {
        var keyword = NewGoodKeyword?.Trim();
        if (string.IsNullOrEmpty(keyword))
            return;

        if (_config.GoodKeywords.Any(k => k.Keyword.Equals(keyword, StringComparison.OrdinalIgnoreCase)))
            return;

        _config.GoodKeywords.Add(new KeywordEntry { Keyword = keyword });
        NewGoodKeyword = string.Empty;
        SyncKeywordLists();
        await SaveAndRescan();
    }

    [RelayCommand]
    private async Task AddBadKeyword()
    {
        var keyword = NewBadKeyword?.Trim();
        if (string.IsNullOrEmpty(keyword))
            return;

        if (_config.BadKeywords.Any(k => k.Keyword.Equals(keyword, StringComparison.OrdinalIgnoreCase)))
            return;

        _config.BadKeywords.Add(new KeywordEntry { Keyword = keyword });
        NewBadKeyword = string.Empty;
        SyncKeywordLists();
        await SaveAndRescan();
    }

    [RelayCommand]
    private async Task RemoveGoodKeyword(string keyword)
    {
        var entry = _config.GoodKeywords.FirstOrDefault(k => k.Keyword == keyword);
        if (entry is not null)
        {
            _config.GoodKeywords.Remove(entry);
            SyncKeywordLists();
            await SaveAndRescan();
        }
    }

    [RelayCommand]
    private async Task RemoveBadKeyword(string keyword)
    {
        var entry = _config.BadKeywords.FirstOrDefault(k => k.Keyword == keyword);
        if (entry is not null)
        {
            _config.BadKeywords.Remove(entry);
            SyncKeywordLists();
            await SaveAndRescan();
        }
    }

    [RelayCommand]
    private void ToggleExtractedText()
    {
        ShowExtractedText = !ShowExtractedText;
    }

    private void SyncKeywordLists()
    {
        GoodKeywords.Clear();
        foreach (var k in _config.GoodKeywords.OrderBy(k => k.Keyword))
            GoodKeywords.Add(k.Keyword);

        BadKeywords.Clear();
        foreach (var k in _config.BadKeywords.OrderBy(k => k.Keyword))
            BadKeywords.Add(k.Keyword);
    }

    private async Task SaveAndRescan()
    {
        try
        {
            await _configService.SaveAsync(_config);
        }
        catch
        {
            // Shared drive might be unavailable -- keywords still work locally
        }

        // Re-analyze if a PDF is loaded
        if (_currentPdfPath is not null && File.Exists(_currentPdfPath))
            await AnalyzePdf(_currentPdfPath);
    }
}
