# Backlog - SQL OData MCP Connector

## v1 Development Steps

### Step 1: Configuration & Settings Management
- [x] Implementare la lettura e il parsing del file `settings.json` (Bearer token, stringa di connessione SQL).
- [x] Assicurarsi che le impostazioni siano caricate in modo sicuro all'avvio dell'applicazione.

### Step 2: T-SQL DQL Parser & Validator (Option A - Security)
- [ ] Creare un parser/validator che enforce strettamente il DQL (`SELECT` statements only).
- [ ] Rifiutare DML, DDL, subqueries, `UNION`s, stored procedure calls, e altre costruzioni pericolose.
- [ ] Assicurarsi che i valori siano passati come parametri puri tramite `SqlCommand.Parameters`.

### Step 3: Database Connector Integration (SqlClient)
- [ ] Implementare la connessione usando `SqlConnection` e `SqlCommand`.
- [ ] Implementare lo tool `list_tables`: Query delle system views (es. `sys.tables`) per listare le tabelle disponibili.
- [ ] Implementare lo tool `get_table_schema(table_name)`: Query delle system views (es. `sys.columns`, `sys.types`) per ottenere i nomi delle colonne e i tipi per una specifica tabella.

### Step 4: MCP Tools Implementation (`modelcontextprotocol` NuGet package)
- [ ] Registrare gli MCP tools: `list_tables`, `get_table_schema(table_name)`, `execute_dql_query(table_name, where_conditions_json_or_sql)`.
- [ ] Implementare `execute_dql_query`: Usare il T-SQL DQL Parser & Validator per validare la query o le condizioni, poi eseguire usando `SqlClient` con query parameterizzate.

### Step 5: MCP Server Initialization & Authentication
- [ ] Inizializzare il MCP server usando `ModelContextProtocol` e `ModelContextProtocol.Server`.
- [ ] Implementare la validazione dell'autenticazione Bearer token usando il token da `settings.json`.

### Step 6: Testing & Validation
- [ ] Testare il MCP server localmente (es. via MCP inspector o client compatibile).
- [ ] Verificare la sicurezza (prevenzione SQL injection, enforcement DQL only).
- [ ] Verificare l'autenticazione (validazione Bearer token).

## vNext / Future Enhancements
- Full OAuth server implementation
- Enhanced security features beyond T-SQL DQL Parser
- Support for other database types (PostgreSQL, MySQL, etc.)
- Advanced query caching and performance optimization
- Integration with more MCP clients and frameworks
