using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

string db = "mdg_cloud.db";

// ========== INIT DB ==========
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

    CREATE TABLE IF NOT EXISTS Users (
        Nom TEXT PRIMARY KEY
    );

    CREATE TABLE IF NOT EXISTS Machines (
        Nom TEXT PRIMARY KEY
    );
    ";
    cmd.ExecuteNonQuery();
}
InitDb();

// ========================
// 🔁 LISTES DU PC -> CLOUD
// ========================
app.MapPost("/sync-users", async (HttpContext ctx) =>
{
    var list = await JsonSerializer.DeserializeAsync<List<string>>(ctx.Request.Body);
    using var con = new SqliteConnection($"Data Source={db}");
    con.Open();
    using var del = con.CreateCommand();
    del.CommandText = "DELETE FROM Users";
    del.ExecuteNonQuery();

    if (list != null)
    {
        foreach (var u in list)
        {
            using var ins = con.CreateCommand();
            ins.CommandText = "INSERT INTO Users (Nom) VALUES ($n)";
            ins.Parameters.AddWithValue("$n", u);
            ins.ExecuteNonQuery();
        }
    }
    return Results.Ok();
});

app.MapPost("/sync-machines", async (HttpContext ctx) =>
{
    var list = await JsonSerializer.DeserializeAsync<List<string>>(ctx.Request.Body);
    using var con = new SqliteConnection($"Data Source={db}");
    con.Open();
    using var del = con.CreateCommand();
    del.CommandText = "DELETE FROM Machines";
    del.ExecuteNonQuery();

    if (list != null)
    {
        foreach (var m in list)
        {
            using var ins = con.CreateCommand();
            ins.CommandText = "INSERT INTO Machines (Nom) VALUES ($n)";
            ins.Parameters.AddWithValue("$n", m);
            ins.ExecuteNonQuery();
        }
    }
    return Results.Ok();
});

// ========================
// 📲 CLOUD -> TÉLÉPHONE
// ========================
app.MapGet("/users", () =>
{
    using var con = new SqliteConnection($"Data Source={db}");
    con.Open();
    var list = new List<string>();
    using var cmd = con.CreateCommand();
    cmd.CommandText = "SELECT Nom FROM Users";
    using var r = cmd.ExecuteReader();
    while (r.Read()) list.Add(r.GetString(0));
    return Results.Json(list);
});

app.MapGet("/machines", () =>
{
    using var con = new SqliteConnection($"Data Source={db}");
    con.Open();
    var list = new List<string>();
    using var cmd = con.CreateCommand();
    cmd.CommandText = "SELECT Nom FROM Machines";
    using var r = cmd.ExecuteReader();
    while (r.Read()) list.Add(r.GetString(0));
    return Results.Json(list);
});

// ========================
// 📲 DÉCLARATION MOBILE
// ========================
app.MapPost("/add", async (HttpContext ctx) =>
{
    var obj = await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(ctx.Request.Body);

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
// 💻 SYNCHRO PC
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
// PRÉVENTIF (inchangé)
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
        foreach (var p in list)
        {
            using var ins = con.CreateCommand();
            ins.CommandText = @"INSERT INTO Preventif (Machine, FrequenceType, ProchaineDate, Tache)
                                VALUES ($m,$t,$d,$ta)";
            ins.Parameters.AddWithValue("$m", p["machine"]);
            ins.Parameters.AddWithValue("$t", p["type"]);
            ins.Parameters.AddWithValue("$d", p["prochaine"]);
            ins.Parameters.AddWithValue("$ta", p.ContainsKey("tache")?p["tache"]:"");
            ins.ExecuteNonQuery();
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
        list.Add(new {
            Machine = r.GetString(1),
            Type = r.GetString(2),
            Prochaine = r.GetString(3),
            Tache = r.IsDBNull(4) ? "" : r.GetString(4)
        });
    return Results.Json(list);
});

// ========================
// ✅ VALIDATION MOBILE -> SUPPRESSION PRÉVENTIF
// ========================
app.MapPost("/validate", async (HttpContext ctx) =>
{
    var obj = await JsonSerializer.DeserializeAsync<Dictionary<string,string>>(ctx.Request.Body);
    using var con = new SqliteConnection($"Data Source={db}");
    con.Open();

    using var ins = con.CreateCommand();
    ins.CommandText = @"INSERT INTO Interventions (Machine, Type, Date, User, Description)
                        VALUES ($m,'Entretien',$d,$u,$desc)";
    ins.Parameters.AddWithValue("$m", obj["machine"]);
    ins.Parameters.AddWithValue("$d", obj["date"]);
    ins.Parameters.AddWithValue("$u", obj["operateur"]);
    ins.Parameters.AddWithValue("$desc", obj["tache"]);
    ins.ExecuteNonQuery();

    using var del = con.CreateCommand();
    del.CommandText = "DELETE FROM Preventif WHERE Machine=$m AND Tache=$t";
    del.Parameters.AddWithValue("$m", obj["machine"]);
    del.Parameters.AddWithValue("$t", obj["tache"]);
    del.ExecuteNonQuery();

    return Results.Ok();
});

app.UseDefaultFiles();
app.UseStaticFiles();
app.Run();


