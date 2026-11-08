import io
import unittest
import zipfile
from datetime import datetime, timedelta, timezone
from types import SimpleNamespace

from rehearsal_restart import (Event, active_container, platform_events, replacement_ready,
                               require_maintenance, restart, wait_for)

T = datetime(2026, 11, 7, 4, 40, tzinfo=timezone.utc)


class RestartTests(unittest.TestCase):
    def test_old_worker_readiness_never_counts_as_restart(self):
        events = [Event(T - timedelta(minutes=5), "started", "old"),
                  Event(T - timedelta(minutes=4), "ready", "old")]
        self.assertIsNone(replacement_ready(events, "old", T))
        events += [Event(T + timedelta(seconds=10), "started", "new"),
                   Event(T + timedelta(seconds=20), "ready", "new")]
        self.assertIsNone(replacement_ready(events, "old", T))

    def test_delayed_termination_blocks_start_even_when_arm_says_stopped(self):
        calls = []
        events = [Event(T - timedelta(minutes=2), "started", "old"),
                  Event(T - timedelta(minutes=1), "ready", "old")]
        def wait(check, window, seconds, label):
            self.assertFalse(check())
            if "termination" in label:
                self.assertEqual(calls, ["stop"])
                events.append(Event(T + timedelta(seconds=50), "terminated", "old"))
            else:
                self.assertEqual(calls, ["stop", "start"])
                events.extend([Event(T + timedelta(seconds=60), "started", "new"),
                               Event(T + timedelta(seconds=90), "ready", "new")])
            return check()
        self.assertEqual(restart("old", stop=lambda: calls.append("stop"), start=lambda: calls.append("start"),
                                 read_events=lambda: events, is_stopped=lambda: True, window_check=lambda: None,
                                 utcnow=lambda: T, wait=wait), "new")

    def test_timeout_or_expired_window_does_not_start_candidate(self):
        for failure in (RuntimeError("deadline"), RuntimeError("logs unavailable")):
            calls = []
            def fail(*args):
                raise failure
            with self.assertRaises(RuntimeError):
                restart("old", stop=lambda: calls.append("stop"), start=lambda: calls.append("start"),
                        read_events=lambda: [], is_stopped=lambda: True, window_check=lambda: None,
                        utcnow=lambda: T, wait=fail)
            self.assertEqual(calls, ["stop"])

    def test_bounded_wait_checks_window_each_poll(self):
        tick = [0]
        calls = []
        def sleep(seconds):
            tick[0] += seconds
        with self.assertRaisesRegex(RuntimeError, "Timed out"):
            wait_for(lambda: False, lambda: calls.append(tick[0]), 10, "termination",
                     clock=lambda: tick[0], sleep=sleep)
        self.assertEqual(calls, [0, 5])

    def test_real_platform_format_and_application_stdout_exclusion(self):
        log = "\n".join([
            "2026-11-07T04:35:29.5747713Z Starting container: f87de912b395_rollandpaint-prod-ca.",
            "2026-11-07T04:36:35.8673240Z Site startup probe succeeded after 65.3848179 seconds.",
            "2026-11-07T04:40:50.7889630Z Stopping container: f87de912b395_rollandpaint-prod-ca.",
            "2026-11-07T04:40:58.2978507Z Container is terminated. Total time elapsed: 7758 ms.",
            "2026-11-07T04:41:56.0536166Z Starting container: 3357d66638e7_rollandpaint-prod-ca.",
            "2026-11-07T04:43:30.1322247Z Site startup probe succeeded after 93.4724506 seconds.",
        ])
        stream = io.BytesIO()
        with zipfile.ZipFile(stream, "w") as bundle:
            bundle.writestr("2026_11_07_worker_docker.log", log)
            bundle.writestr("2026_11_07_worker_default_docker.log", log.replace("3357d66638e7", "deadbeef"))
        events = platform_events(stream.getvalue())
        self.assertEqual(len(events), 5)
        self.assertEqual(replacement_ready(events, "f87de912b395", T), "3357d66638e7")

    def test_stale_or_terminated_replacement_is_rejected(self):
        events = [Event(T - timedelta(seconds=1), "terminated", "old"),
                  Event(T + timedelta(seconds=1), "started", "new"),
                  Event(T + timedelta(seconds=2), "ready", "new")]
        self.assertIsNone(replacement_ready(events, "old", T))
        events[0] = Event(T, "terminated", "old")
        events.append(Event(T + timedelta(seconds=3), "terminated", "new"))
        self.assertIsNone(replacement_ready(events, "old", T))
        with self.assertRaises(RuntimeError):
            active_container(events)

    def test_maintenance_rejects_generic_platform_503(self):
        for status, body in [(200, {}), (503, {}), (503, {"title": "Unavailable", "status": 503})]:
            with self.assertRaises(RuntimeError):
                require_maintenance(SimpleNamespace(status_code=status, json=lambda: body))
        require_maintenance(SimpleNamespace(status_code=503, json=lambda: {"title": "Maintenance mode", "status": 503}))


if __name__ == "__main__":
    unittest.main()
