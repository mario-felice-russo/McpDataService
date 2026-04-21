# MCP Data Service
Server ASP.NET Core per accesso multi-database (SQL Server, Oracle, SQLite) con endpoint MCP/CRUD, audit e UI di configurazione.

## Avvio rapido
1. Configura le connection string in `appsettings.json`.
2. Compila:
   ```bash
   dotnet build McpDataService.sln /p:UseAppHost=false
   ```
3. Avvia:
   ```bash
   dotnet run --project McpDataService/McpDataService.csproj
   ```

## Gestione utenti (semplice e sicura, via appsettings)
Gli utenti UI sono in `UserAuth:Users` dentro `appsettings.json`:

```json
"UserAuth": {
  "Users": [
    {
      "Username": "admin",
      "PasswordHash": "pbkdf2-sha256$...",
      "Role": "admin",
      "CreatedUtc": "2026-01-01T00:00:00Z"
    }
  ]
}
```

### Utenti e password preconfigurati
- `admin` / `Admin@2026!` (ruolo `admin`)
- `user` / `User@2026!` (ruolo `user`)
- `programmatore` / `Programmatore@2026!` (ruolo `programmer`)

### Sicurezza
- Password salvate solo come hash PBKDF2-SHA256 (mai in chiaro).
- Minimo password: 10 caratteri.
- Non è consentita l'eliminazione dell'ultimo utente `admin`.
- Endpoint config utenti protetti da policy `AdminOnly`.

### Ruoli supportati
- `admin`: accesso completo (Configurazione, Log, Audit, API config).
- `programmer`: accesso tecnico (Log, Audit, UI).
- `user`: accesso UI base autenticata.

## Icone ruolo (asset)
Sono presenti in `assets/`:
- `assets/role-admin.svg` → arancione (admin)
- `assets/role-user.svg` → verde (user)
- `assets/role-programmatore.svg` → azzurra (programmatore)

Le icone sono servite anche via URL `/assets/...` (es. `/assets/role-admin.svg`).

## Autenticazione API
- Header richiesti: `X-MCP-Application` e `X-MCP-Key` (se non autenticato come admin UI).
- Gestione chiavi applicative disponibile nella pagina `Config`.

## Pagine principali UI
- `/Login` accesso utenti
- `/Config` gestione configurazione e utenti (solo admin)
- `/Logs` consultazione log (admin/programmatore)
- `/AuditLogs` audit richieste (admin/programmatore)

## Endpoint principali
- `POST /mcp` JSON-RPC MCP
- `GET /db/{dbType}/...` introspezione schema
- `GET|POST|PUT|DELETE /crud/{dbType}/{table}` operazioni CRUD
- `GET /api/audit/...` consultazione audit

## Guida client MCP
- Utilizzo del server MCP da OpenCode e Claude Code: [`GUIDA_CONNESSIONE_CLIENTS`](GUIDA_CONNESSIONE_CLIENTS.md)

