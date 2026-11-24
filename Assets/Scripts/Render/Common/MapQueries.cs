using System;
using EggsOfIron.Map;
using UnityEngine;

namespace Render.Common
{
    public interface IMapGeography
    {
        bool TryGetCell(int cellId, out MapCell cell);
        MapCell Pick(Vector2 position);
    }

    public interface ISettlementTravel
    {
        // Returns meters, or positive infinity when the settlements are disconnected.
        float SettlementDistance(int firstId, int secondId);
    }

    public interface IMapPolitics
    {
        event Action Changed;
        int Owner(MapCell cell);
        Color ColorOf(int state);
    }
}
