# Validate connectivity and a query

Supply a query inline with `--query`, or read it from a file with `--query-file <path>`. Specify exactly one; DbTransfer passes the resulting text to the provider without rewriting it.
Use `validate` as a lightweight preflight check. It opens the selected database source and asks for the result schema, verifying that DbTransfer can connect and that the provider can describe the query without running a transfer.

## First validation

```sh
export DATABASE='Host=localhost;Database=app;Username=app;Password=secret'

dbtransfer validate --provider postgresql --connection "$DATABASE" \
  --query 'select id, created_at from public.orders where 1 = 0'
```

A successful command exits with code `0` and writes a success diagnostic to stderr. Failures such as an invalid connection string, unavailable server, authentication error, or invalid query exit nonzero and report the error on stderr. The command does not write transferred records to stdout.

## What validation covers

- The provider name can be resolved.
- The database connection can be opened.
- The supplied query can be used to obtain a result schema.

DbTransfer passes the query through unchanged. Use read-only SQL and, when appropriate for your provider, an always-false predicate such as `where 1 = 0`. Schema discovery behavior is provider-specific, so validation should not be treated as a guarantee that a later long-running query will complete or that every returned value will fit the destination.

`validate` does not check a destination table, column mappings, transaction mode, permissions needed for writing, or an entire `copy`/`import` plan. Follow it with a small representative transfer when you need end-to-end verification.

Use [`inspect`](inspect.md) instead when you also want the discovered columns, types, nullability, and connector capabilities as JSON.

See the generated [`validate` reference](../commands/validate.md) for every option and its default.
