# Tracks Module — REST API Reference

> Base URL: `/api/v1`
> Authentication: Bearer token required on all endpoints (`[Authorize]`)

---

## Table of Contents

- [Boards](#boards)
- [Swimlanes](#swimlanes)
- [Cards](#cards)
- [Comments](#comments)
- [Checklists](#checklists)
- [Labels](#labels)
- [Sprints](#sprints)
- [Dependencies](#dependencies)
- [Time Entries](#time-entries)
- [Planning Poker](#planning-poker)
- [Attachments](#attachments)
- [Teams](#teams)
- [Board Templates](#board-templates)
- [Card Templates](#card-templates)
- [Bulk Operations](#bulk-operations)
- [Analytics](#analytics)
- [DTOs Reference](#dtos-reference)

---

## Boards

### List Boards

```
GET /api/v1/boards
```

Returns all boards the authenticated user has access to.

**Response:** `200 OK` — `BoardDto[]`

---

### Get Board

```
GET /api/v1/boards/{boardId}
```

**Response:** `200 OK` — `BoardDto`

---

### Create Board

```
POST /api/v1/boards
```

**Request Body:** `CreateBoardDto`

```json
{
  "title": "Sprint Board",
  "description": "Main development board",
  "color": "#3B82F6",
  "teamId": "00000000-0000-0000-0000-000000000000"
}
```

**Response:** `201 Created` — `BoardDto`

---

### Update Board

```
PUT /api/v1/boards/{boardId}
```

**Request Body:** `UpdateBoardDto`

```json
{
  "title": "Updated Title",
  "description": "Updated description",
  "color": "#EF4444",
  "isArchived": false
}
```

**Response:** `200 OK` — `BoardDto`

---

### Delete Board

```
DELETE /api/v1/boards/{boardId}
```

Soft-deletes the board. Requires Owner role.

**Response:** `204 No Content`

---

### Transfer Board

```
POST /api/v1/boards/{boardId}/transfer
```

Transfer board to a different team (or remove team association).

**Request Body:** `TransferBoardDto`

```json
{
  "teamId": "00000000-0000-0000-0000-000000000000"
}
```

**Response:** `200 OK`

---

### Get Board Activity

```
GET /api/v1/boards/{boardId}/activity
```

**Response:** `200 OK` — `BoardActivityDto[]`

---

### List Board Members

```
GET /api/v1/boards/{boardId}/members
```

**Response:** `200 OK` — `BoardMemberDto[]`

---

### Add Board Member

```
POST /api/v1/boards/{boardId}/members
```

**Request Body:** `AddBoardMemberRequest`

```json
{
  "userId": "00000000-0000-0000-0000-000000000000",
  "role": "Member"
}
```

**Response:** `201 Created`

---

### Remove Board Member

```
DELETE /api/v1/boards/{boardId}/members/{userId}
```

**Response:** `204 No Content`

---

### Update Member Role

```
PUT /api/v1/boards/{boardId}/members/{userId}/role
```

**Request Body:** `UpdateMemberRoleRequest`

```json
{
  "role": "Admin"
}
```

**Response:** `200 OK`

---

### List Board Labels

```
GET /api/v1/boards/{boardId}/labels
```

**Response:** `200 OK` — `LabelDto[]`

---

### Create Label

```
POST /api/v1/boards/{boardId}/labels
```

**Request Body:** `CreateLabelDto`

```json
{
  "title": "Bug",
  "color": "#EF4444"
}
```

**Response:** `201 Created` — `LabelDto`

---

### Update Label

```
PUT /api/v1/boards/{boardId}/labels/{labelId}
```

**Request Body:** `UpdateLabelDto`

```json
{
  "title": "Critical Bug",
  "color": "#DC2626"
}
```

**Response:** `200 OK` — `LabelDto`

---

### Delete Label

```
DELETE /api/v1/boards/{boardId}/labels/{labelId}
```

**Response:** `204 No Content`

---

### Export Board

```
GET /api/v1/boards/{boardId}/export
```

Exports the board as JSON including swimlanes, cards, labels, and members.

**Response:** `200 OK` — JSON export object

---

### Import Board

```
POST /api/v1/boards/import
```

Creates a new board from an exported JSON structure.

**Request Body:** Board export JSON

**Response:** `201 Created` — `BoardDto`

---

## Swimlanes

### List Swimlanes

```
GET /api/v1/boards/{boardId}/swimlanes
```

**Response:** `200 OK` — `BoardSwimlaneDto[]`

---

### Create Swimlane

```
POST /api/v1/boards/{boardId}/swimlanes
```

**Request Body:** `CreateBoardSwimlaneDto`

```json
{
  "title": "In Progress",
  "color": "#F59E0B",
  "cardLimit": 5
}
```

**Response:** `201 Created` — `BoardSwimlaneDto`

---

### Update Swimlane

```
PUT /api/v1/boards/{boardId}/swimlanes/{swimlaneId}
```

**Request Body:** `UpdateBoardSwimlaneDto`

```json
{
  "title": "Done",
  "color": "#10B981",
  "cardLimit": null
}
```

**Response:** `200 OK` — `BoardSwimlaneDto`

---

### Delete Swimlane

```
DELETE /api/v1/boards/{boardId}/swimlanes/{swimlaneId}
```

**Response:** `204 No Content`

---

### Reorder Swimlanes

```
PUT /api/v1/boards/{boardId}/swimlanes/reorder
```

**Request Body:** `ReorderSwimlanesRequest`

```json
{
  "swimlaneIds": [
    "11111111-1111-1111-1111-111111111111",
    "22222222-2222-2222-2222-222222222222",
    "33333333-3333-3333-3333-333333333333"
  ]
}
```

**Response:** `200 OK`

---

## Cards

### List Cards

```
GET /api/v1/swimlanes/{swimlaneId}/cards
```

**Response:** `200 OK` — `CardDto[]`

---

### Get Card

```
GET /api/v1/cards/{cardId}
```

**Response:** `200 OK` — `CardDto`

---

### Create Card

```
POST /api/v1/swimlanes/{swimlaneId}/cards
```

**Request Body:** `CreateCardDto`

```json
{
  "title": "Implement authentication",
  "description": "Add JWT-based auth to the API",
  "priority": "High",
  "dueDate": "2025-02-15T00:00:00Z",
  "storyPoints": 5,
  "assigneeIds": ["00000000-0000-0000-0000-000000000000"],
  "labelIds": ["00000000-0000-0000-0000-000000000001"]
}
```

**Response:** `201 Created` — `CardDto`

---

### Update Card

```
PUT /api/v1/cards/{cardId}
```

**Request Body:** `UpdateCardDto`

```json
{
  "title": "Updated title",
  "description": "Updated description",
  "priority": "Urgent",
  "dueDate": "2025-02-20T00:00:00Z",
  "storyPoints": 8,
  "isArchived": false
}
```

**Response:** `200 OK` — `CardDto`

---

### Delete Card

```
DELETE /api/v1/cards/{cardId}
```

Soft-deletes the card.

**Response:** `204 No Content`

---

### Move Card

```
PUT /api/v1/cards/{cardId}/move
```

**Request Body:** `MoveCardDto`

```json
{
  "targetSwimlaneId": "22222222-2222-2222-2222-222222222222",
  "position": 10000
}
```

**Response:** `200 OK` — `CardDto`

---

### Assign Card

```
POST /api/v1/cards/{cardId}/assign
```

**Request Body:** `CardAssignRequest`

```json
{
  "userId": "00000000-0000-0000-0000-000000000000"
}
```

**Response:** `200 OK`

---

### Unassign Card

```
DELETE /api/v1/cards/{cardId}/assign/{userId}
```

**Response:** `204 No Content`

---

### Add Label to Card

```
POST /api/v1/cards/{cardId}/labels
```

**Request Body:** `CardLabelRequest`

```json
{
  "labelId": "00000000-0000-0000-0000-000000000000"
}
```

**Response:** `200 OK`

---

### Remove Label from Card

```
DELETE /api/v1/cards/{cardId}/labels/{labelId}
```

**Response:** `204 No Content`

---

### Get Card Activity

```
GET /api/v1/cards/{cardId}/activity
```

**Response:** `200 OK` — `BoardActivityDto[]`

---

## Comments

### List Comments

```
GET /api/v1/cards/{cardId}/comments
```

**Response:** `200 OK` — `CardCommentDto[]`

---

### Create Comment

```
POST /api/v1/cards/{cardId}/comments
```

**Request Body:** `CreateCommentRequest`

```json
{
  "content": "Looks good, ready for review."
}
```

**Response:** `201 Created` — `CardCommentDto`

---

### Update Comment

```
PUT /api/v1/cards/{cardId}/comments/{commentId}
```

**Request Body:** `UpdateCommentRequest`

```json
{
  "content": "Updated comment content."
}
```

**Response:** `200 OK` — `CardCommentDto`

---

### Delete Comment

```
DELETE /api/v1/cards/{cardId}/comments/{commentId}
```

**Response:** `204 No Content`

---

## Checklists

### List Checklists

```
GET /api/v1/cards/{cardId}/checklists
```

**Response:** `200 OK` — `CardChecklistDto[]`

---

### Create Checklist

```
POST /api/v1/cards/{cardId}/checklists
```

**Request Body:** `CreateChecklistRequest`

```json
{
  "title": "Acceptance Criteria"
}
```

**Response:** `201 Created` — `CardChecklistDto`

---

### Delete Checklist

```
DELETE /api/v1/cards/{cardId}/checklists/{checklistId}
```

**Response:** `204 No Content`

---

### Add Checklist Item

```
POST /api/v1/cards/{cardId}/checklists/{checklistId}/items
```

**Request Body:** `CreateChecklistItemRequest`

```json
{
  "title": "Verify login flow"
}
```

**Response:** `201 Created` — `ChecklistItemDto`

---

### Toggle Checklist Item

```
PUT /api/v1/cards/{cardId}/checklists/{checklistId}/items/{itemId}/toggle
```

Toggles the `isCompleted` state of the item.

**Response:** `200 OK` — `ChecklistItemDto`

---

### Delete Checklist Item

```
DELETE /api/v1/cards/{cardId}/checklists/{checklistId}/items/{itemId}
```

**Response:** `204 No Content`

---

## Labels

Board-level label management is under the [Boards](#boards) section (`/api/v1/boards/{boardId}/labels`).

Card-level label assignment is under the [Cards](#cards) section (`/api/v1/cards/{cardId}/labels`).

---

## Sprints

### List Sprints

```
GET /api/v1/boards/{boardId}/sprints
```

**Response:** `200 OK` — `SprintDto[]`

---

### Get Sprint

```
GET /api/v1/boards/{boardId}/sprints/{sprintId}
```

**Response:** `200 OK` — `SprintDto`

---

### Create Sprint

```
POST /api/v1/boards/{boardId}/sprints
```

**Request Body:** `CreateSprintDto`

```json
{
  "title": "Sprint 1",
  "goal": "Complete authentication module",
  "startDate": "2025-02-01T00:00:00Z",
  "endDate": "2025-02-14T00:00:00Z"
}
```

**Response:** `201 Created` — `SprintDto`

---

### Update Sprint

```
PUT /api/v1/boards/{boardId}/sprints/{sprintId}
```

**Request Body:** `UpdateSprintDto`

```json
{
  "title": "Sprint 1 (Extended)",
  "goal": "Updated goal",
  "endDate": "2025-02-21T00:00:00Z"
}
```

**Response:** `200 OK` — `SprintDto`

---

### Delete Sprint

```
DELETE /api/v1/boards/{boardId}/sprints/{sprintId}
```

**Response:** `204 No Content`

---

### Start Sprint

```
POST /api/v1/boards/{boardId}/sprints/{sprintId}/start
```

Transitions the sprint from `Planning` to `Active`. Only one sprint can be active per board.

**Response:** `200 OK` — `SprintDto`

---

### Complete Sprint

```
POST /api/v1/boards/{boardId}/sprints/{sprintId}/complete
```

Transitions the sprint from `Active` to `Completed`.

**Response:** `200 OK` — `SprintDto`

---

### Add Card to Sprint

```
POST /api/v1/boards/{boardId}/sprints/{sprintId}/cards/{cardId}
```

**Response:** `200 OK`

---

### Remove Card from Sprint

```
DELETE /api/v1/boards/{boardId}/sprints/{sprintId}/cards/{cardId}
```

**Response:** `204 No Content`

---

## Dependencies

### List Dependencies

```
GET /api/v1/cards/{cardId}/dependencies
```

**Response:** `200 OK` — `CardDependencyDto[]`

---

### Add Dependency

```
POST /api/v1/cards/{cardId}/dependencies
```

**Request Body:** `AddDependencyRequest`

```json
{
  "dependsOnCardId": "00000000-0000-0000-0000-000000000000",
  "type": "BlockedBy"
}
```

**Response:** `201 Created`

Fails with `400 Bad Request` if the dependency would create a circular chain.

---

### Remove Dependency

```
DELETE /api/v1/cards/{cardId}/dependencies/{dependsOnCardId}
```

**Response:** `204 No Content`

---

## Time Entries

### List Time Entries

```
GET /api/v1/cards/{cardId}/time-entries
```

**Response:** `200 OK` — `TimeEntryDto[]`

---

### Add Time Entry

```
POST /api/v1/cards/{cardId}/time-entries
```

**Request Body:** `CreateTimeEntryDto`

```json
{
  "startTime": "2025-02-01T09:00:00Z",
  "endTime": "2025-02-01T11:30:00Z",
  "durationMinutes": 150,
  "description": "Implementation work"
}
```

**Response:** `201 Created` — `TimeEntryDto`

---

### Delete Time Entry

```
DELETE /api/v1/cards/{cardId}/time-entries/{entryId}
```

**Response:** `204 No Content`

---

### Start Timer

```
POST /api/v1/cards/{cardId}/timer/start
```

Starts a live timer for the authenticated user. Only one active timer per user.

**Response:** `200 OK` — `TimeEntryDto`

---

### Stop Timer

```
POST /api/v1/cards/{cardId}/timer/stop
```

Stops the active timer and records the elapsed time as an entry.

**Response:** `200 OK` — `TimeEntryDto`

---

## Planning Poker

### Get Card Poker Sessions

```
GET /api/v1/cards/{cardId}/poker
```

**Response:** `200 OK` — `PokerSessionDto[]`

---

### Start Poker Session

```
POST /api/v1/cards/{cardId}/poker
```

**Request Body:** `CreatePokerSessionDto`

```json
{
  "scale": "Fibonacci",
  "customScaleValues": null
}
```

**Poker Scales:** `Fibonacci`, `TShirt`, `PowersOfTwo`, `Custom`

**Response:** `201 Created` — `PokerSessionDto`

---

### Get Session

```
GET /api/v1/poker/{sessionId}
```

**Response:** `200 OK` — `PokerSessionDto`

Votes are only visible when status is `Revealed` or `Completed`.

---

### Submit Vote

```
POST /api/v1/poker/{sessionId}/vote
```

**Request Body:** `SubmitPokerVoteDto`

```json
{
  "estimate": "5"
}
```

**Response:** `200 OK`

---

### Reveal Votes

```
POST /api/v1/poker/{sessionId}/reveal
```

Makes all votes visible. Transitions status from `Voting` to `Revealed`.

**Response:** `200 OK` — `PokerSessionDto`

---

### Accept Estimate

```
POST /api/v1/poker/{sessionId}/accept
```

**Request Body:** `AcceptPokerEstimateDto`

```json
{
  "acceptedEstimate": "5",
  "storyPoints": 5
}
```

The accepted story points are written to the card.

**Response:** `200 OK` — `PokerSessionDto`

---

### Start New Round

```
POST /api/v1/poker/{sessionId}/new-round
```

Resets votes and increments the round counter for re-estimation.

**Response:** `200 OK` — `PokerSessionDto`

---

## Attachments

### List Attachments

```
GET /api/v1/cards/{cardId}/attachments
```

**Response:** `200 OK` — `CardAttachmentDto[]`

---

### Add Attachment

```
POST /api/v1/cards/{cardId}/attachments
```

**Request Body:** `AddAttachmentRequest`

```json
{
  "fileName": "design-mockup.png",
  "fileNodeId": "00000000-0000-0000-0000-000000000000",
  "url": null
}
```

Either `fileNodeId` (from Files module) or `url` (external link) should be provided.

**Response:** `201 Created` — `CardAttachmentDto`

---

### Delete Attachment

```
DELETE /api/v1/cards/{cardId}/attachments/{attachmentId}
```

**Response:** `204 No Content`

---

## Teams

### List Teams

```
GET /api/v1/teams
```

Returns teams the authenticated user is a member of.

**Response:** `200 OK` — `TracksTeamDto[]`

---

### Get Team

```
GET /api/v1/teams/{teamId}
```

**Response:** `200 OK` — `TracksTeamDto`

---

### Create Team

```
POST /api/v1/teams
```

**Request Body:** `CreateTracksTeamDto`

```json
{
  "name": "Engineering",
  "description": "Core engineering team",
  "organizationId": null
}
```

**Response:** `201 Created` — `TracksTeamDto`

---

### Update Team

```
PUT /api/v1/teams/{teamId}
```

**Request Body:** `UpdateTracksTeamDto`

```json
{
  "name": "Engineering Platform",
  "description": "Updated description"
}
```

**Response:** `200 OK` — `TracksTeamDto`

---

### Delete Team

```
DELETE /api/v1/teams/{teamId}
```

**Response:** `204 No Content`

---

### List Team Members

```
GET /api/v1/teams/{teamId}/members
```

**Response:** `200 OK` — `TracksTeamMemberDto[]`

---

### Add Team Member

```
POST /api/v1/teams/{teamId}/members
```

**Request Body:** `AddTracksTeamMemberRequest`

```json
{
  "userId": "00000000-0000-0000-0000-000000000000",
  "role": "Member"
}
```

**Response:** `201 Created`

---

### Remove Team Member

```
DELETE /api/v1/teams/{teamId}/members/{userId}
```

**Response:** `204 No Content`

---

### Update Team Member Role

```
PUT /api/v1/teams/{teamId}/members/{userId}/role
```

**Request Body:** `UpdateTracksTeamMemberRoleRequest`

```json
{
  "role": "Manager"
}
```

**Response:** `200 OK`

---

### List Team Boards

```
GET /api/v1/teams/{teamId}/boards
```

**Response:** `200 OK` — `BoardDto[]`

---

## Board Templates

### List Templates

```
GET /api/v1/tracks/board-templates
```

**Response:** `200 OK` — `BoardTemplateDto[]`

---

### Get Template

```
GET /api/v1/tracks/board-templates/{templateId}
```

**Response:** `200 OK` — `BoardTemplateDto`

---

### Use Template (Create Board)

```
POST /api/v1/tracks/board-templates/{templateId}/use
```

Creates a new board from the template.

**Request Body:** `CreateBoardFromTemplateDto`

```json
{
  "title": "New Sprint Board",
  "description": "Created from Agile template",
  "teamId": null,
  "color": "#8B5CF6"
}
```

**Response:** `201 Created` — `BoardDto`

---

### Save Board as Template

```
POST /api/v1/tracks/board-templates/from-board/{boardId}
```

**Request Body:** `SaveBoardAsTemplateDto`

```json
{
  "name": "Agile Sprint Template",
  "description": "Standard sprint board layout",
  "category": "Agile"
}
```

**Response:** `201 Created` — `BoardTemplateDto`

---

### Delete Template

```
DELETE /api/v1/tracks/board-templates/{templateId}
```

**Response:** `204 No Content`

---

## Card Templates

### List Card Templates

```
GET /api/v1/boards/{boardId}/card-templates
```

**Response:** `200 OK` — `CardTemplateDto[]`

---

### Get Card Template

```
GET /api/v1/boards/{boardId}/card-templates/{templateId}
```

**Response:** `200 OK` — `CardTemplateDto`

---

### Save Card as Template

```
POST /api/v1/boards/{boardId}/card-templates/from-card/{cardId}
```

**Request Body:** `SaveCardAsTemplateDto`

```json
{
  "name": "Bug Report Template",
  "includeChecklists": true,
  "includeLabels": true
}
```

**Response:** `201 Created` — `CardTemplateDto`

---

### Delete Card Template

```
DELETE /api/v1/boards/{boardId}/card-templates/{templateId}
```

**Response:** `204 No Content`

---

## Bulk Operations

All bulk operations require Admin or Owner role on the board.

### Bulk Move Cards

```
POST /api/v1/boards/{boardId}/bulk/cards/move
```

**Request Body:** `BulkMoveCardsDto`

```json
{
  "cardIds": [
    "11111111-1111-1111-1111-111111111111",
    "22222222-2222-2222-2222-222222222222"
  ],
  "targetSwimlaneId": "33333333-3333-3333-3333-333333333333"
}
```

**Response:** `200 OK` — `BulkOperationResultDto`

---

### Bulk Assign Cards

```
POST /api/v1/boards/{boardId}/bulk/cards/assign
```

**Request Body:** `BulkAssignCardsDto`

```json
{
  "cardIds": ["..."],
  "userId": "00000000-0000-0000-0000-000000000000"
}
```

**Response:** `200 OK` — `BulkOperationResultDto`

---

### Bulk Label Cards

```
POST /api/v1/boards/{boardId}/bulk/cards/label
```

**Request Body:** `BulkLabelCardsDto`

```json
{
  "cardIds": ["..."],
  "labelId": "00000000-0000-0000-0000-000000000000"
}
```

**Response:** `200 OK` — `BulkOperationResultDto`

---

### Bulk Archive Cards

```
POST /api/v1/boards/{boardId}/bulk/cards/archive
```

**Request Body:** `BulkCardOperationDto`

```json
{
  "cardIds": ["..."]
}
```

**Response:** `200 OK` — `BulkOperationResultDto`

---

## Analytics

### Board Analytics

```
GET /api/v1/boards/{boardId}/analytics
```

**Response:** `200 OK` — `BoardAnalyticsDto`

```json
{
  "boardId": "...",
  "totalCards": 45,
  "completedCards": 28,
  "overdueCards": 3,
  "averageCycleTimeHours": 18.5,
  "cardsByList": { "To Do": 10, "In Progress": 7, "Done": 28 },
  "cardsByAssignee": { "user-guid": 12 },
  "completionsOverTime": [
    { "date": "2025-02-01", "count": 5 }
  ]
}
```

---

### Team Analytics

```
GET /api/v1/teams/{teamId}/analytics
```

**Response:** `200 OK` — `TeamAnalyticsDto`

---

### Sprint Report

```
GET /api/v1/sprints/{sprintId}/report
```

**Response:** `200 OK` — `SprintReportDto`

```json
{
  "sprintId": "...",
  "title": "Sprint 1",
  "status": "Completed",
  "totalCards": 15,
  "completedCards": 12,
  "totalPoints": 42,
  "completedPoints": 35,
  "burndownData": [
    { "date": "2025-02-01", "remainingPoints": 42, "idealPoints": 42 },
    { "date": "2025-02-07", "remainingPoints": 25, "idealPoints": 21 }
  ]
}
```

---

### Board Velocity

```
GET /api/v1/boards/{boardId}/velocity
```

**Response:** `200 OK` — `SprintVelocityDto[]`

```json
[
  {
    "sprintId": "...",
    "title": "Sprint 1",
    "committedPoints": 42,
    "completedPoints": 35,
    "committedCards": 15,
    "completedCards": 12
  }
]
```

---

## DTOs Reference

### Enums

| Enum | Values |
|---|---|
| `BoardMemberRole` | `Viewer`, `Member`, `Admin`, `Owner` |
| `CardPriority` | `None`, `Low`, `Medium`, `High`, `Urgent` |
| `CardDependencyType` | `BlockedBy`, `RelatesTo` |
| `SprintStatus` | `Planning`, `Active`, `Completed` |
| `PokerSessionStatus` | `Voting`, `Revealed`, `Completed`, `Cancelled` |
| `PokerScale` | `Fibonacci`, `TShirt`, `PowersOfTwo`, `Custom` |
| `TracksTeamMemberRole` | `Member`, `Manager`, `Owner` |

### Error Responses

All endpoints may return:

| Status | Meaning |
|---|---|
| `400 Bad Request` | Invalid input (validation errors, circular dependency, etc.) |
| `401 Unauthorized` | Missing or invalid bearer token |
| `403 Forbidden` | Insufficient role for the operation |
| `404 Not Found` | Board, card, sprint, or other resource not found |
| `409 Conflict` | Concurrent modification (ETag mismatch) |

Error bodies follow the standard ASP.NET Core `ProblemDetails` format:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Bad Request",
  "status": 400,
  "detail": "Circular dependency detected.",
  "traceId": "00-abcdef..."
}
```
