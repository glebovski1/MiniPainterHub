# Architecture

## Overview
The solution follows a layered architecture with clear separation of concerns.

## Layers
- **Web/UI**: Hosts controllers, API endpoints, and user-facing interfaces.
- **Services**: Contains business logic and application workflows.
- **Data**: Contains EF Core context (`HobbyCenterContext`) and data access.
- **Common**: Shared models, utilities, and cross-cutting concerns.

## Dependencies
- Controllers should depend on services.
- Services can depend on `Data` and `Common`.
- Data should not depend on Web/UI.

## Guidance
- Always use the service layer when writing or modifying controller logic.
- Access EF Core via `HobbyCenterContext` in `Data/`.
