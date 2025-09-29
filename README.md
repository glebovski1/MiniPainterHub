# MiniPainterHub

## Authentication API Notes

The authentication endpoints surface validation and credential errors through standard `ProblemDetails` payloads. Registration failures throw `DomainValidationException`, which is translated to a `400 Bad Request` response that includes an `errors` dictionary populated from ASP.NET Identity results. Login failures for bad credentials now raise `UnauthorizedAccessException`, producing a `401 Unauthorized` `ProblemDetails` response with a helpful error message.

## Comments API Notes

Requesting a comment that does not exist (for example, `GET /api/comments/{id}`) now surfaces a consistent `ProblemDetails` payload. The global exception handler translates the service's `NotFoundException` into a `404 Not Found` response with the title `"Not found"` and a `detail` of `"Comment not found."`, allowing clients to rely on the standardized error contract when comments have been deleted or never existed.
