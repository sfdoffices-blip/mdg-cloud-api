using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

string db = "mdg_cloud.db";

void InitDb()
{
    using var con = new SqliteConnection($"Data Source={db}");
    con.Open();

    using var cmd = con.CreateCommand();
    cmd.CommandText = @"
    CREATE TABLE IF NOT EXISTS Interventions (
        Id INTEGER PRIMARY KEY AUTOINCREMENT,
        Machine TEXT,
        Type TEXT,
        Date TEXT,
        User TEXT,
        Description TEXT
    );

    CREATE TABLE IF NOT EXISTS Preventif (
        Id INTEGER PRIMARY KEY AUTOINCREMENT,
        Machine TEXT,
        FrequenceType TEXT,
        ProchaineDate TEXT,
        Tache TEXT
    );
    ";
    cmd.ExecuteNonQuery();
}

InitDb();


// ========================
// INTERVENTIONS CLASSIQUES
// ========================
app.MapPost("/add", async (HttpContext ctx) =>
{
    var obj = await JsonSerializer.DeserializeAsync<Dictionary<string,string>>(ctx.Request.Body);

    using var con = new SqliteConnection($"Data Source={db}");
    con.Open();

    using var cmd = con.CreateCommand();
    cmd.CommandText = @"
        INSERT INTO Interventions (Machine, Type, Date, User, Description)
        VALUES ($m,$t,$d,$u,$desc)";
    cmd.Parameters.AddWithValue("$m", obj["machine"]);
    cmd.Parameters.AddWithValue("$t", obj["type"]);
    cmd.Parameters.AddWithValue("$d", obj["date"]);
    cmd.Parameters.AddWithValue("$u", obj["user"]);
    cmd.Parameters.AddWithValue("$desc", obj["desc"]);
    cmd.ExecuteNonQuery();

    return Results.Ok();
});


// ========================
// LISTE POUR SYNC PC
// ========================
app.MapGet("/list", () =>
{
    using var con = new SqliteConnection($"Data Source={db}");
    con.Open();

    var list = new List<object>();
    using var cmd = con.CreateCommand();
    cmd.CommandText = "SELECT * FROM Interventions";

    using var r = cmd.ExecuteReader();
    while (r.Read())
    {
        list.Add(new {
            Id = r.GetInt64(0),
            Machine = r.GetString(1),
            Type = r.GetString(2),
            Date = r.GetString(3),
            User = r.GetString(4),
            Description = r.GetString(5)
        });
    }

    return Results.Json(list);
});


// ========================
// NETTOYAGE CLOUD
// ========================
app.MapPost("/clear", () =>
{
    using var con = new SqliteConnection($"Data Source={db}");
    con.Open();

    using var cmd = con.CreateCommand();
    cmd.CommandText = "DELETE FROM Interventions";
    cmd.ExecuteNonQuery();

    return Results.Ok();
});


// ========================
// PRÉVENTIF
// ========================
app.MapPost("/preventif", async (HttpContext ctx) =>
{
    var list = await JsonSerializer.DeserializeAsync<List<Dictionary<string, string>>>(ctx.Request.Body);

    using var con = new SqliteConnection($"Data Source={db}");
    con.Open();

    using var clear = con.CreateCommand();
    clear.CommandText = "DELETE FROM Preventif";
    clear.ExecuteNonQuery();

    if (list != null)
    {
        foreach (var p in list)
        {
            using var ins = con.CreateCommand();
            ins.CommandText = @"
                INSERT INTO Preventif (Machine, FrequenceType, ProchaineDate, Tache)
                VALUES ($m,$t,$d,$ta)";
            ins.Parameters.AddWithValue("$m", p["machine"]);
            ins.Parameters.AddWithValue("$t", p["type"]);
            ins.Parameters.AddWithValue("$d", p["prochaine"]);
            ins.Parameters.AddWithValue("$ta", p.ContainsKey("tache") ? p["tache"] : "");
            ins.ExecuteNonQuery();
        }
    }
    return Results.Ok();
});

app.MapGet("/preventif", () =>
{
    using var con = new SqliteConnection($"Data Source={db}");
    con.Open();

    var list = new List<object>();
    using var cmd = con.CreateCommand();
    cmd.CommandText = "SELECT * FROM Preventif";

    using var r = cmd.ExecuteReader();
    while (r.Read())
    {
        list.Add(new {
            Machine = r.GetString(1),
            Type = r.GetString(2),
            Prochaine = r.GetString(3),
            Tache = r.IsDBNull(4) ? "" : r.GetString(4)
        });
    }

    return Results.Json(list);
});


// ========================
// ✅ VALIDATION DEPUIS TEL
// ➜ Enregistre ET SUPPRIME
// ========================
app.MapPost("/validate", async (HttpContext ctx) =>
{
    var obj = await JsonSerializer.DeserializeAsync<Dictionary<string,string>>();

    using var con = new SqliteConnection($"Data Source={db}");
    con.Open();

    // 1) Enregistrer intervention
    using var insert = con.CreateCommand();
    insert.CommandText = @"
        INSERT INTO Interventions (Machine, Type, Date, User, Description)
        VALUES ($m,'Entretien',$d,$u,$desc)";
    insert.Parameters.AddWithValue("$m", obj["machine"]);
    insert.Parameters.AddWithValue("$d", obj["date"]);
    insert.Parameters.AddWithValue("$u", obj["operateur"]);
    insert.Parameters.AddWithValue("$desc", obj["tache"]);
    insert.ExecuteNonQuery();

    // 2) SUPPRIMER LA TÂCHE VALIDÉE
    using var delete = con.CreateCommand();
    delete.CommandText = "DELETE FROM Preventif WHERE Machine=$m AND Tache=$t";
    delete.Parameters.AddWithValue("$m", obj["machine"]);
    delete.Parameters.AddWithValue("$t", obj["tache"]);
    delete.ExecuteNonQuery();

    return Results.Ok();
});


// PAGE MOBILE
app.UseDefaultFiles();
app.UseStaticFiles();
app.Run();

