using Microsoft.EntityFrameworkCore;
using ORBIT.API.Data;
using ORBIT.API.Services;

static class PairingTransactionTests
{
    public static void Run()
    {
        foreach (var state in new[] { "pairing.pending.existing", "pairing.complete.existing", "pairing.aborted", "device.paired" })
            if (PairingTransactions.OwnsNewRecord(state)) throw new Exception("Rollback must not own an existing record.");
        if (!PairingTransactions.OwnsNewRecord("pairing.pending.new") ||
            !PairingTransactions.OwnsNewRecord("pairing.complete.new")) throw new Exception("New record compensation missing.");
        // SQL generation only: no database connection, schema change or write.
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=unused;Database=unused;Integrated Security=true;TrustServerCertificate=true").Options);
        var listSql = db.Devices.Where(d => d.AquariumId == 1).ToQueryString();
        if (!listSql.Contains("NOT EXISTS") || !listSql.Contains("pairing.pending.new"))
            throw new Exception("Pending pairing must be hidden from device lists.");
        var countSql = db.Aquariums.Select(a => new { a.Id, Count = a.Devices!.Count }).ToQueryString();
        if (!countSql.Contains("pairing.pending.new")) throw new Exception("Pending pairing must not inflate counts.");
        var rollbackSql = db.Devices.IgnoreQueryFilters().Where(d => d.Id == 5 && d.Aquarium!.UserId == 3).ToQueryString();
        if (rollbackSql.Contains("pairing.pending.new") || !rollbackSql.Contains("UserId"))
            throw new Exception("Rollback must see pending records but remain user-scoped.");
        Console.WriteLine("Pairing transaction policy and SQL translation tests passed (no DB access).");
    }
}
