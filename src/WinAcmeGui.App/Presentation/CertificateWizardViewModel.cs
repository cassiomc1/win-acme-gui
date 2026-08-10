using System.Collections.ObjectModel;
using WinAcmeGui.App.Localization;
using WinAcmeGui.Application.Certificates;
using WinAcmeGui.Application.Operations;
using WinAcmeGui.Domain.Operations;

namespace WinAcmeGui.App.Presentation;

/// <summary>A validation or storage choice offered by the wizard; <see cref="Value"/> is the win-acme token.</summary>
public sealed class WizardOption(string value, string label)
{
    public string Value { get; } = value;
    public string Label { get; } = label;
}

/// <summary>
/// Drives the certificate wizard: draft assembly, validation feedback, command preview and execution.
/// Kept free of WPF so the preview/validation rules are covered by tests.
/// </summary>
public sealed class CertificateWizardViewModel : ObservableObject
{
    private readonly string _executablePath;
    private readonly Func<CertificateDraft, bool, IProgress<string>?, CancellationToken, Task<OperationResult>> _execute;
    private readonly IShellInteraction _interaction;
    private readonly CertificateDraftValidator _validator = new();
    private readonly WinAcmeCommandFactory _commandFactory = new();

    private string _domains = string.Empty;
    private string _emailAddress = string.Empty;
    private WizardOption _validation;
    private WizardOption _keyType;
    private WizardOption _store;
    private string _storagePath = string.Empty;
    private bool _acceptTerms;
    private bool _useStaging = true;
    private string _preview = string.Empty;
    private bool _isRunning;
    private CancellationTokenSource? _cancellation;

    public CertificateWizardViewModel(
        string executablePath,
        Func<CertificateDraft, bool, IProgress<string>?, CancellationToken, Task<OperationResult>> execute,
        CultureService culture,
        IShellInteraction? interaction = null)
    {
        _executablePath = executablePath;
        _execute = execute;
        Culture = culture;
        _interaction = interaction ?? new NullShellInteraction();

        ValidationOptions = [new("http-01", "HTTP-01"), new("tls-alpn-01", "TLS-ALPN-01")];
        KeyTypeOptions = [new("rsa", "RSA"), new("ec", "EC (ECDSA)")];
        StoreOptions =
        [
            new("certificatestore", "Windows Certificate Store"),
            new("pemfiles", "PEM files"),
            new("pfxfile", "PFX file")
        ];
        _validation = ValidationOptions[0];
        _keyType = KeyTypeOptions[0];
        _store = StoreOptions[0];
        _preview = culture["PreviewPlaceholder"];

        PreviewCommand = new RelayCommand(() => UpdatePreview(), () => !IsRunning);
        ExecuteCommand = new AsyncRelayCommand(RunAsync, () => !IsRunning);
        CancelCommand = new RelayCommand(() => _cancellation?.Cancel(), () => IsRunning);
        culture.CultureChanged += (_, _) => Raise(nameof(StoragePathVisible));
    }

    public CultureService Culture { get; }

    public ObservableCollection<WizardOption> ValidationOptions { get; }
    public ObservableCollection<WizardOption> KeyTypeOptions { get; }
    public ObservableCollection<WizardOption> StoreOptions { get; }
    public ObservableCollection<string> ValidationErrors { get; } = [];
    public ObservableCollection<string> Output { get; } = [];

    public RelayCommand PreviewCommand { get; }
    public AsyncRelayCommand ExecuteCommand { get; }
    public RelayCommand CancelCommand { get; }

    /// <summary>Set to true once win-acme reported success, so the host window can close itself.</summary>
    public bool Completed { get; private set; }

    /// <summary>The last attempted operation, or null when the wizard was dismissed before execution.</summary>
    public OperationResult? LastResult { get; private set; }

    public string Domains { get => _domains; set { if (SetField(ref _domains, value)) InvalidatePreview(); } }
    public string EmailAddress { get => _emailAddress; set { if (SetField(ref _emailAddress, value)) InvalidatePreview(); } }

    public WizardOption Validation
    {
        get => _validation;
        set { if (value is not null && SetField(ref _validation, value)) InvalidatePreview(); }
    }

    public WizardOption KeyType
    {
        get => _keyType;
        set { if (value is not null && SetField(ref _keyType, value)) InvalidatePreview(); }
    }

    public WizardOption Store
    {
        get => _store;
        set
        {
            if (value is null || !SetField(ref _store, value)) return;
            if (!StoragePathVisible) StoragePath = string.Empty;
            Raise(nameof(StoragePathVisible));
            InvalidatePreview();
        }
    }

    /// <summary>PEM and PFX output need an explicit absolute path; the certificate store does not.</summary>
    public bool StoragePathVisible => Store.Value is "pemfiles" or "pfxfile";

    public string StoragePath { get => _storagePath; set { if (SetField(ref _storagePath, value)) InvalidatePreview(); } }
    public bool AcceptTerms { get => _acceptTerms; set { if (SetField(ref _acceptTerms, value)) InvalidatePreview(); } }
    public bool UseStaging { get => _useStaging; set { if (SetField(ref _useStaging, value)) InvalidatePreview(); } }

    public string Preview { get => _preview; private set => SetField(ref _preview, value); }

    public bool HasValidationErrors => ValidationErrors.Count > 0;
    public bool HasOutput => Output.Count > 0;

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (!SetField(ref _isRunning, value)) return;
            PreviewCommand.RaiseCanExecuteChanged();
            ExecuteCommand.RaiseCanExecuteChanged();
            CancelCommand.RaiseCanExecuteChanged();
        }
    }

    public CertificateDraft Draft => new(
        "manual",
        Domains.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
        Validation.Value,
        KeyType.Value,
        Store.Value,
        EmailAddress.Trim(),
        AcceptTerms,
        StoragePath.Trim());

    /// <summary>Validates the draft and renders the exact command line that would run.</summary>
    public bool UpdatePreview()
    {
        ValidationErrors.Clear();
        var errors = _validator.Validate(Draft);
        foreach (var error in errors) ValidationErrors.Add(error.Message);
        Raise(nameof(HasValidationErrors));
        if (errors.Count > 0)
        {
            Preview = Culture["PreviewPlaceholder"];
            return false;
        }
        Preview = _commandFactory.CreateCertificate(_executablePath, Draft, UseStaging).DisplayText;
        return true;
    }

    public async Task RunAsync()
    {
        if (IsRunning || !UpdatePreview()) return;
        if (!_interaction.Confirm(Culture["ConfirmCreateTitle"], $"{Culture["ConfirmCreate"]}\n\n{Preview}")) return;

        Output.Clear();
        Raise(nameof(HasOutput));
        LastResult = null;
        Raise(nameof(LastResult));
        IsRunning = true;
        _cancellation = new CancellationTokenSource();
        try
        {
            var progress = new Progress<string>(line =>
            {
                Output.Add(line);
                Raise(nameof(HasOutput));
            });
            var result = await _execute(Draft, UseStaging, progress, _cancellation.Token);
            LastResult = result;
            Raise(nameof(LastResult));
            foreach (var line in result.Output) Output.Add(line);
            if (result.ErrorCode is not null) Output.Add(result.ErrorCode);
            Raise(nameof(HasOutput));
            Completed = result.Status == OperationStatus.Succeeded;
            if (Completed) CompletedSuccessfully?.Invoke(this, result);
        }
        catch (OperationCanceledException)
        {
            LastResult = new(OperationStatus.Cancelled, null, TimeSpan.Zero, [], "operation.cancelled");
            Raise(nameof(LastResult));
            Output.Add(Culture["ResultCancelled"]);
            Raise(nameof(HasOutput));
        }
        catch (Exception ex)
        {
            LastResult = new(OperationStatus.Failed, null, TimeSpan.Zero, [], "certificate.exception");
            Raise(nameof(LastResult));
            Output.Add(ex.Message);
            Raise(nameof(HasOutput));
        }
        finally
        {
            _cancellation.Dispose();
            _cancellation = null;
            IsRunning = false;
        }
    }

    /// <summary>Raised only on success, so the wizard window can close and the shell can reload.</summary>
    public event EventHandler<OperationResult>? CompletedSuccessfully;

    public void CancelRunningOperation() => _cancellation?.Cancel();

    private void InvalidatePreview()
    {
        if (ValidationErrors.Count == 0) return;
        ValidationErrors.Clear();
        Raise(nameof(HasValidationErrors));
    }
}
