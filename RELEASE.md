# Release Notes - v1.0.2

**Released:** April 15, 2026

## Bug Fixes

- **Context Menu Argument Fix** - Fixed apps not opening correctly when added to Folder and Background context menus. The issue was caused by incorrect path arguments being passed to applications.
  - File context now uses `%1` (file path)
  - Folder/Background context now uses `%V` (folder path)

---

## Previous Release

### v1.0.1
- **Command Prompt Integration** - Added "Command Prompt" submenu with:
  - Open Here - Opens CMD in current folder
  - Open Here as Administrator - Opens CMD with admin privileges
  - Works on folders, desktop background, and drives
- **Drive Support** - Command Prompt now works when right-clicking directly on drive letters

### v1.0.0
- Initial release
- Add/remove installed apps to context menu
- Batch operations support
- Advanced mode for manual entries
- "New" submenu for creating developer files
- Dark theme UI with Windows 11 style
- Auto UAC elevation
- Custom splash screen
- Inno Setup installer
