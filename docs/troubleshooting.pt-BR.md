# Solução de problemas

- **Nenhuma instalação encontrada:** use Selecionar `wacs.exe`, confira permissões e execute `wacs.exe --version` no PowerShell.
- **Renovação ilegível:** preserve o arquivo original, abra o caminho mostrado e consulte o log. Formatos desconhecidos ficam somente leitura.
- **UAC recusado:** repita a operação como administrador ou corrija as permissões da tarefa/loja; a GUI não eleva automaticamente a sessão inteira.
- **Comando falhou:** consulte a saída mascarada e o log original do win-acme. O código de saída é preservado.
- **Download bloqueado:** use uma pasta vazia e confira a origem/integridade exibidas; selecione manualmente uma versão oficial se necessário.
