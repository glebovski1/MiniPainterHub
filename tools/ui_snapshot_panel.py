import asyncio
import base64
from dataclasses import dataclass
from pathlib import Path

from browser_use.browser import BrowserSession


ARTIFACTS_DIR = Path("artifacts/ui-panel-check")
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


async def _save_screenshot(page, path: Path) -> None:
    b64 = await page.screenshot(format="png")
    path.write_bytes(base64.b64decode(b64))


async def _wait_for_selector(page, selector: str, *, timeout_s: float = 30.0) -> None:
    deadline = asyncio.get_running_loop().time() + timeout_s
    while asyncio.get_running_loop().time() < deadline:
        els = await page.get_elements_by_css_selector(selector)
        if els:
            return
        await asyncio.sleep(0.25)
    raise TimeoutError(f"Timed out waiting for selector: {selector}")


async def _login(page) -> None:
    await page.goto(f"{BASE_URL}/login")
    await _wait_for_selector(page, "#login-username")
    await _wait_for_selector(page, "#login-password")
    await _wait_for_selector(page, 'button[type="submit"]')
    await (await page.get_elements_by_css_selector("#login-username"))[0].fill("user")
    await (await page.get_elements_by_css_selector("#login-password"))[0].fill("User123!")
    await (await page.get_elements_by_css_selector('button[type="submit"]'))[0].click()
    # Wait until authenticated UI shows up.
    await _wait_for_selector(page, 'button.btn.btn-link.nav-link', timeout_s=30.0)


async def _ensure_nav_expanded(page) -> None:
    # On mobile widths the navbar is collapsed; open it so the Panel button is clickable.
    width = await page.evaluate("() => window.innerWidth")
    try:
        w = int(width)
    except Exception:
        w = 0
    if w >= 992:
        return

    togglers = await page.get_elements_by_css_selector("button.navbar-toggler")
    if togglers:
        # If collapse already shown, do nothing; otherwise click toggler.
        is_open = await page.evaluate("() => !!document.querySelector('#navbarNav.show')")
        if is_open is True:
            return
        # Use JS click (toggler may be hard to click via CDP in some layouts).
        await page.evaluate("() => document.querySelector('button.navbar-toggler')?.click()")
        await _wait_for_selector(page, "#navbarNav.show", timeout_s=10.0)


async def main() -> int:
    ARTIFACTS_DIR.mkdir(parents=True, exist_ok=True)
    profile_dir = ARTIFACTS_DIR / "browser-profile"
    profile_dir.mkdir(parents=True, exist_ok=True)

    session = BrowserSession(
        is_local=True,
        headless=True,
        user_data_dir=str(profile_dir),
        wait_between_actions=0.05,
    )

    try:
        await session.start()
        page = await session.new_page()
        await _login(page)

        for vp in VIEWPORTS:
            await page.set_viewport_size(vp.width, vp.height)
            await page.goto(f"{BASE_URL}/")
            await asyncio.sleep(2.0)

            await _save_screenshot(page, ARTIFACTS_DIR / f"root.{vp.name}.png")

            if vp.width >= 992:
                await _wait_for_selector(page, 'button[aria-label="Toggle desktop panel"]', timeout_s=10.0)
                await _save_screenshot(page, ARTIFACTS_DIR / f"panel-open.{vp.name}.png")
                await page.evaluate("() => document.querySelector('button[aria-label=\"Toggle desktop panel\"]')?.click()")
                await asyncio.sleep(0.35)
                await _save_screenshot(page, ARTIFACTS_DIR / f"panel-closed.{vp.name}.png")
                await page.evaluate("() => document.querySelector('button[aria-label=\"Toggle desktop panel\"]')?.click()")
                await asyncio.sleep(0.2)
                continue

            # Mobile: open offcanvas panel
            await _ensure_nav_expanded(page)
            await _wait_for_selector(page, 'button[aria-controls="userPanelOffcanvas"]', timeout_s=10.0)
            await page.evaluate("() => document.querySelector('button[aria-controls=\"userPanelOffcanvas\"]')?.click()")
            await _wait_for_selector(page, "#userPanelOffcanvas.show", timeout_s=15.0)
            await asyncio.sleep(0.35)
            await _save_screenshot(page, ARTIFACTS_DIR / f"panel-open.{vp.name}.png")

            # Mobile: close offcanvas panel
            await _wait_for_selector(page, "#userPanelOffcanvas .btn-close", timeout_s=10.0)
            await page.evaluate("() => document.querySelector('#userPanelOffcanvas .btn-close')?.click()")
            await asyncio.sleep(0.35)
            await _save_screenshot(page, ARTIFACTS_DIR / f"panel-closed.{vp.name}.png")

    finally:
        try:
            await session.kill()
        except Exception:
            pass

    return 0


if __name__ == "__main__":
    raise SystemExit(asyncio.run(main()))
