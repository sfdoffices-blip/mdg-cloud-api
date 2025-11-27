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
    ";
    cmd.ExecuteNonQuery();
}

InitDb();

// --- API ---

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

app.Run();
