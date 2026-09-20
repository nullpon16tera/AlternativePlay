using UnityEngine;

namespace AlternativePlay
{
    [DefaultExecutionOrder(-9000)]
    public sealed class DualNunchakuCutDriver : MonoBehaviour
    {
        internal DualNunchakuBehavior Owner;

        private void LateUpdate()
        {
            if (this.Owner != null)
            {
                this.Owner.SampleFreeSabers();
            }
        }
    }
}
