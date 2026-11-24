using System.Collections.Generic;
using CloseTheDeal.FloorGen;
using UnityEngine;

namespace CloseTheDeal.FloorRace
{
    /// <summary>Host only. Picks where a wiped firm comes back: the authored spot nearest the briefcase on foot that no living rival is near or can see.</summary>
    public sealed class FloorRaceSpawns
    {
        const float ChestHeight = 1f;
        const int NearestFreeReach = 6;

        int[] _steps = new int[0];

        /// <summary>
        /// Tries the full rules first, then drops the sight check, then the distance check.
        /// <paramref name="rivalEyes"/> are the living rivals' eye positions in world space.
        /// </summary>
        public Vector3 Pick(BuiltFloor floor, Vector3 briefcaseWorld, List<Vector3> rivalEyes, FloorRaceProfile profile, int solidMask)
        {
            FloorLayout layout = floor.Layout;
            FloorNav nav = layout.Nav;
            if (_steps.Length != nav.Count)
                _steps = new int[nav.Count];

            Vector3 plan = floor.Plan(briefcaseWorld);
            int from = NearestFree(nav, nav.NodeAt(plan.x, plan.y, layout.LevelAt(plan.z)));
            if (from >= 0)
                nav.Distances(from, _steps);

            int minSteps = (int)(profile.MinCaseDistance / FloorNav.Node);
            float minRival = profile.MinRivalDistance * profile.MinRivalDistance, sight = profile.SightRange * profile.SightRange;
            for (int pass = 0; pass < 3; pass++)
            {
                int best = -1, bestSteps = int.MaxValue;
                for (int i = 0; i < layout.SpawnSpots.Count; i++)
                {
                    FloorSpot spot = layout.SpawnSpots[i];
                    if (spot.Room == layout.OfficeRoom)
                        continue;

                    int steps = from >= 0 ? _steps[nav.NodeAt(spot.X, spot.Y, spot.Level)] : i;
                    if (steps < 0 || steps >= bestSteps || (pass < 2 && steps < minSteps))
                        continue;
                    if (pass < 2 && Crowded(floor.World(spot, ChestHeight), rivalEyes, minRival, pass == 0 ? sight : 0f, solidMask))
                        continue;

                    best = i;
                    bestSteps = steps;
                }

                if (best >= 0)
                    return floor.World(layout.SpawnSpots[best]);
            }

            FloorSpot fallback = layout.StartB.Count > 0 ? layout.StartB[0] : layout.SpawnSpots[0];
            return floor.World(fallback);
        }

        static bool Crowded(Vector3 chest, List<Vector3> rivalEyes, float minRivalSqr, float sightSqr, int solidMask)
        {
            foreach (Vector3 eye in rivalEyes)
            {
                float distance = (eye - chest).sqrMagnitude;
                if (distance < minRivalSqr)
                    return true;
                if (distance < sightSqr && !Physics.Linecast(eye, chest, solidMask, QueryTriggerInteraction.Ignore))
                    return true;
            }

            return false;
        }

        // The briefcase can lie on a node furniture closes; walk outward to the nearest open one.
        static int NearestFree(FloorNav nav, int node)
        {
            if (!nav.Blocked[node])
                return node;

            int a = node % nav.W, b = node / nav.W % nav.H, level = nav.LevelOf(node);
            for (int reach = 1; reach <= NearestFreeReach; reach++)
                for (int db = -reach; db <= reach; db++)
                    for (int da = -reach; da <= reach; da++)
                    {
                        int na = a + da, nb = b + db;
                        if (na < 0 || nb < 0 || na >= nav.W || nb >= nav.H)
                            continue;
                        if (!nav.Blocked[nav.Index(na, nb, level)])
                            return nav.Index(na, nb, level);
                    }

            return -1;
        }
    }
}
