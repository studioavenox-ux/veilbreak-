using UnityEngine;

namespace Veilbreak
{
    public enum VeilState { Living, Broken }
    public sealed class VeilReactiveObject : MonoBehaviour
    {
        public VeilState ActiveState;
        public bool ExistsIn(VeilState state) => state == ActiveState;
        public void Apply(VeilState state) => gameObject.SetActive(ExistsIn(state));
    }
}
