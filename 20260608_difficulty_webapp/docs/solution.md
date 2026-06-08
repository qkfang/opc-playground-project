# Solution: Difficulty Selector Web App (20260608_difficulty_webapp)

## Stack

- **Frontend:** React + TypeScript + Vite
- **Styling:** Plain CSS (custom properties, CSS Grid/Flexbox)
- **Hosting:** Static — no backend required

## Architecture

Single-page application. All difficulty data lives in a typed `difficulties` array in `src/data/difficulties.ts`. The `App` component renders the hero section and a responsive grid of `DifficultyCard` components.

## Difficulty Levels

| Level     | Player HP | Enemy HP | Enemy Damage | Loot  | Spawn Rate |
|-----------|-----------|----------|--------------|-------|------------|
| Very Easy | 150       | 50       | 5            | 200%  | 0.3×       |
| Easy      | 120       | 70       | 10           | 150%  | 0.5×       |
| Medium    | 100       | 100      | 20           | 100%  | 1.0×       |
| Hard      | 80        | 140      | 35           | 70%   | 1.5×       |
| Insane    | 60        | 200      | 60           | 50%   | 2.5×       |

## Component Tree

```
App
├── HeroSection
└── DifficultyGrid
    └── DifficultyCard ×5
```

## Selected State

Clicking a card sets `selectedId` in App state. The active card receives a `.card--selected` class which applies a bright accent ring and slightly elevated shadow.
