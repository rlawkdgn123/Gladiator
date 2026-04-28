using Unity.Cinemachine;
using UnityEngine;

namespace Game.VFX.Runtime
{
    /// <summary>
    /// Cinemachine 3 CinemachineImpulseSource 래퍼.
    /// Owner GameObject에 CinemachineImpulseSource를 자동 추가/탐색.
    /// 씬에 CinemachineBrain + CinemachineImpulseListener가 있어야 효과가 보임.
    /// </summary>
    [DisallowMultipleComponent]
    public class CameraShakeImpulse : MonoBehaviour
    {
        CinemachineImpulseSource _source;

        void Awake()
        {
            _source = GetComponent<CinemachineImpulseSource>();
            if (_source == null)
                _source = gameObject.AddComponent<CinemachineImpulseSource>();
        }

        public void Generate(float amplitude = 0.3f, float duration = 0.25f)
        {
            if (_source == null) return;
            _source.ImpulseDefinition.ImpulseDuration = Mathf.Max(0.01f, duration);
            _source.GenerateImpulseWithForce(amplitude);
        }

        public void GenerateAt(Vector3 position, float amplitude = 0.3f, float duration = 0.25f)
        {
            if (_source == null) return;
            _source.ImpulseDefinition.ImpulseDuration = Mathf.Max(0.01f, duration);
            _source.GenerateImpulseAtPositionWithVelocity(position, Random.insideUnitSphere * amplitude);
        }
    }
}
