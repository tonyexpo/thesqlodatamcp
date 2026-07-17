using System;
using System.Text.RegularExpressions;

namespace TheSqlODataMCP;

public class DqlValidator
{
    private static readonly string[] ForbiddenKeywords = new string[]
    {
        "INSERT", "UPDATE", "DELETE", "DROP", "ALTER", "CREATE", "TRUNCATE", "GRANT", "REVOKE", 
        "EXEC", "EXECUTE", "CALL", "BEGIN", "COMMIT", "ROLLBACK", "DECLARE", "OPEN", "FETCH", "CLOSE"
    };

    private static readonly string[] ForbiddenPatterns = new string[]
    {
        @"(?i)\bUNION\s+", 
        @"(?i)\bEXCEPT\b", 
        @"(?i)\bINTERSECT\b",
        @"(?i)^\s*(EXEC|EXECUTE|CALL)\s+",
    };

    public bool IsValidDql(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException("Query cannot be empty or whitespace.", nameof(query));
        }

        // Remove comments
        query = RemoveSqlComments(query);

        // Check for forbidden keywords at the beginning of the query (after optional whitespace)
        string trimmedQuery = query.TrimStart();
        
        // Ensure it starts with SELECT
        if (!Regex.IsMatch(trimmedQuery, @"(?i)^SELECT\b"))
        {
            throw new InvalidOperationException("Invalid DQL: Query must start with a SELECT statement.");
        }

        // Check for forbidden patterns (UNION, EXCEPT, INTERSECT, EXEC, etc.)
        foreach (var pattern in ForbiddenPatterns)
        {
            if (Regex.IsMatch(query, pattern))
            {
                throw new InvalidOperationException("Invalid DQL: Query contains forbidden constructs such as UNION, EXCEPT, INTERSECT, or DML/DDL operations.");
            }
        }

        // Check for multiple SELECT statements (subqueries) - but exclude UNION etc. which are already handled
        // Count occurrences of 'SELECT' keyword
        int selectCount = Regex.Matches(query, @"(?i)\bSELECT\b").Count;
        if (selectCount > 1)
        {
            throw new InvalidOperationException("Invalid DQL: Query contains subqueries or multiple SELECT statements.");
        }

        // Check for forbidden keywords within the query
        foreach (var keyword in ForbiddenKeywords)
        {
            if (Regex.IsMatch(query, $"(?i)\\b{keyword}\\b"))
            {
                throw new InvalidOperationException($"Invalid DQL: Query contains forbidden keyword or command: {keyword}.");
            }
        }

        return true;
    }

    private string RemoveSqlComments(string query)
    {
        // Remove single-line comments (-- ...)
        query = Regex.Replace(query, @"--.*$", "", RegexOptions.Multiline);
        
        // Remove multi-line comments (/* ... */)
        query = Regex.Replace(query, @"/\*.*?\*/", "", RegexOptions.Singleline);
        
        return query;
    }
}