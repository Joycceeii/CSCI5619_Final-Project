using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;

namespace WhoseStory.EditorTools
{
    // 头显追踪：XR Origin（脚底在原点）→ Camera Offset → Main Camera + Tracked Pose Driver。
    // 戴头显时 XR Origin 用地板模式，相机高度就是玩家真实身高；
    // 编辑器里没开 XR 时 Camera Offset 保持 1.6 米，键盘测试照常能用。
    public static class XrRigBuilder
    {
        const float EditorEyeHeight = 1.6f;

        public static void Setup(Camera camera)
        {
            var origin = new GameObject("XR Origin");
            var offset = new GameObject("Camera Offset");
            offset.transform.SetParent(origin.transform, false);
            offset.transform.localPosition = new Vector3(0f, EditorEyeHeight, 0f);

            camera.transform.SetParent(offset.transform, false);
            camera.transform.localPosition = Vector3.zero;
            camera.transform.localRotation = Quaternion.identity;
            camera.nearClipPlane = 0.05f;
            camera.tag = "MainCamera";

            XROrigin xrOrigin = origin.AddComponent<XROrigin>();
            xrOrigin.Camera = camera;
            xrOrigin.CameraFloorOffsetObject = offset;
            xrOrigin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
            xrOrigin.CameraYOffset = EditorEyeHeight;

            TrackedPoseDriver driver = camera.gameObject.AddComponent<TrackedPoseDriver>();
            driver.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            driver.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
            driver.positionInput = new InputActionProperty(Action("Position", "Vector3",
                "<XRHMD>/centerEyePosition", "<HandheldARInputDevice>/devicePosition"));
            driver.rotationInput = new InputActionProperty(Action("Rotation", "Quaternion",
                "<XRHMD>/centerEyeRotation", "<HandheldARInputDevice>/deviceRotation"));
            driver.trackingStateInput = new InputActionProperty(Action("Tracking State", "Integer",
                "<XRHMD>/trackingState"));
        }

        static InputAction Action(string name, string controlType, params string[] bindings)
        {
            var action = new InputAction(name, InputActionType.Value, expectedControlType: controlType);
            foreach (string binding in bindings)
            {
                action.AddBinding(binding);
            }
            return action;
        }
    }
}
