namespace WinAcmeGui.App.Localization;

public static partial class LocalizationTable
{
    private static LocalizedEntry[] Shell() =>
    [
        new("AppTitle", "win-acme GUI", "win-acme GUI"),
        new("AppSubtitle", "CENTRAL DE ADMINISTRAÇÃO", "ADMINISTRATION CENTER"),
        new("GuiSubtitle", "CENTRAL DE ADMINISTRAÇÃO", "ADMINISTRATION CENTER"),
        new("NavigationListName", "Navegação", "Navigation"),
        new("Status", "STATUS", "STATUS"),
        new("StatusIdle", "Pronto", "Idle"),
        new("StatusBusy", "Operação em andamento…", "Operation running…"),
        new("Language", "Idioma", "Language"),
        new("Theme", "Tema", "Theme"),
        new("ThemeLight", "Claro", "Light"),
        new("ThemeDark", "Escuro", "Dark"),
        new("ToggleTheme", "Alternar tema claro/escuro", "Toggle light/dark theme"),
        new("Download", "Baixar win-acme", "Download win-acme"),
        new("DownloadTooltip", "Baixa a versão oficial x64 mais recente e verifica o SHA-256.",
            "Downloads the latest official x64 release and verifies its SHA-256 digest."),
        new("Update", "Atualizar", "Refresh"),
        new("UpdateTooltip", "Redescobre instalações e recarrega o inventário (F5).",
            "Rediscovers installations and reloads the inventory (F5)."),
        new("CancelOperation", "Cancelar operação", "Cancel operation"),
        new("CancelOperationTooltip", "Cancela a operação em andamento (Esc).", "Cancels the running operation (Esc)."),
        new("SelectExecutable", "Selecionar wacs.exe", "Select wacs.exe"),
        new("SelectExecutableAction", "Selecionar wacs.exe", "Select wacs.exe"),
        new("SelectExecutableTooltip", "Escolhe manualmente o wacs.exe que a GUI deve administrar.",
            "Manually picks the wacs.exe this GUI should administer.")
    ];

    private static LocalizedEntry[] Navigation() =>
    [
        new("Home", "Início", "Home"),
        new("Renewals", "Renovações", "Renewals"),
        new("NewCertificate", "Novo certificado", "New certificate"),
        new("Installation", "Instalação", "Installation"),
        new("System", "Sistema", "System"),
        new("Settings", "Configurações", "Settings"),
        new("Logs", "Atividade", "Activity"),
        new("About", "Sobre", "About"),
        new("HomeDescription", "Resumo da instalação ativa e das renovações detectadas.",
            "Overview of the active installation and detected renewals."),
        new("RenewalsDescription", "Pesquise, revise e opere renovações sem misturar instalações.",
            "Search, review and operate renewals without mixing installations."),
        new("NewDescription", "Abra o assistente para revisar e emitir um certificado manual.",
            "Open the wizard to review and issue a manual certificate."),
        new("InstallationDescription", "Troque a instalação ativa e confira o caminho efetivo de configuração.",
            "Switch the active installation and inspect the effective configuration path."),
        new("SystemDescription", "Consulte o estado da sessão, do worker elevado e do endpoint ACME.",
            "Review session state, elevated worker and ACME endpoint status."),
        new("SettingsDescription", "Ajuste idioma e tema da GUI; alterações no win-acme continuam backup-first.",
            "Adjust GUI language and theme; win-acme changes remain backup-first."),
        new("LogsDescription", "Histórico das operações desta sessão, com saída redigida.",
            "History of this session's operations, with redacted output."),
        new("AboutDescription", "Administração portátil para win-acme, com operações tipadas e validação explícita.",
            "Portable administration for win-acme with typed operations and explicit validation.")
    ];
}
