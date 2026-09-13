using Cyqwel.Ast;
using Cyqwel.Dialects;
using Cyqwel.Generation;
using Cyqwel.Parsing;
using Cyqwel.Visitors;

namespace Cyqwel.Tests;

public class TableHintTests
{
    [Fact]
    public void Generates_tsql_table_hints_from_builder()
    {
        var query = Sql.Select("u.id")
            .From(
                "users",
                "u",
                hints:
                [
                    Sql.TableHint("NOLOCK"),
                    Sql.TableHint("ROWLOCK"),
                ])
            .Build();

        Assert.Equal("SELECT u.id FROM users AS u WITH (NOLOCK, ROWLOCK)", query.ToSql(SqlDialects.TSql));
    }

    [Fact]
    public void Generates_table_hints_with_arguments()
    {
        var query = Sql.Select("*")
            .From("users", hints: [Sql.TableHint("INDEX", Sql.Id("IX_Users"))])
            .Build();

        Assert.Equal("SELECT * FROM users WITH (INDEX(IX_Users))", query.ToSql(SqlDialects.TSql));
    }

    [Fact]
    public void Parses_tsql_table_hints_after_the_alias()
    {
        var document = SqlDialects.TSql.Parse(
            "SELECT * FROM users AS u WITH (NOLOCK, INDEX(IX_Users))");
        var table = Assert.IsType<NamedTable>(Assert.IsType<SelectStatement>(
            Assert.Single(document.Statements)).From);

        Assert.Equal("u", table.Alias!.Value);
        var hints = table.Hints!;
        Assert.Equal(["NOLOCK", "INDEX"], hints.Select(hint => hint.Name.Value));
        var indexArguments = hints[1].Arguments!;
        Assert.Single(indexArguments);
        Assert.Equal("IX_Users", Assert.IsType<ColumnExpression>(indexArguments[0]).Parts[0].Value);
        Assert.Equal(
            "SELECT * FROM users AS u WITH (NOLOCK, INDEX(IX_Users))",
            document.ToSql(SqlDialects.TSql));
    }

    [Fact]
    public void Rejects_table_hints_when_the_source_dialect_does_not_support_them()
    {
        Assert.Throws<SqlParseException>(() => SqlDialects.PostgreSql.Parse(
            "SELECT * FROM users WITH (NOLOCK)"));

        var query = Sql.Select("*")
            .From("users", hints: [Sql.TableHint("NOLOCK")])
            .Build();

        Assert.Throws<NotSupportedException>(() => query.ToSql(SqlDialects.PostgreSql));
        Assert.Equal(
            "SELECT * FROM users",
            query.ToSql(
                SqlDialects.PostgreSql,
                new SqlGenerationOptions { UnsupportedBehavior = UnsupportedSqlBehavior.Ignore }));
    }

    [Fact]
    public void Table_hints_are_rewriter_and_visitor_nodes()
    {
        var query = Sql.Select("*")
            .From("users", hints: [Sql.TableHint("INDEX", Sql.Id("IX_Users"))])
            .Build();

        Assert.Single(query.FindAll<WithTableHint>());
        Assert.Single(query.FindAll<ColumnExpression>(), column =>
            column.Parts.Count == 1 && column.Parts[0].Value == "IX_Users");
        Assert.Equal(
            "SELECT * FROM accounts WITH (INDEX(IX_Users))",
            query.RenameTable("users", "accounts").ToSql(SqlDialects.TSql));
    }
}
