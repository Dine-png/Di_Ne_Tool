public class DiNeMultiDresser : UnityEngine.MonoBehaviour
{
    public UnityEditor.Animations.AnimatorController animatorController;
    public VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu expressionsMenu;
    public bool failGeneration;
    public void TryAutoAssignFXController() { }
    public void Generate(string folder, bool clear, bool merge)
    {
        if (failGeneration) throw new System.InvalidOperationException("Simulated dresser generation failure");
    }
}
