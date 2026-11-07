namespace DotNetCloud.Core.Data.Naming;

/// <summary>
/// Naming strategy for a sandboxed module whose schema was provisioned by the core.
/// </summary>
/// <remarks>
/// Sandboxed modules own a generated schema (for example <c>dnc_example_1a2b3c4d</c>) rather than
/// one derived from their module name. This decorator delegates every naming concern to the
/// provider strategy and only overrides the schema returned for the module's own name.
/// </remarks>
public sealed class SandboxTableNamingStrategy : ITableNamingStrategy
{
    private readonly ITableNamingStrategy _inner;
    private readonly string _moduleName;
    private readonly string _schema;

    /// <summary>
    /// Initializes a new instance of the <see cref="SandboxTableNamingStrategy"/> class.
    /// </summary>
    /// <param name="inner">The provider-specific naming strategy.</param>
    /// <param name="moduleName">The module name the context asks about (for example <c>example</c>).</param>
    /// <param name="schema">The provisioned schema for this module.</param>
    public SandboxTableNamingStrategy(ITableNamingStrategy inner, string moduleName, string schema)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);
        _moduleName = moduleName;
        _schema = schema;
    }

    /// <inheritdoc />
    public DatabaseProvider Provider => _inner.Provider;

    /// <inheritdoc />
    public string? GetSchemaForModule(string moduleName) =>
        string.Equals(moduleName, _moduleName, StringComparison.OrdinalIgnoreCase)
            ? _schema
            : _inner.GetSchemaForModule(moduleName);

    /// <inheritdoc />
    public string GetTableName(string entityName, string moduleName) =>
        _inner.GetTableName(entityName, moduleName);

    /// <inheritdoc />
    public string GetColumnName(string propertyName) => _inner.GetColumnName(propertyName);

    /// <inheritdoc />
    public string GetIndexName(string tableName, params string[] columnNames) =>
        _inner.GetIndexName(tableName, columnNames);

    /// <inheritdoc />
    public string GetForeignKeyName(string sourceTable, string targetTable, string columnName) =>
        _inner.GetForeignKeyName(sourceTable, targetTable, columnName);

    /// <inheritdoc />
    public string GetUniqueConstraintName(string tableName, params string[] columnNames) =>
        _inner.GetUniqueConstraintName(tableName, columnNames);
}
