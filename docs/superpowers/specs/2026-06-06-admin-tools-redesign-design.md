# Admin Tools Redesign Design

## Purpose

Redesign MiniPainterHub admin tools around fast, low-friction operations. The admin should be able to review recent content, react during spam or abuse events, and monitor live site health without typing ids or navigating through many disconnected pages.

## Approved Page Structure

The admin area has three primary pages:

- Inbox
- Control Center
- Dashboard

The existing Audit Log remains a supporting page linked from admin navigation, but it is not one of the three main work surfaces.

## Inbox

Inbox is the default admin page and the most important implementation target.

The Inbox combines posts, comments, and reported items into one dense review stream. It should support fast filtering by time window, item type, status, and search text. The first useful filters are:

- Last 1 hour, 2 hours, 24 hours, and 7 days
- Posts only, comments only, or both
- Active, reported, hidden, or all states
- Author, content text, tag, or report reason search

The core layout is a dense table plus one inspector panel. Rows show time, type, author, content summary, report signal, status, and direct actions. Selecting a row opens the inspector with richer context, preview, author history, report details, and action buttons.

Primary Inbox actions:

- Hide content
- Restore content
- Mark reviewed or OK
- Suspend author
- Open target page
- Open audit history for the item

The Inbox must not require manually typing post ids, comment ids, or user ids for normal moderation work.

## Control Center

Control Center is intentionally simple for the first implementation. It should expose only critical switches needed during spam, abuse, or maintenance events.

Critical controls:

- Pause the public website while allowing admin bypass
- Disable new posts and image uploads
- Disable new comments
- Disable new registrations

Each control change requires:

- Current state
- Expiry or duration
- Reason
- Actor
- Audit entry

Non-critical controls, presets, direct-message controls, and complex policy configuration are deferred.

## Dashboard

Dashboard is a live site statistics page. It should not become a second moderation queue and should not duplicate the Inbox workflow.

Dashboard should show:

- Live update state
- Current active sessions
- Page views
- New posts
- New comments
- New registrations
- API success or error rate
- Simple site activity trend
- Basic traffic source split
- System health indicators such as response time, SignalR connections, image upload queue, and background jobs

Dashboard implementation can be handled as its own stage after the Inbox and initial Control Center are stable.

## Interaction Principles

- Keep the Inbox dense and operational, not card-heavy.
- Use one selected-item inspector instead of opening many tabs or modal layers.
- Prefer one-click row actions for obvious admin decisions.
- Require reason and confirmation for destructive or site-wide actions.
- Keep dashboard metrics actionable but visually quiet.
- Keep emergency controls separate from daily review, but one click away.

## Implementation Order

1. Inbox shell and unified content stream.
2. Inbox filters and selected-item inspector.
3. Inbox actions wired to moderation/report APIs.
4. Minimal Control Center with critical switches and audit logging.
5. Dashboard live statistics page.

## Open Design Notes

- Dashboard visual polish should be handled during implementation after real available metrics are known.
- The first Inbox implementation can start with table and inspector behavior before advanced statistics.
- If site-wide controls require new backend policy checks, implement the policy layer before exposing switches in UI.
