# PII Inventory (SOC 2 Privacy — P1–P8)

**Owner:** engineering / privacy
**Last updated:** 2026-08-18
**Companion docs:** `docs/security/SOC2_CONTROL_MATRIX.md`, `docs/security/SOC2_TYPE_II_AUDITOR_REPORT.md`, `docs/admin/SOC2_COMPLIANCE_ADMIN_GUIDE.md`.

This inventory lists the personal data (PII) the DotNetCloud platform stores, where it lives, why it is kept, how long it is kept, whether it is encrypted, and how it is disposed. It is generated from the compliance scanner's PII check (criterion P6) plus a manual review of the entity/DTO/model layer.

---

## 1. Categories of personal data

| #   | Category         | Examples                                                           | Where stored (source of truth)                                                               |
| --- | ---------------- | ------------------------------------------------------------------ | -------------------------------------------------------------------------------------------- |
| 1   | Account identity | Email, display name, phone, birth date, locale, timezone           | `ApplicationUser` (`[core].[AspNetUsers]`), `UserDevice`                                     |
| 2   | Authentication   | Password hash, MFA/TOTP secret, backup-code hashes, passkeys       | `ApplicationUser` password hash, `UserBackupCode`, `FidoCredential` (Identity / auth tables) |
| 3   | Contacts         | Contact email, phone, address, birthday, notes, avatar             | Contacts module (`contacts` schema)                                                          |
| 4   | Calendar         | Event titles/descriptions/locations (may contain personal data)    | Calendar module (`calendar` schema)                                                          |
| 5   | Chat messages    | Message content, attachments, mentions (may contain personal data) | Chat module (`chat` schema)                                                                  |
| 6   | Files & media    | User file names, contents, thumbnails, EXIF metadata               | Files module (`files` schema) + object storage; Photos module (`photos` schema)              |
| 7   | Geolocation      | Photo EXIF GPS latitude/longitude                                  | Photos module (`photos` schema) via `ExifMetadataExtractor`                                  |
| 8   | Email accounts   | IMAP/SMTP credentials (encrypted), email bodies/attachments        | Email module (`email` schema), `EmailCredentialEncryptionService`                            |
| 9   | Device & session | Device tokens, push tokens, IP-derived metadata                    | `UserDevice` (`[core].[UserDevices]`)                                                        |
| 10  | Audit trail      | Caller user ID + roles on audited actions (not message content)    | `[core].[AuditLogs]`                                                                         |

---

## 2. Field-level inventory (PII-bearing fields)

Generated from the scanner's P6 check (`Entities/`, `DTOs/`, `Models/`). The table records **purpose**, **retention**, **encryption-at-rest**, and **disposal** for each sensitive field class.

| Entity / area                       | PII field(s)                                       | Purpose                                        | Retention                                                    | Encryption at rest                                    | Disposal                                                         |
| ----------------------------------- | -------------------------------------------------- | ---------------------------------------------- | ------------------------------------------------------------ | ----------------------------------------------------- | ---------------------------------------------------------------- |
| `ApplicationUser`                   | `Email`, `PhoneNumber`, `BirthDate`, `DisplayName` | Identity, login, profile display               | Account lifetime + configurable (admin delete)               | No (hashed pwd)                                       | `UserManagementController.DeleteUserAsync` + data-subject delete |
| `ApplicationUser`                   | `PasswordHash`                                     | Authentication                                 | Account lifetime                                             | Yes (PBKDF2 hash)                                     | Account deletion                                                 |
| `UserBackupCode` / `FidoCredential` | Backup code hashes, passkey data                   | Account recovery / passwordless auth           | Account lifetime                                             | Yes (SHA-256 hash)                                    | Account deletion                                                 |
| `UserDevice`                        | Device token, push token                           | Push notifications, device management          | Account lifetime / device removal                            | Partially                                             | Device removal endpoint + account deletion                       |
| Contacts `Contact`                  | `Email`, `Phone`, `Address`, `Birthday`            | Contact directory, share, vCard/CardDAV export | User-controlled; deleted with contact or account             | No                                                    | `DeleteAsync` in Contacts module + account deletion              |
| Calendar `CalendarEvent`            | Title, description, location                       | Scheduling                                     | User-controlled; deleted with event or account               | No                                                    | Event delete + account deletion                                  |
| Chat `Message`                      | Message content, attachments                       | Communication                                  | User-controlled; message retention per module settings       | No (in transit TLS)                                   | Message delete + account deletion                                |
| Files `FileNode`                    | File name, content                                 | Storage and sync                               | User-controlled; trash retention (`core.TrashRetentionDays`) | No (at-rest storage)                                  | Trash purge + account deletion                                   |
| Photos `Photo` / `PhotoMetadata`    | EXIF GPS `Latitude`/`Longitude`                    | Photo library and geolocation features         | User-controlled                                              | No                                                    | Photo delete + account deletion                                  |
| Email `EmailAccount`                | Email address, credentials                         | Email access                                   | User-controlled                                              | Yes (`EmailCredentialEncryptionService`, AES-256-GCM) | Account delete                                                   |
| Audit `AuditLog`                    | `CallerUserId`, `CallerRoles`                      | Attributable audit trail (SOC 2 CC4)           | `core.AuditLogRetentionDays` (default 365 days)              | No                                                    | `AuditLogPurgeHostedService` daily purge                         |

---

## 3. Retention, disposal & data-subject rights

### Retention

- **Audit trail:** purged daily by `AuditLogPurgeHostedService` per `core.AuditLogRetentionDays` (default 365).
- **Soft-deleted user data (trash):** kept per `core.TrashRetentionDays` (default 30) before permanent purge.
- **Accounts:** retained until the account is deleted by an administrator or by a data-subject deletion request.

### Disposal procedure

1. **Data-subject delete:** administrator (or the user) deletes the account via
   `UserManagementController.DeleteUserAsync` (audited). This soft-deletes identity rows and
   flags module data for the trash window.
2. **Module data:** contacts/calendar/notes/files/email rows are deleted per module delete
   flows; trash is permanently purged after `core.TrashRetentionDays`.
3. **Audit purge:** `AuditLogPurgeHostedService` removes audit rows older than the retention
   window **daily**; the purge count is logged (the purge itself is auditable).
4. **Backup tapes:** encrypted backups (see `docs/admin/BACKUP.md`) are the only place data
   persists past deletion; backup retention is configured by the operator.

### Data-subject requests (DSAR)

- **Export:** the operator uses the per-module export/download capabilities (vCard/CardDAV for
  contacts, iCal for calendar, file download for files) to assemble a user's data bundle. The
  export actions are audited via `IAuditLogger`.
- **Delete:** the data-subject delete path above, audited.
- **Consent / notice:** the operator maintains the privacy notice (placeholder template —
  legal text is operator-supplied, not provided here).
- The admin guide (§7.4) documents the end-to-end DSAR procedure for operators.

> **Note:** A centralized `DataSubjectController` that aggregates export/delete across all
> modules is planned future work. Today, DSARs are fulfilled via the per-module flows above and
> are documented in `docs/admin/SOC2_COMPLIANCE_ADMIN_GUIDE.md`.

---

## 4. Data flows that handle PII

| Flow             | PII involved           | Control                                                                                                   |
| ---------------- | ---------------------- | --------------------------------------------------------------------------------------------------------- |
| Login / MFA      | Email, password, TOTP  | TLS in transit; PBKDF2 password hash; TOTP secret; login events audited (`login-success`, `login-failed`) |
| Contacts CardDAV | Contact PII            | TLS in transit; export/import audited (`Export`/`Import`)                                                 |
| Calendar CalDAV  | Event PII              | TLS in transit; export/import audited                                                                     |
| Chat             | Message content        | TLS in transit; `HtmlSanitizer` on rendered HTML; message ops audited                                     |
| Photos EXIF      | GPS geolocation        | EXIF extracted into `PhotoMetadata`; `ExifMetadataExtractor` strips/stores GPS deliberately               |
| Email            | Email credentials/body | Credentials AES-256-GCM encrypted; TLS to mail servers gated by config                                    |
| Audit            | Caller id + roles      | Audit rows contain no message content or email bodies; purged on retention                                |

---

## 5. References

- Scanner P6 check: `scripts/soc2-compliance-scan.sh` (check 10).
- `docs/security/SECURITY_MODEL.md` — security model.
- `docs/security/SOC2_CONTROL_MATRIX.md` — criterion → control → evidence mapping (P1–P8).
- `docs/admin/SOC2_COMPLIANCE_ADMIN_GUIDE.md` §7 — retention + DSAR operations.
