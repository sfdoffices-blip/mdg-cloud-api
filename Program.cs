using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Data.Sqlite;
using System.Text.Json;
using System.IO;
using System.Collections.Generic;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// --- Dossier web (pages téléphone)
app.UseDefaultFiles();   // index.html par défaut
app.UseStaticFiles();    // wwwroot

// --- Base SQLite
string dbPath = "mdg.db";
string connStr = $"Data Source={dbPath}";

// --- Initialisation DB
void InitDb()
{
    if (!File.Exists(dbPath))
    {
        using var c = new SqliteConnection(connStr);
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
        );

        CREATE TABLE IF NOT EXISTS Machines (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Nom TEXT
        );

        CREATE TABLE IF NOT EXISTS Interventions (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Machine TEXT,
            Type TEXT,
            Description TEXT,
            Operateur TEXT,
            DateHeure TEXT
        );
        ";
        cmd.ExecuteNonQuery();
    }
}
InitDb();

// ----------------------------------------------------
// ✅ PAGE TEST
app.MapGet("/api", () => "MDG API OK");

// ----------------------------------------------------
// ✅ PREVENTIFS : PC → CLOUD (envoi liste ou unitaire)
// Accepte soit un objet, soit une liste
app.MapPost("/preventifs", async (HttpRequest request) =>
{
    try
    {
        using var doc = await JsonDocument.ParseAsync(request.Body);
        var root = doc.RootElement;

        using var c = new SqliteConnection(connStr);
        c.Open();

        void InsertOne(JsonElement e)
        {
            var cmd = c.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Preventifs (Machine, Tache, Frequence, DateProchaine, Effectue)
                VALUES (@m,@t,@f,@d,@e);
            ";
            cmd.Parameters.AddWithValue("@m", e.GetProperty("machine").GetString());
            cmd.Parameters.AddWithValue("@t", e.GetProperty("tache").GetString());
            cmd.Parameters.AddWithValue("@f", e.GetProperty("frequence").GetString());
            cmd.Parameters.AddWithValue("@d", e.GetProperty("dateProchaine").GetString());
            cmd.Parameters.AddWithValue("@e", e.TryGetProperty("effectue", out var ef) ? ef.GetInt32() : 0);
            cmd.ExecuteNonQuery();
        }

        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var e in root.EnumerateArray())
                InsertOne(e);
        }
        else if (root.ValueKind == JsonValueKind.Object)
        {
            InsertOne(root);
        }
        else
        {
            return Results.BadRequest("JSON invalide");
        }

        return Results.Ok("Envoyé au cloud ✅");
    }
    catch (System.Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});

// ----------------------------------------------------
// ✅ PREVENTIFS : TELEPHONE ← CLOUD (liste des non faits)
app.MapGet("/preventifs", () =>
{
    using var c = new SqliteConnection(connStr);
    c.Open();

    var cmd = c.CreateCommand();
    cmd.CommandText = "SELECT * FROM Preventifs WHERE Effectue = 0";

    var r = cmd.ExecuteReader();
    var list = new List<object>();

    while (r.Read())
    {
        list.Add(new
        {
            id = r.GetInt32(0),
            machine = r.GetString(1),
            tache = r.GetString(2),
            frequence = r.GetString(3),
            dateProchaine = r.GetString(4),
            effectue = r.GetInt32(5)
        });
    }

    return list;
});

// ----------------------------------------------------
// ✅ VALIDER UN PREVENTIF DEPUIS TELEPHONE
app.MapPut("/preventifs/{id}", (int id) =>
{
    using var c = new SqliteConnection(connStr);
    c.Open();

    var cmd = c.CreateCommand();
    cmd.CommandText = "UPDATE Preventifs SET Effectue = 1 WHERE Id=@id";
    cmd.Parameters.AddWithValue("@id", id);
    cmd.ExecuteNonQuery();

    return Results.Ok("Validé ✅");
});

// ----------------------------------------------------
// ✅ MACHINES : pour liste sur téléphone
app.MapGet("/machines", () =>
{
    using var c = new SqliteConnection(connStr);
    c.Open();

    var cmd = c.CreateCommand();
    cmd.CommandText = "SELECT Nom FROM Machines ORDER BY Nom";

    var r = cmd.ExecuteReader();
    var list = new List<object>();

    while (r.Read())
    {
        list.Add(new { nom = r.GetString(0) });
    }

    return list;
});

// ----------------------------------------------------
// ✅ Interventions NON PLANIFIÉES depuis téléphone
app.MapPost("/interventions", async (HttpRequest request) =>
{
    try
    {
        var dto = await JsonSerializer.DeserializeAsync<InterventionDTO>(request.Body);

        using var c = new SqliteConnection(connStr);
        c.Open();

        var cmd = c.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Interventions (Machine, Type, Description, Operateur, DateHeure)
            VALUES (@m,@t,@d,@o,@dh);
        ";
        cmd.Parameters.AddWithValue("@m", dto.machine);
        cmd.Parameters.AddWithValue("@t", dto.type);
        cmd.Parameters.AddWithValue("@d", dto.description);
        cmd.Parameters.AddWithValue("@o", dto.operateur);
        cmd.Parameters.AddWithValue("@dh", System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

        cmd.ExecuteNonQuery();

        return Results.Ok("Intervention envoyée ✅");
    }
    catch (System.Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});

// ----------------------------------------------------
// ✅ Interventions (pour import côté PC plus tard)
app.MapGet("/interventions", () =>
{
    using var c = new SqliteConnection(connStr);
    c.Open();

    var cmd = c.CreateCommand();
    cmd.CommandText = "SELECT * FROM Interventions ORDER BY Id DESC";

    var r = cmd.ExecuteReader();
    var list = new List<object>();

    while (r.Read())
    {
        list.Add(new
        {
            id = r.GetInt32(0),
            machine = r.GetString(1),
            type = r.GetString(2),
            description = r.GetString(3),
            operateur = r.GetString(4),
            dateHeure = r.GetString(5)
        });
    }

    return list;
});

app.Run();

public record InterventionDTO(string machine, string type, string description, string operateur);
