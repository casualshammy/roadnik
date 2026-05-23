# Room Management API

Base URL: `/api/v1`

All endpoints in this section require authentication via the `api-key` HTTP header. Requests without a valid key receive `403 Forbidden`.

---

## POST /register-room

Registers a new room or updates an existing room's configuration.

### Request

**Header**

| Name      | Required | Description              |
|-----------|----------|--------------------------|
| `api-key` | Yes      | Admin API key            |

**Body** — `application/json`

| Field                    | Type     | Required | Description                                                                 |
|--------------------------|----------|----------|-----------------------------------------------------------------------------|
| `roomId`                 | `string` | Yes      | Unique room identifier                                                      |
| `email`                  | `string` | Yes      | Contact email associated with the room                                      |
| `maxPathPoints`          | `uint`   | No       | Maximum number of path points stored per room. Overrides the server default |
| `maxPointsPerPath`       | `uint`   | No       | Maximum number of points per individual user path. `0` = no limit           |
| `maxPathPointAgeHours`   | `double` | No       | Maximum age of a path point in hours before it is purged                    |
| `minPathPointIntervalMs` | `uint`   | No       | Minimum interval between accepted path points (milliseconds)                |

**Example**

```json
{
  "roomId": "my-room",
  "email": "user@example.com",
  "maxPathPoints": 1000,
  "maxPointsPerPath": 500,
  "maxPathPointAgeHours": 72.0,
  "minPathPointIntervalMs": 5000
}
```

### Response

| Status | Description                        |
|--------|------------------------------------|
| `200`  | Room registered or updated         |
| `403`  | Missing or invalid `api-key`       |

---

## POST /unregister-room

Removes a room's registration. Path point data is not deleted.

### Request

**Header**

| Name      | Required | Description   |
|-----------|----------|---------------|
| `api-key` | Yes      | Admin API key |

**Body** — `application/json`

| Field    | Type     | Required | Description            |
|----------|----------|----------|------------------------|
| `roomId` | `string` | Yes      | Room identifier to remove |

**Example**

```json
{
  "roomId": "my-room"
}
```

### Response

| Status | Description                  |
|--------|------------------------------|
| `200`  | Room unregistered            |
| `403`  | Missing or invalid `api-key` |

---

## GET /list-registered-rooms

Returns a list of all registered rooms with their configurations.

### Request

**Header**

| Name      | Required | Description   |
|-----------|----------|---------------|
| `api-key` | Yes      | Admin API key |

### Response

| Status | Description                  |
|--------|------------------------------|
| `200`  | JSON array of room objects   |
| `403`  | Missing or invalid `api-key` |

**Body** — `application/json` — array of room objects

| Field                    | Type     | Nullable | Description                                              |
|--------------------------|----------|----------|----------------------------------------------------------|
| `roomId`                 | `string` | No       | Unique room identifier                                   |
| `email`                  | `string` | No       | Contact email associated with the room                   |
| `maxPathPoints`          | `uint`   | Yes      | Custom max path points limit, or `null` for server default |
| `maxPointsPerPath`       | `uint`   | Yes      | Custom max points per path, or `null` for server default |
| `maxPathPointAgeHours`   | `double` | Yes      | Custom path point max age in hours, or `null` for server default |
| `minPathPointIntervalMs` | `uint`   | Yes      | Custom minimum point interval in ms, or `null` for server default |

**Example**

```json
[
  {
    "roomId": "my-room",
    "email": "user@example.com",
    "maxPathPoints": 1000,
    "maxPointsPerPath": null,
    "maxPathPointAgeHours": 72.0,
    "minPathPointIntervalMs": 5000
  }
]
```
