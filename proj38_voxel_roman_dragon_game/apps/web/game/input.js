// proj38 — Input. Maps keyboard / pointer / touch to a simple input struct the engine reads.
// Exposes: input.left, input.right, input.fire (edge), and helpers for UI buttons.
export class Input {
  constructor(canvas, { onStart, onPause, onRestart, getAimFromPointer } = {}) {
    this.left = false;
    this.right = false;
    this.fireHeld = false;
    this._fireEdge = false;
    this.pointerX = null; // normalized -1..1 across the canvas, or null
    this.onStart = onStart || (() => {});
    this.onPause = onPause || (() => {});
    this.onRestart = onRestart || (() => {});

    const keydown = (e) => {
      switch (e.code) {
        case "ArrowLeft": case "KeyA": this.left = true; break;
        case "ArrowRight": case "KeyD": this.right = true; break;
        case "Space":
          this.fireHeld = true; this._fireEdge = true;
          this.onStart(); // space also starts from title
          e.preventDefault();
          break;
        case "Enter": this.onStart(); break;
        case "KeyP": this.onPause(); break;
        case "KeyR": this.onRestart(); break;
      }
    };
    const keyup = (e) => {
      switch (e.code) {
        case "ArrowLeft": case "KeyA": this.left = false; break;
        case "ArrowRight": case "KeyD": this.right = false; break;
        case "Space": this.fireHeld = false; break;
      }
    };
    window.addEventListener("keydown", keydown);
    window.addEventListener("keyup", keyup);

    // pointer: move to aim, press to fire
    const rectX = (clientX) => {
      const r = canvas.getBoundingClientRect();
      return ((clientX - r.left) / r.width) * 2 - 1; // -1..1
    };
    const pointermove = (e) => { this.pointerX = rectX(e.clientX); };
    const pointerdown = (e) => {
      this.pointerX = rectX(e.clientX);
      this.fireHeld = true; this._fireEdge = true;
      this.onStart();
    };
    const pointerup = () => { this.fireHeld = false; };
    canvas.addEventListener("pointermove", pointermove);
    canvas.addEventListener("pointerdown", pointerdown);
    window.addEventListener("pointerup", pointerup);

    this._cleanup = () => {
      window.removeEventListener("keydown", keydown);
      window.removeEventListener("keyup", keyup);
      canvas.removeEventListener("pointermove", pointermove);
      canvas.removeEventListener("pointerdown", pointerdown);
      window.removeEventListener("pointerup", pointerup);
    };
  }

  // Bind on-screen touch buttons (mobile). Each arg is an element or null.
  bindTouchButtons({ leftBtn, rightBtn, fireBtn } = {}) {
    const hold = (el, on, off) => {
      if (!el) return;
      const press = (e) => { e.preventDefault(); on(); };
      const release = (e) => { e.preventDefault(); off(); };
      el.addEventListener("pointerdown", press);
      el.addEventListener("pointerup", release);
      el.addEventListener("pointerleave", release);
      el.addEventListener("pointercancel", release);
    };
    hold(leftBtn, () => (this.left = true), () => (this.left = false));
    hold(rightBtn, () => (this.right = true), () => (this.right = false));
    hold(fireBtn,
      () => { this.fireHeld = true; this._fireEdge = true; },
      () => { this.fireHeld = false; });
  }

  // Build the per-frame input the engine consumes. `aimX` lets pointer steer the aim:
  // when the pointer is active we translate pointerX into a desired lane and nudge.
  frame() {
    const fire = this.fireHeld; // engine rate-limits; holding to autofire is fine
    this._fireEdge = false;
    return { left: this.left, right: this.right, fire };
  }

  // Optional: pointer-driven absolute aim. Returns a lane target in [-1,1] or null.
  pointerAimNorm() { return this.pointerX; }

  dispose() { this._cleanup && this._cleanup(); }
}
