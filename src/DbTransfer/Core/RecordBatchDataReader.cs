using System.Collections;
using System.Data;

namespace DbTransfer.Core;

/// <summary>Adapts one already-bounded record batch to provider bulk APIs without a DataTable copy.</summary>
public sealed class RecordBatchDataReader(RecordBatch batch) : IDataReader
{
    private int row = -1;

    public int FieldCount => batch.Schema.Count;

    public object this[int i] => GetValue(i);

    public object this[string name] => GetValue(GetOrdinal(name));

    public bool Read() => ++row < batch.Count;

    public object GetValue(int i) => batch.Rows[row][i] ?? DBNull.Value;

    public int GetValues(object[] values)
    {
        var count = Math.Min(values.Length, FieldCount);
        for (var i = 0; i < count; i++)
        {
            values[i] = GetValue(i);
        }

        return count;
    }

    public string GetName(int i) => batch.Schema.Columns[i].Name;

    public int GetOrdinal(string name) => batch.Schema.GetOrdinal(name);

    public Type GetFieldType(int i) => batch.Schema.Columns[i].DataType;

    public string GetDataTypeName(int i) => GetFieldType(i).Name;

    public bool IsDBNull(int i) => GetValue(i) is DBNull;

    public bool NextResult() => false;

    public int Depth => 0;

    public bool IsClosed => false;

    public int RecordsAffected => -1;

    public void Close()
    {
    }

    public DataTable? GetSchemaTable() => null;

    public void Dispose()
    {
    }

    public bool GetBoolean(int i) => (bool)GetValue(i);
    public byte GetByte(int i) => (byte)GetValue(i);

    public long GetBytes(int i, long o, byte[]? b, int bo, int l)
    {
        var v = (byte[])GetValue(i);
        if (b is null)
        {
            return v.Length;
        }

        var n = Math.Min(l, v.Length - (int)o);
        Array.Copy(v, o, b, bo, n);
        return n;
    }

    public char GetChar(int i) => (char)GetValue(i);

    public long GetChars(int i, long o, char[]? b, int bo, int l)
    {
        var v = GetString(i).ToCharArray();
        if (b is null)
        {
            return v.Length;
        }

        var n = Math.Min(l, v.Length - (int)o);
        Array.Copy(v, o, b, bo, n);
        return n;
    }

    public Guid GetGuid(int i) => (Guid)GetValue(i);
    public short GetInt16(int i) => Convert.ToInt16(GetValue(i));
    public int GetInt32(int i) => Convert.ToInt32(GetValue(i));
    public long GetInt64(int i) => Convert.ToInt64(GetValue(i));

    public float GetFloat(int i) => Convert.ToSingle(GetValue(i));
    public double GetDouble(int i) => Convert.ToDouble(GetValue(i));
    public string GetString(int i) => (string)GetValue(i);
    public decimal GetDecimal(int i) => Convert.ToDecimal(GetValue(i));
    public DateTime GetDateTime(int i) => (DateTime)GetValue(i);

    public IDataReader GetData(int i) => throw new NotSupportedException();
    public IEnumerator GetEnumerator() => batch.Rows.GetEnumerator();
}
