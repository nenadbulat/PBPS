
using Microsoft.Data.SqlClient;

namespace PaymentBatchProcessingService.IntegrationTests;

internal static class IntegrationTestDatabaseGuard
{
    private const string ExpectedDatabase =
        "PaymentBatchProcessingService_IntegrationTests";

    public static void Validate(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);

        if (!string.Equals(
                builder.InitialCatalog,
                ExpectedDatabase,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Integration tests must use the '{ExpectedDatabase}' database.");
        }

        if (!string.Equals(
                builder.DataSource,
                @"(localdb)\MSSQLLocalDB",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Integration tests must use the expected SQL Server LocalDB instance.");
        }
    }
}
