// Game mode definitions — the core "different modes" requirement.
// Each mode meaningfully changes the rules/difficulty of the survival loop.

export type ModeId = "classic" | "blitz" | "nightfall";

export interface GameMode {
  id: ModeId;
  name: string;
  tagline: string;
  description: string;
  /** Accent colour (hex) for UI + player glow. */
  accent: string;
  /** Seconds between difficulty escalations. */
  waveIntervalSec: number;
  /** Base seconds between enemy spawns at wave 0 (scales down over time). */
  baseSpawnIntervalSec: number;
  /** Lowest the spawn interval can reach. */
  minSpawnIntervalSec: number;
  /** Enemy speed in px/sec at wave 0. */
  baseEnemySpeed: number;
  /** Extra enemy speed added per wave. */
  enemySpeedPerWave: number;
  /** Player movement speed in px/sec. */
  playerSpeed: number;
  /** Player starting hit points. */
  startingLives: number;
  /** If set, player can only "see" within this radius (px); rest is darkened. */
  visionRadius: number | null;
  /** Score multiplier (riskier modes reward more). */
  scoreMultiplier: number;
  /** Max enemies allowed on screen at once. */
  maxEnemies: number;
}

export const MODES: Record<ModeId, GameMode> = {
  classic: {
    id: "classic",
    name: "Classic",
    tagline: "Steady, escalating waves",
    description:
      "The pure survival loop. Enemies arrive in steadily escalating waves. Learn the rhythm, keep moving, and see how long you last.",
    accent: "#38bdf8",
    waveIntervalSec: 15,
    baseSpawnIntervalSec: 1.5,
    minSpawnIntervalSec: 0.45,
    baseEnemySpeed: 70,
    enemySpeedPerWave: 8,
    playerSpeed: 230,
    startingLives: 3,
    visionRadius: null,
    scoreMultiplier: 1,
    maxEnemies: 60,
  },
  blitz: {
    id: "blitz",
    name: "Blitz",
    tagline: "Faster, meaner, relentless",
    description:
      "High intensity. Enemies are faster, spawn faster, and waves ramp quickly. Fewer lives, but every second survived is worth more.",
    accent: "#f472b6",
    waveIntervalSec: 9,
    baseSpawnIntervalSec: 0.9,
    minSpawnIntervalSec: 0.28,
    baseEnemySpeed: 95,
    enemySpeedPerWave: 12,
    playerSpeed: 250,
    startingLives: 2,
    visionRadius: null,
    scoreMultiplier: 1.6,
    maxEnemies: 90,
  },
  nightfall: {
    id: "nightfall",
    name: "Nightfall",
    tagline: "Survive what you can't see",
    description:
      "A survival-horror twist. Darkness limits your vision to a small light radius, and the things in the dark hit hard. Trust your ears... and your nerve.",
    accent: "#a78bfa",
    waveIntervalSec: 13,
    baseSpawnIntervalSec: 1.4,
    minSpawnIntervalSec: 0.5,
    baseEnemySpeed: 78,
    enemySpeedPerWave: 9,
    playerSpeed: 220,
    startingLives: 3,
    visionRadius: 150,
    scoreMultiplier: 1.4,
    maxEnemies: 55,
  },
};

export const MODE_LIST: GameMode[] = [MODES.classic, MODES.blitz, MODES.nightfall];

export function getMode(id: string): GameMode | null {
  return (MODES as Record<string, GameMode>)[id] ?? null;
}
