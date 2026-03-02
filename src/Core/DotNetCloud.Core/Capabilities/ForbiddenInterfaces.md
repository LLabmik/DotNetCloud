# Forbidden Interfaces

Forbidden tier capabilities are strictly prohibited and should never be exposed to any module. These represent system-critical operations that bypass all security checks.

## List of Forbidden Interfaces

### Database Access (Direct)
- Direct DbContext access
- Raw SQL query execution
- Database schema modification
- Transaction management outside of module boundaries

### Cryptography & Secrets
- Master encryption key access
- Token signing key access
- System certificate store access
- Password hashing algorithm modification

### Core Infrastructure
- Service container modification
- Dependency injection container access
- Application shutdown/restart
- Process management (beyond module lifecycle)

### Authentication & Authorization (Bypass)
- Token validation bypass
- Permission check bypass
- Role assignment without audit
- User impersonation

### Audit & Compliance
- Audit log tampering
- Compliance flag modification
- Security policy bypass
- Encryption policy modification

### Module System (Core)
- Module loading/unloading without validation
- Capability grant/revocation without approval
- Module manifest modification
- System module replacement

### Multi-Tenancy (If Implemented)
- Organization switching without authorization
- Tenant isolation bypass
- Tenant data visibility bypass

## Rationale

These interfaces are forbidden because:

1. **Security**: Exposing them would create major security vulnerabilities
2. **Data Integrity**: Unauthorized access could corrupt critical system data
3. **Audit Trail**: Bypassing audit mechanisms would violate compliance requirements
4. **System Stability**: Direct manipulation could destabilize the entire platform
5. **Multi-Tenancy**: Bypassing isolation could cause data leaks between organizations

## Enforcement

- The capability system validates all capability requests
- Attempts to use forbidden interfaces are logged as security events
- Modules requesting forbidden capabilities are refused initialization
- System administrators cannot grant forbidden capabilities
- Forbidden interfaces have no public implementations

## Future Review

This list should be reviewed during:
- Security audits
- Major version updates
- New module integration requirements
- Compliance requirement changes
