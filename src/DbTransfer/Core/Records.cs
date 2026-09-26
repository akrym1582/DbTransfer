using System.Collections.ObjectModel;

namespace DbTransfer.Core;

public sealed record RecordColumn
{
    public RecordColumn(string name, Type dataType, bool isNullable = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(dataType);
        Name = name;
        DataType = dataType;
        IsNullable = isNullable;
    }

    public string Name { get; }
    public Type DataType { get; }
    public bool IsNullable { get; }
}

public sealed class RecordSchema
{
    private readonly ReadOnlyCollection<RecordColumn> _columns;
    private readonly Dictionary<string, int> _ordinals;

    public RecordSchema(IEnumerable<RecordColumn> columns, StringComparer? comparer = null)
    {
        ArgumentNullException.ThrowIfNull(columns);
        comparer ??= StringComparer.Ordinal;
        var materialized = columns.ToArray();
        if (materialized.Length == 0) throw new ArgumentException("A schema must have at least one column.", nameof(columns));
        _ordinals = new Dictionary<string, int>(comparer);
        for (var i = 0; i < materialized.Length; i++)
            if (!_ordinals.TryAdd(materialized[i].Name, i))
                throw new ArgumentException($"Duplicate column name '{materialized[i].Name}'.", nameof(columns));
        _columns = Array.AsReadOnly(materialized);
    }

    public IReadOnlyList<RecordColumn> Columns => _columns;
    public int Count => _columns.Count;
    public int GetOrdinal(string name) => _ordinals.TryGetValue(name, out var ordinal)
        ? ordinal : throw new KeyNotFoundException($"Column '{name}' was not found.");
}

public sealed class RecordBatch
{
    private readonly object?[][] _rows;

    public RecordBatch(RecordSchema schema, IEnumerable<object?[]> rows, long estimatedBytes)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(rows);
        if (estimatedBytes < 0) throw new ArgumentOutOfRangeException(nameof(estimatedBytes));
        Schema = schema;
        _rows = rows.Select(row =>
        {
            if (row.Length != schema.Count) throw new ArgumentException("A row does not match the schema width.", nameof(rows));
            return (object?[])row.Clone();
        }).ToArray();
        EstimatedBytes = estimatedBytes;
    }

    public RecordSchema Schema { get; }
    public IReadOnlyList<object?[]> Rows => _rows;
    public int Count => _rows.Length;
    public long EstimatedBytes { get; }
}
