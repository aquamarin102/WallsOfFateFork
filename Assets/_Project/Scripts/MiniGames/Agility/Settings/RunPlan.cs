using System.Collections.Generic;
using UnityEngine;

public class RunPlanItem
{
    public PatternDefinition pattern;
    public float startTime;
    public float endTime;
}

public class RunPlan
{
    public readonly List<RunPlanItem> items = new();
    private readonly List<Object> _ownedRuntimeObjects = new();

    public void TrackRuntimeObject(Object runtimeObject)
    {
        if (runtimeObject != null)
            _ownedRuntimeObjects.Add(runtimeObject);
    }

    public void DisposeRuntimeObjects()
    {
        for (int i = 0; i < _ownedRuntimeObjects.Count; i++)
        {
            Object runtimeObject = _ownedRuntimeObjects[i];
            if (runtimeObject == null)
                continue;

            if (Application.isPlaying)
                Object.Destroy(runtimeObject);
            else
                Object.DestroyImmediate(runtimeObject);
        }

        _ownedRuntimeObjects.Clear();
    }
}
