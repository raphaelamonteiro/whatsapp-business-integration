from datetime import date
import os

today = date.today().isoformat()

folder = "logs/daily"
filename = f"{folder}/{today}.md"

template = f"""# Daily Log - {today}

## What I did today
- 

## What I learned
- 

## What I struggled with
- 

## What I solved / fixed
- 

## What I’ll improve tomorrow
- 
"""

os.makedirs(folder, exist_ok=True)

if not os.path.exists(filename):
    with open(filename, "w", encoding="utf-8") as f:
        f.write(template)
    print(f"Created {filename}")
else:
    print("Log already exists for today.")