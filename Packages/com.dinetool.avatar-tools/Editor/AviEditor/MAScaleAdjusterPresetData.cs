using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "MAScalePreset", menuName = "DiNe/MA Scale Preset")]
public sealed class MAScaleAdjusterPresetData : ScriptableObject
{
    public const int CurrentSchemaVersion = 2;

    [SerializeField] private int schemaVersion = CurrentSchemaVersion;
    public List<Entry> entries = new List<Entry>();

    public int SchemaVersion => schemaVersion;

    [Serializable]
    public sealed class Entry
    {
        public string part;
        public float x = 1f;
        public float y = 1f;
        public float z = 1f;
        public bool adjustChildPositions;

        public Vector3 Scale => new Vector3(x, y, z);

        public Entry(string part, Vector3 scale, bool adjustChildPositions)
        {
            this.part = part;
            x = scale.x;
            y = scale.y;
            z = scale.z;
            this.adjustChildPositions = adjustChildPositions;
        }
    }
}
