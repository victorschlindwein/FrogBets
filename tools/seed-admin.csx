#!/usr/bin/env dotnet-script
#r "nuget: BCrypt.Net-Next, 4.0.3"
#r "nuget: Npgsql, 8.0.0"

using BCrypt.Net;
using Npgsql;

var hash = BCrypt.HashPassword("Dizzygod");
var id = Guid.NewGuid();
var connStr = "Host=127.0.0.1;Port=5432;Database=FrogBets;Username=postgres;Password=postgres";

await using var conn = new NpgsqlConnection(connStr);
await conn.OpenAsync();

await using var cmd = conn.CreateCommand();
cmd.CommandText = """
    INSERT INTO "Users" ("Id","Username","PasswordHash","IsAdmin","VirtualBalance","ReservedBalance","WinsCount","LossesCount","CreatedAt")
    VALUES (@id, 'admin', @hash, true, 1000, 0, 0, 0, NOW())
    ON CONFLICT ("Username") DO UPDATE SET "PasswordHash" = @hash, "IsAdmin" = true;
    """;
cmd.Parameters.AddWithValue("id", id);
cmd.Parameters.AddWithValue("hash", hash);
await cmd.ExecuteNonQueryAsync();

Console.WriteLine($"Admin criado/atualizado. Id: {id}");
