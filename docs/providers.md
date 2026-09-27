# Data providers

DbTransfer accepts provider aliases case-insensitively and ignores hyphens. The canonical names are `postgresql`, `sqlserver`, `mysql`, `oracle`, `cosmosdb`, `mongodb`, and `azure-table-storage`. Connection strings are passed to the provider driver; keep secrets in environment variables rather than command history.

## Capability summary

| Provider | Source query | Destination | Native write | Transactions | Important requirements |
|---|---|---|---|---|---|
| PostgreSQL | SQL | 1–3 part table | Binary `COPY` | `none`, `batch`, `all` | `--create-table` supports only the CLR types listed below. |
| SQL Server | SQL | 1–3 part table | streaming `SqlBulkCopy` | `none`, `batch`, `all` | Bulk copy semantics can differ from ordinary inserts. |
| MySQL | SQL | 1–3 part table | `MySqlBulkCopy` | `none`, `batch`, `all` | Server/connection settings must permit bulk copy. |
| Oracle | SQL | 1–3 part table | array binding | `none`, `batch`, `all` | Normal Oracle quoted-name casing rules apply. |
| Cosmos DB | `database/container|SQL` | `database.container` | concurrent item creates | **`none` only** | Every record needs `id`; new containers use partition key `/id`. |
| MongoDB | `database/collection|filter JSON` | `database.collection` | ordered `InsertMany` | **`none` only** | Writes are inserts, not upserts. |
| Azure Table Storage | `table|OData filter` | one-part table | up to 100 entities per partition transaction | **`none` only** | Every record needs `PartitionKey` and `RowKey`. |

`--no-native-bulk` selects parameterized inserts for the four SQL destinations. It is not a fallback for document providers. No connector currently exposes upsert or partial-success semantics. `copy --resume` is a DbTransfer batch checkpoint, not a provider-side change feed: it requires `--transaction batch`, so it cannot be used with the document destinations.

## SQL providers

The complete `--query`/`--sql` text is sent to the driver without rewriting. Commands with `--query` alternatively accept `--query-file <path>`; specify exactly one query source. Use an explicit, deterministic `ORDER BY` for resumable copies. Destination names are parsed into structured components and each component is quoted; data values use parameters when native bulk is disabled.

```sh
dbtransfer copy \
  --source-provider postgresql --source-connection "$PG" \
  --query 'select id, name from public.customer order by id' \
  --destination-provider sqlserver --destination-connection "$MSSQL" \
  --destination-table dbo.Customer --create-table --transaction batch
```

With `--create-table`, DbTransfer maps Boolean, byte/integer types, single/double, Decimal, DateTime, String, Guid, and `byte[]`. Notable choices include unbounded text/binary types, SQL Server `decimal(38,18)`, MySQL `decimal(65,30)`, PostgreSQL `numeric`, and Oracle `number`. Other CLR types are rejected. Creation does not reproduce keys, indexes, defaults, identity/sequence behavior, computed columns, collations, constraints, triggers, or provider-specific precision. For production schemas, create the table yourself.

Native bulk is optimized for insertion, but it does not promise ordinary row-by-row trigger, constraint, identity, or error-reporting behavior. Test these semantics for the destination. All writes are inserts: duplicate-key handling is the database's normal error behavior.

## Azure Cosmos DB for NoSQL

```sh
dbtransfer export --provider cosmosdb --connection "$COSMOS" \
  --query 'sales/orders|SELECT * FROM c WHERE c.status = "open"' \
  --format extended-json --output orders.jsonl

dbtransfer import --input orders.jsonl --format extended-json \
  --destination-provider cosmosdb --destination-connection "$COSMOS" \
  --destination-table archive.orders --transaction none --create-table
```

The query is Cosmos SQL and is not rewritten. Reads may span partitions. Writes create items, require a mapped `id`, and fail on an existing id rather than replacing it. `--create-table` creates the database if necessary and a container with `/id` as partition key; it cannot select another key path. For an existing container, records must contain its expected partition-key value. Cross-partition atomicity is not provided, so only `--transaction none` is accepted.

## MongoDB

The text after `|` is a filter document. Leave it empty to select every document:

```sh
dbtransfer export --provider mongodb --connection "$MONGO" \
  --query 'app/events|{ "active": true }' --output events.jsonl

dbtransfer import --input events.jsonl --destination-provider mongodb \
  --destination-connection "$MONGO" --destination-table app.events \
  --transaction none --create-table
```

Only a filter document is accepted—not an aggregation pipeline, JavaScript expression, sort, projection, or update. The destination database may be the first component of `database.collection`, or come from the MongoDB URI for a one-part destination. Writes use ordered inserts, not upserts/replacements; a duplicate `_id` stops the operation. `--create-table` creates the collection if absent but not validation rules or indexes. Only `--transaction none` is accepted.

## Azure Table Storage

The text after `|` is an OData filter; leave it empty for all entities:

```sh
dbtransfer export --provider azure-table-storage --connection "$TABLES" \
  --query "events|PartitionKey eq 'north'" --output north.jsonl

dbtransfer import --input north.jsonl --destination-provider azure-table-storage \
  --destination-connection "$TABLES" --destination-table eventarchive \
  --transaction none --create-table
```

The destination must be one part. Map source columns to `PartitionKey` and `RowKey` when necessary. Add operations fail on an existing key rather than merging or replacing. DbTransfer groups an input batch by partition and submits chunks of at most 100 entities; each service transaction is partition-local, while the transfer is not atomic. Only `--transaction none` is accepted.

## Document shape and type limitations

For Cosmos DB, MongoDB, and Table Storage, the first source record fixes the schema. Properties missing later become `null`; properties occurring only later are not added. Nested objects and arrays remain JSON values. Use `extended-json` rather than plain JSON/JSONL when file round trips must preserve Int64, Decimal, date/time, UUID, or binary scalars.

Use `inspect` against representative data before a heterogeneous document transfer. `validate` checks connection/query preparation but does not prove every later record is compatible, destination keys are unique, or a full transfer fits service quotas.
