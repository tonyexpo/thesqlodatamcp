# Backlog - Connettore SQL OData MCP

## Fasi di Sviluppo v1

### Fase 1: Gestione Configurazione & Impostazioni
- [x] Implementare la lettura e il parsing del file `settings.json` (Bearer token, stringa di connessione SQL).
- [x] Assicurarsi che le impostazioni siano caricate in modo sicuro all'avvio dell'applicazione.

### Fase 2: T-SQL DQL Parser & Validator (Opzione A - Sicurezza)
- [ ] Creare un parser/validator che enforce strettamente il DQL (`SELECT` statements only).
- [ ] Rifiutare DML, DDL, subqueries, `UNION`s, stored procedure calls, e altre costruzioni pericolose.
- [ ] Assicurarsi che i valori siano passati come parametri puri tramite `SqlCommand.Parameters`.

### Fase 3: Integrazione Database Connector (SqlClient)
- [ ] Implementare la connessione usando `SqlConnection` e `SqlCommand`.
- [ ] Implementare lo tool `list_tables`: Query delle system views (es. `sys.tables`) per listare le tabelle disponibili.
- [ ] Implementare lo tool `get_table_schema(table_name)`: Query delle system views (es. `sys.columns`, `sys.types`) per ottenere i nomi delle colonne e i tipi per una specifica tabella.

### Fase 4: Implementazione MCP Tools (`modelcontextprotocol` NuGet package)
- [ ] Registrare gli MCP tools: `list_tables`, `get_table_schema(table_name)`, `execute_dql_query(table_name, where_conditions_json_or_sql)`.
- [ ] Implementare `execute_dql_query`: Usare il T-SQL DQL Parser & Validator per validare la query o le condizioni, poi eseguire usando `SqlClient` con query parameterizzate.

### Fase 5: Inizializzazione MCP Server & Autenticazione
- [ ] Inizializzare il MCP server usando `ModelContextProtocol` e `ModelContextProtocol.Server`.
- [ ] Implementare la validazione dell'autenticazione Bearer token usando il token da `settings.json`.

### Fase 6: Testing & Validazione
- [ ] Testare il MCP server localmente (es. via MCP inspector o client compatibile).
- [ ] Verificare la sicurezza (prevenzione SQL injection, enforcement DQL only).
- [ ] Verificare l'autenticazione (validazione Bearer token).

## vNext / Miglioramenti Futuri
- Implementazione completa del server OAuth
- Funzionalità di sicurezza avanzate oltre il T-SQL DQL Parser
- Supporto per altri tipi di database (PostgreSQL, MySQL, ecc.)
- Caching avanzato delle query e ottimizzazione delle prestazioni
- Integrazione con più client e framework MCP
