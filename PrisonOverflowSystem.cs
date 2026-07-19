// Attribution: this system is an architectural port of the author's Cemetery Overflow
// Manager. Two core techniques — scaling prefab data (PrisonData here) anchored to its
// authoring baseline, and restoring a value only if nothing else has changed it since —
// derive from the MIT-licensed mod "Magic Hearse + Funeral Director",
// Copyright (c) 2026 River-Mochi (https://github.com/River-Mochi/MagicHearse).
// See the third-party notice in this project's LICENSE file. The backlog-triggered
// temporary scaling, grace-period early release, and the code in this file are original.

using System.Collections.Generic;
using Game;
using Game.Citizens;
using Game.Common;
using Game.Prefabs;
using Game.Simulation;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;

namespace PrisonOverflowManager
{
    /// <summary>
    /// Eases prison-transport backlogs (sentenced criminals stuck at police stations waiting for a
    /// prison van) in two stages.
    ///
    /// Soft scaling (on by default) temporarily raises the prison van capacity of every prison
    /// while the city is backlogged, so more vans are dispatched to collect prisoners, and puts it
    /// back once the backlog clears. Nobody is released. Early release (off by default) is a last
    /// resort that frees sentenced criminals who have been stuck awaiting transport for longer than
    /// the grace period.
    /// </summary>
    public partial class PrisonOverflowSystem : GameSystemBase
    {
        /// <summary>Simulation frames per in-game day.</summary>
        public const int kFramesPerDay = 262144;

        /// <summary>Upper bound of the "sweeps per day" setting; also the system's base tick rate.</summary>
        public const int kMaxSweepsPerDay = 64;

        /// <summary>
        /// How long the backlog must stay under the threshold before the city counts as recovered
        /// and every early-release grace period restarts. Soft scaling exists precisely to push the
        /// backlog under the threshold, so a brief dip is the system working — not a reason to
        /// throw away days of accumulated tracking.
        /// </summary>
        public const int kRecoveryFrames = kFramesPerDay;

        /// <summary>
        /// Master gate for the early-release fallback. Held false while the feature is still being
        /// verified in-game: the option stays visible in the UI but is locked off, and this system
        /// never runs the release path regardless of any value persisted in a settings file. Flip
        /// this one field to true — no other change needed — once early release has been confirmed
        /// working in a throwaway save.
        /// </summary>
        public static readonly bool EarlyReleaseAvailable = false;

        /// <summary>Sentenced criminals currently awaiting a prison van, as of the last sweep.</summary>
        public static int CurrentBacklog;

        /// <summary>Highest backlog seen since game start (shown in options UI).</summary>
        public static int PeakBacklog;

        /// <summary>Whether scaled-up van capacity is applied right now.</summary>
        public static bool ScalingActive;

        /// <summary>Criminals freed by the early-release fallback since game start.</summary>
        public static int TotalReleased;

        private SimulationSystem m_SimulationSystem;
        private PrefabSystem m_PrefabSystem;
        private EntityQuery m_CriminalQuery;
        private EntityQuery m_PrisonPrefabQuery;

        // First simulation frame at which we saw each criminal stuck awaiting transport, used for
        // the early-release grace period. In-memory only: after loading a save the grace period
        // simply restarts, which errs on the side of releasing later, not sooner.
        private readonly Dictionary<Entity, uint> m_StuckSince = new Dictionary<Entity, uint>();

        // What we last wrote to each scaled prefab. If a prefab's data no longer matches, some
        // other mod (or the game) changed it after us, so we leave it alone rather than stomp it.
        private readonly Dictionary<Entity, PrisonData> m_Applied =
            new Dictionary<Entity, PrisonData>();

        private uint m_LastSweepFrame;

        // Last frame the backlog was at or above the threshold, used to tell a brief dip apart
        // from the city genuinely catching up.
        private uint m_LastBacklogFrame;

        public static void ResetStatistics()
        {
            PeakBacklog = 0;
            TotalReleased = 0;
        }

        protected override void OnCreate()
        {
            base.OnCreate();

            m_SimulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();
            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();

            // CriminalFlags live in component data, not the archetype, so the flag check itself
            // happens per-entity in CountBacklog / EarlyRelease.
            m_CriminalQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<Criminal>(),
                },
                None = new[]
                {
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Temp>(),
                },
            });

            // Prefab entities for prisons. The van capacity knob lives here, not on the placed
            // buildings, so scaling these covers every prison in the city at once.
            m_PrisonPrefabQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadWrite<PrisonData>(),
                },
            });
        }

        public override int GetUpdateInterval(SystemUpdatePhase phase)
        {
            // Tick at the fastest configurable rate; OnUpdate throttles down to the
            // configured sweeps-per-day so the slider takes effect immediately.
            return kFramesPerDay / kMaxSweepsPerDay;
        }

        protected override void OnUpdate()
        {
            var setting = Mod.Setting;
            if (setting == null || !setting.EnableMod)
            {
                // Never leave scaled capacity applied after the mod is switched off.
                if (ScalingActive)
                {
                    RestoreScaling();
                }

                return;
            }

            var frame = m_SimulationSystem.frameIndex;
            var sweepsPerDay = Clamp(setting.SweepsPerDay, 1, kMaxSweepsPerDay);
            var sweepInterval = (uint)(kFramesPerDay / sweepsPerDay);

            // frame < m_LastSweepFrame means a different (older) save was loaded; sweep now.
            if (frame >= m_LastSweepFrame && frame - m_LastSweepFrame < sweepInterval)
            {
                return;
            }

            m_LastSweepFrame = frame;

            CurrentBacklog = CountBacklog();
            if (CurrentBacklog > PeakBacklog)
            {
                PeakBacklog = CurrentBacklog;
            }

            if (setting.EnableSoftScaling)
            {
                UpdateSoftScaling(setting, CurrentBacklog);
            }
            else if (ScalingActive)
            {
                RestoreScaling();
            }

            if (EarlyReleaseAvailable && setting.EnableEarlyRelease)
            {
                var graceFrames = (uint)((long)Clamp(setting.ReleaseGraceDays, 1, 30) * kFramesPerDay);
                TotalReleased += EarlyRelease(setting, frame, graceFrames);
            }
            else
            {
                m_StuckSince.Clear();
            }
        }

        /// <summary>
        /// Raises prison van capacity once the backlog crosses the threshold and lowers it again
        /// once the backlog has largely cleared. The restore point sits at half the threshold so a
        /// city hovering right at the limit doesn't flip capacity on and off every sweep.
        /// </summary>
        private void UpdateSoftScaling(Setting setting, int backlog)
        {
            var threshold = Clamp(setting.BacklogThreshold, 1, 200);
            var factor = Clamp(setting.ScaleFactor, 100, 500);

            if (!ScalingActive)
            {
                if (backlog >= threshold)
                {
                    ApplyScaling(factor);
                }

                return;
            }

            if (backlog <= threshold / 2)
            {
                RestoreScaling();
            }
            else
            {
                // Threshold or factor may have been changed in the options UI mid-backlog.
                ApplyScaling(factor);
            }
        }

        private void ApplyScaling(int factor)
        {
            var prefabs = m_PrisonPrefabQuery.ToEntityArray(Allocator.Temp);
            var scaledCount = 0;

            foreach (var prefab in prefabs)
            {
                if (!TryGetBaseline(prefab, out var baseline))
                {
                    continue;
                }

                var current = EntityManager.GetComponentData<PrisonData>(prefab);

                // Always scale off the authoring baseline, never off the live value, or repeated
                // sweeps would compound the multiplier. Only van capacity is touched — prisoner
                // holding capacity is left alone so the "build more prisons" loop stays intact.
                var scaled = current;
                scaled.m_PrisonVanCapacity = Scale(baseline.m_PrisonVanCapacity, factor);

                if (scaled.Equals(current))
                {
                    m_Applied[prefab] = scaled;
                    continue;
                }

                EntityManager.SetComponentData(prefab, scaled);
                m_Applied[prefab] = scaled;
                scaledCount++;
            }

            prefabs.Dispose();

            if (!ScalingActive)
            {
                ScalingActive = true;
                Mod.Log.Info($"Backlog reached {CurrentBacklog} criminal(s) awaiting a prison van; scaled {scaledCount} prison prefab(s) to {factor}% of vanilla van capacity.");
            }
            else if (scaledCount > 0)
            {
                Mod.Log.Info($"Re-applied prison van scaling at {factor}% to {scaledCount} prefab(s).");
            }
        }

        private void RestoreScaling()
        {
            var restored = 0;
            var skipped = 0;

            foreach (var pair in m_Applied)
            {
                var prefab = pair.Key;
                if (!EntityManager.Exists(prefab) ||
                    !EntityManager.HasComponent<PrisonData>(prefab))
                {
                    continue;
                }

                var current = EntityManager.GetComponentData<PrisonData>(prefab);

                // Somebody else wrote to this prefab after we did; their value wins.
                if (!current.Equals(pair.Value))
                {
                    skipped++;
                    continue;
                }

                if (!TryGetBaseline(prefab, out var baseline))
                {
                    continue;
                }

                var restoredData = current;
                restoredData.m_PrisonVanCapacity = baseline.m_PrisonVanCapacity;

                // Skip no-op writes (e.g. upgrade prefabs whose van capacity is 0) so this
                // counter stays symmetric with the one logged when scaling was applied.
                if (restoredData.Equals(current))
                {
                    continue;
                }

                EntityManager.SetComponentData(prefab, restoredData);
                restored++;
            }

            m_Applied.Clear();
            ScalingActive = false;

            if (restored > 0 || skipped > 0)
            {
                var suffix = skipped > 0 ? $" ({skipped} left alone — changed by something else since)" : string.Empty;
                Mod.Log.Info($"Backlog cleared; restored {restored} prison prefab(s) to vanilla van capacity{suffix}.");
            }
        }

        /// <summary>
        /// Reads a prison prefab's true vanilla van capacity from its authoring component, which
        /// the game never rewrites — unlike the runtime <see cref="PrisonData"/> we scale.
        /// </summary>
        private bool TryGetBaseline(Entity prefabEntity, out PrisonData baseline)
        {
            baseline = default;

            if (!m_PrefabSystem.TryGetPrefab<PrefabBase>(prefabEntity, out var prefab) || prefab == null)
            {
                return false;
            }

            // Fully qualified: Game.Buildings.Prison is the runtime component of the same short
            // name, and binding to it here would silently never match.
            if (!prefab.TryGetExactly<Game.Prefabs.Prison>(out var authoring) || authoring == null)
            {
                return false;
            }

            baseline.m_PrisonVanCapacity = authoring.m_PrisonVanCapacity;
            baseline.m_PrisonerCapacity = authoring.m_PrisonerCapacity;
            baseline.m_PrisonerWellbeing = authoring.m_PrisonerWellbeing;
            baseline.m_PrisonerHealth = authoring.m_PrisonerHealth;
            return true;
        }

        private int CountBacklog()
        {
            if (m_CriminalQuery.IsEmptyIgnoreFilter)
            {
                return 0;
            }

            var criminals = m_CriminalQuery.ToComponentDataArray<Criminal>(Allocator.Temp);
            var count = 0;

            foreach (var criminal in criminals)
            {
                if (IsAwaitingTransport(criminal.m_Flags))
                {
                    count++;
                }
            }

            criminals.Dispose();
            return count;
        }

        /// <summary>
        /// Frees criminals who have been sentenced and awaiting a prison van for longer than the
        /// grace period, but only while the city is still backlogged despite soft scaling.
        /// </summary>
        private int EarlyRelease(Setting setting, uint frame, uint graceFrames)
        {
            var threshold = Clamp(setting.BacklogThreshold, 1, 200);

            // frame < m_LastBacklogFrame means a different (older) save was loaded.
            if (frame < m_LastBacklogFrame)
            {
                m_LastBacklogFrame = frame;
                m_StuckSince.Clear();
            }

            if (CurrentBacklog >= threshold)
            {
                m_LastBacklogFrame = frame;
            }
            else if (frame - m_LastBacklogFrame >= kRecoveryFrames)
            {
                // Under the threshold for a full in-game day: the city has genuinely caught up,
                // so nobody is stranded and every grace period starts fresh.
                if (m_StuckSince.Count > 0)
                {
                    Mod.Log.Info($"Backlog stayed under {threshold} for an in-game day; reset grace tracking for {m_StuckSince.Count} criminal(s).");
                    m_StuckSince.Clear();
                }

                return 0;
            }
            else
            {
                // Only a brief dip — soft scaling doing its job. Hold the accumulated grace
                // periods rather than wiping them, but don't release anyone while the city copes.
                return 0;
            }

            if (m_CriminalQuery.IsEmptyIgnoreFilter)
            {
                m_StuckSince.Clear();
                return 0;
            }

            var entities = m_CriminalQuery.ToEntityArray(Allocator.Temp);
            var criminals = m_CriminalQuery.ToComponentDataArray<Criminal>(Allocator.Temp);
            var stillStuck = new HashSet<Entity>();
            var released = 0;

            for (var i = 0; i < entities.Length; i++)
            {
                if (!IsAwaitingTransport(criminals[i].m_Flags))
                {
                    continue;
                }

                var entity = entities[i];

                if (!m_StuckSince.TryGetValue(entity, out var since) || since > frame)
                {
                    // Newly stuck (or a save was loaded); start its grace period.
                    m_StuckSince[entity] = frame;
                    stillStuck.Add(entity);
                    continue;
                }

                if (frame - since >= graceFrames)
                {
                    Release(entity);
                    released++;
                }
                else
                {
                    stillStuck.Add(entity);
                }
            }

            entities.Dispose();
            criminals.Dispose();

            // Drop entries for criminals who were released, finally collected by a van, or belong
            // to a previously loaded city.
            var stale = new List<Entity>();
            foreach (var tracked in m_StuckSince.Keys)
            {
                if (!stillStuck.Contains(tracked))
                {
                    stale.Add(tracked);
                }
            }

            foreach (var entity in stale)
            {
                m_StuckSince.Remove(entity);
            }

            if (released > 0)
            {
                Mod.Log.Info($"Early release freed {released} criminal(s) stuck awaiting a prison van past their grace period. Session total: {TotalReleased + released}.");
            }

            return released;
        }

        /// <summary>
        /// Frees a stuck criminal exactly the way the game does when a sentence ends: removes the
        /// <see cref="Criminal"/> component and any lingering jail-related travel purpose. The
        /// police station drops them from its cells on its next tick once they are no longer a
        /// criminal.
        /// </summary>
        private void Release(Entity entity)
        {
            if (EntityManager.HasComponent<TravelPurpose>(entity))
            {
                var purpose = EntityManager.GetComponentData<TravelPurpose>(entity).m_Purpose;
                if (purpose == Purpose.GoingToJail || purpose == Purpose.InJail ||
                    purpose == Purpose.GoingToPrison || purpose == Purpose.InPrison)
                {
                    EntityManager.RemoveComponent<TravelPurpose>(entity);
                }
            }

            EntityManager.RemoveComponent<Criminal>(entity);
        }

        private static bool IsAwaitingTransport(CriminalFlags flags)
        {
            // Sentenced to prison but not yet loaded into a van (which sets Prisoner).
            return (flags & CriminalFlags.Sentenced) != 0
                && (flags & CriminalFlags.Prisoner) == 0;
        }

        private static int Scale(int value, int factor)
        {
            return (int)((long)value * factor / 100);
        }

        private static int Clamp(int value, int min, int max)
        {
            return value < min ? min : value > max ? max : value;
        }
    }
}
