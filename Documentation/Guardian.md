# Veil Guardian combat

One enemy, not the other five future concepts. Primitive bronze armor and heavy limbs surround a visible energy core; an armor plate covers it in Living and disappears in Broken. Hit feedback briefly squashes the visual body. Defeat tips the visual body over and triggers Victory.

FSM:
- **Observe:** 1.5 s at start/reset.
- **Approach:** move at 2.2 m/s until within 3.3 m for slam or 8 m for strike; turn toward the player. Attacks also require a 1.2 s cooldown after recovery.
- **Telegraph:** face the current player position once, then hold aim for 1.3 s. Orange disc marks slam radius; orange rectangle marks the strike lane. HUD names the impending attack.
- **Attack:** one explicit damage evaluation, followed by .22 s of impact footprint. No persistent damage overlap.
- **Recovery:** 1.6 s, stationary and punishable.
- **Stagger:** damage accumulated past 100 during recovery opens a 1.4 s stagger. Attacks remain interruptible only at this deliberate opening.
- **Defeated:** no AI or damage, visible collapsed pose, result overlay.

Ground slam deals 25 damage within 4.2 m plus player capsule radius. Energy strike is a deterministic forward lane, 10 m long and 2.2 m wide (plus capsule radius), dealing 20. Both reject vertical separations above 2.5 m and require clear environment line of sight. Dodge provides temporary immunity; cover can block a direct hit. Damage is resolved once at the end of windup, not continuously.

Health: 540. Player's base hit: 18. Living armor takes 0.65× (11.7); Broken exposed core takes 2× (36). Living is never permanently invulnerable. At these defaults Broken needs 15 clean hits; actual encounter difficulty still requires playtesting.

The boss uses a CharacterController and `ArenaNavigator`, not NavMesh. Direct approach is used only with sight and a clear capsule sweep. Otherwise a four-connected 31×31 flat grid routes around cover to an in-range, visible position. Clearance is cached lazily and invalidated on shifts/restart; search uses bounded arrays and is throttled to 0.4 s. There is no support for arbitrary terrain, moving obstacles or open-world routing. Profile worst-case search time on target devices. A regression test is prepared for pursuit around the Living western cover. Both initialization subscriptions are guarded against repeated setup.
