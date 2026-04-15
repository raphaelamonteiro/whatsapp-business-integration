#!/bin/bash

DATE=$(date +%F)
FILE="logs/daily/$DATE.md"

mkdir -p logs/daily

if [ ! -f "$FILE" ]; then
  cat <<EOF > "$FILE"
# Daily Log - $DATE

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
EOF

  echo "Created $FILE"
else
  echo "Log already exists"
fi