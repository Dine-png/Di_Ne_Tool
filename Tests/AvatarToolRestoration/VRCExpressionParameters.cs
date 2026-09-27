namespace VRC.SDK3.Avatars.ScriptableObjects
{
    public class VRCExpressionParameters : UnityEngine.ScriptableObject
    {
        [System.Serializable] public class Parameter { }
        public Parameter[] parameters;
        public const int MAX_PARAMETER_COST = 256;
        public int CalcTotalCost() => 0;
    }
}
