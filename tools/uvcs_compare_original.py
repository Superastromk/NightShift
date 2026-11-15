from datetime import datetime
from pathlib import Path
import shutil

from restyle_code import compact, read_source, remove_comments

root = Path(__file__).resolve().parents[1]
original = Path(r"C:\Users\Owner\Documents\kimi\tasks\2026-11-14\13-22-51-63aed0bf\TheNightShift")
backup = root / "qa" / "original-pending"
files = [
    "Assets/Scripts/Core/GameManager.cs",
    "Assets/Scripts/Rewind/RewindController.cs",
    "Assets/Scripts/Rewind/DeathManager.cs",
    "Assets/Scripts/UI/UIManager.cs",
    "Assets/Scripts/Audio/AudioDirector.cs",
    "Assets/Scripts/Scares/JumpscareTrigger.cs",
    "Assets/Scripts/Scares/FirstKillDirector.cs",
    "Assets/Scripts/Scares/ElevatorScare.cs",
    "Assets/Scripts/Scares/CoffeeMachineScare.cs",
    "Assets/Scripts/Objectives/ObjectiveManager.cs",
    "ProjectSettings/ProjectSettings.asset",
    "Packages/packages-lock.json",
    "Packages/manifest.json",
]
differences = []
for relative in files:
    source = original / relative
    destination = backup / relative
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, destination)
    old = read_source(destination)
    new = read_source(root / relative)
    if relative.endswith(".cs"):
        same = compact(remove_comments(old)[0]) == compact(remove_comments(new)[0])
    else:
        same = old == new
    modified = datetime.fromtimestamp(source.stat().st_mtime).isoformat(timespec="seconds")
    print(f"{relative}\n  original modified: {modified}\n  tokens: {'MATCH' if same else 'DIFFERENT'}")
    if not same:
        differences.append(relative)
print(f"Backed up {len(files)} original pending files under qa/original-pending")
print(f"Token differences: {len(differences)}")
for relative in differences:
    print(relative)
