using Rage;
using Rage.Native;

namespace AdvancedK9
{
    internal sealed class DogCamera
    {
        private int _camera;
        public bool Active => _camera != 0;

        public void Toggle(Ped dog)
        {
            if (Active) { Disable(); return; }
            if (dog == null || !dog.Exists()) return;
            var p = dog.GetOffsetPosition(new Vector3(0f, 0.15f, 0.65f));
            _camera = NativeFunction.Natives.CREATE_CAM_WITH_PARAMS<int>("DEFAULT_SCRIPTED_CAMERA", p.X, p.Y, p.Z, 0f, 0f, dog.Heading, 65f, true, 2);
            NativeFunction.Natives.ATTACH_CAM_TO_ENTITY(_camera, dog, 0f, 0.15f, 0.65f, true);
            NativeFunction.Natives.SET_CAM_ROT(_camera, -8f, 0f, dog.Heading, 2);
            NativeFunction.Natives.RENDER_SCRIPT_CAMS(true, false, 0, true, false, 0);
            Game.DisplayNotification("~b~Advanced K9~s~ dog camera enabled.");
        }

        public void Disable()
        {
            if (!Active) return;
            NativeFunction.Natives.RENDER_SCRIPT_CAMS(false, false, 0, true, false, 0);
            NativeFunction.Natives.DESTROY_CAM(_camera, false);
            _camera = 0;
        }
    }
}
