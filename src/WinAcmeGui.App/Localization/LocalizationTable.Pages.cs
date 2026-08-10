namespace WinAcmeGui.App.Localization;

public static partial class LocalizationTable
{
    private static LocalizedEntry[] InstallationPage() =>
    [
        new("DiscoveredInstallations", "Instalações descobertas", "Discovered installations"),
        new("DiscoveredInstallationsHint",
            "Somente executáveis assinados e válidos aparecem aqui. Instalações que compartilham o mesmo diretório de configuração ficam somente leitura.",
            "Only signed, valid executables appear here. Installations sharing one configuration directory stay read-only."),
        new("ExecutableColumn", "Executável", "Executable"),
        new("VersionColumn", "Versão", "Version"),
        new("OperationalColumn", "Operacional", "Operational"),
        new("UseInstallation", "Usar esta instalação", "Use this installation"),
        new("NoInstallationsTitle", "Nenhuma instalação descoberta", "No installation discovered"),
        new("InstallationDiagnostic", "Observação", "Note"),
        new("ActiveBadge", "ATIVA", "ACTIVE"),
        new("ReadOnlyBadge", "SOMENTE LEITURA", "READ-ONLY"),
        new("DownloadDestination", "Destino do download", "Download destination"),
        new("DownloadCompleted", "Download concluído", "Download completed"),
        new("DownloadCompletedMessage", "win-acme {0} foi extraído em:\n{1}\n\nUse Atualizar para detectá-lo.",
            "win-acme {0} was extracted to:\n{1}\n\nUse Refresh to detect it."),
        new("DownloadFailed", "Falha no download do win-acme", "win-acme download failed"),
        new("InvalidExecutable", "O arquivo selecionado não é um wacs.exe válido e confiável.",
            "The selected file is not a valid, trusted wacs.exe.")
    ];

    private static LocalizedEntry[] SystemPage() =>
    [
        new("SessionState", "Estado da sessão", "Session state"),
        new("ElevationMode", "Execução de comandos", "Command execution"),
        new("ElevationWorker", "Worker elevado por named pipe autenticado", "Elevated worker over authenticated named pipe"),
        new("ElevationDirect", "Processo direto (host não-Windows)", "Direct process (non-Windows host)"),
        new("HostPlatform", "Plataforma", "Platform"),
        new("GuiVersion", "Versão da GUI", "GUI version"),
        new("EndpointKind", "Tipo de endpoint", "Endpoint kind"),
        new("EndpointProduction", "Produção", "Production"),
        new("EndpointStaging", "Testes (staging)", "Staging"),
        new("EndpointUnknown", "Indeterminado", "Undetermined"),
        new("SettingsFile", "Arquivo settings.json", "settings.json file"),
        new("CopyDetails", "Copiar detalhes", "Copy details"),
        new("CopiedToClipboard", "Detalhes copiados para a área de transferência.", "Details copied to the clipboard."),
        new("NotAvailableShort", "—", "—")
    ];

    private static LocalizedEntry[] LogsPage() =>
    [
        new("ActivityLog", "Registro de atividade", "Activity log"),
        new("ActivityLogHint",
            "Somente esta sessão. Segredos são redigidos; os logs originais do win-acme permanecem intactos.",
            "This session only. Secrets are redacted; original win-acme logs stay untouched."),
        new("TimeColumn", "Hora", "Time"),
        new("OperationColumn", "Operação", "Operation"),
        new("ResultColumn", "Resultado", "Result"),
        new("DetailColumn", "Detalhe", "Detail"),
        new("NoActivityTitle", "Nenhuma operação registrada", "No operation recorded"),
        new("NoActivityHint", "Descobertas, renovações e emissões desta sessão aparecem aqui.",
            "Discoveries, renewals and issuances from this session appear here."),
        new("ClearActivity", "Limpar registro", "Clear log"),
        new("CopyActivity", "Copiar registro", "Copy log"),
        new("ResultSucceeded", "Sucesso", "Succeeded"),
        new("ResultFailed", "Falha", "Failed"),
        new("ResultCancelled", "Cancelada", "Cancelled"),
        new("ResultInfo", "Informação", "Information")
    ];

    private static LocalizedEntry[] SettingsAndAbout() =>
    [
        new("Appearance", "Aparência", "Appearance"),
        new("AppearanceHint", "Preferências da GUI; nada é gravado no win-acme.",
            "GUI preferences; nothing is written to win-acme."),
        new("WinAcmeSettings", "Configurações do win-acme", "win-acme settings"),
        new("WinAcmeSettingsHint",
            "A edição de settings.json ainda não é exposta. A infraestrutura backup-first existe, mas a tela de edição/restauração falta validação em Windows.",
            "Editing settings.json is not exposed yet. The backup-first infrastructure exists, but the editor/restore screen still lacks Windows validation."),
        new("Capabilities", "Escopo atual", "Current scope"),
        new("NotExposedYet", "Não exposto nesta versão", "Not exposed in this version"),
        new("AboutProject", "Sobre este projeto", "About this project"),
        new("AboutBody",
            "GUI portátil de administração para o win-acme. Ela nunca edita *.renewal.json diretamente nem descriptografa segredos do win-acme; renovações desconhecidas ou malformadas permanecem visíveis e somente leitura.",
            "Portable administration GUI for win-acme. It never edits *.renewal.json directly nor decrypts win-acme secrets; unknown or malformed renewals stay visible and read-only."),
        new("AboutUpstream", "win-acme é um projeto independente de terceiros.", "win-acme is an independent third-party project."),
        new("OpenWinAcmeSite", "Abrir win-acme.com", "Open win-acme.com"),
        new("OpenDocumentation", "Abrir documentação local", "Open local documentation"),
        new("OpenCertificate", "Abrir assistente de certificado", "Open certificate wizard"),
        new("ChooseInstallation", "Selecionar wacs.exe", "Select wacs.exe")
    ];

    private static LocalizedEntry[] Common() =>
    [
        new("Confirm", "Confirmar", "Confirm"),
        new("Close", "Fechar", "Close"),
        new("Dismiss", "Dispensar", "Dismiss"),
        new("Yes", "Sim", "Yes"),
        new("No", "Não", "No"),
        new("Ok", "OK", "OK"),
        new("Refresh", "Atualizar", "Refresh"),
        new("Warning", "Atenção", "Warning"),
        new("Error", "Erro", "Error"),
        new("Information", "Informação", "Information")
    ];

    private static LocalizedEntry[] CertificateWizard() =>
    [
        new("CertificateTitle", "Emitir certificado", "Issue certificate"),
        new("CertificateInstructions",
            "Preencha as opções; a linha de comando será revisada antes da execução.",
            "Fill the options; the command line will be reviewed before execution."),
        new("DomainsLabel", "Domínios (separados por vírgula)", "Domains (comma separated)"),
        new("DomainsPlaceholder", "exemplo.com, www.exemplo.com", "example.com, www.example.com"),
        new("EmailAddress", "E-mail da conta (opcional)", "Account email (optional)"),
        new("EmailPlaceholder", "admin@exemplo.com", "admin@example.com"),
        new("Validation", "Validação", "Validation"),
        new("PrivateKey", "Chave privada", "Private key"),
        new("Storage", "Armazenamento", "Storage"),
        new("StoragePath", "Caminho de saída (PEM/PFX)", "Output path (PEM/PFX)"),
        new("StoragePathPlaceholder", @"C:\certificados", @"C:\certificates"),
        new("AcceptTerms", "Aceito os termos de serviço da Let's Encrypt para esta operação",
            "I accept the Let's Encrypt terms of service for this operation"),
        new("UseStaging", "Usar servidor de testes (staging)", "Use test server (staging)"),
        new("UseStagingHint", "Recomendado na primeira execução: não consome limites de emissão.",
            "Recommended for the first run: it does not consume issuance rate limits."),
        new("CommandPreview", "Linha de comando", "Command line"),
        new("PreviewPlaceholder", "A prévia aparecerá aqui.", "The preview will appear here."),
        new("PreviewAction", "Pré-visualizar", "Preview"),
        new("Execute", "Executar", "Execute"),
        new("ConfirmCreate", "Executar esta operação no win-acme?", "Run this operation in win-acme?"),
        new("ConfirmCreateTitle", "Confirmar emissão", "Confirm issuance"),
        new("ValidationErrors", "Corrija os itens abaixo", "Fix the items below"),
        new("OperationOutput", "Saída da operação", "Operation output")
    ];
}
