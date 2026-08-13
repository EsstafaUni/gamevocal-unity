# GameVocal — Unity Plugin

[![Unity 2022.3+](https://img.shields.io/badge/Unity-2022.3%2B-blue)](https://unity.com)
[![MIT License](https://img.shields.io/badge/license-MIT-green)](LICENSE.md)

Connects your Unity project to the [GameVocal](https://gamevocal.com) cloud platform and syncs dialogue audio, lip-sync data, and localization files directly into your project with a single click.

---

## Features

- **Editor Sync Window** — authenticate with your API key, select a cloud project, preview sync status, and download incrementally using a local manifest.
- **Live Dialogue Sync** — optional polling mode that auto-triggers a sync whenever your cloud project is updated.
- **`GameVocalManager` singleton** — runtime dialogue graph walker that evaluates condition logic and handles variable mutations.
- **`GameVocalCharacter` component** — plays a voiced dialogue line and drives ARKit52 blendshape animation in perfect sync with the audio playback position.
- **ARKit52 Blendshape Mapper** — editor tool that auto-suggests mappings between your SkinnedMeshRenderer's blendshapes and the 52 ARKit channels, with a live preview toggle per channel.
- **2D Viseme support** — map GameVocal viseme groups to Sprite frames, Sprite swaps, or Animation states.
- **Localization** — `GameVocalLocalizationLoader` loads downloaded JSON translation files at runtime.
- **Incremental sync** — a local manifest (`Library/GameVocalManifest.json`) tracks checksums so unchanged files are never re-downloaded.
- **Secure credential storage** — the API key is saved in `UserSettings/GameVocalSettings.json`, never committed to your project files and works seamlessly across standard and admin privileges.

---

## Requirements

- Unity **2022.3** or later
- Package dependency: `com.unity.nuget.newtonsoft-json` (automatically installed)
- A [GameVocal](https://gamevocal.com) account with at least one project

---

## Installation

### Via Unity Package Manager (Git)
1. Open Unity **Package Manager** (Window → Package Manager).
2. Click the `+` button in the top left and select **Add package from git URL**.
3. Enter `https://github.com/GameVocal/gamevocal-unity.git` and click **Add**.

---

## Quick Start

### 1 — Authenticate
After enabling the plugin, open the sync window via **GameVocal → Sync from GameVocal**.  
Paste your API key and click **Save API Key & Refresh**.  
The window will automatically fetch your cloud projects.

### 2 — Sync Assets
Select your project from the drop-down and click **Sync Assets Now**.  
Audio, lip-sync JSON, and localization files are downloaded into `Assets/GameVocal/`.

### 3 — Set Up a Character (3D)

1. Select your character GameObject in the scene.
2. Add the `GameVocalCharacter` component.
3. It will automatically add an `AudioSource` if one is missing.
4. Create a `GameVocalCharacterProfile` (Right-click in Project view → Create → GameVocal → Character Profile) and assign it to the character.
5. In the Inspector, click **Open ARKit52 Blendshape Mapper**.
6. Click **Auto-Suggest Mappings**, review the results (use the Test checkbox to preview), and click **Save Profile**.

### 4 — Play Dialogue

```csharp
using GameVocal;

public class MyDialogueTrigger : MonoBehaviour
{
    public GameVocalCharacter character;

    public void TriggerDialogue()
    {
        // For individual lines:
        character.PlayDialogue("Assets/GameVocal/Voice/npc_greeting.ogg", "Assets/GameVocal/Voice/npc_greeting.json");
    }
}
```

Call `Stop()` to interrupt playback early:

```csharp
character.Stop();
```

---

## Dialogue Graph API

When you sync your project, GameVocal downloads `dialogue.json`. You can use the `GameVocalManager` to walk this graph automatically:

```csharp
using GameVocal;
using UnityEngine;

public class MyGameManager : MonoBehaviour
{
    void Start()
    {
        // 1. Load the graph
        GameVocalManager.Instance.LoadDialogue("dialogue.json");
        
        // 2. Subscribe to events
        GameVocalManager.Instance.OnLineStarted += HandleLineStarted;
        GameVocalManager.Instance.OnChoicesPresented += HandleChoices;
        
        // 3. Start a tree
        GameVocalManager.Instance.PlayTree("tree_intro_01");
    }
    
    void HandleLineStarted(GameVocalDialogueLine line, string treeId)
    {
        Debug.Log($"{line.character_id} says: {line.text}");
        
        // If voiced, you'd tell your GameVocalCharacter to play it here
        // myCharacter.PlayDialogue(line.GetAudioPath(), line.GetAudioPath().Replace(".ogg", ".json"));
    }
    
    void HandleChoices(List<Dictionary<string, object>> choices, GameVocalDialogueLine node, string treeId)
    {
        for(int i = 0; i < choices.Count; i++)
        {
            Debug.Log($"Choice {i}: {choices[i]["text"]}");
        }
        
        // Later: GameVocalManager.Instance.SelectChoice(index);
    }
}
```

---

## 2D Viseme Setup

For 2D characters, assign the **Mouth Sprite 2D** slot on `GameVocalCharacter` to a `SpriteRenderer` node, then populate the 2D mapping dictionary on the character's `GameVocalCharacterProfile` resource. The mapper window currently supports ARKit52; 2D mappings must be set up via script or inspector array manually for now.

---

## License

MIT License — Copyright (c) 2026 GameVocal. See [LICENSE.md](LICENSE.md) for full text.
