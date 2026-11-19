(() => {
    class CaptureCountdown {
        // Keep browser timers bound to their global receiver. Calling a stored
        // native setTimeout as this.schedule(...) can throw Illegal invocation.
        constructor({ canCapture, onTick, onCapture, now = () => performance.now(), schedule = (fn, delay) => globalThis.setTimeout(fn, delay), cancel = id => globalThis.clearTimeout(id) }) {
            Object.assign(this, { canCapture, onTick, onCapture, now, schedule, cancel });
            this.started = null; this.timer = null;
        }
        get active() { return this.started !== null; }
        update(ready) {
            if (!ready) { this.reset(); return; }
            if (this.active || this.fired) return;
            this.started = this.now(); this.pulse();
        }
        reset() {
            if (this.timer !== null) this.cancel(this.timer);
            this.timer = null; this.started = null; this.fired = false;
            this.onTick(null);
        }
        pulse() {
            if (!this.canCapture()) { this.reset(); return; }
            const remaining = Math.max(0, Math.ceil((3000 - (this.now() - this.started)) / 1000));
            if (remaining === 0) {
                this.timer = null; this.started = null; this.fired = true;
                this.onTick(null); this.onCapture(); return;
            }
            this.onTick(remaining);
            this.timer = this.schedule(() => this.pulse(), 100);
        }
    }
    if (typeof module !== 'undefined') module.exports = CaptureCountdown;
    else window.IdentityCaptureCountdown = CaptureCountdown;
})();
