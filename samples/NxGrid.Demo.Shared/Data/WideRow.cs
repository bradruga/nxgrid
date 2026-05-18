namespace NxGrid.Demo.Shared.Data;

public readonly record struct WideRow(int Id)
{
    // Columns 0-24 return int (0–9999); columns 25-48 return double (0.00–99.99).
    // Values are computed from (Id, col) using prime mixing — no per-instance storage.
    public object GetValue(int col)
    {
        unchecked
        {
            int v = Math.Abs(Id * Primes[col] ^ (col + 1) * 1_000_003);
            return col < 25 ? (object)(v % 10000) : Math.Round((v % 10000) / 100.0, 2);
        }
    }

    private static readonly int[] Primes =
    [
        2,   3,   5,   7,  11,  13,  17,  19,  23,  29,
       31,  37,  41,  43,  47,  53,  59,  61,  67,  71,
       73,  79,  83,  89,  97, 101, 103, 107, 109, 113,
      127, 131, 137, 139, 149, 151, 157, 163, 167, 173,
      179, 181, 191, 193, 197, 199, 211, 223, 227, 229
    ];
}
