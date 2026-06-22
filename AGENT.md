# Agent Entry Point

This root file stays in place because local tools may look for it here. Durable project guidance lives in the Obsidian vault.

- Start with [Agent Navigation](<ObsidianVault/00 Start Here/Agent Navigation.md>) to choose the smallest relevant project note.
- Use [Vault Specification](<ObsidianVault/00 Start Here/Vault Specification.md>) before changing vault structure or documentation ownership.
- If docs conflict with running code, trust the code and update the affected vault note in the same change.

## UI Alignment Lesson

For header, shell, nav, or hero layout work, visual centering is not enough. Measure the actual content grid:

- Compare the logo or brand mark left edge against the main content shell, hero title, and first major content surface.
- Check the requested viewport explicitly, especially `1365x768` with `deviceScaleFactor: 1.5` for 2K at 150% scale.
- Treat any unintended left-edge delta over 1-2px as a UI defect, even when the overall header shell is centered and there is no horizontal overflow.
- When a user reports that something is "off" or "not aligned", verify the exact element-to-element geometry before declaring the screenshot clean.
