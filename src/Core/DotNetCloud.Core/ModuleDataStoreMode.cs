// This enum is a module *manifest* concept (manifest.dataStore), so it lives in DotNetCloud.Core — the
// assembly the module SDK, an out-of-tree module, the packer and the persisted EF entities can all
// reference. It used to live in DotNetCloud.Core.Data, which Core cannot reference.
//
// The namespace is retained so the entities in DotNetCloud.Core.Data and every existing call site kept
// their usings; do not infer the assembly from the namespace.
namespace DotNetCloud.Core.Data.Entities.Modules;

/// <summary>How a module stores its own data.</summary>
public enum ModuleDataStoreMode
{
    /// <summary>The module has no database; it persists through module settings and files.</summary>
    None,

    /// <summary>The module owns a dedicated schema and database principal.</summary>
    Isolated,
}
