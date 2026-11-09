"""Restart evidence for the isolated Linux candidate; no Azure calls on import.

ARM acknowledgements and healthy HTTP responses alone are not restart evidence.
Only platform docker logs (not application stdout) establish container transitions.
"""
from dataclasses import dataclass
from datetime import datetime, timezone
import io
import re
import time
import zipfile

CANDIDATE = "rollandpaint-prod-ca"
LINE = re.compile(r"^(\d{4}-\d\d-\d\dT\S+Z) (.+)$")
START = re.compile(r"^Starting container: ([a-f0-9]+)_rollandpaint-prod-ca\.$")
STOP = re.compile(r"^Stopping container: ([a-f0-9]+)_rollandpaint-prod-ca\.$")


@dataclass(frozen=True)
class Event:
    at: datetime
    kind: str
    container: str


def platform_events(archive):
    """Read bounded archives in memory; never emit logs, application text or secrets."""
    if not isinstance(archive, bytes) or len(archive) > 32 * 1024 * 1024:
        raise RuntimeError("Platform log archive unavailable or too large")
    events = set()
    with zipfile.ZipFile(io.BytesIO(archive)) as bundle:
        entries = [x for x in bundle.infolist() if x.filename.endswith("_docker.log")
                   and not x.filename.endswith(("_default_docker.log", "_scm_docker.log"))]
        if sum(x.file_size for x in entries) > 64 * 1024 * 1024:
            raise RuntimeError("Platform logs exceed inspection limit")
        for entry in entries:
            container = stopping = None
            for line in bundle.read(entry).decode("utf-8", errors="replace").splitlines():
                match = LINE.match(line)
                if not match:
                    continue
                at = datetime.fromisoformat(match[1].replace("Z", "+00:00"))
                message = match[2]
                started, stopped = START.match(message), STOP.match(message)
                if started:
                    container = started[1]
                    events.add(Event(at, "started", container))
                elif stopped:
                    stopping = stopped[1]
                elif message.startswith("Container is terminated.") and stopping:
                    events.add(Event(at, "terminated", stopping))
                    stopping = None
                elif message.startswith("Site startup probe succeeded after ") and container:
                    events.add(Event(at, "ready", container))
    return sorted(events, key=lambda x: (x.at, x.kind, x.container))


def active_container(events):
    live = {}
    for event in events:
        if event.kind == "started":
            live[event.container] = False
        elif event.kind == "ready" and event.container in live:
            live[event.container] = True
        elif event.kind == "terminated":
            live.pop(event.container, None)
    if len(live) != 1 or not all(live.values()):
        raise RuntimeError("Expected exactly one ready application container")
    return next(iter(live))


def terminated(events, old, since):
    return any(e.container == old and e.kind == "terminated" and e.at >= since for e in events)


def replacement_ready(events, old, since):
    deaths = [e.at for e in events if e.container == old and e.kind == "terminated" and e.at >= since]
    if not deaths:
        return None
    for start in events:
        if start.kind != "started" or start.container == old or start.at <= min(deaths):
            continue
        if any(e.container == start.container and e.kind == "ready" and e.at > start.at for e in events):
            try:
                if active_container(events) == start.container:
                    return start.container
            except RuntimeError:
                pass
    return None


def wait_for(check, window_check, seconds, label, *, clock=time.monotonic, sleep=time.sleep):
    deadline = clock() + seconds
    while clock() < deadline:
        window_check()
        result = check()
        if result:
            return result
        sleep(5)
    raise RuntimeError("Timed out waiting for " + label)


def restart(old, *, stop, start, read_events, is_stopped, window_check,
            utcnow=lambda: datetime.now(timezone.utc), wait=wait_for):
    """Do not send start until the old container's termination is observed."""
    requested = utcnow()
    window_check()
    stop()
    wait(lambda: is_stopped() and terminated(read_events(), old, requested),
         window_check, 240, "old container termination")
    window_check()
    start()
    return wait(lambda: replacement_ready(read_events(), old, requested),
                window_check, 360, "different container startup probe")


def require_maintenance(response):
    """A platform 503 or login failure is not evidence of application maintenance."""
    try:
        body = response.json()
    except (ValueError, AttributeError):
        body = {}
    if response.status_code != 503 or body.get("title") != "Maintenance mode" or body.get("status") != 503:
        raise RuntimeError("Application maintenance response not established")
