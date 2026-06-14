# difficulty-webapp frontend

React + Vite + TypeScript static web app for choosing a game difficulty level.

## Local run

```bash
npm install
npm run dev
```

Open [http://localhost:5173](http://localhost:5173) in your browser.

## Build & verify

```bash
npm run lint
npm run build
```

The built output is placed in `dist/`. Serve it with any static file server:

```bash
npm run preview
```

## Features

- Five difficulty levels: **Very Easy · Easy · Medium · Hard · Insane**
- Each card shows a short description and balancing values:
  - Player Health, Enemy Health, Enemy Damage, Loot/Resources, Enemy Spawn Rate
- Click any card to select it; click again to deselect
- Confirm bar at the bottom shows the selected difficulty and a **Play** button
- Fully responsive — stacks to a single column on small screens

## Extending

All difficulty data lives in `src/data/difficulties.ts` as a typed `Difficulty[]` array. Add or remove entries there — the UI adapts automatically.
