using System;
using Xunit;

namespace TheSqlODataMCP.Tests;

public class DqlValidatorTests
{
    private readonly DqlValidator _validator = new DqlValidator();

    [Fact]
    public void IsValidDql_ValidSelectQuery_ReturnsTrue()
    {
        // Arrange
        string query = "SELECT id, name FROM users WHERE status = 'active';";

        // Act & Assert
        bool result = _validator.IsValidDql(query);
        Assert.True(result);
    }

    [Fact]
    public void IsValidDql_ValidSelectQueryWithComments_ReturnsTrue()
    {
        // Arrange
        string query = "SELECT id, name FROM users WHERE status = 'active'; -- this is a comment";

        // Act & Assert
        bool result = _validator.IsValidDql(query);
        Assert.True(result);
    }

    [Fact]
    public void IsValidDql_InvalidQuery_InsertStatement_ThrowsInvalidOperationException()
    {
        // Arrange
        string query = "INSERT INTO users (name) VALUES ('test');";

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => _validator.IsValidDql(query));
        Assert.Contains("must start with a SELECT statement", exception.Message);
    }

    [Fact]
    public void IsValidDql_InvalidQuery_UpdateStatement_ThrowsInvalidOperationException()
    {
        // Arrange
        string query = "UPDATE users SET status = 'inactive' WHERE id = 1;";

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => _validator.IsValidDql(query));
        Assert.Contains("must start with a SELECT statement", exception.Message);
    }

    [Fact]
    public void IsValidDql_InvalidQuery_DeleteStatement_ThrowsInvalidOperationException()
    {
        // Arrange
        string query = "DELETE FROM users WHERE id = 1;";

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => _validator.IsValidDql(query));
        Assert.Contains("must start with a SELECT statement", exception.Message);
    }

    [Fact]
    public void IsValidDql_InvalidQuery_DropTableStatement_ThrowsInvalidOperationException()
    {
        // Arrange
        string query = "DROP TABLE users;";

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => _validator.IsValidDql(query));
        Assert.Contains("must start with a SELECT statement", exception.Message);
    }

    [Fact]
    public void IsValidDql_InvalidQuery_UnionClause_ThrowsInvalidOperationException()
    {
        // Arrange
        string query = "SELECT id FROM users UNION SELECT id FROM admins;";

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => _validator.IsValidDql(query));
        Assert.Contains("forbidden constructs such as UNION", exception.Message);
    }

    [Fact]
    public void IsValidDql_InvalidQuery_Subquery_ThrowsInvalidOperationException()
    {
        // Arrange
        string query = "SELECT id FROM users WHERE department IN (SELECT department_id FROM departments);";

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => _validator.IsValidDql(query));
        Assert.Contains("subqueries or multiple SELECT statements", exception.Message);
    }

    [Fact]
    public void IsValidDql_InvalidQuery_ExecStatement_ThrowsInvalidOperationException()
    {
        // Arrange
        string query = "EXEC sp_getUsers;";

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => _validator.IsValidDql(query));
        Assert.Contains("must start with a SELECT statement", exception.Message);
    }

    [Fact]
    public void IsValidDql_EmptyQuery_ThrowsArgumentException()
    {
        // Arrange
        string query = "   ";

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => _validator.IsValidDql(query));
        Assert.Contains("Query cannot be empty or whitespace.", exception.Message);
    }
}