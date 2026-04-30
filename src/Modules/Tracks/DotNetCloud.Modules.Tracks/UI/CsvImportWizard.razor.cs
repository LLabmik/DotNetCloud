using DotNetCloud.Modules.Tracks.Models;
using DotNetCloud.Modules.Tracks.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace DotNetCloud.Modules.Tracks.UI;

/// <summary>
/// Code-behind for the 5-step CSV import wizard.
/// </summary>
public class CsvImportWizardBase : ComponentBase, IDisposable
{
    [Inject] protected ICsvImportUiService CsvImportService { get; set; } = null!;
    [Inject] protected IJSRuntime JsRuntime { get; set; } = null!;

    [Parameter] public Guid ProductId { get; set; }
    [Parameter] public Guid SwimlaneId { get; set; }
    [Parameter] public EventCallback OnImportComplete { get; set; }
    [Parameter] public EventCallback OnCancel { get; set; }

    protected bool _isVisible = true;
    protected int _currentStep;
    protected bool _isDragOver;

    protected readonly string[] _steps = ["Upload", "Preview", "Map", "Validate", "Import"];

    protected ElementReference _fileInput;
    protected InputFile? _fileInputComponent;
    protected IBrowserFile? _selectedFile;
    protected Stream? _fileStream;

    protected CsvParseResult? _parseResult;
    protected CsvValidationResult? _validationResult;
    protected CsvImportResult? _importResult;

    protected List<FieldMapping> _fieldMappings = [];

    /// <summary>
    /// Describes a mappable CSV field in the column mapping step.
    /// </summary>
    public sealed class FieldMapping
    {
        public string Label { get; set; } = string.Empty;
        public int SelectedColumn { get; set; } = -1;
    }

    protected override void OnInitialized()
    {
        InitializeFieldMappings();
    }

    private void InitializeFieldMappings()
    {
        _fieldMappings =
        [
            new() { Label = "Title *", SelectedColumn = -1 },
            new() { Label = "Description", SelectedColumn = -1 },
            new() { Label = "Priority", SelectedColumn = -1 },
            new() { Label = "Type", SelectedColumn = -1 },
            new() { Label = "Story Points", SelectedColumn = -1 },
            new() { Label = "Assignee (Email)", SelectedColumn = -1 },
            new() { Label = "Due Date", SelectedColumn = -1 },
            new() { Label = "Labels", SelectedColumn = -1 },
        ];
    }

    protected bool CanGoNext =>
        _currentStep switch
        {
            0 => _selectedFile is not null && _parseResult is not null,
            1 => _parseResult is not null,
            2 => _fieldMappings.Any(m => m.Label == "Title *" && m.SelectedColumn >= 0),
            3 => _validationResult is not null,
            4 => _importResult is not null,
            _ => false
        };

    protected async Task OpenFilePicker()
    {
        await JsRuntime.InvokeVoidAsync("eval",
            "document.querySelector('.csv-upload-zone input[type=file]')?.click()");
    }

    protected async Task HandleFileSelected(InputFileChangeEventArgs e)
    {
        _selectedFile = e.File;

        if (_selectedFile is null) return;

        _fileStream = _selectedFile.OpenReadStream(maxAllowedSize: 10 * 1024 * 1024); // 10MB max

        try
        {
            _parseResult = await CsvImportService.ParseCsvAsync(_fileStream, CancellationToken.None);

            // Auto-map columns by header name
            for (int i = 0; i < _parseResult.Headers.Count; i++)
            {
                var header = _parseResult.Headers[i].ToLowerInvariant();
                foreach (var mapping in _fieldMappings)
                {
                    var label = mapping.Label.ToLowerInvariant().Replace(" *", "").Replace(" (email)", "");
                    if ((header.Contains(label) || label.Contains(header)) && mapping.SelectedColumn == -1)
                    {
                        mapping.SelectedColumn = i;
                        break;
                    }
                }
            }

            _currentStep = 1;
        }
        catch (Exception ex)
        {
            // TODO: Show error toast
            Console.WriteLine($"CSV parse error: {ex.Message}");
        }
    }

    protected async Task HandleDragOver()
    {
        _isDragOver = true;
        await Task.CompletedTask;
    }

    protected async Task HandleDrop(DragEventArgs e)
    {
        _isDragOver = false;
        // Drag & drop file handling via JS interop
        await Task.CompletedTask;
    }

    protected async Task GoNext()
    {
        if (!CanGoNext) return;

        if (_currentStep == 2)
        {
            // Validate before proceeding
            await RunValidationAsync();
            _currentStep = 3;
        }
        else if (_currentStep == 3)
        {
            // Start import
            _currentStep = 4;
            await RunImportAsync();
        }
        else
        {
            _currentStep++;
        }
    }

    protected void GoBack()
    {
        if (_currentStep > 0)
            _currentStep--;
    }

    private async Task RunValidationAsync()
    {
        if (_selectedFile is null || _parseResult is null) return;

        var mapping = BuildColumnMapping();

        _fileStream?.Dispose();
        _fileStream = _selectedFile.OpenReadStream(maxAllowedSize: 10 * 1024 * 1024);

        try
        {
            _validationResult = await CsvImportService.ValidateCsvAsync(
                ProductId, _fileStream, mapping, CancellationToken.None);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"CSV validation error: {ex.Message}");
        }
    }

    private async Task RunImportAsync()
    {
        if (_selectedFile is null || _parseResult is null) return;

        var mapping = BuildColumnMapping();

        _fileStream?.Dispose();
        _fileStream = _selectedFile.OpenReadStream(maxAllowedSize: 10 * 1024 * 1024);

        try
        {
            _importResult = await CsvImportService.ImportCsvAsync(
                ProductId, SwimlaneId, _fileStream, mapping, skipDuplicates: false, CancellationToken.None);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"CSV import error: {ex.Message}");
            _importResult = new CsvImportResult
            {
                Created = 0,
                Failed = 1,
                Errors = [ex.Message]
            };
        }
    }

    private CsvColumnMapping BuildColumnMapping()
    {
        var mapping = new CsvColumnMapping();

        foreach (var fm in _fieldMappings)
        {
            var col = fm.SelectedColumn;
            switch (fm.Label)
            {
                case "Title *": mapping.TitleColumn = col; break;
                case "Description": mapping.DescriptionColumn = col; break;
                case "Priority": mapping.PriorityColumn = col; break;
                case "Type": mapping.TypeColumn = col; break;
                case "Story Points": mapping.StoryPointsColumn = col; break;
                case "Assignee (Email)": mapping.AssigneeEmailColumn = col; break;
                case "Due Date": mapping.DueDateColumn = col; break;
                case "Labels": mapping.LabelsColumn = col; break;
            }
        }

        return mapping;
    }

    protected async Task Cancel()
    {
        _isVisible = false;
        await OnCancel.InvokeAsync();
    }

    protected async Task Finish()
    {
        _isVisible = false;
        await OnImportComplete.InvokeAsync();
    }

    protected static string FormatFileSize(long bytes)
    {
        return bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
            _ => $"{bytes / (1024.0 * 1024.0):F1} MB"
        };
    }

    protected static string GetDelimiterName(char delimiter)
    {
        return delimiter switch
        {
            ',' => "Comma",
            '\t' => "Tab",
            ';' => "Semicolon",
            '|' => "Pipe",
            _ => delimiter.ToString()
        };
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _fileStream?.Dispose();
    }
}
