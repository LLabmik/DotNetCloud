# DotNetCloud — Documentation Index

This folder holds every DotNetCloud document: reference material (architecture, API, admin, user
guides) and the implementation plans/records that track how each feature was built and verified.

New here? Read the [start-here table](#start-here) first; everything else is grouped by audience
below. The project-level [README](../README.md) covers the build/test quick start and
[CONTRIBUTING](../CONTRIBUTING.md) covers the contribution workflow.

> **Conventions**
>
> - Deliverable checklists use **✓** (done) and **☐** (pending) — never `[x]` / `[ ]`.
> - `*_PLAN.md` files are working documents: they describe intent **and** record what was actually
>   implemented, including live-verification evidence and known gaps.
> - Reference docs (`architecture/`, `api/`, `admin/`, `modules/`, `user/`, `security/`,
>   `development/`) describe how the system works **today**.
> - When a change lands, update [`IMPLEMENTATION_CHECKLIST.md`](IMPLEMENTATION_CHECKLIST.md) and
>   [`MASTER_PROJECT_PLAN.md`](MASTER_PROJECT_PLAN.md) with targeted edits.

---

## Start here

| Document | What it covers |
| --- | --- |
| [../README.md](../README.md) | Project overview, quick start, solution layout |
| [MASTER_PROJECT_PLAN.md](MASTER_PROJECT_PLAN.md) | Phase-by-phase plan, status table and per-step deliverables |
| [IMPLEMENTATION_CHECKLIST.md](IMPLEMENTATION_CHECKLIST.md) | Task checklist across all phases, plus "Recent Work/Fix/Feature" records |
| [architecture/ARCHITECTURE.md](architecture/ARCHITECTURE.md) | System design: process-isolated modules, gRPC, capability model, data layer |
| [development/README.md](development/README.md) | Development setup & workflow entry point |
| [admin/server/INSTALLATION.md](admin/server/INSTALLATION.md) | Installing a server |
| [user/README.md](user/README.md) | End-user documentation entry point |
| [api/README.md](api/README.md) | REST API reference entry point |

---

## Architecture

| Document | What it covers |
| --- | --- |
| [architecture/ARCHITECTURE.md](architecture/ARCHITECTURE.md) | Full system architecture (core supervisor, module hosts, storage) |
| [architecture/core-abstractions.md](architecture/core-abstractions.md) | `IModule`, capability tiers, event bus, `CallerContext` |
| [architecture/role-system.md](architecture/role-system.md) | Role and permission design |
| [architecture/chat-module.md](architecture/chat-module.md) | Chat module architecture |
| [architecture/observability.md](architecture/observability.md) | Logging, metrics, tracing, health checks |
| [architecture/internationalization.md](architecture/internationalization.md) | Localization (i18n) guide |
| [GRPC_MODULE_CONVERSION_PLAN.md](GRPC_MODULE_CONVERSION_PLAN.md) | How modules were moved to gRPC-only process isolation |
| [REQUIRED_MODULES_AND_SCHEMA_SEPARATION_PLAN.md](REQUIRED_MODULES_AND_SCHEMA_SEPARATION_PLAN.md) | Required vs optional modules; per-module schema separation |
| [THIRD_PARTY_MODULE_SANDBOX_AND_STORE_PLAN.md](THIRD_PARTY_MODULE_SANDBOX_AND_STORE_PLAN.md) | Sandboxed third-party modules and the module store |
| [modules/MODULE_UI_HOSTING.md](modules/MODULE_UI_HOSTING.md) | How module UIs are hosted (server-rendered and WASM frames) |

---

## Modules

### Files (core module)

| Document | What it covers |
| --- | --- |
| [modules/README.md](modules/README.md) | Files module overview |
| [modules/ARCHITECTURE.md](modules/ARCHITECTURE.md) | File storage, chunking, whole-file blobs |
| [modules/API.md](modules/API.md) | Files REST API |
| [modules/SHARING.md](modules/SHARING.md) | Sharing model (users, teams, permissions) |
| [modules/SYNC.md](modules/SYNC.md) | Desktop sync architecture & protocol |
| [modules/VERSIONING.md](modules/VERSIONING.md) | File versioning |
| [modules/WOPI.md](modules/WOPI.md) | WOPI and Collabora integration |

### Search, packaging and the store

| Document | What it covers |
| --- | --- |
| [modules/SEARCH.md](modules/SEARCH.md) | Search module (indexing, ranking, extraction) |
| [modules/MODULE_PACKAGING.md](modules/MODULE_PACKAGING.md) | `.dncpkg` package format and Ed25519 signing |
| [modules/MODULE_STORE.md](modules/MODULE_STORE.md) | Store catalogue, installer state machine, trust tiers |
| [modules/MODULE_SECURITY.md](modules/MODULE_SECURITY.md) | Module capability enforcement and trust boundaries |
| [modules/AUTO_UPDATES.md](modules/AUTO_UPDATES.md) | Auto-update mechanism |
| [guides/MODULE_DEVELOPMENT.md](guides/MODULE_DEVELOPMENT.md) | Building, testing and packaging a module |

### Chat and Tracks

| Document | What it covers |
| --- | --- |
| [modules/chat/README.md](modules/chat/README.md) | Chat module overview |
| [modules/chat/ARCHITECTURE.md](modules/chat/ARCHITECTURE.md) | Chat architecture |
| [modules/chat/API.md](modules/chat/API.md) | Chat REST API |
| [modules/chat/REALTIME.md](modules/chat/REALTIME.md) | SignalR real-time events |
| [modules/chat/PUSH.md](modules/chat/PUSH.md) | Push notifications |
| [modules/CHAT_TRACKS_INTEGRATION.md](modules/CHAT_TRACKS_INTEGRATION.md) | Chat ↔ Tracks real-time integration |
| [modules/tracks/README.md](modules/tracks/README.md) | Tracks (project management) module |
| [modules/tracks/API.md](modules/tracks/API.md) | Tracks REST API |
| [modules/tracks/USER_GUIDE.md](modules/tracks/USER_GUIDE.md) | Tracks user guide |
| [modules/AI_ASSISTANT.md](modules/AI_ASSISTANT.md) | AI assistant module |

---

## API reference

| Document | What it covers |
| --- | --- |
| [api/README.md](api/README.md) | API overview and conventions |
| [api/AUTHENTICATION.md](api/AUTHENTICATION.md) | Auth, tokens, scopes |
| [api/RESPONSE_FORMAT.md](api/RESPONSE_FORMAT.md) | Envelopes and pagination |
| [api/ERROR_HANDLING.md](api/ERROR_HANDLING.md) | Error codes and shapes |
| [api/CALENDAR.md](api/CALENDAR.md) | Calendar API |
| [api/CONTACTS.md](api/CONTACTS.md) | Contacts API |
| [api/NOTES.md](api/NOTES.md) | Notes API |
| [api/search.md](api/search.md) | Search API |

---

## Administration & operations

### Server

| Document | What it covers |
| --- | --- |
| [admin/server/INSTALLATION.md](admin/server/INSTALLATION.md) | Installation guide (Linux service, layout, hardening) |
| [admin/server/UPGRADING.md](admin/server/UPGRADING.md) | Upgrade procedure |
| [admin/server/CONFIGURATION.md](admin/server/CONFIGURATION.md) | Every configuration key |
| [admin/server/DOCKER_BEGINNER_GUIDE.md](admin/server/DOCKER_BEGINNER_GUIDE.md) | Docker deployment for newcomers |
| [admin/server/REVERSE_PROXY_BEGINNER_GUIDE.md](admin/server/REVERSE_PROXY_BEGINNER_GUIDE.md) | Reverse proxy in front of the server |
| [admin/server/WINDOWS_IIS_INSTALL_GUIDE.md](admin/server/WINDOWS_IIS_INSTALL_GUIDE.md) | Windows + IIS installation |
| [admin/server/WINDOWS_SERVICE_ARCHITECTURE_NOTES.md](admin/server/WINDOWS_SERVICE_ARCHITECTURE_NOTES.md) | Windows hosting architecture options |
| [admin/SITES.md](admin/SITES.md) | Static `www` and `modules` sites on IIS |
| [admin/BACKUP.md](admin/BACKUP.md) | Backup and restore procedures |
| [distribution-infrastructure/install-script-hosting.md](distribution-infrastructure/install-script-hosting.md) | How the install script is hosted |

### Module administration

| Document | What it covers |
| --- | --- |
| [admin/CONFIGURATION.md](admin/CONFIGURATION.md) | Files module configuration |
| [admin/COLLABORA.md](admin/COLLABORA.md) | Collabora CODE administration |
| [admin/CHAT.md](admin/CHAT.md) | Chat administration |
| [admin/VIDEO_CALLING.md](admin/VIDEO_CALLING.md) | Video calling (WebRTC/TURN) setup |
| [admin/PIM_MODULES.md](admin/PIM_MODULES.md) | Contacts, Calendar and Notes administration |
| [admin/PHASE_3_RELEASE_NOTES.md](admin/PHASE_3_RELEASE_NOTES.md) | PIM suite release notes |
| [admin/AI_ASSISTANT.md](admin/AI_ASSISTANT.md) | AI assistant configuration |
| [admin/GMAIL_OAUTH_SETUP.md](admin/GMAIL_OAUTH_SETUP.md) | Gmail OAuth for the Email module |
| [admin/SOC2_COMPLIANCE_ADMIN_GUIDE.md](admin/SOC2_COMPLIANCE_ADMIN_GUIDE.md) | Running the compliance controls day to day |

---

## User guides

| Document | What it covers |
| --- | --- |
| [user/README.md](user/README.md) | User documentation entry point |
| [user/GETTING_STARTED.md](user/GETTING_STARTED.md) | First steps with Files |
| [user/SYNC_CLIENT.md](user/SYNC_CLIENT.md) | Desktop sync client |
| [user/DOCUMENT_EDITING.md](user/DOCUMENT_EDITING.md) | Online document editing |
| [user/PHOTOS.md](user/PHOTOS.md) | Photos |
| [user/MUSIC.md](user/MUSIC.md) | Music |
| [user/VIDEO.md](user/VIDEO.md) | Video |
| [user/CALENDAR.md](user/CALENDAR.md) | Calendar |
| [user/CONTACTS.md](user/CONTACTS.md) | Contacts |
| [user/NOTES.md](user/NOTES.md) | Notes |
| [user/EMAIL.md](user/EMAIL.md) | Email |
| [user/CHAT.md](user/CHAT.md) | Chat |
| [user/VIDEO_CALLS.md](user/VIDEO_CALLS.md) | Video and audio calls |
| [user/TRACKS.md](user/TRACKS.md) | Tracks |
| [user/BOOKMARKS.md](user/BOOKMARKS.md) | Bookmarks |
| [user/BROWSER_EXTENSION.md](user/BROWSER_EXTENSION.md) | Bookmarks browser extension |
| [user/SEARCH.md](user/SEARCH.md) | Search |
| [user/AI_ASSISTANT.md](user/AI_ASSISTANT.md) | AI assistant |
| [user/ANDROID.md](user/ANDROID.md) | Android app |
| [user/AUTO_UPDATES.md](user/AUTO_UPDATES.md) | Client auto-updates |

---

## Clients

### Desktop

| Document | What it covers |
| --- | --- |
| [clients/desktop/README.md](clients/desktop/README.md) | Desktop client overview (Avalonia SyncTray) |
| [clients/desktop/SETUP.md](clients/desktop/SETUP.md) | Build and run the desktop client |
| [clients/desktop/SYNC_PROTOCOL.md](clients/desktop/SYNC_PROTOCOL.md) | Sync protocol details |
| [clients/desktop/TRAYSYNC_VERIFICATION_WALKTHROUGH.md](clients/desktop/TRAYSYNC_VERIFICATION_WALKTHROUGH.md) | Build, install and verify walkthrough |
| [clients/desktop/TROUBLESHOOTING.md](clients/desktop/TROUBLESHOOTING.md) | Desktop client troubleshooting |

### Android

| Document | What it covers |
| --- | --- |
| [clients/android/README.md](clients/android/README.md) | Android client overview |
| [clients/android/SETUP.md](clients/android/SETUP.md) | Android development environment |
| [clients/android/DISTRIBUTION.md](clients/android/DISTRIBUTION.md) | Packaging and distribution |

### Browser extension

| Document | What it covers |
| --- | --- |
| [clients/BROWSER_EXTENSION_INSTALLATION.md](clients/BROWSER_EXTENSION_INSTALLATION.md) | Installing the Bookmarks extension |

---

## Development

| Document | What it covers |
| --- | --- |
| [development/README.md](development/README.md) | Setup & workflow overview |
| [development/IDE_SETUP.md](development/IDE_SETUP.md) | IDE configuration |
| [development/DATABASE_SETUP.md](development/DATABASE_SETUP.md) | PostgreSQL / SQL Server setup |
| [development/DOCKER_SETUP.md](development/DOCKER_SETUP.md) | Docker-based development |
| [development/DEVELOPMENT_WORKFLOW.md](development/DEVELOPMENT_WORKFLOW.md) | Branching, reviews, commit rules |
| [development/RUNNING_TESTS.md](development/RUNNING_TESTS.md) | Running the test suites |
| [development/TEST_FILTERING.md](development/TEST_FILTERING.md) | Skipping integration tests in Test Explorer |
| [development/TESTING_PLAN.md](development/TESTING_PLAN.md) | Client testing on a Windows machine |
| [development/REVERSE_PROXY.md](development/REVERSE_PROXY.md) | Local reverse proxy configuration |
| [development/STATUS_BADGES.md](development/STATUS_BADGES.md) | CI status badges |
| [development/migration-verification-postgresql.md](development/migration-verification-postgresql.md) | Verifying EF Core migrations on PostgreSQL |
| [development/migration-initial-postgresql.sql](development/migration-initial-postgresql.sql) | Initial PostgreSQL schema script |
| [development/CLIENT_SERVER_MEDIATION_HANDOFF.md](development/CLIENT_SERVER_MEDIATION_HANDOFF.md) | Active cross-machine handoff (server ↔ clients) |
| [development/CLIENT_SERVER_MEDIATION_ARCHIVE.md](development/CLIENT_SERVER_MEDIATION_ARCHIVE.md) | Archived handoff history |

### Sync engineering

| Document | What it covers |
| --- | --- |
| [development/SYNC_FLOW_REFERENCE.md](development/SYNC_FLOW_REFERENCE.md) | End-to-end sync flow reference |
| [development/SYNC_IMPLEMENTATION_GUIDE.md](development/SYNC_IMPLEMENTATION_GUIDE.md) | Implementing sync changes |
| [development/SYNC_IMPROVEMENT_PLAN.md](development/SYNC_IMPROVEMENT_PLAN.md) | Sync improvement plan |
| [development/SYNC_IMPROVEMENT_PROPOSALS.md](development/SYNC_IMPROVEMENT_PROPOSALS.md) | Improvement proposals |
| [development/SYNC_REMEDIATION_PLAN.md](development/SYNC_REMEDIATION_PLAN.md) | Remediation plan |
| [development/SYNC_VERIFICATION_PLAN.md](development/SYNC_VERIFICATION_PLAN.md) | Verification plan and evidence |
| [development/SYNC_RECOVERY_PLAYBOOK.md](development/SYNC_RECOVERY_PLAYBOOK.md) | Recovering a broken sync state |
| [development/MULTI_CLIENT_SYNC_HARDENING_PLAN.md](development/MULTI_CLIENT_SYNC_HARDENING_PLAN.md) | Multi-client hardening |
| [development/PHASE_2_5_2_10_CLIENT_PLAN.md](development/PHASE_2_5_2_10_CLIENT_PLAN.md) | Phase 2.5–2.10 client implementation plan |
| [development/WINDOWS_INSTALLER_IMPROVEMENT_PLAN.md](development/WINDOWS_INSTALLER_IMPROVEMENT_PLAN.md) | Windows installer improvements |

---

## Security, privacy & compliance

| Document | What it covers |
| --- | --- |
| [security/SECURITY_MODEL.md](security/SECURITY_MODEL.md) | Threat model, trust boundaries, controls |
| [security/DEPLOYMENT_HARDENING.md](security/DEPLOYMENT_HARDENING.md) | Hardening checklist for deployments |
| [security/CROSS_MODULE_TRUST.md](security/CROSS_MODULE_TRUST.md) | Cross-module trust and event-subscription policy |
| [security/PII_INVENTORY.md](security/PII_INVENTORY.md) | PII inventory (SOC 2 privacy criteria P1–P8) |
| [security/SOC2_CONTROL_MATRIX.md](security/SOC2_CONTROL_MATRIX.md) | Trust Services Criteria → controls → evidence |
| [security/SOC2_AUDITOR_GUIDE.md](security/SOC2_AUDITOR_GUIDE.md) | Technical controls and evidence for auditors |
| [security/SOC2_TYPE_II_AUDITOR_REPORT.md](security/SOC2_TYPE_II_AUDITOR_REPORT.md) | Draft auditor report template |
| [security/VULNERABILITY_DISCLOSURE.md](security/VULNERABILITY_DISCLOSURE.md) | How to report a vulnerability |
| [SOC2_TYPE_II_COMPLIANCE_PLAN.md](SOC2_TYPE_II_COMPLIANCE_PLAN.md) | Compliance implementation plan |
| [SECURITY_REVIEW_PLAN.md](SECURITY_REVIEW_PLAN.md) | Security code-review plan |
| [SECURITY_REVIEW_FINDINGS.md](SECURITY_REVIEW_FINDINGS.md) | Security review findings |
| [SECURITY_AUDIT_REMEDIATION_PLAN.md](SECURITY_AUDIT_REMEDIATION_PLAN.md) | Audit remediation tracking |

---

## Implementation plans & records

Plans are grouped by theme. Each records status, deliverables (✓/☐), notes and verification
evidence; the master roll-up lives in [MASTER_PROJECT_PLAN.md](MASTER_PROJECT_PLAN.md).

### Foundation, phases and releases

| Document | What it covers |
| --- | --- |
| [REMAINING_WORK_PLAN.md](REMAINING_WORK_PLAN.md) | Phases 0–3 remaining work |
| [PHASE_0_2_12_COMPLETION_REPORT.md](PHASE_0_2_12_COMPLETION_REPORT.md) | Data-access layer tests |
| [PHASE_0_4_COMPLETION.md](PHASE_0_4_COMPLETION.md) | HTTP endpoints completion |
| [PHASE_1_COMPLETION_PLAN.md](PHASE_1_COMPLETION_PLAN.md) | Phase 1 (Files + sync) completion |
| [PHASE_1_REMAINING_WORK_PLAN.md](PHASE_1_REMAINING_WORK_PLAN.md) | Phase 1 remaining work |
| [PHASE_3_IMPLEMENTATION_PLAN.md](PHASE_3_IMPLEMENTATION_PLAN.md) | Phase 3 (PIM suite) |
| [PHASE_3_DEFERRED_PLAN.md](PHASE_3_DEFERRED_PLAN.md) | Phase 3 deferred items |
| [PHASE_4_IMPLEMENTATION_PLAN.md](PHASE_4_IMPLEMENTATION_PLAN.md) | Phase 4 (Tracks) |
| [PHASE_5_IMPLEMENTATION_PLAN.md](PHASE_5_IMPLEMENTATION_PLAN.md) | Phase 5 (Photos, Music, Video) |
| [PHASE_6_EMAIL_BOOKMARKS_IMPLEMENTATION_PLAN.md](PHASE_6_EMAIL_BOOKMARKS_IMPLEMENTATION_PLAN.md) | Phase 6 (Email, Bookmarks) |
| [PHASE_7_VIDEO_CALLS_PLAN.md](PHASE_7_VIDEO_CALLS_PLAN.md) | Phase 7 (WebRTC calls) |
| [PHASE_11_AUTO_UPDATES_PLAN.md](PHASE_11_AUTO_UPDATES_PLAN.md) | Phase 11 (auto-updates) |
| [PHASE_MOBILE_NOTIFICATIONS_PLAN.md](PHASE_MOBILE_NOTIFICATIONS_PLAN.md) | Mobile push notifications for Tracks |

### Platform, deployment and operations

| Document | What it covers |
| --- | --- |
| [MINT22_FRESH_DEPLOY_PLAN.md](MINT22_FRESH_DEPLOY_PLAN.md) | Fresh deploy via `deploy.sh` |
| [WS4_LIVE_VERIFICATION_EXECUTION_PLAN.md](WS4_LIVE_VERIFICATION_EXECUTION_PLAN.md) | Live verification execution plan |
| [DB_OUTAGE_RESILIENCE_PLAN.md](DB_OUTAGE_RESILIENCE_PLAN.md) | Surviving database/server outages |
| [MODULE_ERROR_RECOVERY_PLAN.md](MODULE_ERROR_RECOVERY_PLAN.md) | Module crash recovery |
| [REMOVE_INMEMORY_FROM_MODULES_PLAN.md](REMOVE_INMEMORY_FROM_MODULES_PLAN.md) | Removing EF InMemory from production code |
| [DI_REGISTRATION_PLAN.md](DI_REGISTRATION_PLAN.md) | Blazor component DI registration |
| [CLOSED_SYSTEM_MODE_PLAN.md](CLOSED_SYSTEM_MODE_PLAN.md) | Closed-system mode |
| [DEMO_MODE_PLAN.md](DEMO_MODE_PLAN.md) | Restricted demo accounts |
| [USERNAME_LOGIN_PLAN.md](USERNAME_LOGIN_PLAN.md) | Username-based login |
| [DEFAULT_USER_CONTENT_PLAN.md](DEFAULT_USER_CONTENT_PLAN.md) | Default user content (templates) |
| [MOVE_SEARCH_INTO_CORE_PLAN.md](MOVE_SEARCH_INTO_CORE_PLAN.md) | Search as a core capability |

### Files, sharing and storage

| Document | What it covers |
| --- | --- |
| [CHUNK_INTEGRITY_HARDENING_PLAN.md](CHUNK_INTEGRITY_HARDENING_PLAN.md) | Chunk integrity checks |
| [MEDIA_WHOLE_FILE_STORAGE_PLAN.md](MEDIA_WHOLE_FILE_STORAGE_PLAN.md) | Whole-file blobs for media |
| [MEDIA_CONTENT_DEDUPLICATION_PLAN.md](MEDIA_CONTENT_DEDUPLICATION_PLAN.md) | Canonical media entities and dedup |
| [SHARED_FILE_FOLDER_IMPLEMENTATION_PLAN.md](SHARED_FILE_FOLDER_IMPLEMENTATION_PLAN.md) | Shared folders |
| [SHARED_WITH_ME_VIRTUAL_FOLDERS_PLAN.md](SHARED_WITH_ME_VIRTUAL_FOLDERS_PLAN.md) | Shared-with-me virtual folders |
| [SHARE_DIALOG_UNIFICATION_AND_TEAM_SHARES_PLAN.md](SHARE_DIALOG_UNIFICATION_AND_TEAM_SHARES_PLAN.md) | Unified share dialogs and team shares |
| [FILES_MULTISELECT_CONTEXT_MENU_PLAN.md](FILES_MULTISELECT_CONTEXT_MENU_PLAN.md) | Multi-select context-menu actions |
| [VIRTUAL_FILE_SYNCING_PLAN.md](VIRTUAL_FILE_SYNCING_PLAN.md) | Files-on-demand for SyncTray |

### Media: Photos, Music and Video

| Document | What it covers |
| --- | --- |
| [MUSICBRAINZ_ENRICHMENT_PLAN.md](MUSICBRAINZ_ENRICHMENT_PLAN.md) | Music metadata enrichment and scan progress |
| [PERSISTENT_MUSIC_PLAYBAR_PLAN.md](PERSISTENT_MUSIC_PLAYBAR_PLAN.md) | Playbar that survives navigation |
| [BUTTERCHURN_VISUALIZER_INTEGRATION_PLAN.md](BUTTERCHURN_VISUALIZER_INTEGRATION_PLAN.md) | Music visualizer |
| [BUTTERCHURN_PRESET_AUTHORING.md](BUTTERCHURN_PRESET_AUTHORING.md) | Authoring visualizer presets |
| [BACKGROUND_BLUR_PLAN.md](BACKGROUND_BLUR_PLAN.md) | Video-call background blur |
| [VIDEO_TRANSCODING_PLAN.md](VIDEO_TRANSCODING_PLAN.md) | Transcoding for HTML5 playback |
| [VIDEO_TRANSCODE_SEEK_PLAN.md](VIDEO_TRANSCODE_SEEK_PLAN.md) | Seek across the full transcoded timeline |
| [VIDEO_PLAYER_REBUILD_PLAN.md](VIDEO_PLAYER_REBUILD_PLAN.md) | Player rebuild |
| [VIDEO_ENRICHMENT_BUGFIX_CONTEXT.md](VIDEO_ENRICHMENT_BUGFIX_CONTEXT.md) | Enrichment bugfix context |

### Tracks (project management)

| Document | What it covers |
| --- | --- |
| [TRACKS_MODULE_FINISH_PLAN.md](TRACKS_MODULE_FINISH_PLAN.md) | Finishing the module |
| [TRACKS_COMPREHENSIVE_FEATURE_ANALYSIS.md](TRACKS_COMPREHENSIVE_FEATURE_ANALYSIS.md) | Professional feature analysis |
| [TRACKS_PROFESSIONAL_GAP_ANALYSIS.md](TRACKS_PROFESSIONAL_GAP_ANALYSIS.md) | Gap analysis vs commercial tools |
| [TRACKS_PROFESSIONALIZATION_PLAN.md](TRACKS_PROFESSIONALIZATION_PLAN.md) | Professionalization plan |
| [TRACKS_REMAINING_GAPS_PLAN.md](TRACKS_REMAINING_GAPS_PLAN.md) | Remaining gaps |
| [TRACKS_DUAL_MODE_REWORK_PLAN.md](TRACKS_DUAL_MODE_REWORK_PLAN.md) | Dual-mode rework |
| [TRACKS_HIERARCHY_EXPANSION.md](TRACKS_HIERARCHY_EXPANSION.md) | Hierarchy expansion |
| [TRACKS_HIERARCHY_CLARITY_PLAN.md](TRACKS_HIERARCHY_CLARITY_PLAN.md) | Hierarchy clarity and creation wizards |
| [SPRINT_PLANNING_WORKFLOW_PLAN.md](SPRINT_PLANNING_WORKFLOW_PLAN.md) | Sprint planning workflow |

### Chat, notifications and presence

| Document | What it covers |
| --- | --- |
| [CHAT_ADMIN_SETTINGS_PLAN.md](CHAT_ADMIN_SETTINGS_PLAN.md) | Limits, retention and archiving |
| [CHAT_TYPING_INDICATOR_PLAN.md](CHAT_TYPING_INDICATOR_PLAN.md) | Typing indicator (web + Android) |
| [LINK_PREVIEW_PLAN.md](LINK_PREVIEW_PLAN.md) | Link previews |
| [DIRECT_MESSAGING_AND_HOST_CALLS_PLAN.md](DIRECT_MESSAGING_AND_HOST_CALLS_PLAN.md) | Direct messages and calls |
| [PRESENCE_DOTS_4STATE_PLAN.md](PRESENCE_DOTS_4STATE_PLAN.md) | Four-state presence indicators |
| [NOTIFICATION_BELL_READSTATE_AND_REALTIME_PLAN.md](NOTIFICATION_BELL_READSTATE_AND_REALTIME_PLAN.md) | Notification bell read state |
| [NOTIFICATION_FANOUT_UNIFICATION_PLAN.md](NOTIFICATION_FANOUT_UNIFICATION_PLAN.md) | Unified notification fan-out |

### PIM: Calendar, Contacts, Notes, Email and Bookmarks

| Document | What it covers |
| --- | --- |
| [CALENDAR_RECURRENCE_ORG_PLAN.md](CALENDAR_RECURRENCE_ORG_PLAN.md) | Calendar recurrence and organization support |
| [NOTES_REQUIRED_MODULE_PLAN.md](NOTES_REQUIRED_MODULE_PLAN.md) | Notes as a required module |
| [EMAIL_ATTACHMENTS_PLAN.md](EMAIL_ATTACHMENTS_PLAN.md) | Attachments (inbound + outbound) |
| [EMAIL_BROWSE_FILES_PLAN.md](EMAIL_BROWSE_FILES_PLAN.md) | Browse-files in email compose |
| [BROWSER_EXTENSION_BOOKMARKS_PLAN.md](BROWSER_EXTENSION_BOOKMARKS_PLAN.md) | Bookmarks browser extension |

### Android client

| Document | What it covers |
| --- | --- |
| [ANDROID_MUSIC_TAB_PLAN.md](ANDROID_MUSIC_TAB_PLAN.md) | Music tab |
| [ANDROID_MUSIC_SEARCH_PLAN.md](ANDROID_MUSIC_SEARCH_PLAN.md) | Music title-bar search |
| [ANDROID_NOTES_TAB_PLAN.md](ANDROID_NOTES_TAB_PLAN.md) | Notes tab |
| [ANDROID_CALENDAR_TAB_PLAN.md](ANDROID_CALENDAR_TAB_PLAN.md) | Calendar tab |
| [ANDROID_AI_TAB_PLAN.md](ANDROID_AI_TAB_PLAN.md) | AI tab |
| [ANDROID_COLLABORA_EDIT_PLAN.md](ANDROID_COLLABORA_EDIT_PLAN.md) | Collabora editing in Files |
| [ANDROID_MEDIA_AUTOUPLOAD_REDESIGN_PLAN.md](ANDROID_MEDIA_AUTOUPLOAD_REDESIGN_PLAN.md) | Media auto-upload redesign |
| [ANDROID_CHAT_BACKGROUND_ALERTS_PLAN.md](ANDROID_CHAT_BACKGROUND_ALERTS_PLAN.md) | Background chat alerts |
| [ANDROID_CHAT_ALERT_SOUND_PLAN.md](ANDROID_CHAT_ALERT_SOUND_PLAN.md) | Chat alert sound |
| [ANDROID_CHAT_MUTE_PLAN.md](ANDROID_CHAT_MUTE_PLAN.md) | Channel mute and suppression |
| [ANDROID_CHAT_PRESENCE_DOTS_PLAN.md](ANDROID_CHAT_PRESENCE_DOTS_PLAN.md) | Presence dots in DMs |

### Desktop sync (SyncTray) and sync hardening

| Document | What it covers |
| --- | --- |
| [SYNCSERVICE_SYNCTRAY_MERGE_PLAN.md](SYNCSERVICE_SYNCTRAY_MERGE_PLAN.md) | Merging SyncService into SyncTray |
| [SYNCTRAY_MULTI_FOLDER_SYNC_PLAN.md](SYNCTRAY_MULTI_FOLDER_SYNC_PLAN.md) | Multiple local sync folders |
| [SYNCTRAY_FOLDER_MANAGEMENT_PLAN.md](SYNCTRAY_FOLDER_MANAGEMENT_PLAN.md) | Managing subscribed folders |
| [SYNCTRAY_ADD_FOLDER_CREATE_REMOTE_PLAN.md](SYNCTRAY_ADD_FOLDER_CREATE_REMOTE_PLAN.md) | Creating the remote folder on add |
| [SYNCTRAY_TRANSFER_SIZE_AND_PENDING_COUNTS_PLAN.md](SYNCTRAY_TRANSFER_SIZE_AND_PENDING_COUNTS_PLAN.md) | Transfer size and pending counts |
| [SYNCTRAY_PERFORMANCE_IMPROVEMENTS.md](SYNCTRAY_PERFORMANCE_IMPROVEMENTS.md) | Performance work |
| [SYNCTRAY_ICON_ENHANCEMENT_PLAN.md](SYNCTRAY_ICON_ENHANCEMENT_PLAN.md) | Tray icon states |

### Admin UI, widgets and help

| Document | What it covers |
| --- | --- |
| [ADMIN_SYSTEM_PAGE_PLAN.md](ADMIN_SYSTEM_PAGE_PLAN.md) | Admin system page |
| [ADMIN_SYSTEM_PAGE_PER_USER_TRAFFIC_PLAN.md](ADMIN_SYSTEM_PAGE_PER_USER_TRAFFIC_PLAN.md) | Per-user live traffic |
| [ADMIN_BROADCAST_PLAN.md](ADMIN_BROADCAST_PLAN.md) | Admin broadcast messages |
| [ADMIN_SHARE_DELETION_CLEANUP_PLAN.md](ADMIN_SHARE_DELETION_CLEANUP_PLAN.md) | Share deletion cleanup reporting |
| [HOME_WIDGET_CUSTOMIZATION_PLAN.md](HOME_WIDGET_CUSTOMIZATION_PLAN.md) | Home widget customization |
| [MODULE_WIDGETS_PLAN.md](MODULE_WIDGETS_PLAN.md) | Module home widgets |
| [MODULE_HELP_SIDEBAR_PLAN.md](MODULE_HELP_SIDEBAR_PLAN.md) | Module help sidebar and help pages |
| [FORM_ENTER_SUBMIT_PLAN.md](FORM_ENTER_SUBMIT_PLAN.md) | Enter-to-submit form defaults |
| [BLAZOR_CODE_STANDARDS_PLAN.md](BLAZOR_CODE_STANDARDS_PLAN.md) | Code/style separation enforcement |

### Search and AI

| Document | What it covers |
| --- | --- |
| [FULL_TEXT_SEARCH_IMPLEMENTATION_PLAN.md](FULL_TEXT_SEARCH_IMPLEMENTATION_PLAN.md) | Full-text search |
| [SEARCH_REALTIME_INDEXING_BRIDGE_PLAN.md](SEARCH_REALTIME_INDEXING_BRIDGE_PLAN.md) | Real-time indexing bridge |
| [AI_REQUEST_QUEUEING_PLAN.md](AI_REQUEST_QUEUEING_PLAN.md) | AI request queueing |

### Code quality and testing

| Document | What it covers |
| --- | --- |
| [CODE_REVIEW_PLAN.md](CODE_REVIEW_PLAN.md) | Base + module code review plan |
| [CODE_REVIEW_FINDINGS.md](CODE_REVIEW_FINDINGS.md) | Code review findings |
| [TEST_TAUTOLOGY_AUDIT_PLAN.md](TEST_TAUTOLOGY_AUDIT_PLAN.md) | Audit for tests that cannot fail |
| [TEST_TAUTOLOGY_FINDINGS.md](TEST_TAUTOLOGY_FINDINGS.md) | Tautology audit findings |

---

## Document conventions & maintenance

- **Formatting:** GitHub-flavoured Markdown. Use `✓` / `☐` in checklists, fenced code blocks with a
  language tag, and relative links between documents.
- **Plans:** every plan carries `Status`, `Deliverables` (✓/☐) and `Notes` per step, and records the
  verification evidence that closed it (unit counts, deploy summary, `/health/ready` result).
- **Accuracy first:** a plan is a living record — when reality changes, correct the plan rather than
  leaving a stale claim in place.
- **Private topology:** committed documents must not name servers, hostnames or addresses of a
  specific deployment; describe the code and the evidence instead.
- **Adding a document:** put reference material in the matching folder and add a row to the relevant
  table above; working plans live at the root of `docs/` until they are fully absorbed.

[`LICENSE`](LICENSE) covers the documentation in this folder.
