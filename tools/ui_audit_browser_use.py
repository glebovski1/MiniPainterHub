import asyncio
import base64
import json
import time
from dataclasses import dataclass
from pathlib import Path

from browser_use.browser import BrowserSession


ARTIFACTS_DIR = Path("artifacts/ui-audit")
BASE_URL = "http://localhost:5176"


@dataclass(frozen=True)
class Viewport:
    name: str
    width: int
    height: int


VIEWPORTS: list[Viewport] = [
    Viewport(name="desktop", width=1280, height=720),
    Viewport(name="mobile", width=390, height=844),
]


ROUTES: list[str] = [
    "/",
    "/home",
    "/posts/all",
    "/posts/new",
    "/about",
    "/login",
    "/register",
]


async def _wait_for(page, predicate_js: str, *, timeout_s: float = 20.0, interval_s: float = 0.25) -> None:
    deadline = time.time() + timeout_s
    last_error: str | None = None
    while time.time() < deadline:
        try:
            ok = await page.evaluate(f"() => Boolean({predicate_js})")
            if ok is True:
                return
            if isinstance(ok, str) and ok.strip().lower() == "true":
                return
        except Exception as e:  # noqa: BLE001 - best-effort polling against a live browser
            last_error = f"{type(e).__name__}: {e}"
        await asyncio.sleep(interval_s)
    raise TimeoutError(f"Timed out waiting for predicate. Last error: {last_error}")


async def _wait_for_dom_ready(page) -> None:
    # In practice, `document.readyState === 'complete'` can be brittle for SPAs and/or slow CDNs.
    # For Blazor WASM, prefer waiting for the navbar to render.
    await _wait_for(page, "document && document.readyState !== 'loading'", timeout_s=20)
    await _wait_for(page, "document.body", timeout_s=20)
    await _wait_for(page, "document.querySelector('#app')", timeout_s=20)
    await _wait_for(page, "document.querySelector('nav.navbar') || document.querySelector('.navbar')", timeout_s=45)
    await asyncio.sleep(0.5)


async def _save_screenshot(page, path: Path) -> None:
    b64 = await page.screenshot(format="png")
    path.write_bytes(base64.b64decode(b64))


async def _collect_ui_metrics(page) -> dict:
    metrics_json = await page.evaluate(
        r"""() => {
  const esc = (s) => (s ?? "").toString().trim();
  const textOf = (el) => esc(el?.textContent);

  const inputs = Array.from(document.querySelectorAll("input, textarea, select"))
    .filter(el => el.type !== "hidden" && !el.disabled);

  const labelFor = (el) => {
    const id = el.getAttribute("id");
    if (id) {
      const label = document.querySelector(`label[for="${CSS.escape(id)}"]`);
      if (label) return textOf(label);
    }
    const parent = el.closest("label");
    if (parent) return textOf(parent);
    const aria = el.getAttribute("aria-label");
    if (aria) return esc(aria);
    return null;
  };

  const unlabeledInputs = inputs
    .map(el => ({
      tag: el.tagName.toLowerCase(),
      type: el.getAttribute("type") || null,
      id: el.getAttribute("id") || null,
      name: el.getAttribute("name") || null,
      placeholder: el.getAttribute("placeholder") || null,
      ariaLabel: el.getAttribute("aria-label") || null,
      label: labelFor(el),
    }))
    .filter(x => !x.label);

  const imgs = Array.from(document.querySelectorAll("img"));
  const imagesMissingAlt = imgs.filter(img => !img.hasAttribute("alt")).length;
  const imagesEmptyAlt = imgs.filter(img => img.getAttribute("alt") === "").length;

  const links = Array.from(document.querySelectorAll("a"));
  const linksNoText = links
    .map(a => ({
      href: a.getAttribute("href"),
      text: textOf(a),
      ariaLabel: a.getAttribute("aria-label") || null,
    }))
    .filter(x => !x.text && !x.ariaLabel);

  const h1s = Array.from(document.querySelectorAll("h1")).map(h => textOf(h)).filter(Boolean);

  const errorAlerts = Array.from(document.querySelectorAll(".alert.alert-danger, .validation-message, .text-danger"))
    .map(el => textOf(el))
    .filter(Boolean);

  return {
    title: document.title,
    url: window.location.href,
    h1Count: h1s.length,
    h1Text: h1s,
    inputsCount: inputs.length,
    unlabeledInputsCount: unlabeledInputs.length,
    unlabeledInputs: unlabeledInputs.slice(0, 20),
    imagesCount: imgs.length,
    imagesMissingAlt,
    imagesEmptyAlt,
    linksCount: links.length,
    linksNoTextCount: linksNoText.length,
    linksNoText: linksNoText.slice(0, 20),
    errorAlertSamples: errorAlerts.slice(0, 10),
  };
}""".strip()
    )
    try:
        return json.loads(metrics_json)
    except Exception:
        # Some CDP implementations return native values rather than JSON strings.
        return {"raw": metrics_json}


async def _audit_route(page, viewport: Viewport, route: str) -> dict:
    await page.set_viewport_size(viewport.width, viewport.height)
    await page.goto(f"{BASE_URL}{route}")
    await _wait_for_dom_ready(page)

    # Wait for at least a body element to exist and have some content.
    await _wait_for(page, "document.body && document.body.innerText.length >= 0", timeout_s=10)

    safe_route = route.strip("/").replace("/", "_") or "root"
    screenshot_path = ARTIFACTS_DIR / f"{safe_route}.{viewport.name}.png"
    await _save_screenshot(page, screenshot_path)

    metrics = await _collect_ui_metrics(page)
    metrics["screenshot"] = str(screenshot_path.as_posix())
    metrics["route"] = route
    metrics["viewport"] = viewport.name
    return metrics


async def _login_as_seed_user(page) -> dict:
    await page.goto(f"{BASE_URL}/login")
    await _wait_for_dom_ready(page)

    user_input = (await page.get_elements_by_css_selector("#login-username"))[0]
    pass_input = (await page.get_elements_by_css_selector("#login-password"))[0]

    # Seeded dev user is created with UserName="user" (email is user@local),
    # but the API login endpoint currently authenticates by username only.
    await user_input.fill("user")
    await pass_input.fill("User123!")

    submit = (await page.get_elements_by_css_selector('button[type="submit"]'))[0]
    await submit.click()

    # Wait for navbar to show a Logout button (AuthorizeView -> Authorized).
    await _wait_for(
        page,
        "Array.from(document.querySelectorAll('button')).some(b => (b.textContent||'').toLowerCase().includes('logout'))",
        timeout_s=20,
    )

    return {"login": "ok", "user": "user@local"}


def _render_report(results: list[dict]) -> str:
    lines: list[str] = []
    lines.append("# UI Audit Report (browser-use)")
    lines.append("")
    lines.append(f"- Base URL: `{BASE_URL}`")
    lines.append(f"- Timestamp (local): `{time.strftime('%Y-%m-%d %H:%M:%S')}`")
    lines.append("")
    for r in results:
        title = r.get("title") or ""
        url = r.get("url") or ""
        route = r.get("route") or ""
        viewport = r.get("viewport") or ""
        screenshot = r.get("screenshot") or ""

        lines.append(f"## `{route}` ({viewport})")
        if title:
            lines.append(f"- Title: `{title}`")
        if url:
            lines.append(f"- URL: `{url}`")
        if screenshot:
            lines.append(f"- Screenshot: `{screenshot}`")

        def add_metric(label: str, key: str) -> None:
            if key in r:
                lines.append(f"- {label}: `{r[key]}`")

        add_metric("H1 count", "h1Count")
        add_metric("Inputs", "inputsCount")
        add_metric("Unlabeled inputs", "unlabeledInputsCount")
        add_metric("Images", "imagesCount")
        add_metric("Images missing alt", "imagesMissingAlt")
        add_metric("Links", "linksCount")
        add_metric("Links with no text", "linksNoTextCount")

        unlabeled = r.get("unlabeledInputs") or []
        if unlabeled:
            lines.append("- Unlabeled input samples:")
            for x in unlabeled[:5]:
                lines.append(f"  - `{x}`")

        errors = r.get("errorAlertSamples") or []
        if errors:
            lines.append("- Error/alert samples:")
            for e in errors[:5]:
                lines.append(f"  - `{e}`")

        links_no_text = r.get("linksNoText") or []
        if links_no_text:
            lines.append("- Links with no accessible text samples:")
            for x in links_no_text[:5]:
                lines.append(f"  - `{x}`")

        lines.append("")

    return "\n".join(lines)


async def main() -> int:
    ARTIFACTS_DIR.mkdir(parents=True, exist_ok=True)

    browser_profile_dir = ARTIFACTS_DIR / "browser-profile"
    browser_profile_dir.mkdir(parents=True, exist_ok=True)

    session = BrowserSession(
        is_local=True,
        headless=True,
        user_data_dir=str(browser_profile_dir),
        wait_between_actions=0.05,
    )

    results: list[dict] = []
    errors: list[str] = []
    try:
        await session.start()
        page = await session.new_page()

        # Baseline: audit routes as not-authenticated (should surface gated pages/redirects).
        for viewport in VIEWPORTS:
            for route in ROUTES:
                try:
                    results.append(await _audit_route(page, viewport, route))
                except Exception as e:  # noqa: BLE001 - audit should be best-effort across routes
                    errors.append(f"{route} ({viewport.name}): {type(e).__name__}: {e}")

        # Authenticated flow: login then re-audit key pages.
        try:
            await _login_as_seed_user(page)
            for viewport in VIEWPORTS:
                for route in ["/", "/posts/new", "/posts/all", "/posts/mine"]:
                    try:
                        results.append(await _audit_route(page, viewport, route))
                    except Exception as e:  # noqa: BLE001
                        errors.append(f"{route} ({viewport.name}, authed): {type(e).__name__}: {e}")
        except Exception as e:  # noqa: BLE001
            errors.append(f"login: {type(e).__name__}: {e}")

    finally:
        try:
            await session.kill()
        except Exception:
            pass

    (ARTIFACTS_DIR / "results.json").write_text(json.dumps(results, indent=2), encoding="utf-8")
    (ARTIFACTS_DIR / "report.md").write_text(_render_report(results), encoding="utf-8")
    if errors:
        (ARTIFACTS_DIR / "errors.txt").write_text("\n".join(errors) + "\n", encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(asyncio.run(main()))
