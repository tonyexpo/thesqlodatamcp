using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;
using Xunit;

namespace TheSqlODataMcp.Spikes.SqlServerTests;

public sealed class MsSqlContainerTests
{
    private const string SqlServerImage = "mcr.microsoft.com/mssql/server:2022-CU25-ubuntu-22.04";

    [Fact]
    [Trait("Category", "DockerIntegration")]
    public async Task Disposable_sql_server_accepts_a_real_parameterized_select()
    {
        var password = "A_StrongPassword_123!";
        await using var container = new MsSqlBuilder()
            .WithImage(SqlServerImage)
            .WithPassword(password)
            .Build();
        await container.StartAsync(CancellationToken.None);

        await using var connection = new SqlConnection(container.GetConnectionString());
        await connection.OpenAsync(CancellationToken.None);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT @value";
        command.Parameters.Add(new SqlParameter("@value", System.Data.SqlDbType.Int) { Value = 42 });

        var value = await command.ExecuteScalarAsync(CancellationToken.None);
        Assert.Equal(42, value);
    }
}
