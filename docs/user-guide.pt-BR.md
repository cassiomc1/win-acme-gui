# Guia rápido — win-acme GUI

1. Extraia o ZIP portátil para uma pasta gravável.
2. Abra `WinAcmeGui.exe`. Na abertura, o programa procura `wacs.exe` em tarefas/processos, `PATH`, locais conhecidos e na pasta do próprio aplicativo.
3. Confira versão, endpoint, caminho de configuração e a lista de renovações. A descoberta não grava nos arquivos existentes.
4. Selecione `wacs.exe` para trocar a instalação ativa. Instalações e endpoints diferentes nunca são misturados.
5. Selecione uma renovação e use Renovar, Forçar, Cancelar ou Revogar. Cancelamento e revogação pedem o nome amigável; revogação é indicada para comprometimento de chave.
6. Use Novo certificado para revisar domínios, validação, chave, armazenamento e staging antes da execução.

Use staging para validar a integração. Senhas protegidas pelo win-acme aparecem apenas como configuradas; a GUI não as descriptografa.
