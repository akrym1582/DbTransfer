# Inspect a query result schema

Use `inspect` to see the columns and .NET data types that DbTransfer discovers for a query before transferring its rows. The command writes a JSON description to stdout and a completion diagnostic to stderr.

## First inspection

```sh
export DATABASE='Host=localhost;Database=app;Username=app;Password=secret'

dbtransfer inspect --provider postgresql --connection "$DATABASE" \
  --query 'select id, created_at, total from public.orders where 1 = 0'
```

Example output shape:

```json
{
  "Provider": "postgresql",
  "Capabilities": "None",
  "Columns": [
    { "Name": "id", "Type": "System.Int64", "IsNullable": false }
  ]
}
```

The exact capability names, types, and nullability come from the selected provider and query. DbTransfer executes `--query` unchanged. A provider may obtain schema by executing or preparing the query, so use a read-only query and do not assume DbTransfer adds a row limit.

## Useful patterns

- Select the same expressions and aliases planned for `copy` or `export` to preview their resulting schema.
- Add the provider-appropriate always-false predicate when you want metadata without result rows, such as `where 1 = 0`.
- Redirect stdout to capture the machine-readable result: `dbtransfer inspect ... > schema.json`.
- Review type and nullability differences before using `--create-table` across different database providers.

`inspect` describes a query result; it does not enumerate every table in a database or compare source and destination schemas.

See the generated [`inspect` reference](../commands/inspect.md) for every option and its default.
