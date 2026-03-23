# Contacts API Reference

> **Base URL:** `/api/v1/contacts`  
> **Authentication:** Bearer token (OpenIddict)  
> **Response Format:** Standard envelope (see [RESPONSE_FORMAT.md](RESPONSE_FORMAT.md))

---

## REST Endpoints

### List Contacts

```
GET /api/v1/contacts?search={query}&skip={n}&take={n}
```

| Parameter | Type | Default | Description |
|---|---|---|---|
| `search` | string | — | Filter by name, email, phone, or organization |
| `skip` | int | 0 | Pagination offset |
| `take` | int | 50 | Page size |

**Response:** Paginated array of `ContactDto`.

---

### Get Contact

```
GET /api/v1/contacts/{contactId}
```

| Parameter | Type | Description |
|---|---|---|
| `contactId` | GUID | Contact identifier |

**Response:** `ContactDto`

**Errors:** `404` CONTACT_NOT_FOUND

---

### Create Contact

```
POST /api/v1/contacts
```

**Request Body:** `CreateContactDto`

```json
{
  "contactType": "Person",
  "displayName": "Jane Smith",
  "firstName": "Jane",
  "lastName": "Smith",
  "organization": "Acme Corp",
  "department": "Engineering",
  "jobTitle": "Lead Developer",
  "notes": "Met at conference",
  "birthday": "1990-06-15",
  "websiteUrl": "https://example.com",
  "emails": [
    { "address": "jane@acme.com", "label": "Work", "isPrimary": true }
  ],
  "phoneNumbers": [
    { "number": "+15551234567", "label": "Mobile", "isPrimary": true }
  ],
  "addresses": [
    {
      "label": "Work",
      "street": "123 Main St",
      "city": "Portland",
      "region": "OR",
      "postalCode": "97201",
      "country": "US",
      "isPrimary": true
    }
  ],
  "customFields": {
    "twitter": "@janesmith"
  }
}
```

**Required Fields:** `contactType`, `displayName`

**Contact Types:** `Person`, `Organization`, `Group`

**Response:** `201` with created `ContactDto`

---

### Update Contact

```
PUT /api/v1/contacts/{contactId}
```

**Request Body:** `UpdateContactDto` — all fields optional (patch semantics). Only provided fields are updated.

**Response:** Updated `ContactDto`

**Errors:** `404` CONTACT_NOT_FOUND, `403` insufficient permissions

---

### Delete Contact

```
DELETE /api/v1/contacts/{contactId}
```

Soft-deletes the contact. Only the owner can delete.

**Response:** `204` No Content

**Errors:** `404` CONTACT_NOT_FOUND, `403` not the owner

---

## Groups

### List Groups

```
GET /api/v1/contacts/groups
```

**Response:** Array of `ContactGroupDto`

---

### Get Group

```
GET /api/v1/contacts/groups/{groupId}
```

**Response:** `ContactGroupDto`

```json
{
  "id": "...",
  "ownerId": "...",
  "name": "Engineering Team",
  "memberCount": 12,
  "createdAt": "2026-01-15T10:00:00Z",
  "updatedAt": "2026-03-01T14:30:00Z"
}
```

---

### Create Group

```
POST /api/v1/contacts/groups
```

**Request Body:**

```json
{
  "name": "Engineering Team"
}
```

**Response:** `201` with created `ContactGroupDto`

---

### Rename Group

```
PUT /api/v1/contacts/groups/{groupId}
```

**Request Body:**

```json
{
  "name": "New Group Name"
}
```

**Response:** Updated `ContactGroupDto`

---

### Delete Group

```
DELETE /api/v1/contacts/groups/{groupId}
```

Deletes the group. Contacts in the group are not deleted.

**Response:** `204` No Content

---

### Add Contact to Group

```
POST /api/v1/contacts/groups/{groupId}/members/{contactId}
```

**Response:** `204` No Content

---

### Remove Contact from Group

```
DELETE /api/v1/contacts/groups/{groupId}/members/{contactId}
```

**Response:** `204` No Content

---

### List Group Members

```
GET /api/v1/contacts/groups/{groupId}/members
```

**Response:** Array of `ContactDto`

---

## Sharing

### List Shares

```
GET /api/v1/contacts/{contactId}/shares
```

**Response:** Array of `ContactShare`

---

### Share Contact

```
POST /api/v1/contacts/{contactId}/shares
```

**Request Body:**

```json
{
  "userId": "...",
  "teamId": null,
  "permission": "ReadOnly"
}
```

Provide either `userId` or `teamId`, not both.

**Permissions:** `ReadOnly`, `ReadWrite`

**Response:** `201` with created share

**Errors:** `403` not the owner

---

### Remove Share

```
DELETE /api/v1/contacts/shares/{shareId}
```

**Response:** `204` No Content

---

## Avatars & Attachments

### Upload Avatar

```
PUT /api/v1/contacts/{contactId}/avatar
```

**Content-Type:** `multipart/form-data`

| Parameter | Type | Description |
|---|---|---|
| `file` | IFormFile | Image file (JPEG, PNG, GIF, WebP, SVG) |

**Constraints:**
- Maximum file size: 5 MB
- Allowed types: `image/jpeg`, `image/png`, `image/gif`, `image/webp`, `image/svg+xml`

**Response:** Updated `ContactDto` with `avatarUrl` populated

**Errors:** `400` VALIDATION_ERROR (invalid type/size), `404` CONTACT_NOT_FOUND, `403` not the owner

---

### Download Avatar

```
GET /api/v1/contacts/{contactId}/avatar
```

**Response:** Binary image data with appropriate `Content-Type` header

**Errors:** `404` if contact has no avatar

---

### Delete Avatar

```
DELETE /api/v1/contacts/{contactId}/avatar
```

**Response:** `204` No Content

---

### List Attachments

```
GET /api/v1/contacts/{contactId}/attachments
```

**Response:** Array of `ContactAttachmentDto`

---

### Upload Attachment

```
POST /api/v1/contacts/{contactId}/attachments?description={text}
```

**Content-Type:** `multipart/form-data`

| Parameter | Type | Description |
|---|---|---|
| `file` | IFormFile | Any file |
| `description` | string (query) | Optional description |

**Constraints:**
- Maximum file size: 25 MB

**Response:** `201` with created `ContactAttachmentDto`

---

### Download Attachment

```
GET /api/v1/contacts/attachments/{attachmentId}
```

**Response:** Binary file data with appropriate `Content-Type` and `Content-Disposition` headers

**Errors:** `404` attachment not found

---

### Delete Attachment

```
DELETE /api/v1/contacts/attachments/{attachmentId}
```

**Response:** `204` No Content

**Errors:** `404` attachment not found, `403` not the owner

---

## vCard Import / Export

### Export Single Contact as vCard

```
GET /api/v1/contacts/{contactId}/vcard
```

**Response:** `text/vcard` content (vCard 3.0)

```
BEGIN:VCARD
VERSION:3.0
FN:Jane Smith
N:Smith;Jane;;;
ORG:Acme Corp
EMAIL;TYPE=WORK:jane@acme.com
TEL;TYPE=CELL:+15551234567
PHOTO;ENCODING=b;TYPE=JPEG:<base64-encoded-image-data>
END:VCARD
```

---

### Export All Contacts as vCard

```
GET /api/v1/contacts/export
```

**Response:** `text/vcard` with all contacts concatenated

---

### Import Contacts from vCard

```
POST /api/v1/contacts/import
```

**Request Body:** Raw vCard text (`text/plain` or `text/vcard`)

**Response:** Array of created contact GUIDs

**Note:** If a vCard contains a `PHOTO` property with base64-encoded image data, the avatar is automatically saved to the contact.

---

## ContactDto Schema

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "ownerId": "...",
  "contactType": "Person",
  "displayName": "Jane Smith",
  "firstName": "Jane",
  "lastName": "Smith",
  "middleName": null,
  "prefix": null,
  "suffix": null,
  "phoneticName": null,
  "nickname": null,
  "organization": "Acme Corp",
  "department": "Engineering",
  "jobTitle": "Lead Developer",
  "avatarUrl": null,
  "notes": "Met at conference",
  "birthday": "1990-06-15",
  "anniversary": null,
  "websiteUrl": "https://example.com",
  "etag": "\"a1b2c3d4\"",
  "isDeleted": false,
  "createdAt": "2026-03-01T10:00:00Z",
  "updatedAt": "2026-03-15T14:30:00Z",
  "emails": [
    { "address": "jane@acme.com", "label": "Work", "isPrimary": true }
  ],
  "phoneNumbers": [
    { "number": "+15551234567", "label": "Mobile", "isPrimary": true }
  ],
  "addresses": [
    {
      "label": "Work",
      "street": "123 Main St",
      "city": "Portland",
      "region": "OR",
      "postalCode": "97201",
      "country": "US",
      "isPrimary": true
    }
  ],
  "groupIds": ["..."],
  "customFields": {
    "twitter": "@janesmith"
  },
  "attachments": [
    {
      "id": "...",
      "contactId": "...",
      "fileName": "resume.pdf",
      "contentType": "application/pdf",
      "fileSizeBytes": 204800,
      "isAvatar": false,
      "description": "Latest resume",
      "createdAt": "2026-03-20T10:00:00Z",
      "updatedAt": "2026-03-20T10:00:00Z"
    }
  ]
}
```

---

## Error Codes

| Code | HTTP | Description |
|---|---|---|
| `CONTACT_NOT_FOUND` | 404 | Contact does not exist or is not accessible |
| `CONTACT_ALREADY_EXISTS` | 409 | Duplicate contact detected |
| `CONTACT_GROUP_NOT_FOUND` | 404 | Group does not exist |
| `CONTACT_INVALID_EMAIL` | 400 | Invalid email format |
| `CONTACT_SHARE_NOT_FOUND` | 404 | Share does not exist |
| `VALIDATION_ERROR` | 400 | Invalid file type, size, or missing data |

---

## CardDAV Endpoints

CardDAV (RFC 6352) endpoints for external client synchronization.

### Discovery

```
GET /.well-known/carddav
```

Returns `301` redirect to the user's address book collection.

### DAV Capabilities

```
OPTIONS /carddav
OPTIONS /carddav/{path}
```

**Response Headers:**

```
DAV: 1, addressbook
Allow: OPTIONS, GET, PUT, DELETE, PROPFIND, REPORT
```

### Address Book Discovery (PROPFIND)

```
PROPFIND /carddav/
PROPFIND /carddav/{userId}
PROPFIND /carddav/{userId}/addressbook
```

Returns XML with address book properties and contact listing.

### Get Contact (vCard)

```
GET /carddav/{userId}/addressbook/{contactId}.vcf
```

**Response:** `text/vcard` with `ETag` header

### Create / Update Contact

```
PUT /carddav/{userId}/addressbook/{contactId}.vcf
```

**Request Body:** vCard 3.0 text

**Headers:**
- `If-Match: "etag"` — update existing (412 on mismatch)
- Omit `If-Match` — create new

**Response:** `201` Created or `204` No Content with updated `ETag`

### Delete Contact

```
DELETE /carddav/{userId}/addressbook/{contactId}.vcf
```

**Response:** `204` No Content
