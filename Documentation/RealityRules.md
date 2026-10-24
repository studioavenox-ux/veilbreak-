# Reality shift rules

`VeilStateManager` owns Living/Broken and the one-second success cooldown. `VeilReactiveObject` switches its entire GameObject, so visual and collider presence agree. The manager retains explicit references even while objects are inactive; no per-frame searches are used.

On SHIFT during Playing:
1. Reject while cooling down; HUD displays remaining time.
2. Transiently activate inactive destination `VeilReactiveObject` roots in a `try/finally` scope and synchronize physics transforms so inactive target colliders have live PhysX shapes during validation and are restored immediately if the shift is rejected.
3. Check penetration of a `CapsuleCollider` probe matching the player's `CharacterController` center/radius/height against all active, enabled, non-trigger destination solids. More than .025 m penetration blocks the transition. This tolerance avoids false rejection at a resting surface.
4. Check the Guardian capsule against incoming solids too; reject with a HUD reason if forming stone would trap it. Ray-test down from the player's feet for support within 1.35 m, ignoring current-only objects while including shared and destination surfaces. Without destination/shared support, reject. This conservative check also rejects shifting far above the ground.
5. Apply object presence, shared energy color, sun, ambient light, fog, sky color, armor/core appearance, and damage rules without moving the player.
6. Start cooldown and publish an event for HUD feedback.

A rejection is explained on the HUD and consumes no cooldown. No coroutine, scene reload, teleport, or animation substitutes for world-state changes. Reset applies Living and clears the cooldown before resetting the guardian.

Current support validation assumes static/shared floor and simple box platforms as built by this arena. Before introducing moving platforms, overhang puzzles, uneven terrain, or arbitrary destination meshes, extend the support test to query destination geometry directly and cover those cases with physics tests. The current validator is deliberately not a general-purpose level streaming system.
