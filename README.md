# MiniPainterHub

## Authentication API Notes

The authentication endpoints surface validation and credential errors through standard `ProblemDetails` payloads. Registration failures throw `DomainValidationException`, which is translated to a `400 Bad Request` response that includes an `errors` dictionary populated from ASP.NET Identity results. Login failures for bad credentials now raise `UnauthorizedAccessException`, producing a `401 Unauthorized` `ProblemDetails` response with a helpful error message.
