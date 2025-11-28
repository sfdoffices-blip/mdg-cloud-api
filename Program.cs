using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Data.Sqlite;
using System.Text.Json;
using System.IO;
using System.Collections.Generic;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

string dbPath = "mdg.db";

void InitDb()
{
    if (!File.Exists(dbPath))
    {
        using var c = new SqliteConnection($"Data Source={dbPath}");
        c.Open();

        var cmd = c.CreateCommand();
        cmd.CommandText = @"
        CREATE TABLE IF NOT EXISTS Preventifs (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Machine TEXT,
            Tache TEXT,
            Frequence TEXT,
            DateProchaine TEXT,
            Effectue INTEGER DEFAULT 0
        );";
        cmd.ExecuteNonQuery();
    }
}

InitDb();



// ✅ TEST API
app.MapGet("/", () => "MDG API OK");


// ✅ RECEPTION DES PREVENTIFS
app.MapPost("/preventifs", async (HttpRequest request) =>
{
    try
    {
        var dto = await JsonSerializer.DeserializeAsync<PreventifDTO>(request.Body);

        using var c = new SqliteConnection($"Data Source={dbPath}");
        c.Open();
        var cmd = c.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Preventifs (Machine, Tache, Frequence, DateProchaine, Effectue)
            VALUES (@m,@t,@f,@d,0);
        ";

        cmd.Parameters.AddWithValue("@m", dto.Machine);
        cmd.Parameters.AddWithValue("@t", dto.Tache);
        cmd.Parameters.AddWithValue("@f", dto.Frequence);
        cmd.Parameters.AddWithValue("@d", dto.DateProchaine);
        cmd.ExecuteNonQuery();

        return Results.Ok("Envoyé ✅");
    }
    catch (System.Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});


// ✅ LISTE TELEPHONE
app.MapGet("/preventifs", () =>
{
    using var c = new SqliteConnection($"Data Source={dbPath}");
    c.Open();

    var cmd = c.CreateCommand();
    cmd.CommandText = "SELECT * FROM Preventifs WHERE Effectue = 0";

    var r = cmd.ExecuteReader();
    var list = new List<PreventifDTO>();

    while (r.Read())
    {
        list.Add(new PreventifDTO
        {
            Id = r.GetInt32(0),
            Machine = r.GetString(1),
            Tache = r.GetString(2),
            Frequence = r.GetString(3),
            DateProchaine = r.GetString(4),
            Effectue = r.GetInt32(5)
        });
    }

    return list;
});


// ✅ VALIDATION DEPUIS TELEPHONE
app.MapPut("/preventifs/{id}", (int id) =>
{
    using var c = new SqliteConnection($"Data Source={dbPath}");
    c.Open();

    var cmd = c.CreateCommand();
    cmd.CommandText = "UPDATE Preventifs SET Effectue = 1 WHERE Id=@id";
    cmd.Parameters.AddWithValue("@id", id);
    cmd.ExecuteNonQuery();

    return Results.Ok("Validé ✅");
});


app.Run();



// ✅ OBJET TRANSFERT
public class PreventifDTO
{
    public int Id { get; set; }
    public string Machine { get; set; }
    public string Tache { get; set; }
    public string Frequence { get; set; }
    public string DateProchaine { get; set; }
    public int Effectue { get; set; }
}

