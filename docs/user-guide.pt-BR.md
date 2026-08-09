# Guia rápido — win-acme GUI

1. Extraia o ZIP portátil para uma pasta gravável e mantenha a pasta `worker` ao lado de `WinAcmeGui.exe`.
2. Abra `WinAcmeGui.exe`. Na abertura, o programa procura `wacs.exe` em tarefas/processos, `PATH`, locais conhecidos e na pasta do próprio aplicativo.
3. Confira versão, endpoint, caminho efetivo de configuração e a lista de renovações. A descoberta é somente leitura; arquivos inválidos aparecem como linhas de diagnóstico e não são descartados.
4. Selecione `wacs.exe` para trocar a instalação ativa. Instalações, endpoints e diretórios de configuração diferentes nunca são misturados.
5. Use a busca por nome, ID, domínio ou status para localizar uma renovação. Renovar e Forçar executam a renovação selecionada; Forçar pede confirmação adicional. Cancelamento e revogação pedem o nome amigável; revogação é indicada somente para comprometimento de chave. Linhas ilegíveis, desconhecidas ou com configuração compartilhada permanecem somente leitura.
6. Use Novo certificado para revisar domínios, e-mail opcional, validação, chave, armazenamento e staging antes da execução. Para PEM/PFX, informe um caminho absoluto de saída. Marque explicitamente a aceitação dos termos Let's Encrypt; o assistente suporta a fonte manual e HTTP-01 ou TLS-ALPN-01, mas não configura plugins DNS.
7. Operações que alteram o sistema são enviadas ao worker elevado via UAC, uma operação por vez. O cancelamento encerra o processo e aguarda seu término. Se o UAC for recusado, o worker não for assinado/confiável ou estiver ausente, a operação falha sem executar um processo arbitrário.

Use staging para validar a integração. O download integrado aceita apenas o release x64 oficial com digest SHA-256, valida a assinatura Authenticode do `wacs.exe` e faz extração segura em uma pasta vazia. Um pacote de produção deve ser assinado pelo processo de publicação; `-AllowUnsigned` é apenas para CI/desenvolvimento. Senhas protegidas pelo win-acme aparecem apenas como configuradas; a GUI não as descriptografa.
