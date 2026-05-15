using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlusFormationPattern : FormationPhasePattern
{
    public float outerRadius = 1.65f;
    public float innerRadius = 0.52f;

    protected override void CreateTelegraphs()
    {
        float lineLength = Mathf.Max(outerRadius * 2.4f, ArenaRadius * 0.95f);
        AddTelegraphLine(Center, new Vector3(lineLength, 0.08f, 0.38f), 0f);
        AddTelegraphLine(Center, new Vector3(0.38f, 0.08f, lineLength), 0f);
    }

    protected override IReadOnlyList<Vector3> BuildEntryTargets()
    {
        return CardinalTargets(Mathf.Min(outerRadius, ArenaRadius * 0.5f));
    }

    protected override IEnumerator ExecuteFormation(float activeDuration)
    {
        float outer = Mathf.Min(outerRadius, ArenaRadius * 0.52f);
        float inner = Mathf.Min(innerRadius, ArenaRadius * 0.2f);
        int pulses = Definition.intensity >= 0.75f ? 2 : 1;
        float moveDuration = Mathf.Max(0.28f, activeDuration * 0.22f);
        float holdDuration = Mathf.Max(0.18f, (activeDuration - moveDuration * pulses * 2f) / (pulses * 2f + 1f));
        Vector3[] outerTargets = CardinalTargets(outer);
        Vector3[] innerTargets = CardinalTargets(inner);

        yield return new WaitForSeconds(holdDuration);

        for (int i = 0; i < pulses; i++)
        {
            yield return MoveActors(innerTargets, moveDuration);
            yield return new WaitForSeconds(holdDuration);
            yield return MoveActors(outerTargets, moveDuration);
            yield return new WaitForSeconds(holdDuration);
        }
    }
}
