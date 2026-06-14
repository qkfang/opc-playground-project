import type { Product } from "./types";

export const PRODUCTS: Product[] = [
  {
    id: "city-harbor-patrol",
    name: "City Harbor Patrol",
    theme: "City",
    price: 89.99,
    pieces: 612,
    ageRange: "7+",
    rating: 4.6,
    featured: true,
    blurb: "Patrol the docks with a rescue boat and crane.",
    description:
      "Set sail with the City Harbor Patrol. Includes a working crane, a rescue boat, two minifigures, and a dockside control tower. Great for imaginative harbor adventures.",
    colorFrom: "from-sky-700",
    colorTo: "to-cyan-400",
  },
  {
    id: "technic-rally-racer",
    name: "Technic Rally Racer",
    theme: "Technic",
    price: 129.99,
    pieces: 1042,
    ageRange: "10+",
    rating: 4.8,
    featured: true,
    blurb: "Functional suspension and a real gearbox.",
    description:
      "The Technic Rally Racer features working steering, independent suspension, and a 4-speed gearbox. A satisfying build for older fans who love mechanical detail.",
    colorFrom: "from-orange-700",
    colorTo: "to-amber-400",
  },
  {
    id: "creator-cozy-cottage",
    name: "Creator Cozy Cottage",
    theme: "Creator",
    price: 74.99,
    pieces: 528,
    ageRange: "8+",
    rating: 4.5,
    featured: true,
    blurb: "Three builds in one charming countryside home.",
    description:
      "Build a cottage, a windmill, or a lighthouse with this 3-in-1 Creator set. Rebuildable, full of character, and endlessly replayable.",
    colorFrom: "from-emerald-700",
    colorTo: "to-lime-400",
  },
  {
    id: "space-orbit-explorer",
    name: "Space Orbit Explorer",
    theme: "Space",
    price: 99.99,
    pieces: 744,
    ageRange: "9+",
    rating: 4.7,
    featured: true,
    blurb: "Launch a modular station into low orbit.",
    description:
      "Assemble a modular orbital station with a docking arm, solar panels, and a detachable shuttle. Includes two astronaut minifigures and a rover.",
    colorFrom: "from-indigo-800",
    colorTo: "to-violet-400",
  },
  {
    id: "city-fire-brigade",
    name: "City Fire Brigade",
    theme: "City",
    price: 64.99,
    pieces: 410,
    ageRange: "6+",
    rating: 4.4,
    featured: false,
    blurb: "Race to the rescue with a ladder truck.",
    description:
      "The City Fire Brigade includes a ladder truck, a small fire scene, and three firefighter minifigures. A classic action-packed city set.",
    colorFrom: "from-red-700",
    colorTo: "to-orange-400",
  },
  {
    id: "technic-cargo-loader",
    name: "Technic Cargo Loader",
    theme: "Technic",
    price: 109.99,
    pieces: 868,
    ageRange: "10+",
    rating: 4.6,
    featured: false,
    blurb: "Pneumatic arms that actually lift.",
    description:
      "Operate real pneumatic lifting arms on this Technic Cargo Loader. A rewarding mechanical build with authentic functions and rugged styling.",
    colorFrom: "from-yellow-700",
    colorTo: "to-amber-300",
  },
  {
    id: "creator-botanical-garden",
    name: "Creator Botanical Garden",
    theme: "Creator",
    price: 59.99,
    pieces: 486,
    ageRange: "12+",
    rating: 4.9,
    featured: false,
    blurb: "A calming display of buildable blooms.",
    description:
      "Decorate any shelf with the Creator Botanical Garden. A relaxing build full of colorful flowers and leaves that never need watering.",
    colorFrom: "from-pink-600",
    colorTo: "to-rose-300",
  },
  {
    id: "space-lunar-rover",
    name: "Space Lunar Rover",
    theme: "Space",
    price: 49.99,
    pieces: 322,
    ageRange: "7+",
    rating: 4.3,
    featured: false,
    blurb: "Six-wheel rover for the moon's surface.",
    description:
      "Explore the lunar surface with this six-wheeled rover. Includes a sample-collection arm, an astronaut minifigure, and a small crater scene.",
    colorFrom: "from-slate-700",
    colorTo: "to-zinc-400",
  },
  {
    id: "city-train-express",
    name: "City Train Express",
    theme: "City",
    price: 159.99,
    pieces: 1216,
    ageRange: "8+",
    rating: 4.8,
    featured: false,
    blurb: "A full passenger train with track loop.",
    description:
      "Keep the city moving with the City Train Express. Includes a motorized engine, passenger cars, a station, and a full loop of track.",
    colorFrom: "from-teal-700",
    colorTo: "to-cyan-300",
  },
  {
    id: "creator-vintage-roadster",
    name: "Creator Vintage Roadster",
    theme: "Creator",
    price: 44.99,
    pieces: 298,
    ageRange: "9+",
    rating: 4.5,
    featured: false,
    blurb: "Classic curves in a compact display build.",
    description:
      "The Creator Vintage Roadster is a stylish display model with smooth curves, chrome accents, and an opening hood revealing a detailed engine.",
    colorFrom: "from-amber-700",
    colorTo: "to-yellow-300",
  },
];

export function listProducts(opts?: {
  theme?: string | null;
  featured?: boolean | null;
}): Product[] {
  let results = PRODUCTS;
  if (opts?.theme) {
    const t = opts.theme.toLowerCase();
    results = results.filter((p) => p.theme.toLowerCase() === t);
  }
  if (opts?.featured) {
    results = results.filter((p) => p.featured);
  }
  return results;
}

export function getProduct(id: string): Product | undefined {
  return PRODUCTS.find((p) => p.id === id);
}

export function listThemes(): string[] {
  return Array.from(new Set(PRODUCTS.map((p) => p.theme))).sort();
}
