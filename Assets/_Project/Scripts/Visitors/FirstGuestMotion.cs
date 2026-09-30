using UnityEngine;

namespace NO404.Visitors
{
    /// <summary>Transform rig driven only by the same scaled clock as VisitorBodies.</summary>
    public sealed class FirstGuestMotion : MonoBehaviour
    {
        Transform _body, _head, _leftArm, _rightArm, _leftLeg, _rightLeg;
        Transform _leftForeArm, _rightForeArm, _leftShin, _rightShin, _leftFoot, _rightFoot;
        Quaternion _lfRest, _rfRest, _lsRest, _rsRest, _lfootRest, _rfootRest;
        Quaternion _headRest, _laRest, _raRest, _llRest, _rlRest;
        Vector3 _bodyRest;
        float _phase, _walkPhase, _blend;

        void Awake()
        {
            foreach (var joint in GetComponentsInChildren<Transform>(true))
            {
                switch (joint.name)
                {
                    case "BodyPivot": _body = joint; _bodyRest = joint.localPosition; break;
                    case "HeadPivot": _head = joint; _headRest = joint.localRotation; break;
                    case "LeftArmPivot": _leftArm = joint; _laRest = joint.localRotation; break;
                    case "RightArmPivot": _rightArm = joint; _raRest = joint.localRotation; break;
                    case "LeftLegPivot": _leftLeg = joint; _llRest = joint.localRotation; break;
                    case "RightLegPivot": _rightLeg = joint; _rlRest = joint.localRotation; break;
                    case "LeftForeArmPivot": _leftForeArm = joint; _lfRest = joint.localRotation; break;
                    case "RightForeArmPivot": _rightForeArm = joint; _rfRest = joint.localRotation; break;
                    case "LeftShinPivot": _leftShin = joint; _lsRest = joint.localRotation; break;
                    case "RightShinPivot": _rightShin = joint; _rsRest = joint.localRotation; break;
                    case "LeftFootPivot": _leftFoot = joint; _lfootRest = joint.localRotation; break;
                    case "RightFootPivot": _rightFoot = joint; _rfootRest = joint.localRotation; break;
                }
            }
        }

        public void Tick(float scaledSeconds, float speed)
        {
            if (scaledSeconds <= 0f || _body == null) return;
            _phase += scaledSeconds;
            _blend = Mathf.MoveTowards(_blend, Mathf.Clamp01(speed / VisitorBodies.WalkSpeed), scaledSeconds * 5f);
            _walkPhase += scaledSeconds * 7.5f * Mathf.Max(0f, speed) / 1.1f;
            float step = Mathf.Sin(_walkPhase) * _blend;
            // Convert world axes into each FBX pivot's parent space. This also works with
            // the axis conversion introduced by Blender's FBX exporter.
            Rotate(_leftLeg, _llRest, transform.right, step * 23f);
            Rotate(_rightLeg, _rlRest, transform.right, -step * 23f);
            Rotate(_leftArm, _laRest, transform.right, -step * 9f);
            Rotate(_rightArm, _raRest, transform.right, step * 9f);
            Rotate(_leftForeArm, _lfRest, transform.right, -Mathf.Max(0f, -step) * 13f);
            Rotate(_rightForeArm, _rfRest, transform.right, -Mathf.Max(0f, step) * 13f);
            float leftLift = Mathf.Max(0f, -Mathf.Sin(_walkPhase + .4f)) * _blend;
            float rightLift = Mathf.Max(0f, Mathf.Sin(_walkPhase + .4f)) * _blend;
            Rotate(_leftShin, _lsRest, transform.right, leftLift * 32f);
            Rotate(_rightShin, _rsRest, transform.right, rightLift * 32f);
            Rotate(_leftFoot, _lfootRest, transform.right, -leftLift * 16f);
            Rotate(_rightFoot, _rfootRest, transform.right, -rightLift * 16f);
            Rotate(_head, _headRest, transform.up, Mathf.Sin(_phase * .45f) * 1.2f * (1f - _blend));
            Vector3 up = _body.parent.InverseTransformVector(transform.up);
            _body.localPosition = _bodyRest + up * (Mathf.Sin(_phase * 1.9f) * .0025f + Mathf.Abs(step) * .006f);
        }

        static void Rotate(Transform joint, Quaternion rest, Vector3 worldAxis, float angle)
        {
            if (joint == null) return;
            joint.localRotation = Quaternion.AngleAxis(angle, joint.parent.InverseTransformDirection(worldAxis)) * rest;
        }
    }
}
