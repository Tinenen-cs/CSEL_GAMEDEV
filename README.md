# CSEL_GAMEDEV – 2D Physics Platformer

A Unity 2D platformer for the CSEL Game Development assignments. You play an animated character (idle, run and jump animations) and cross a course made from the Nature pixel-art asset pack. The course has physics obstacles (falling platforms, seesaws, rope bridges, swinging rocks, pushable blocks), spike grass that kills you, background music and a respawn system.

- **Unity version:** `6000.3.24f1` (Unity 6). Use this exact version.
- **Main scene:** `Assets/Scenes/Level1.unity`
- **Controls:** `A` / `D` or the arrow keys to move, `Space` to jump.

---

## 1. What you need

| Tool | Where to get it |
|---|---|
| Unity Hub | https://unity.com/download |
| Unity Editor **6000.3.24f1** | In Unity Hub go to **Installs → Install Editor → Archive** (or https://unity.com/releases/editor/archive) |
| Git (optional but recommended) | Windows: https://git-scm.com · macOS: run `git --version` in Terminal and accept the install prompt |

> If you open the project with a different Unity version, Unity may upgrade or break it. Install **6000.3.24f1** first.

---

## 2. Get the project

### Option A – Clone with Git (recommended, easy to update later)

**Windows (PowerShell or Git Bash)**
```bash
cd %USERPROFILE%\Documents
git clone https://github.com/Tinenen-cs/CSEL_GAMEDEV.git
```

**macOS / Linux (Terminal)**
```bash
cd ~/Documents
git clone https://github.com/Tinenen-cs/CSEL_GAMEDEV.git
```

This creates a `CSEL_GAMEDEV` folder with the whole project.

### Option B – Download as a ZIP (no Git needed)

1. Open https://github.com/Tinenen-cs/CSEL_GAMEDEV
2. Click the green **Code** button, then **Download ZIP**.
3. Extract the ZIP to a short path, for example `C:\Projects\CSEL_GAMEDEV` or `~/Documents/CSEL_GAMEDEV`.

> **Windows tip:** keep the folder path short. Very long paths (over 260 characters) can make Unity package imports fail.

A ZIP download is a snapshot. To get later updates you have to download it again, or switch to Option A.

---

## 3. Open the project in Unity

1. Open **Unity Hub**.
2. Go to **Projects**, click **Add** (or **Add → Add project from disk**), then select the `CSEL_GAMEDEV` folder (the one that contains `Assets`, `Packages` and `ProjectSettings`).
3. Make sure the editor version shown is **6000.3.24f1**, then click the project to open it.
4. The first time you open it, Unity rebuilds its `Library` cache. This can take several minutes. Let it finish.
5. In the **Project** window, open `Assets/Scenes/Level1.unity`.
6. Press **Play** ▶.

### Notes for macOS

- On Apple Silicon Macs (M1–M4), install the **Apple Silicon** build of 6000.3.24f1.
- If macOS blocks Unity Hub the first time, open **System Settings → Privacy & Security** and click **Open Anyway**.
- Everything else works the same as on Windows.

### Input setting

The player script uses Unity's old Input Manager, so **Active Input Handling** must be **Both**. The project already has this setting. If you ever see input errors in the Console, check **Edit → Project Settings → Player → Other Settings → Active Input Handling → Both**, then restart Unity.

---

## 4. Keep the project updated (Git)

**Before you start working on any device, get the latest changes:**
```bash
cd path/to/CSEL_GAMEDEV
git pull
```

**After you finish working, save and upload your changes:**
```bash
git add -A
git commit -m "Describe what you changed"
git push
```

**Tips for working on more than one device**
- **Close Unity before `git pull`,** so Unity doesn't overwrite incoming changes with what it has open.
- **Save the scene** in Unity (`Ctrl+S` / `Cmd+S`) before you commit.
- **Commit and push on one device before you switch to another,** to avoid merge conflicts in `Level1.unity`.
- **If `git pull` reports a conflict in a `.unity` scene file,** the simplest fix is to keep one version. Keep the version on GitHub with `git checkout --theirs Assets/Scenes/Level1.unity`, or keep your local version with `git checkout --ours Assets/Scenes/Level1.unity`. Then run `git add` and `git commit`.

**First-time Git setup on a new device**
```bash
git config --global user.name "Your Name"
git config --global user.email "you@example.com"
```
To push, sign in to GitHub. On Windows, Git Credential Manager opens a browser login. With the GitHub CLI you can run `gh auth login` instead.

---

## 5. Build a playable game (optional)

1. **File → Build Profiles.**
2. Make sure `Scenes/Level1` is in the scene list.
3. Pick a platform:
   - **Windows** gives you a `.exe` (build on Windows, or install *Windows Build Support* in Unity Hub).
   - **macOS** gives you a `.app` (build on a Mac, or install *Mac Build Support*).
4. Click **Build** and choose an empty output folder, for example `Builds/`, which Git ignores.

To submit the project as an archive instead, compress the whole `CSEL_GAMEDEV` folder. You can leave out `Library`, `Temp`, `Logs` and `UserSettings`; Unity regenerates them.

---

## 6. Project layout

```
Assets/
  Scenes/Level1.unity            main game scene
  Scripts/                       assignment scripts (provided by the instructor, unchanged)
    CharacterController2D.cs     character physics / movement
    playermovement.cs            player input + animation parameters
    CameraFollow.cs              camera follows the player
    trap.cs                      restarts the level when the player touches a hazard
  Animations/                    Idle, Run, Jump clips + Player animator controller
  Sprites/Player/                character frames (IDLE, RUN, JUMP)
  Audio/Music.mp3                looping background music
  Nature_pixel_art_assets/       tiles, props and the Nature_assets scene used for the level
BuildTools/LevelBuilder.cs       editor tool that generated Level1 (outside Assets, not compiled)
```

### Regenerating the level (advanced)

`BuildTools/LevelBuilder.cs` is the editor script that generated `Level1`. It sits outside `Assets` so it isn't part of the submitted game. To use it:
1. Copy it into `Assets/Editor/`.
2. Run **Tools → Build Platformer Level**. This replaces `Level1.unity`.
3. Remove it from `Assets/Editor/` again.

---

## 7. Troubleshooting

| Problem | Fix |
|---|---|
| Pink/magenta sprites | Wait for the import to finish, then reopen the scene. Make sure you're on 6000.3.24f1 (URP 2D). |
| "Input System" errors in the Console | Set **Active Input Handling** to **Both** (see section 3) and restart Unity. |
| Scene is empty when the project opens | Open `Assets/Scenes/Level1.unity` from the Project window. |
| Unity asks to upgrade the project | Cancel and install 6000.3.24f1 instead. |
| Import errors about paths on Windows | Move the project to a shorter folder path, such as `C:\Projects\CSEL_GAMEDEV`. |
| No sound | Make sure the Game view's **Mute Audio** button is off. |
