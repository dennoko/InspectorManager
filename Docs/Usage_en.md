# Inspector Manager Documentation

Inspector Manager is a Unity Editor extension that improves the management of Inspector windows.
It allows you to automatically rotate multiple Inspector windows, manage selection history, and bookmark favorite objects.

## Key Features

### 🔄 Inspector Rotation
When multiple Inspector windows are open, selecting an object typically updates all windows to show the same content.
Enabling the "Rotation" feature changes this behavior so that Inspector windows are updated sequentially.
This makes it easier to compare properties of multiple objects or view parent and child objects simultaneously.

**Block Folder Selection:**
When enabled in Settings, clicking a folder in the Project window will *not* update the current Inspector. This helps prevent accidental content replacement.

### 📜 Selection History
Inspector Manager automatically records the history of previously selected objects.
You can navigate back and forth through your selection history, just like a web browser.

### ⭐ Favorites
Bookmark frequently used objects to your Favorites list.
Select an object and press `Ctrl+D` to add it, or drag and drop from the History list.

### 🔒 Inspector Overlay
Each Inspector window displays an overlay header at the top, showing its index number and lock status.
Clicking the overlay button toggles the lock state of that specific Inspector.

## Shortcuts

| Shortcut | Action |
| --- | --- |
| `Ctrl + L` | Toggle active Inspector lock |
| `Ctrl + Alt + L` | Toggle all Inspector locks |
| `Ctrl + [` | Backward history |
| `Ctrl + ]` | Forward history |
| `Ctrl + D` | Add/Remove current selection to/from Favorites |

## Settings

The Settings tab allows you to configure:

*   **Language**: Switch between Japanese (日本語) and English.
*   **Max History Count**: Set the maximum number of history entries to keep.
*   **Record Scene Objects**: Whether to record objects in the scene hierarchy.
*   **Record Assets**: Whether to record project assets.
*   **Auto Clean Invalid Entries**: Automatically remove deleted or missing objects from history.
*   **Block Folder Selection**: Prevent Inspector updates when selecting a folder.

## Installation

Simply place the `Assets/Editor/InspectorManager` folder into your project.
Open the window via `dennokoworks > Inspector Manager` in the menu bar.
