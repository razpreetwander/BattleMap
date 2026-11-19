// BattleMaps - 60FPS Territory Capture Engine by X Supreme
(function () {
    const canvas = document.getElementById("gameCanvas");
    const ctx = canvas.getContext("2d", { alpha: false });
    const minimapCanvas = document.getElementById("minimapCanvas");
    const miniCtx = minimapCanvas.getContext("2d");

    // UI elements
    const startScreen = document.getElementById("start-screen");
    const gameoverScreen = document.getElementById("gameover-screen");
    const hud = document.getElementById("hud");
    const playBtn = document.getElementById("play-btn");
    const restartBtn = document.getElementById("restart-btn");
    const playerNameInput = document.getElementById("player-name");
    const percentVal = document.getElementById("percent-val");
    const killsVal = document.getElementById("kills-val");
    const lbEntries = document.getElementById("lb-entries");
    const overPercent = document.getElementById("over-percent");
    const overKills = document.getElementById("over-kills");
    const overRank = document.getElementById("over-rank");
    const overTitle = document.getElementById("over-title");
    const joystick = document.getElementById("virtual-joystick");
    const joystickStick = document.getElementById("joystick-stick");

    // Sound FX using Web Audio API
    const AudioCtx = window.AudioContext || window.webkitAudioContext;
    let audioCtx = null;
    function playSfx(type) {
        try {
            if (!audioCtx) audioCtx = new AudioCtx();
            const now = audioCtx.currentTime;
            const osc = audioCtx.createOscillator();
            const gain = audioCtx.createGain();
            osc.connect(gain);
            gain.connect(audioCtx.destination);

            if (type === "capture") {
                osc.type = "sine";
                osc.frequency.setValueAtTime(320, now);
                osc.frequency.exponentialRampToValueAtTime(640, now + 0.15);
                gain.gain.setValueAtTime(0.2, now);
                gain.gain.exponentialRampToValueAtTime(0.01, now + 0.15);
                osc.start(now);
                osc.stop(now + 0.15);
            } else if (type === "kill") {
                osc.type = "sawtooth";
                osc.frequency.setValueAtTime(450, now);
                osc.frequency.exponentialRampToValueAtTime(150, now + 0.25);
                gain.gain.setValueAtTime(0.3, now);
                gain.gain.exponentialRampToValueAtTime(0.01, now + 0.25);
                osc.start(now);
                osc.stop(now + 0.25);
            } else if (type === "death") {
                osc.type = "triangle";
                osc.frequency.setValueAtTime(280, now);
                osc.frequency.exponentialRampToValueAtTime(70, now + 0.35);
                gain.gain.setValueAtTime(0.35, now);
                gain.gain.exponentialRampToValueAtTime(0.01, now + 0.35);
                osc.start(now);
                osc.stop(now + 0.35);
            }
        } catch (_) {}
    }

    function triggerVibrate(ms) {
        if (window.AndroidBridge && window.AndroidBridge.vibrate) {
            window.AndroidBridge.vibrate(ms);
        } else if (navigator.vibrate) {
            navigator.vibrate(ms);
        }
    }

    // Grid Configuration
    const GRID_SIZE = 160; // 160x160 cells
    const CELL_SIZE = 10;  // 10px world units per cell => 1600x1600 arena
    const WORLD_SIZE = GRID_SIZE * CELL_SIZE;
    const ARENA_RADIUS = WORLD_SIZE / 2 - 20;
    const ARENA_CENTER = WORLD_SIZE / 2;

    // Grid buffer: 0 = neutral, id = player/bot ID
    const grid = new Uint8Array(GRID_SIZE * GRID_SIZE);

    // Entity Colors
    const BOT_PALETTES = [
        { main: "#EF4444", dark: "#991B1B" },
        { main: "#F97316", dark: "#9A3412" },
        { main: "#F59E0B", dark: "#78350F" },
        { main: "#8B5CF6", dark: "#5B21B6" },
        { main: "#EC4899", dark: "#9D174D" },
        { main: "#3B82F6", dark: "#1E3A8A" },
        { main: "#14B8A6", dark: "#115E59" },
        { main: "#84CC16", dark: "#3F6212" },
    ];

    let playerColor = "#10B981";
    let playerColorDark = "#064E3B";

    // Setup color picker
    document.querySelectorAll(".color-btn").forEach(btn => {
        btn.addEventListener("click", () => {
            document.querySelectorAll(".color-btn").forEach(b => b.classList.remove("active"));
            btn.classList.add("active");
            playerColor = btn.getAttribute("data-color");
            playerColorDark = adjustColor(playerColor, -40);
        });
    });

    function adjustColor(hex, amount) {
        let col = hex.replace("#", "");
        let num = parseInt(col, 16);
        let r = Math.max(0, Math.min(255, (num >> 16) + amount));
        let g = Math.max(0, Math.min(255, ((num >> 8) & 0x00FF) + amount));
        let b = Math.max(0, Math.min(255, (num & 0x0000FF) + amount));
        return `rgb(${r},${g},${b})`;
    }

    // Screen resize
    let viewW = window.innerWidth;
    let viewH = window.innerHeight;
    function resize() {
        viewW = window.innerWidth;
        viewH = window.innerHeight;
        canvas.width = viewW;
        canvas.height = viewH;
    }
    window.addEventListener("resize", resize);
    resize();

    // Game state
    let isRunning = false;
    let nextId = 1;
    let player = null;
    let bots = [];
    let particles = [];
    let floatingTexts = [];
    let camera = { x: ARENA_CENTER, y: ARENA_CENTER };

    class Entity {
        constructor(id, name, color, darkColor, isBot = false) {
            this.id = id;
            this.name = name;
            this.color = color;
            this.darkColor = darkColor;
            this.isBot = isBot;

            this.x = 0;
            this.y = 0;
            this.angle = 0;
            this.targetAngle = 0;
            this.speed = 2.4;
            this.radius = 8;

            this.trail = [];
            this.isOutside = false;
            this.kills = 0;
            this.conqueredTiles = 0;
            this.dead = false;

            // Bot behavior variables
            this.aiChangeTimer = 0;
            this.aiLoopTimer = 0;
        }

        spawn(spawnX, spawnY) {
            this.x = spawnX;
            this.y = spawnY;
            this.angle = Math.random() * Math.PI * 2;
            this.targetAngle = this.angle;
            this.trail = [];
            this.isOutside = false;
            this.dead = false;

            // Claim initial 5x5 circle in grid
            const cx = Math.floor(spawnX / CELL_SIZE);
            const cy = Math.floor(spawnY / CELL_SIZE);
            const r = 4;
            for (let dy = -r; dy <= r; dy++) {
                for (let dx = -r; dx <= r; dx++) {
                    if (dx * dx + dy * dy <= r * r) {
                        const gx = cx + dx;
                        const gy = cy + dy;
                        if (gx >= 0 && gx < GRID_SIZE && gy >= 0 && gy < GRID_SIZE) {
                            grid[gy * GRID_SIZE + gx] = this.id;
                        }
                    }
                }
            }
            this.updateStats();
        }

        updateStats() {
            let count = 0;
            for (let i = 0; i < grid.length; i++) {
                if (grid[i] === this.id) count++;
            }
            this.conqueredTiles = count;
        }

        update() {
            if (this.dead) return;

            // Turn towards target angle
            let diff = this.targetAngle - this.angle;
            while (diff < -Math.PI) diff += Math.PI * 2;
            while (diff > Math.PI) diff -= Math.PI * 2;
            this.angle += diff * 0.22;

            // Move
            const nx = this.x + Math.cos(this.angle) * this.speed;
            const ny = this.y + Math.sin(this.angle) * this.speed;

            // Boundary check
            const distFromCenter = Math.hypot(nx - ARENA_CENTER, ny - ARENA_CENTER);
            if (distFromCenter > ARENA_RADIUS - this.radius) {
                if (!this.isBot) {
                    this.die("Border Impact");
                    return;
                } else {
                    // Turn away from wall
                    this.targetAngle = Math.atan2(ARENA_CENTER - ny, ARENA_CENTER - nx);
                }
            }

            this.x = nx;
            this.y = ny;

            // Check current cell in grid
            const gx = Math.floor(this.x / CELL_SIZE);
            const gy = Math.floor(this.y / CELL_SIZE);

            if (gx >= 0 && gx < GRID_SIZE && gy >= 0 && gy < GRID_SIZE) {
                const cellOwner = grid[gy * GRID_SIZE + gx];

                if (cellOwner !== this.id) {
                    // Outside own territory: record trail
                    this.isOutside = true;
                    const last = this.trail[this.trail.length - 1];
                    if (!last || Math.hypot(last.x - this.x, last.y - this.y) >= 4) {
                        // Check self trail collision
                        for (let i = 0; i < this.trail.length - 8; i++) {
                            if (Math.hypot(this.trail[i].x - this.x, this.trail[i].y - this.y) < this.radius) {
                                this.die("Self Trail Collision");
                                return;
                            }
                        }
                        this.trail.push({ x: this.x, y: this.y, gx, gy });
                    }
                } else if (this.isOutside && this.trail.length > 3) {
                    // Re-entered home: CAPTURE TERRITORY!
                    this.captureTerritory();
                    this.isOutside = false;
                    this.trail = [];
                }
            }

            // Bot AI updates
            if (this.isBot) {
                this.updateAI();
            }
        }

        updateAI() {
            this.aiChangeTimer--;
            this.aiLoopTimer++;

            // If outside for too long, steer back home
            if (this.isOutside && this.aiLoopTimer > 110) {
                // Find nearest cell of own territory
                const gx = Math.floor(this.x / CELL_SIZE);
                const gy = Math.floor(this.y / CELL_SIZE);
                let bestDist = Infinity;
                let targetX = ARENA_CENTER;
                let targetY = ARENA_CENTER;

                for (let r = 1; r < 25; r += 2) {
                    let found = false;
                    for (let dy = -r; dy <= r; dy += 2) {
                        for (let dx = -r; dx <= r; dx += 2) {
                            const cx = gx + dx;
                            const cy = gy + dy;
                            if (cx >= 0 && cx < GRID_SIZE && cy >= 0 && cy < GRID_SIZE) {
                                if (grid[cy * GRID_SIZE + cx] === this.id) {
                                    targetX = cx * CELL_SIZE + CELL_SIZE / 2;
                                    targetY = cy * CELL_SIZE + CELL_SIZE / 2;
                                    found = true;
                                    break;
                                }
                            }
                        }
                        if (found) break;
                    }
                    if (found) break;
                }

                this.targetAngle = Math.atan2(targetY - this.y, targetX - this.x);
            } else if (this.aiChangeTimer <= 0) {
                this.aiChangeTimer = 25 + Math.floor(Math.random() * 40);
                // Slight random wander
                this.targetAngle += (Math.random() - 0.5) * 1.4;
            }
        }

        captureTerritory() {
            // High-performance 2D flood fill on grid
            const visited = new Uint8Array(GRID_SIZE * GRID_SIZE);

            // Mark trail cells on grid
            const trailSet = new Set();
            for (const pt of this.trail) {
                if (pt.gx >= 0 && pt.gx < GRID_SIZE && pt.gy >= 0 && pt.gy < GRID_SIZE) {
                    trailSet.add(pt.gy * GRID_SIZE + pt.gx);
                }
            }

            // Queue for flood-filling from boundary
            const queue = [];

            // Seed boundary edges
            for (let x = 0; x < GRID_SIZE; x++) {
                queue.push(0 * GRID_SIZE + x);
                queue.push((GRID_SIZE - 1) * GRID_SIZE + x);
                visited[0 * GRID_SIZE + x] = 1;
                visited[(GRID_SIZE - 1) * GRID_SIZE + x] = 1;
            }
            for (let y = 0; y < GRID_SIZE; y++) {
                queue.push(y * GRID_SIZE + 0);
                queue.push(y * GRID_SIZE + (GRID_SIZE - 1));
                visited[y * GRID_SIZE + 0] = 1;
                visited[y * GRID_SIZE + (GRID_SIZE - 1)] = 1;
            }

            // BFS flood fill
            let head = 0;
            while (head < queue.length) {
                const idx = queue[head++];
                const cx = idx % GRID_SIZE;
                const cy = Math.floor(idx / GRID_SIZE);

                const neighbors = [
                    cx > 0 ? idx - 1 : -1,
                    cx < GRID_SIZE - 1 ? idx + 1 : -1,
                    cy > 0 ? idx - GRID_SIZE : -1,
                    cy < GRID_SIZE - 1 ? idx + GRID_SIZE : -1,
                ];

                for (const n of neighbors) {
                    if (n !== -1 && !visited[n]) {
                        // If cell is part of trail or owned by player, it blocks outer flood fill
                        if (!trailSet.has(n) && grid[n] !== this.id) {
                            visited[n] = 1;
                            queue.push(n);
                        }
                    }
                }
            }

            // Everything not reached by flood fill is CAPTURED!
            let capturedCount = 0;
            for (let i = 0; i < grid.length; i++) {
                if (!visited[i] || trailSet.has(i)) {
                    if (grid[i] !== this.id) {
                        grid[i] = this.id;
                        capturedCount++;
                    }
                }
            }

            this.updateStats();

            if (this === player) {
                playSfx("capture");
                spawnParticles(this.x, this.y, this.color, 14);
                if (capturedCount > 10) {
                    const percentGained = ((capturedCount / (GRID_SIZE * GRID_SIZE)) * 100).toFixed(1);
                    spawnFloatingText(`+${percentGained}%`, this.x, this.y - 20, "#10B981");
                }
            }

            this.aiLoopTimer = 0;
        }

        die(reason) {
            if (this.dead) return;
            this.dead = true;

            // Clear entity territory from grid
            for (let i = 0; i < grid.length; i++) {
                if (grid[i] === this.id) grid[i] = 0;
            }

            spawnParticles(this.x, this.y, this.color, 24);

            if (this === player) {
                playSfx("death");
                triggerVibrate(200);
                endGame();
            } else {
                // Respawn bot after 3 seconds
                setTimeout(() => {
                    if (isRunning) {
                        const spawnPos = getSafeSpawnPosition();
                        this.spawn(spawnPos.x, spawnPos.y);
                    }
                }, 3000);
            }
        }
    }

    // Particle FX
    function spawnParticles(x, y, color, count) {
        for (let i = 0; i < count; i++) {
            const angle = Math.random() * Math.PI * 2;
            const speed = 1.5 + Math.random() * 4.5;
            particles.push({
                x,
                y,
                vx: Math.cos(angle) * speed,
                vy: Math.sin(angle) * speed,
                color,
                life: 1.0,
                decay: 0.02 + Math.random() * 0.03,
                size: 3 + Math.random() * 4,
            });
        }
    }

    function spawnFloatingText(text, x, y, color) {
        floatingTexts.push({
            text,
            x,
            y,
            color,
            life: 1.0,
            vy: -1.2,
        });
    }

    function getSafeSpawnPosition() {
        for (let tries = 0; tries < 20; tries++) {
            const angle = Math.random() * Math.PI * 2;
            const r = Math.random() * (ARENA_RADIUS - 120);
            const x = ARENA_CENTER + Math.cos(angle) * r;
            const y = ARENA_CENTER + Math.sin(angle) * r;
            return { x, y };
        }
        return { x: ARENA_CENTER, y: ARENA_CENTER };
    }

    // Collision checking between entities and trails
    function checkTrailCollisions() {
        const allEntities = [player, ...bots];

        for (const killer of allEntities) {
            if (killer.dead) continue;

            for (const victim of allEntities) {
                if (victim.dead || !victim.isOutside || victim.trail.length < 2) continue;

                // Check distance to each segment of victim's trail
                for (let i = 0; i < victim.trail.length - 2; i++) {
                    const pt = victim.trail[i];
                    const dist = Math.hypot(killer.x - pt.x, killer.y - pt.y);

                    if (dist < killer.radius + 4) {
                        // Trail cut! Victim dies!
                        victim.die("Trail Sliced");
                        killer.kills++;

                        if (killer === player) {
                            playSfx("kill");
                            triggerVibrate(100);
                            spawnFloatingText("KILL! +500", victim.x, victim.y - 25, "#F59E0B");
                            killsVal.textContent = player.kills;
                        }
                        break;
                    }
                }
            }
        }
    }

    // Controls
    let touchActive = false;
    let touchStartX = 0;
    let touchStartY = 0;

    window.addEventListener("touchstart", (e) => {
        if (!isRunning) return;
        const touch = e.touches[0];
        touchActive = true;
        touchStartX = touch.clientX;
        touchStartY = touch.clientY;

        joystick.classList.remove("hidden");
        joystick.style.left = touchStartX + "px";
        joystick.style.top = touchStartY + "px";
        joystickStick.style.transform = `translate(0px, 0px)`;
    }, { passive: false });

    window.addEventListener("touchmove", (e) => {
        if (!isRunning || !touchActive) return;
        const touch = e.touches[0];
        const dx = touch.clientX - touchStartX;
        const dy = touch.clientY - touchStartY;
        const dist = Math.hypot(dx, dy);

        if (dist > 8) {
            player.targetAngle = Math.atan2(dy, dx);
            const maxDrag = 40;
            const clamped = Math.min(dist, maxDrag);
            const stickX = (dx / dist) * clamped;
            const stickY = (dy / dist) * clamped;
            joystickStick.style.transform = `translate(${stickX}px, ${stickY}px)`;
        }
    }, { passive: false });

    window.addEventListener("touchend", () => {
        touchActive = false;
        joystick.classList.add("hidden");
    });

    // Keyboard support
    const keys = {};
    window.addEventListener("keydown", (e) => { keys[e.key] = true; });
    window.addEventListener("keyup", (e) => { keys[e.key] = false; });

    function handleKeyboard() {
        if (!player || player.dead) return;
        let dx = 0;
        let dy = 0;
        if (keys["ArrowUp"] || keys["w"]) dy -= 1;
        if (keys["ArrowDown"] || keys["s"]) dy += 1;
        if (keys["ArrowLeft"] || keys["a"]) dx -= 1;
        if (keys["ArrowRight"] || keys["d"]) dx += 1;

        if (dx !== 0 || dy !== 0) {
            player.targetAngle = Math.atan2(dy, dx);
        }
    }

    // Main Game Loop
    let lastTime = performance.now();
    function loop(time) {
        requestAnimationFrame(loop);

        const dt = (time - lastTime) / 1000;
        lastTime = time;

        if (!isRunning) return;

        handleKeyboard();

        // Update player
        player.update();

        // Update bots
        for (const b of bots) {
            b.update();
        }

        checkTrailCollisions();

        // Update particles
        for (let i = particles.length - 1; i >= 0; i--) {
            const p = particles[i];
            p.x += p.vx;
            p.y += p.vy;
            p.life -= p.decay;
            if (p.life <= 0) particles.splice(i, 1);
        }

        // Update floating texts
        for (let i = floatingTexts.length - 1; i >= 0; i--) {
            const ft = floatingTexts[i];
            ft.y += ft.vy;
            ft.life -= 0.02;
            if (ft.life <= 0) floatingTexts.splice(i, 1);
        }

        // Smooth Camera Follow
        camera.x += (player.x - camera.x) * 0.12;
        camera.y += (player.y - camera.y) * 0.12;

        render();
        renderMinimap();
        updateHUD();
    }

    // Render Game World
    function render() {
        ctx.fillStyle = "#0D1117";
        ctx.fillRect(0, 0, viewW, viewH);

        ctx.save();
        ctx.translate(viewW / 2 - camera.x, viewH / 2 - camera.y);

        // Draw Arena Floor & Subtle Grid
        ctx.fillStyle = "#161B22";
        ctx.beginPath();
        ctx.arc(ARENA_CENTER, ARENA_CENTER, ARENA_RADIUS, 0, Math.PI * 2);
        ctx.fill();

        // Arena Border Ring
        ctx.strokeStyle = "#DC2626";
        ctx.lineWidth = 10;
        ctx.stroke();

        // Draw Territories
        const allEntities = [player, ...bots];
        const entityMap = new Map();
        for (const e of allEntities) entityMap.set(e.id, e);

        // Draw grid cells
        for (let gy = 0; gy < GRID_SIZE; gy++) {
            for (let gx = 0; gx < GRID_SIZE; gx++) {
                const ownerId = grid[gy * GRID_SIZE + gx];
                if (ownerId > 0) {
                    const owner = entityMap.get(ownerId);
                    if (owner) {
                        ctx.fillStyle = owner.color;
                        ctx.globalAlpha = 0.65;
                        ctx.fillRect(gx * CELL_SIZE, gy * CELL_SIZE, CELL_SIZE, CELL_SIZE);
                    }
                }
            }
        }
        ctx.globalAlpha = 1.0;

        // Draw Trails
        for (const e of allEntities) {
            if (e.dead || e.trail.length < 2) continue;
            ctx.strokeStyle = e.color;
            ctx.lineWidth = 6;
            ctx.lineCap = "round";
            ctx.lineJoin = "round";
            ctx.beginPath();
            ctx.moveTo(e.trail[0].x, e.trail[0].y);
            for (let i = 1; i < e.trail.length; i++) {
                ctx.lineTo(e.trail[i].x, e.trail[i].y);
            }
            ctx.stroke();
        }

        // Draw Entity Heads
        for (const e of allEntities) {
            if (e.dead) continue;

            ctx.save();
            ctx.translate(e.x, e.y);
            ctx.rotate(e.angle);

            // Head body (smooth square/circle)
            ctx.fillStyle = e.color;
            ctx.beginPath();
            ctx.arc(0, 0, e.radius, 0, Math.PI * 2);
            ctx.fill();

            // Head crown border
            ctx.strokeStyle = "#FFFFFF";
            ctx.lineWidth = 2.5;
            ctx.stroke();

            // Eyes looking forward
            ctx.fillStyle = "#FFFFFF";
            ctx.beginPath();
            ctx.arc(4, -3, 2.5, 0, Math.PI * 2);
            ctx.arc(4, 3, 2.5, 0, Math.PI * 2);
            ctx.fill();

            ctx.fillStyle = "#0F172A";
            ctx.beginPath();
            ctx.arc(5, -3, 1.2, 0, Math.PI * 2);
            ctx.arc(5, 3, 1.2, 0, Math.PI * 2);
            ctx.fill();

            ctx.restore();

            // Name tag
            ctx.fillStyle = "#FFFFFF";
            ctx.font = "bold 10px sans-serif";
            ctx.textAlign = "center";
            ctx.fillText(e.name, e.x, e.y - 14);
        }

        // Draw Particles
        for (const p of particles) {
            ctx.globalAlpha = p.life;
            ctx.fillStyle = p.color;
            ctx.beginPath();
            ctx.arc(p.x, p.y, p.size, 0, Math.PI * 2);
            ctx.fill();
        }
        ctx.globalAlpha = 1.0;

        // Draw Floating Texts
        for (const ft of floatingTexts) {
            ctx.globalAlpha = ft.life;
            ctx.fillStyle = ft.color;
            ctx.font = "bold 14px sans-serif";
            ctx.textAlign = "center";
            ctx.fillText(ft.text, ft.x, ft.y);
        }
        ctx.globalAlpha = 1.0;

        ctx.restore();
    }

    // Render Minimap
    function renderMinimap() {
        const mw = minimapCanvas.width;
        const mh = minimapCanvas.height;
        miniCtx.fillStyle = "#0B0E14";
        miniCtx.fillRect(0, 0, mw, mh);

        // Circular boundary
        miniCtx.strokeStyle = "#475569";
        miniCtx.lineWidth = 2;
        miniCtx.beginPath();
        miniCtx.arc(mw / 2, mh / 2, mw / 2 - 4, 0, Math.PI * 2);
        miniCtx.stroke();

        const scale = mw / WORLD_SIZE;

        // Player dot
        if (player && !player.dead) {
            miniCtx.fillStyle = "#10B981";
            miniCtx.beginPath();
            miniCtx.arc(player.x * scale, player.y * scale, 3.5, 0, Math.PI * 2);
            miniCtx.fill();
        }
    }

    // Update HUD & Leaderboard
    let hudTimer = 0;
    function updateHUD() {
        hudTimer++;
        if (hudTimer % 15 !== 0) return;

        const totalCells = GRID_SIZE * GRID_SIZE;
        const pPercent = ((player.conqueredTiles / totalCells) * 100).toFixed(1);
        percentVal.textContent = pPercent;

        // Leaderboard sorting
        const all = [player, ...bots];
        all.sort((a, b) => b.conqueredTiles - a.conqueredTiles);

        let html = "";
        for (let i = 0; i < Math.min(5, all.length); i++) {
            const e = all[i];
            const pct = ((e.conqueredTiles / totalCells) * 100).toFixed(1);
            const isMe = e === player;
            html += `<div class="lb-row ${isMe ? 'me' : ''}">
                <span>${i + 1}. ${e.name}</span>
                <span>${pct}%</span>
            </div>`;
        }
        lbEntries.innerHTML = html;
    }

    // Start Game
    function startGame() {
        const pName = playerNameInput.value.trim() || "Player 1";

        // Reset grid
        grid.fill(0);
        particles = [];
        floatingTexts = [];

        // Create player
        player = new Entity(1, pName, playerColor, playerColorDark, false);
        const pSpawn = getSafeSpawnPosition();
        player.spawn(pSpawn.x, pSpawn.y);

        // Create 8 bots
        bots = [];
        const botNames = ["BlazeBot", "ApexMap", "Shadow", "Titan", "Vortex", "Ghost", "Cipher", "Slayer"];
        for (let i = 0; i < 8; i++) {
            const palette = BOT_PALETTES[i % BOT_PALETTES.length];
            const bot = new Entity(i + 2, botNames[i], palette.main, palette.dark, true);
            const bSpawn = getSafeSpawnPosition();
            bot.spawn(bSpawn.x, bSpawn.y);
            bots.push(bot);
        }

        camera.x = player.x;
        camera.y = player.y;

        startScreen.classList.add("hidden");
        gameoverScreen.classList.add("hidden");
        hud.classList.remove("hidden");
        killsVal.textContent = "0";

        isRunning = true;
    }

    // Game Over
    function endGame() {
        isRunning = false;

        const totalCells = GRID_SIZE * GRID_SIZE;
        const pPercent = ((player.conqueredTiles / totalCells) * 100).toFixed(1);

        const all = [player, ...bots];
        all.sort((a, b) => b.conqueredTiles - a.conqueredTiles);
        const rank = all.indexOf(player) + 1;

        overPercent.textContent = pPercent + "%";
        overKills.textContent = player.kills;
        overRank.textContent = "#" + rank;

        if (rank === 1) {
            overTitle.textContent = "VICTORY! 👑";
            overTitle.style.color = "#10B981";
        } else {
            overTitle.textContent = "DEFEATED!";
            overTitle.style.color = "#EF4444";
        }

        hud.classList.add("hidden");
        gameoverScreen.classList.remove("hidden");
    }

    playBtn.addEventListener("click", startGame);
    restartBtn.addEventListener("click", startGame);

    // Initial loop start
    requestAnimationFrame(loop);
})();
