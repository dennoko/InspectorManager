# Inspector Manager Documentation

Inspector Manager is a Unity Editor extension that improves the management of Inspector windows.
It allows you to automatically update multiple Inspector windows, manage selection history, and bookmark favorite objects.

---

## Inspector Rotation

### Overview

When multiple Inspector windows are open, selecting an object typically updates all windows to show the same content.
Enabling the "Rotation" feature changes this behavior so that Inspector windows are updated automatically on each selection.

### Update Modes

#### History Mode (Default)

Each tab displays a fixed position in the selection history.

| Tab | Content |
|-----|---------|
| Inspector 1 | Latest selection |
| Inspector 2 | Previous selection |
| Inspector 3 | Two selections ago |

> **Use Case**: Ideal when comparing parent and child objects side by side.
> Each tab's role (Latest, Previous...) is fixed, so you always know "the top of the list is the latest," making it intuitive to compare properties across multiple objects.
> The Inspector window number itself (#1, #2...) is fixed. Which window acts as "latest" is determined by its order in the Inspector Manager list.

#### Cycle Mode

Updates the oldest Inspector first, one at a time.

> **Use Case**: Useful when you want to "pin" specific objects to Inspectors.
> For example, with 3 Inspectors, you can have 3 different objects each displayed in their own tab.

### Pause

While rotation is active, click the pause button to pause. While paused, selecting objects will not update any Inspectors.

> **Use Case**: Keep current Inspector displays while selecting and manipulating other objects in the Hierarchy.
> For example, viewing material settings while moving a different object.

---

## Inspector Management (Status Tab)

### Inspector List

The Status tab shows all currently open Inspector windows.

Each row includes:
Each row includes:
- **Drag Handle**: Drag & drop to reorder rotation order (roles)
- **Lock Icon**: Click to toggle lock/unlock
- **Inspector Number**: Fixed window identifier
- **Rotation Badge**: Shows current target (▶) or order
- **Displayed Object Name**: Currently inspected object
- **Exclude/Include Button**: Exclude from / include in rotation
- **Close Button**: Close the Inspector window
- **Focus Button**: Focus that Inspector window

### Adding Inspectors

Click the "+ Add Inspector" button at the bottom of the list to create a new Inspector window.

- When rotation is enabled: Automatically added to the rotation
- When rotation is disabled: Opens as a regular Inspector

> **Use Case**: Quickly add another Inspector when you need one, without navigating Unity's menu. The new tab is immediately part of your rotation workflow.

### Removing Inspectors

Click the "✕" button on any row to close that Inspector.

> **Use Case**: Easily clean up Inspectors you no longer need.

### Reordering

Drag the handle icon on the left side of each row to change the rotation order.

> **Use Case**: In History mode, "Top of list = Latest", so place the most visible Inspector (e.g. #1) at the top of the list for comfort.

### Exclusion

When rotation is enabled, click "－" to exclude an Inspector from rotation. Excluded Inspectors show an "(Ex)" badge and maintain their locked state.

> **Use Case**: Pin a specific object (like a material or prefab) to one Inspector, while the rest rotate normally.

---

## Selection History

Automatically records the history of previously selected objects.

- **One-click Selection**: Click an object name in history to re-select it
- **Back/Forward**: Navigate history with back/forward buttons, just like a browser
- **Favorite Registration**: Click the star icon to add to favorites
- **Drag & Drop**: Drag objects from history into Hierarchy or Inspector

> **Use Case**: Quickly find and re-select an object you were editing earlier, without having to search through the Hierarchy or Project window.

---

## Favorites

Bookmark frequently used objects.

- One-click registration from the star icon in History
- Drag & drop to reorder
- Persisted across project sessions

> **Use Case**: Register frequently accessed materials, scripts, or prefabs to avoid repetitive searches in the Project window.

---

## Inspector Overlay

Each Inspector window displays an overlay at the top showing its index number and lock state.
Click the overlay button to toggle that Inspector's lock state.

---

## Shortcuts

| Shortcut | Action |
| --- | --- |
| `Ctrl + L` | Toggle active Inspector lock |
| (Customizable) | Toggle all Inspector locks |
| (Customizable) | Toggle rotation pause |

> Shortcuts can be customized via Unity's Shortcut Manager. Open it from Settings tab > "Open Shortcut Settings..." button.

---

## Settings

### General

- **Language**: Switch between Japanese (日本語) and English.

### History Settings

- **Max History Count**: Maximum number of history entries to keep.
- **Record Scene Objects**: Whether to record objects in the scene hierarchy.
- **Record Assets**: Whether to record project assets.
- **Auto Clean Invalid Entries**: Automatically remove deleted or missing objects from history.

### Rotation Settings

- **Auto Focus Inspector on Update**: Automatically focus the updated Inspector.
- **Update Mode**: Choose between History mode (fixed position) and Cycle mode (rotate in order).

### Block Inspector Update

Block Inspector updates when selecting certain types of objects.

- **Category A (Non-interactive)**: Folders, DefaultAsset, Assembly Definitions, Native Plugins
- **Category B (Limited interaction)**: Scripts, Shaders/Compute Shaders, Fonts

> Prevents accidental Inspector content replacement when clicking folders or scripts.

---

## Installation

Simply place the `Assets/Editor/InspectorManager` folder into your project.
Open the window via `dennokoworks > Inspector Manager` in the menu bar.
