export type Difficulty = {
  id: string
  label: string
  description: string
  accentColor: string
  stats: {
    playerHealth: number
    enemyHealth: number
    enemyDamage: number
    lootPercent: number
    spawnRate: number
  }
}

export const difficulties: Difficulty[] = [
  {
    id: 'very-easy',
    label: 'Very Easy',
    description:
      'A forgiving adventure for newcomers. Plentiful resources, sluggish enemies, and a generous health pool let you learn the ropes at your own pace.',
    accentColor: '#22c55e',
    stats: {
      playerHealth: 150,
      enemyHealth: 50,
      enemyDamage: 5,
      lootPercent: 200,
      spawnRate: 0.3,
    },
  },
  {
    id: 'easy',
    label: 'Easy',
    description:
      'A relaxed challenge for players who want a smooth experience. Enemies hit lighter and loot drops more often, giving you room to experiment.',
    accentColor: '#84cc16',
    stats: {
      playerHealth: 120,
      enemyHealth: 70,
      enemyDamage: 10,
      lootPercent: 150,
      spawnRate: 0.5,
    },
  },
  {
    id: 'medium',
    label: 'Medium',
    description:
      'The balanced default. Resources match the threat level and every decision matters. The intended experience for most players.',
    accentColor: '#eab308',
    stats: {
      playerHealth: 100,
      enemyHealth: 100,
      enemyDamage: 20,
      lootPercent: 100,
      spawnRate: 1.0,
    },
  },
  {
    id: 'hard',
    label: 'Hard',
    description:
      'Enemies hit harder, spawn faster, and loot is scarce. Positioning and resource management are critical — mistakes are costly.',
    accentColor: '#f97316',
    stats: {
      playerHealth: 80,
      enemyHealth: 140,
      enemyDamage: 35,
      lootPercent: 70,
      spawnRate: 1.5,
    },
  },
  {
    id: 'insane',
    label: 'Insane',
    description:
      'No mercy. Near-lethal enemies swarm relentlessly with minimal loot to sustain you. Only veterans who have mastered every mechanic should enter.',
    accentColor: '#ef4444',
    stats: {
      playerHealth: 60,
      enemyHealth: 200,
      enemyDamage: 60,
      lootPercent: 50,
      spawnRate: 2.5,
    },
  },
]
