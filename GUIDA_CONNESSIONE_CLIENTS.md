# Guida test MCP Data Service da OpenCode e Claude Code

Questa guida descrive come collegare e testare `MCP Data Service` come server MCP remoto in:
- OpenCode
- Claude Code

## Prerequisiti
- Servizio avviato localmente:
  - endpoint MCP: `http://localhost:5244/mcp` (consigliato in locale)
  - endpoint MCP HTTPS: `https://localhost:7129/mcp`
- Almeno una chiave API applicativa generata in `/Config` (sezione **Chiavi API applicative**).
- Le API richiedono entrambi gli header:
  - `X-MCP-Application`
  - `X-MCP-Key`

## 1) Avvio del servizio
Dalla root del repository:

```bash
dotnet run --project McpDataService/McpDataService.csproj
```

## 2) Creazione chiave API applicativa
1. Apri `https://localhost:7129/Login`.
2. Accedi con un utente admin configurato.
3. Vai in `https://localhost:7129/Config`.
4. Nella sezione **Chiavi API applicative**, crea una nuova chiave (es. `opencode-local`).
5. Copia la chiave generata.

Nota: `ApplicationName` e `ApiKey` devono combaciare con gli header inviati dal client MCP.

## 3) Smoke test MCP (facoltativo ma consigliato)
Esempio PowerShell:

```powershell
$headers = @{
  "X-MCP-Application" = "opencode-local"
  "X-MCP-Key" = "<INSERISCI_API_KEY>"
}

$body = @{
  jsonrpc = "2.0"
  id      = 1
  method  = "db_get_tables"
  params  = @{ db_type = "SQLite" }
} | ConvertTo-Json -Depth 5

Invoke-RestMethod `
  -Method Post `
  -Uri "http://localhost:5244/mcp" `
  -Headers $headers `
  -ContentType "application/json" `
  -Body $body
```

Se risponde con `result.tables`, il server è pronto.

## 4) Configurazione OpenCode
Inserisci o aggiorna `opencode.json` nella root del progetto:

```json
{
  "$schema": "https://opencode.ai/config.json",
  "mcp": {
    "mcp-data-service": {
      "type": "remote",
      "url": "http://localhost:5244/mcp",
      "enabled": true,
      "oauth": false,
      "headers": {
        "X-MCP-Application": "{env:MCP_DATA_SERVICE_APP}",
        "X-MCP-Key": "{env:MCP_DATA_SERVICE_KEY}"
      }
    }
  }
}
```

Imposta variabili ambiente (PowerShell):

```powershell
$env:MCP_DATA_SERVICE_APP = "opencode-local"
$env:MCP_DATA_SERVICE_KEY = "<INSERISCI_API_KEY>"
```

Verifica:

```bash
opencode mcp list
```

Prompt di test suggerito in OpenCode:
- `Usa mcp-data-service per elencare le tabelle nel db SQLite.`

## 5) Configurazione Claude Code
Metodo consigliato (CLI):

```bash
claude mcp add --transport http --scope project --header "X-MCP-Application: claude-code-local" --header "X-MCP-Key: <INSERISCI_API_KEY>" mcp-data-service http://localhost:5244/mcp
```

Verifica:

```bash
claude mcp list
```

Dentro Claude Code:
- usa `/mcp` per vedere stato server e tool disponibili.

In alternativa, puoi configurare `.mcp.json` nella root progetto:

```json
{
  "mcpServers": {
    "mcp-data-service": {
      "type": "http",
      "url": "http://localhost:5244/mcp",
      "headers": {
        "X-MCP-Application": "${MCP_DATA_SERVICE_APP}",
        "X-MCP-Key": "${MCP_DATA_SERVICE_KEY}"
      }
    }
  }
}
```

Prompt di test suggerito in Claude Code:
- `Usa mcp-data-service per trovare le tabelle disponibili e poi mostrare le colonne della prima tabella.`

## 6) Troubleshooting rapido
- `401 Unauthorized`
  - chiave errata/scaduta
  - `X-MCP-Application` non corrisponde all'app registrata
  - header mancanti
- `Connection refused` / timeout
  - servizio non avviato
  - porta/URL errati
- Tool MCP non visibili
  - server non configurato nel file giusto
  - scope errato (es. configurato localmente ma usato in altro progetto)
- Problemi certificato HTTPS locale
  - usa `http://localhost:5244/mcp` per test locale

## 7) Sicurezza
- Non committare API key in chiaro in `opencode.json` o `.mcp.json`.
- Usa variabili ambiente per i segreti.
- Ruota periodicamente le chiavi dalla pagina `Config`.
