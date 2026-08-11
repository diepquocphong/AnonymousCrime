// ----------------------------------------------------
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System.Collections.Generic;
using UnityEngine;

namespace PampelGames.Shared.Utility
{
    public static class PGMathUtility
    {
        public static List<float> GetEvenlySpacedValues(float length, float spacing, out float gap)
        {
            var evaluations = new List<float>();
            gap = 0f;

            var count = Mathf.FloorToInt(length / spacing);
            if (count < 1)
            {
                evaluations.Add(0.5f);
                return evaluations;
            }

            var leftover = length - count * spacing;
            gap = leftover / count;
            var adjustedSpacing = spacing + gap;

            for (var i = 0; i < count; i++)
            {
                var distance = (i + 0.5f) * adjustedSpacing;
                var t = distance / length;
                evaluations.Add(t);
            }

            return evaluations;
        }
    }
}