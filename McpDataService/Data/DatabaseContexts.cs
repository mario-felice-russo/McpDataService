using Microsoft.EntityFrameworkCore;

namespace McpDataService.Data;

public class SqlServerContext : DbContext
{
    public SqlServerContext(DbContextOptions<SqlServerContext> options) : base(options) { }
}

public class OracleContext : DbContext
{
    public OracleContext(DbContextOptions<OracleContext> options) : base(options) { }
}

public class SQLiteContext : DbContext
{
    public SQLiteContext(DbContextOptions<SQLiteContext> options) : base(options) { }
}