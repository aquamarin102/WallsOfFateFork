using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "MiniGame/Dex/PatternDatabase")]
public class PatternDatabase : ScriptableObject
{
    public List<PatternDefinition> patterns = new();

    private readonly List<PatternDefinition> _runtimePatterns = new();

    public int AllCount => patterns.Count + _runtimePatterns.Count;

    public IEnumerable<PatternDefinition> All
    {
        get
        {
            for (int i = 0; i < patterns.Count; i++)
            {
                PatternDefinition pattern = patterns[i];
                if (pattern != null)
                    yield return pattern;
            }

            for (int i = 0; i < _runtimePatterns.Count; i++)
            {
                PatternDefinition runtimePattern = _runtimePatterns[i];
                if (runtimePattern != null)
                    yield return runtimePattern;
            }
        }
    }

    public PatternDefinition GetAt(int index)
    {
        if (index < 0)
            return null;

        if (index < patterns.Count)
            return patterns[index];

        index -= patterns.Count;
        return index < _runtimePatterns.Count ? _runtimePatterns[index] : null;
    }

    public PatternDefinition FindById(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;

        for (int i = 0; i < patterns.Count; i++)
        {
            PatternDefinition pattern = patterns[i];
            if (pattern != null && string.Equals(pattern.id, id, StringComparison.Ordinal))
                return pattern;
        }

        for (int i = 0; i < _runtimePatterns.Count; i++)
        {
            PatternDefinition runtimePattern = _runtimePatterns[i];
            if (runtimePattern != null && string.Equals(runtimePattern.id, id, StringComparison.Ordinal))
                return runtimePattern;
        }

        return null;
    }

    public void ReplaceRuntimePatterns(IEnumerable<PatternDefinition> runtimePatterns)
    {
        _runtimePatterns.Clear();
        if (runtimePatterns == null)
            return;

        foreach (PatternDefinition runtimePattern in runtimePatterns)
        {
            if (runtimePattern != null)
                _runtimePatterns.Add(runtimePattern);
        }
    }

    public void ClearRuntimePatterns()
    {
        _runtimePatterns.Clear();
    }
}
