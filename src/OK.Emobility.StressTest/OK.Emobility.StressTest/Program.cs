using Dapper;
using Microsoft.Data.SqlClient;
using Npgsql;

var countArg = args[0];
var cleanArg = args[1];
var count = Convert.ToInt32(string.IsNullOrEmpty(countArg) ? "10" : countArg);

using var managementReplConnection = new NpgsqlConnection("Server=localhost;Port=6110;Database=central;User Id=postgres;Password=Passw0rd;");
using var csManagementConnection = new SqlConnection("Server=localhost,7433;Database=cs-management;User ID=SA;Password=Passw0rd;Pooling=true;TrustServerCertificate=True");

if (cleanArg.ToLower() == "clean")
{
    await csManagementConnection.ExecuteAsync(@"
       DELETE FROM ChargingStation WHERE CreatedBy = 'Locust seed';
       DELETE FROM ChargingStationLocation WHERE CreatedBy = 'Locust seed';
       DELETE FROM Connector WHERE CreatedBy = 'Locust seed';
    ");
    await managementReplConnection.ExecuteScalarAsync(@"
       DELETE FROM management_repl.chargingstation WHERE Identifier LIKE 'LOCUST-CS-%';
    ");
}

for (var i = 0; i < count; i++)
{
    var evseId = "DK*OKO*E" + Guid.NewGuid().ToString("N").Substring(0,30);
    var sqlLocationParams = new
    {
        Identifier = Guid.NewGuid(),
        LocationName = "Locust test location " + i,
        Road = "Test road " + i,
        InstallationNumber = 5100 + i
    };

    Console.WriteLine(i + " Insert location into CsManagement");
    var locationId = await csManagementConnection.ExecuteScalarAsync<int>(@"
      INSERT dbo.ChargingStationLocation
      OUTPUT Inserted.Id
      VALUES (GETDATE(), 'Locust seed', GETDATE(), NULL, 0, @Identifier,
        @LocationName, 'Viby', null, 0, 0, null, 0, 123, 8000,
        @Road, @InstallationNumber, 10, null, '', '')", 
        sqlLocationParams);
    
    Console.WriteLine(i + " Insert location into management repl");
    await managementReplConnection.ExecuteScalarAsync<int>(
        "INSERT INTO management_repl.location VALUES (@Identifier, false, now(), 10);", sqlLocationParams);

    var sqlChargingStationParams = new
    {
        CsIdentifier = "LOCUST-CS-" + i,
        CslIdentifier = sqlLocationParams.Identifier,
        LocationId = locationId
    };
    
    Console.WriteLine(i + " Insert charging station into CsManagement");
    var csId = await csManagementConnection.ExecuteScalarAsync<int>(@"INSERT dbo.ChargingStation 
            OUTPUT Inserted.Id
        VALUES (GETDATE(), 'Locust seed', GETDATE(), null, 0, @CsIdentifier, 900, 180, 
            @LocationId, 'FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF', 0, 1727260502711000000, 1, 0, 13, 0, 0, null, 0)",
        sqlChargingStationParams);
    
    Console.WriteLine(i + " Insert charging station into management repl");
    var replCsId = await managementReplConnection.ExecuteScalarAsync<int>(@"INSERT INTO management_repl.chargingstation 
        VALUES(default, @CsIdentifier, 'FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF', now(), now(), false, false, false, now(), @CslIdentifier) 
        RETURNING id;",
        sqlChargingStationParams);
    
    Console.WriteLine(i + " Insert connector into CsManagement");
    await csManagementConnection.ExecuteAsync(@"INSERT dbo.Connector
        VALUES(GETDATE(), 'Locust seed', GETDATE(), 'Locust seed', 0, 1, @CsId, 20, 11, 20, 70, 1727260502819000000, 80, 1, null, @EvseId, 0);",
        new {CsId = csId, EvseId = evseId});
    
    Console.WriteLine(i + " Insert connector into management repl");
    await managementReplConnection.ExecuteAsync(
        @"INSERT INTO management_repl.connector VALUES(default, 1, false, @CsId, @EvseId);",
        new {CsId = replCsId, EvseId = evseId});

}