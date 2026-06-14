// proj38 — Voxel renderer (Three.js). Reads engine state each frame and draws the scene.
// Owns NO game rules. Everything is built from boxes for a chunky voxel look.
import * as THREE from "three";
import { WORLD, CONFIG, aliveCount, isEnraged } from "./engine.js";

// Map engine lane-x [WORLD.minX..maxX] to world space X.
const LANE_SCALE = 2.2;
function laneToWorldX(x) { return x * LANE_SCALE; }

const LINE_Z = 16;  // legionaries stand here (near camera)
const KEEP_Z = -22; // keep / dragon perch (far)
const DRAGON_BASE_Y = 16; // dragon hovers above the keep

const COLORS = {
  sky: 0x8ec5ff,
  skyEnraged: 0xb14a3a,
  ground: 0xb9a06a,
  groundDark: 0xa8915d,
  stone: 0xc9bfa6,
  stoneDark: 0x9c937c,
  stoneShadow: 0x7d745f,
  banner: 0xb22222,
  bannerGold: 0xe3b341,
  legionBody: 0xb5453b,
  legionArmor: 0xc7ccd1,
  legionSkin: 0xe0b48a,
  crest: 0xd23b2e,
  shield: 0xb22222,
  shieldBoss: 0xe3b341,
  spear: 0x6b4a2b,
  spearTip: 0xd9dde2,
  dragonBody: 0x3f9b46,
  dragonBelly: 0xc7d96a,
  dragonWing: 0x2f7a36,
  dragonHorn: 0xe8e2c8,
  dragonEye: 0xffd23b,
  dragonEyeRage: 0xff5a2a,
  fire: 0xff7a1a,
  fireCore: 0xffd23b,
  pila: 0xcfd3d8,
  dust: 0xd8c79a,
  spark: 0xffe08a,
  torch: 0xff8a2a,
};

function box(w, h, d, color, opts = {}) {
  const geo = new THREE.BoxGeometry(w, h, d);
  const mat = new THREE.MeshLambertMaterial({
    color,
    emissive: opts.emissive ?? 0x000000,
    transparent: opts.transparent ?? false,
    opacity: opts.opacity ?? 1,
  });
  const m = new THREE.Mesh(geo, mat);
  m.castShadow = !!opts.cast;
  m.receiveShadow = !!opts.receive;
  return m;
}

export class Renderer {
  constructor(canvas) {
    this.canvas = canvas;
    this.renderer = new THREE.WebGLRenderer({ canvas, antialias: true });
    this.renderer.setPixelRatio(Math.min(2, window.devicePixelRatio || 1));
    this.renderer.shadowMap.enabled = true;
    this.renderer.shadowMap.type = THREE.PCFSoftShadowMap;

    this.scene = new THREE.Scene();
    this.scene.background = new THREE.Color(COLORS.sky);
    this.scene.fog = new THREE.Fog(COLORS.sky, 60, 120);

    this.camera = new THREE.PerspectiveCamera(55, 1, 0.1, 400);
    this.camera.position.set(0, 21, 48);
    this.camera.lookAt(0, 7, -6);

    this._torches = [];
    this._buildLights();
    this._buildGround();
    this._buildCastle();
    this._buildSky();

    this.soldiers = [];
    this._buildCohort();

    this.dragon = this._buildDragon();
    this.scene.add(this.dragon.group);

    this.aimMarker = this._buildAimMarker();
    this.scene.add(this.aimMarker);

    // dynamic pools
    this.pilaPool = [];
    this.firePool = [];
    this.particlePool = [];
    this.pilaGroup = new THREE.Group();
    this.fireGroup = new THREE.Group();
    this.particleGroup = new THREE.Group();
    this.scene.add(this.pilaGroup, this.fireGroup, this.particleGroup);

    this._t = 0;
    this._shake = 0;
    this.resize();
  }

  _buildLights() {
    this.scene.add(new THREE.AmbientLight(0xffffff, 0.55));
    this.hemi = new THREE.HemisphereLight(0xbfe3ff, 0x6b5a3a, 0.6);
    this.scene.add(this.hemi);
    this.sun = new THREE.DirectionalLight(0xfff4d6, 1.0);
    this.sun.position.set(-18, 38, 26);
    this.sun.castShadow = true;
    this.sun.shadow.mapSize.set(1024, 1024);
    const c = this.sun.shadow.camera;
    c.left = -55; c.right = 55; c.top = 55; c.bottom = -55; c.near = 1; c.far = 130;
    this.scene.add(this.sun);
  }

  _buildGround() {
    const g = new THREE.Group();
    const tile = 6, nx = 9, nz = 12;
    for (let i = 0; i < nx; i++) {
      for (let j = 0; j < nz; j++) {
        const dark = (i + j) % 2 === 0;
        const t = box(tile, 1, tile, dark ? COLORS.ground : COLORS.groundDark, { receive: true });
        t.position.set((i - nx / 2 + 0.5) * tile, -0.5, (j - nz / 2 + 0.5) * tile + 2);
        g.add(t);
      }
    }
    this.scene.add(g);
  }

  _crenellatedWall(length, height, horizontal) {
    const grp = new THREE.Group();
    const seg = 2;
    const count = Math.floor(length / seg);
    const main = box(horizontal ? length : 1.6, height, horizontal ? 1.6 : length, COLORS.stone, { cast: true, receive: true });
    main.position.y = height / 2;
    grp.add(main);
    for (let i = 0; i < count; i++) {
      if (i % 2 === 0) continue;
      const merlon = box(horizontal ? seg * 0.8 : 1.6, 1.4, horizontal ? 1.6 : seg * 0.8, COLORS.stoneDark, { cast: true });
      const along = (i - count / 2 + 0.5) * seg;
      merlon.position.set(horizontal ? along : 0, height + 0.7, horizontal ? 0 : along);
      grp.add(merlon);
    }
    return grp;
  }

  _tower(x, z, h) {
    const grp = new THREE.Group();
    const body = box(6, h, 6, COLORS.stone, { cast: true, receive: true });
    body.position.y = h / 2;
    grp.add(body);
    for (let i = -1; i <= 1; i++) {
      for (let k = -1; k <= 1; k++) {
        if ((i === 0 && k === 0) || (i !== 0 && k !== 0)) continue;
        const m = box(1.6, 1.6, 1.6, COLORS.stoneDark, { cast: true });
        m.position.set(i * 2.2, h + 0.8, k * 2.2);
        grp.add(m);
      }
    }
    const roof1 = box(6.4, 1.4, 6.4, COLORS.banner, { cast: true }); roof1.position.y = h + 2.2;
    const roof2 = box(4.2, 1.4, 4.2, COLORS.banner, { cast: true }); roof2.position.y = h + 3.4;
    const roof3 = box(2.0, 1.6, 2.0, COLORS.bannerGold, { cast: true }); roof3.position.y = h + 4.6;
    grp.add(roof1, roof2, roof3);
    grp.position.set(x, 0, z);
    return grp;
  }

  _buildCastle() {
    const castle = new THREE.Group();
    const wallSpan = WORLD.maxX * LANE_SCALE * 2 + 14;

    const back = this._crenellatedWall(wallSpan, 9, true);
    back.position.set(0, 0, KEEP_Z - 9);
    castle.add(back);

    const sideMidZ = (KEEP_Z - 9 + LINE_Z + 4) / 2;
    const sideLen = Math.abs(KEEP_Z - 9 - (LINE_Z + 4));
    const leftWall = this._crenellatedWall(sideLen, 7, false);
    leftWall.position.set(-wallSpan / 2, 0, sideMidZ);
    const rightWall = this._crenellatedWall(sideLen, 7, false);
    rightWall.position.set(wallSpan / 2, 0, sideMidZ);
    castle.add(leftWall, rightWall);

    castle.add(this._tower(-wallSpan / 2, KEEP_Z - 9, 13));
    castle.add(this._tower(wallSpan / 2, KEEP_Z - 9, 13));

    // central keep — the dragon perches above it
    const keep = new THREE.Group();
    const keepBody = box(16, 16, 12, COLORS.stone, { cast: true, receive: true });
    keepBody.position.y = 8;
    keep.add(keepBody);
    for (let i = -2; i <= 2; i++) {
      const m = box(2.0, 2.0, 2.0, COLORS.stoneDark, { cast: true });
      m.position.set(i * 3.2, 17, 5.6);
      const m2 = box(2.0, 2.0, 2.0, COLORS.stoneDark, { cast: true });
      m2.position.set(i * 3.2, 17, -5.6);
      keep.add(m, m2);
    }
    const gate = box(5, 8, 1, COLORS.stoneShadow, {});
    gate.position.set(0, 4, 6.2);
    keep.add(gate);
    keep.add(this._banner(-6.8, 13, 6.6));
    keep.add(this._banner(6.8, 13, 6.6));
    keep.position.set(0, 0, KEEP_Z);
    castle.add(keep);

    castle.add(this._torch(-wallSpan / 2 + 3, LINE_Z + 3));
    castle.add(this._torch(wallSpan / 2 - 3, LINE_Z + 3));

    this.scene.add(castle);
    this.castle = castle;
  }

  _banner(x, y, z) {
    const grp = new THREE.Group();
    const pole = box(0.4, 7, 0.4, COLORS.spear, { cast: true }); pole.position.y = y;
    const cloth = box(0.3, 4, 2.4, COLORS.banner, { cast: true }); cloth.position.set(0, y + 1, 1.4);
    const emblem = box(0.35, 1.2, 1.2, COLORS.bannerGold, {}); emblem.position.set(0, y + 1.4, 1.4);
    grp.add(pole, cloth, emblem);
    grp.position.set(x, 0, z);
    return grp;
  }

  _torch(x, z) {
    const grp = new THREE.Group();
    const pole = box(0.5, 6, 0.5, COLORS.spear, { cast: true }); pole.position.y = 3;
    const flame = box(1.1, 1.6, 1.1, COLORS.torch, { emissive: 0xff5a1a }); flame.position.y = 6.4;
    const light = new THREE.PointLight(0xff8a2a, 0.8, 22); light.position.set(0, 6.6, 0);
    grp.add(pole, flame, light);
    grp.userData.flame = flame;
    grp.position.set(x, 0, z);
    this._torches.push(grp);
    return grp;
  }

  _buildSky() {
    this.clouds = new THREE.Group();
    for (let i = 0; i < 6; i++) {
      const c = new THREE.Group();
      for (let k = 0; k < 3; k++) {
        const b = box(6 + Math.random() * 5, 3, 4, 0xffffff, { transparent: true, opacity: 0.9 });
        b.position.set(k * 4 - 4, Math.random() * 1.5, Math.random() * 2);
        c.add(b);
      }
      c.position.set(-60 + Math.random() * 120, 36 + Math.random() * 16, -72 + Math.random() * 30);
      c.userData.speed = 0.6 + Math.random() * 0.8;
      this.clouds.add(c);
    }
    this.scene.add(this.clouds);
  }

  _buildLegionary() {
    const g = new THREE.Group();
    const legL = box(0.7, 1.6, 0.7, COLORS.legionBody, { cast: true }); legL.position.set(-0.5, 0.8, 0);
    const legR = box(0.7, 1.6, 0.7, COLORS.legionBody, { cast: true }); legR.position.set(0.5, 0.8, 0);
    const skirt = box(2.0, 0.9, 1.2, COLORS.legionBody, { cast: true }); skirt.position.set(0, 1.7, 0);
    const torso = box(1.9, 2.0, 1.1, COLORS.legionArmor, { cast: true }); torso.position.set(0, 2.6, 0);
    const head = box(1.0, 1.0, 1.0, COLORS.legionSkin, { cast: true }); head.position.set(0, 4.0, 0);
    const helm = box(1.2, 0.7, 1.2, COLORS.legionArmor, { cast: true }); helm.position.set(0, 4.6, 0);
    const crest = box(0.35, 0.7, 1.5, COLORS.crest, { cast: true }); crest.position.set(0, 5.2, 0);
    const shield = box(0.4, 2.4, 1.7, COLORS.shield, { cast: true }); shield.position.set(-1.25, 2.5, -0.2);
    const boss = box(0.5, 0.7, 0.7, COLORS.shieldBoss, {}); boss.position.set(-1.45, 2.5, -0.2);
    const spear = box(0.25, 5.5, 0.25, COLORS.spear, { cast: true }); spear.position.set(1.2, 3.0, 0); spear.rotation.x = 0.12;
    const tip = box(0.4, 0.8, 0.4, COLORS.spearTip, {}); tip.position.set(1.2, 5.8, -0.3);
    g.add(legL, legR, skirt, torso, head, helm, crest, shield, boss, spear, tip);
    g.userData.crest = crest;
    g.scale.setScalar(0.62);
    return g;
  }

  _buildCohort() {
    this.cohortGroup = new THREE.Group();
    for (let i = 0; i < CONFIG.cohort.count; i++) {
      const s = this._buildLegionary();
      this.cohortGroup.add(s);
      this.soldiers.push(s);
    }
    this.scene.add(this.cohortGroup);
  }

  _buildAimMarker() {
    const g = new THREE.Group();
    const ring = box(2.6, 0.2, 2.6, COLORS.bannerGold, { emissive: 0x6b5300, transparent: true, opacity: 0.85 });
    ring.position.y = 0.25;
    const arrow = box(0.6, 0.3, 2.4, COLORS.fireCore, { emissive: 0x665100 });
    arrow.position.set(0, 0.4, -2.0);
    g.add(ring, arrow);
    g.userData.ring = ring;
    return g;
  }

  _buildDragon() {
    const group = new THREE.Group();
    const body = box(5.5, 3.6, 8, COLORS.dragonBody, { cast: true });
    const belly = box(4.0, 1.2, 7.0, COLORS.dragonBelly, {}); belly.position.set(0, -1.6, 0.2);
    const neck = box(2.2, 2.2, 3.2, COLORS.dragonBody, { cast: true }); neck.position.set(0, 1.6, 4.2); neck.rotation.x = -0.4;
    const head = box(3.0, 2.6, 3.4, COLORS.dragonBody, { cast: true }); head.position.set(0, 3.0, 6.6);
    const snout = box(2.0, 1.4, 1.8, COLORS.dragonBody, {}); snout.position.set(0, 2.5, 8.4);
    const hornL = box(0.5, 1.8, 0.5, COLORS.dragonHorn, { cast: true }); hornL.position.set(-0.9, 4.4, 6.0); hornL.rotation.z = 0.3;
    const hornR = box(0.5, 1.8, 0.5, COLORS.dragonHorn, { cast: true }); hornR.position.set(0.9, 4.4, 6.0); hornR.rotation.z = -0.3;

    this.dragonEyes = [];
    for (const sx of [-0.8, 0.8]) {
      const eye = box(0.6, 0.6, 0.4, COLORS.dragonEye, { emissive: 0x665200 });
      eye.position.set(sx, 3.4, 8.0);
      group.add(eye);
      this.dragonEyes.push(eye);
    }

    const wingL = box(0.4, 3.0, 6.5, COLORS.dragonWing, { cast: true }); wingL.position.set(-3.6, 1.5, -0.5);
    const wingR = box(0.4, 3.0, 6.5, COLORS.dragonWing, { cast: true }); wingR.position.set(3.6, 1.5, -0.5);
    const tail1 = box(1.8, 1.8, 3.0, COLORS.dragonBody, { cast: true }); tail1.position.set(0, 0, -5.0);
    const tail2 = box(1.0, 1.0, 3.0, COLORS.dragonBody, { cast: true }); tail2.position.set(0, -0.4, -7.6);
    const tailTip = box(1.6, 0.6, 1.4, COLORS.dragonHorn, {}); tailTip.position.set(0, -0.6, -9.2);
    const legFL = box(1.2, 2.2, 1.2, COLORS.dragonBody, { cast: true }); legFL.position.set(-2.2, -2.4, 2.4);
    const legFR = box(1.2, 2.2, 1.2, COLORS.dragonBody, { cast: true }); legFR.position.set(2.2, -2.4, 2.4);
    const legBL = box(1.4, 2.2, 1.4, COLORS.dragonBody, { cast: true }); legBL.position.set(-2.2, -2.4, -2.4);
    const legBR = box(1.4, 2.2, 1.4, COLORS.dragonBody, { cast: true }); legBR.position.set(2.2, -2.4, -2.4);

    group.add(body, belly, neck, head, snout, hornL, hornR, wingL, wingR, tail1, tail2, tailTip, legFL, legFR, legBL, legBR);
    group.scale.setScalar(1.05);

    const mouthLight = new THREE.PointLight(0xff5a1a, 0, 30);
    mouthLight.position.set(0, 2.5, 9);
    group.add(mouthLight);

    return { group, wingL, wingR, head, neck, mouthLight, bodyMats: [body, belly, neck, head, snout] };
  }

  resize() {
    const w = this.canvas.clientWidth || this.canvas.width || window.innerWidth;
    const h = this.canvas.clientHeight || this.canvas.height || window.innerHeight;
    this.renderer.setSize(w, h, false);
    this.camera.aspect = w / Math.max(1, h);
    this.camera.updateProjectionMatrix();
  }

  // ---- pooling helpers ----
  _ensurePool(pool, group, make, n) {
    while (pool.length < n) { const o = make(); group.add(o); pool.push(o); }
  }

  _makePila() {
    const g = new THREE.Group();
    const shaft = box(0.18, 2.0, 0.18, COLORS.spear, {}); shaft.rotation.x = Math.PI / 2.6;
    const tip = box(0.3, 0.3, 0.7, COLORS.pila, {}); tip.position.set(0, 0.0, 1.0);
    g.add(shaft, tip);
    return g;
  }

  _makeFire() {
    const g = new THREE.Group();
    const core = box(2.0, 1.4, 2.0, COLORS.fireCore, { emissive: 0xffae2a, transparent: true, opacity: 0.95 });
    const outer = box(3.2, 2.2, 3.2, COLORS.fire, { emissive: 0xff5a1a, transparent: true, opacity: 0.7 });
    g.add(outer, core);
    g.userData.core = core; g.userData.outer = outer;
    return g;
  }

  _makeParticle() {
    return box(0.5, 0.5, 0.5, COLORS.spark, { emissive: 0x4a3a00, transparent: true, opacity: 1 });
  }

  // Update the visual scene from engine `state`. dtMs drives local animation.
  sync(state, dtMs) {
    this._t += dtMs / 1000;
    const t = this._t;
    const enraged = isEnraged(state);
    const d = state.dragon;

    // mood: sky + fog shift toward red when enraged
    const targetSky = new THREE.Color(enraged ? COLORS.skyEnraged : COLORS.sky);
    this.scene.background.lerp(targetSky, 0.04);
    this.scene.fog.color.copy(this.scene.background);
    this.hemi.intensity = enraged ? 0.45 : 0.6;

    // drifting clouds
    for (const c of this.clouds.children) {
      c.position.x += c.userData.speed * dtMs / 1000;
      if (c.position.x > 72) c.position.x = -72;
    }
    // torch flicker
    for (const tg of this._torches) {
      const f = tg.userData.flame;
      const s = 1 + Math.sin(t * 12 + tg.position.x) * 0.18;
      f.scale.set(s, 1 + Math.cos(t * 9) * 0.2, s);
    }

    // ----- soldiers -----
    for (let i = 0; i < this.soldiers.length; i++) {
      const mesh = this.soldiers[i];
      const s = state.cohort[i];
      const sway = Math.sin(t * 2 + i) * 0.06;
      if (s.down) {
        // fallen: lie down + sink slightly
        mesh.rotation.x = -Math.PI / 2.2;
        mesh.position.set(laneToWorldX(s.x), 0.2, LINE_Z + 0.6);
        mesh.visible = true;
        mesh.traverse((o) => { if (o.material) o.material.opacity = 0.85; });
      } else {
        mesh.rotation.x = 0;
        mesh.position.set(laneToWorldX(s.x), Math.abs(sway) * 0.5, LINE_Z);
        mesh.rotation.z = sway;
        mesh.visible = true;
      }
      // hit flash via crest brighten
      const crest = mesh.userData.crest;
      if (crest) crest.material.emissive.setHex(s.hitFlash > 0 ? 0x661100 : 0x000000);
    }

    // aim marker on the ground at the line
    this.aimMarker.position.set(laneToWorldX(state.aim.x), 0, LINE_Z - 4);
    const pulse = 0.8 + Math.sin(t * 6) * 0.2;
    this.aimMarker.userData.ring.material.opacity = state.status === "playing" ? pulse : 0.25;
    this.aimMarker.visible = state.status === "playing";

    // ----- dragon -----
    const dg = this.dragon;
    const hover = Math.sin(t * 1.6) * 0.8;
    dg.group.position.set(laneToWorldX(d.x), DRAGON_BASE_Y + hover, KEEP_Z);
    // face the cohort (look toward +Z / camera)
    dg.group.rotation.y = Math.PI;
    // bank slightly in the direction of travel
    dg.group.rotation.z = -d.dir * 0.12 + Math.sin(t * 1.2) * 0.03;
    // wing flap
    const flap = Math.sin(t * (enraged ? 10 : 6)) * 0.6;
    dg.wingL.rotation.z = 0.3 + flap;
    dg.wingR.rotation.z = -0.3 - flap;
    // eyes: rage color
    for (const eye of this.dragonEyes) eye.material.color.setHex(enraged ? COLORS.dragonEyeRage : COLORS.dragonEye);
    // hit flash: whiten body briefly
    const flashAmt = d.hitFlash || 0;
    for (const m of dg.bodyMats) m.material.emissive.setHex(flashAmt > 0.01 ? 0x554433 : 0x000000);

    // breathing telegraph/active: dip head + glow mouth
    let mouthGlow = 0;
    if (d.state === "breath") {
      const teleg = d.breathTelegraphMs > 0;
      dg.head.rotation.x = teleg ? 0.25 : 0.6; // rear back then lunge
      mouthGlow = teleg ? 1.5 : 3.0;
    } else if (d.state === "dive") {
      dg.head.rotation.x = 0.4;
      mouthGlow = 0.4;
    } else {
      dg.head.rotation.x = 0;
    }
    dg.mouthLight.intensity += (mouthGlow - dg.mouthLight.intensity) * 0.3;

    // ----- pila -----
    this._ensurePool(this.pilaPool, this.pilaGroup, () => this._makePila(), state.pila.length);
    for (let i = 0; i < this.pilaPool.length; i++) {
      const obj = this.pilaPool[i];
      const p = state.pila[i];
      if (!p) { obj.visible = false; continue; }
      obj.visible = true;
      // interpolate from the line (LINE_Z) up to the dragon (KEEP_Z, DRAGON height) by t
      const z = LINE_Z + (KEEP_Z - LINE_Z) * p.t;
      const y = 3 + (DRAGON_BASE_Y - 3) * p.t + Math.sin(p.t * Math.PI) * 3.0; // arc
      obj.position.set(laneToWorldX(p.x), y, z);
    }

    // ----- fire zones -----
    this._ensurePool(this.firePool, this.fireGroup, () => this._makeFire(), state.fireZones.length);
    for (let i = 0; i < this.firePool.length; i++) {
      const obj = this.firePool[i];
      const z = state.fireZones[i];
      if (!z) { obj.visible = false; continue; }
      obj.visible = true;
      const telegraphing = z.active && z.ttlMs > 0; // active zones are the danger
      obj.position.set(laneToWorldX(z.x), 1.4 + Math.sin(t * 18) * 0.4, LINE_Z);
      const sx = z.halfWidth * 0.9;
      obj.scale.set(sx, 1 + Math.sin(t * 22) * 0.3, 1.4);
      const flick = 0.7 + Math.sin(t * 30 + i) * 0.3;
      obj.userData.core.material.opacity = flick;
      obj.userData.outer.material.opacity = 0.55 * flick;
    }
    // beam from dragon mouth to active fire (visual link while breathing)
    if (d.state === "breath" && d.breathTelegraphMs <= 0) this._shake = Math.min(1, this._shake + 0.3);

    // ----- particles -----
    this._ensurePool(this.particlePool, this.particleGroup, () => this._makeParticle(), state.particles.length);
    for (let i = 0; i < this.particlePool.length; i++) {
      const obj = this.particlePool[i];
      const p = state.particles[i];
      if (!p) { obj.visible = false; continue; }
      obj.visible = true;
      const col = p.kind === "fire" ? COLORS.fire : p.kind === "dust" ? COLORS.dust : COLORS.spark;
      obj.material.color.setHex(col);
      // dust/down particles near the line; sparks near the dragon
      const zBase = p.kind === "spark" ? KEEP_Z : LINE_Z;
      const yBase = p.kind === "spark" ? DRAGON_BASE_Y : 1.5;
      obj.position.set(laneToWorldX(p.x), yBase + (1 - p.life) * 3, zBase + (p.y || 0));
      const sc = 0.3 + p.life * 0.7;
      obj.scale.setScalar(sc);
      obj.material.opacity = p.life;
    }

    // camera shake decay (impact feedback)
    this._shake *= 0.86;
    const sh = this._shake;
    this.camera.position.set(
      (Math.random() - 0.5) * sh * 1.2,
      21 + (Math.random() - 0.5) * sh * 0.8,
      48
    );
    this.camera.lookAt(0, 7, -6);
  }

  render() {
    this.renderer.render(this.scene, this.camera);
  }
}
