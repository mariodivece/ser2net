namespace Unosquare.Ser2Net;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        using var cts = new CancellationTokenSource();
        var builder = Host
            .CreateDefaultBuilder(args)
            .ConfigureLifetimeAndLogging()
            .UseMainHostedService();

        using var host = builder.Build();

        await host.RunAsync(cts.Token).ConfigureAwait(false);
        return Environment.ExitCode;
    }

    private static void TestQueue()
    {
        using var q = new MemoryQueue<int>(8);
        Span<int> span = [1,2,3,4,5];

        q.Enqueue(span);
        Print(q, $"Wrote {span.Length} items");

        Console.WriteLine($"Peek 0: {(q.TryPeek(0, out var e) ? e.ToString() : "NA")}");
        Console.WriteLine($"Peek 1: {(q.TryPeek(1, out e) ? e.ToString() : "NA")}");
        Console.WriteLine($"Peek 2: {(q.TryPeek(2, out e) ? e.ToString() : "NA")}");
        Console.WriteLine($"Peek 3: {(q.TryPeek(3, out e) ? e.ToString() : "NA")}");
        Console.WriteLine($"Peek 4: {(q.TryPeek(4, out e) ? e.ToString() : "NA")}");
        Console.WriteLine($"Peek 5: {(q.TryPeek(5, out e) ? e.ToString() : "NA")}");

        Print(q.Dequeue(3));
        Print(q, $"Read 3 items");
        
        q.Enqueue(span);
        Print(q, $"Wrote {span.Length} items");
        
        q.Enqueue(span);
        Print(q, $"Wrote {span.Length} items");

        Print(q.Dequeue(11));
        Print(q, $"Read 11 items");
        
        q.Enqueue(span);
        Print(q, $"Wrote {span.Length} items");
        
        Print(q.Dequeue(6));
        Print(q, $"Read 6 items");

    }

    private static void Print(MemoryQueue<int> q, string message)
    {
        const int Digits = 3;
        Console.WriteLine($"{message}: Count = {q.Count}, Capacity = {q.Capacity}");
        Console.WriteLine(new string(' ', q.ReadIndex * Digits) + new string(' ', Digits - 1) + "R" + new string(' ', (q.Capacity - q.ReadIndex) * Digits));
        Console.WriteLine(string.Join(string.Empty, q.Buffer.Span.ToArray().Select(c => $"{c,Digits:n0}")));
        Console.WriteLine(new string(' ', q.WriteIndex * Digits) + new string(' ', Digits - 1) + "W" + new string(' ', (q.Capacity - q.WriteIndex) * Digits));
        Console.WriteLine();
    }

    private static void Print(int[] ints)
    {
        Console.WriteLine(string.Join(", ", ints.Select(c => c.ToString())));
    }
}
